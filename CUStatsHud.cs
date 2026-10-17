using System;
using BepInEx;
using BepInEx.Configuration;
using CuStatsHud.Core;
using CuStatsHud.Patch;
using CuStatsHud.UI;
using CuStatsHud.UI.Panels;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// CU Stats HUD —— CU（Casualties Unknown）玩家状态监护面板。
///
/// 重构要点（v2.0）：
///   - 数据层 / 规则层 / UI 层分离，阈值全部对齐游戏自身的 isDying / isCriticallyDying 判定；
///   - 医疗监护仪视觉：标题栏通道大字 + 告警横幅 + 三条波形 + 分级配色卡片；
///   - 队友简表（血量条 + 距离 + 分级），危机状态优先置顶；
///   - 快捷键、面板位置/尺寸/缩放、全部行为开关均持久化到 BepInEx 配置。
/// </summary>
[BepInPlugin(Guid, "CU Stats HUD", Version)]
public class CUStatsHud : BaseUnityPlugin
{
    public const string Guid = "com.fox.custatshud";
    public const string Version = "2.0.0";

    private const float RefreshInterval = 0.2f;
    private const float GeometrySaveDelay = 0.8f;
    /// <summary>副本卡片的落盘延时（拖动/滚轮过程中不反复写配置）。</summary>
    private const float FloatCardSaveDelay = 0.6f;

    /// <summary>关闭摆放界面后继续阻断游戏输入的时间（覆盖 Esc 被游戏处理的那一帧）。</summary>
    private const float InputBlockHoldSeconds = 0.25f;

    /// <summary>统计的落盘间隔。</summary>
    private const float StatsSaveInterval = 60f;

    private Harmony _harmony;
    private float _statsSaveTimer;

    private PluginConfig _config;
    private HudState _state;
    private float _refreshTimer;

    private bool _gameInputBlocked;
    private bool _savedPauseState;
    private bool _savedPauseContainer;
    /// <summary>关闭摆放界面后继续维持输入阻断的截止时刻。</summary>
    private float _inputBlockHoldUntil;

    private void Awake()
    {
        try
        {
            _config = new PluginConfig(Config);
            _state = new HudState(_config);

            // 游戏行为统计：读取历史（首次运行即生成新文件）+ 记录本次启动，然后挂采集点
            LoadStats();
            _state.GameStats.NoteLaunch(DateTime.UtcNow);
            _state.StatsDirty = true;
            GameStatsHooks.Stats = _state.GameStats;
            ApplyHarmonyPatches();

            // 先取一次数据，避免打开面板的瞬间是空的
            _state.Reader.Read(_state.Snapshot, true, _config.ShowNormalSecondary.Value);

            Logger.LogInfo("CU Stats HUD " + Version + " 已加载：按 " + _config.ToggleKey.Value + " 显示/隐藏监护面板");
        }
        catch (Exception ex)
        {
            Logger.LogError("CU Stats HUD 初始化失败：" + ex);
        }
    }

    private void Update()
    {
        if (_state == null) return;

        try
        {
            _state.CheckScreenChange();
            HandleToggleKey();
            HandleFloatKey();

            _state.FloatingCards.EnsureLoaded(_config);
            _state.FloatingCards.Flush(_config, Time.unscaledTime, FloatCardSaveDelay);

            // 行为统计：刷新本地玩家、累加移动/高度，并定时加密落盘
            RefreshLocalBody();
            CollectMovement();
            SyncSkills();

            _statsSaveTimer += Time.unscaledDeltaTime;
            if (_statsSaveTimer >= StatsSaveInterval)
            {
                _statsSaveTimer = 0f;
                if (_state.StatsDirty) SaveStats();
            }

            // 组件卡可见性：存档未加载（读不到本地玩家）不显示；
            // 进入暂停界面时跟着游戏自己的暂停动画淡出，回到游戏再淡入。
            UpdateCardVisibility();

            // 摆放界面的抽屉动画（靠近屏幕边缘展开、离开收起）每帧推进一次
            CardLayoutEditor.Update(_state, Time.unscaledDeltaTime);

            // 摆放界面期间阻断游戏输入，避免点卡片、拖滑块时误触发游戏操作。
            // 关闭后还要再维持一小段时间——见 UpdateGameInputBlock 的说明。
            UpdateGameInputBlock(_state.FloatingCards.Editing);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                _state.Reader.Read(_state.Snapshot, _state.Visible, _config.ShowNormalSecondary.Value);
            }

            if (_state.Visible && _config.WaveformsEnabled.Value && _state.Waves != null)
            {
                // 尺寸发生变化时重建纸带并复位相位（避免缩放瞬间出现波形撕裂）
                if (_state.WaveWidth > 8 && _state.WaveHeight > 8)
                {
                    _state.Waves.EnsureSize(_state.WaveWidth, _state.WaveHeight);
                }
                _state.Waves.Advance(Time.unscaledDeltaTime, _state.Reader.LocalBody, _state.Snapshot);
            }

            // 状态条的呼吸相位：只由“闪烁提醒”开关决定（状态条本身是否显示由“显示告警横幅”决定）
            _state.Pulse = _config.AlertFlash.Value
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)
                : 0f;

            if (_state.GeometryDirty && Time.unscaledTime - _state.GeometryDirtyAt >= GeometrySaveDelay)
            {
                _state.SaveGeometry();
            }
        }
        catch (Exception ex)
        {
            _state.Note("更新异常：" + ex.Message);
        }
    }

    private void OnGUI()
    {
        if (_state == null) return;

        try
        {
            _state.EnsureUi();
            if (_state.Styles == null) return;

            _state.AdvanceVisibility(Time.unscaledDeltaTime);
            HandleKeyCapture();

            // 组件卡矩形在绘制前算好，保证 OnGUI 的 Layout / Repaint 两趟用同一套几何
            _state.FloatingCards.SyncRects(_state);

            bool panelOn = _state.PanelVisibility > 0.002f;
            bool editing = _state.FloatingCards.Editing;

            if (_state.Waves != null) _state.Waves.Flush();

            if (editing)
            {
                // 摆放界面：整屏成为画布，并先把左右栏登记为鼠标事件禁区
                CardLayoutEditor.Prepare(_state);
                FloatingCardHost.DrawGridOverlay(_state);
            }

            float opacity = Mathf.Clamp(_config.Opacity.Value, 0.3f, 1f);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _state.PanelVisibility * opacity);

            // 摆放界面打开时主面板让位（否则两栏 + 面板 + 画布会互相遮挡）
            if (panelOn && !editing)
            {
                // 模态窗口打开时把主面板设为不可交互。
                // IMGUI 的 disabled 只影响“响应”，我自绘的底纹/文字外观不受影响，
                // 因此能做到「仍然可见、但点不动」，从根上避免点击穿透。
                bool previousEnabled = GUI.enabled;
                if (_state.HasModal) GUI.enabled = false;
                try
                {
                    MonitorWindow.Draw(_state);
                }
                finally
                {
                    GUI.enabled = previousEnabled;
                }
            }

            GUI.color = old;

            // 组件卡：摆放界面里可拖动/可选中；平时只显示（并受“显示组件卡”开关控制）
            if (editing || _config.FloatCardsEnabled.Value)
            {
                _state.FloatingCards.Draw(_state, editing);
            }

            if (editing)
            {
                CardLayoutEditor.DrawPanes(_state);
                FloatingCardHost.DrawHintBar(_state);
            }

            // 面板不可见时不绘制模态窗口（否则按快捷键隐藏后弹窗会留在屏幕上）
            if (panelOn && _state.ShowMateDetail)
            {
                MateDetailDialog.Draw(_state);
            }

            if (panelOn && _state.ShowAbout)
            {
                AboutDialog.Draw(_state);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("CU Stats HUD 绘制异常：" + ex);
        }
    }

    // ---------------------------------------------------------------
    // 游戏行为统计（本地、加密）
    //
    // 只读写 BepInEx 配置目录下的一个**加密**文件：不联网、不上报、不含任何身份信息。
    // 采集点见 Patch/GameStatsHooks，全部在反编译源码里核对过。
    // ---------------------------------------------------------------

    /// <summary>统计文件路径（加密文本，与插件配置同目录；首次运行会直接生成）。</summary>
    private static string StatsFilePath()
    {
        try
        {
            return System.IO.Path.Combine(Paths.ConfigPath, "CUStatsHud.stats.dat");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void LoadStats()
    {
        try
        {
            string path = StatsFilePath();
            if (string.IsNullOrEmpty(path)) return;

            _state.StatsPath = path;
            if (!System.IO.File.Exists(path)) return;   // 首次运行：稍后写盘即生成

            string plain = StatsCipher.Decrypt(System.IO.File.ReadAllText(path));
            if (plain == null)
            {
                Logger.LogWarning("统计数据无法解密（可能被改动过或来自其它版本），本次将重新开始统计");
                return;
            }

            _state.GameStats.Deserialize(plain);

            // 旧版把空手挥击也计进了「道具使用」，但当时没有分类 id，
            // 于是老数据里 itemsUsed 与明细之和存在差额 —— 那部分就是空挥。
            // 空挥现在不再计数，因此把 itemsUsed 收敛到明细之和；明细本身原样保留。
            long dropped = _state.GameStats.DropLegacyItemUseGap();
            if (dropped > 0L)
            {
                Logger.LogInfo("统计数据已按新版口径清理：去掉 " + dropped
                    + " 次旧版误计入道具使用的空手挥击（道具明细原样保留）");
                _state.StatsDirty = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("读取统计数据失败（不影响游玩）：" + ex.Message);
        }
    }

    private void SaveStats()
    {
        try
        {
            string path = _state != null ? _state.StatsPath : null;
            if (string.IsNullOrEmpty(path)) return;

            System.IO.File.WriteAllText(path, StatsCipher.Encrypt(_state.GameStats.Serialize()));
            _state.StatsDirty = false;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("写入统计数据失败（不影响游玩）：" + ex.Message);
        }
    }

    /// <summary>挂载采集点。失败只影响统计，插件其余功能照常。</summary>
    private void ApplyHarmonyPatches()
    {
        try
        {
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(GameStatsHooks).Assembly);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("行为统计采集点挂载失败（统计将只有移动数据）：" + ex.Message);
        }
    }

    /// <summary>刷新本地玩家引用；换存档 / 重生时丢弃位置锚点，避免把跳变算成位移。</summary>
    private void RefreshLocalBody()
    {
        Body body = null;
        try
        {
            PlayerCamera camera = PlayerCamera.main;
            if (camera != null) body = camera.body;
        }
        catch (Exception)
        {
            body = null;
        }

        if (GameStatsHooks.LocalBody == body) return;

        GameStatsHooks.LocalBody = body;
        _state.GameStats.DropAnchor();
        // 换存档 / 重生：技能基线也要重建，否则新角色的初始经验会被算成「获得」
        _state.GameStats.ResetSkillBaseline();
    }

    /// <summary>
    /// 同步技能与经验。
    ///
    /// <c>Skills.expSTR/expRES/expINT</c> 是**累计经验**而不是当前级内进度：
    /// <c>Setup</c> 把它们初始化成 <c>GetExperienceForLevel(等级)</c>，之后 <c>AddExp</c> 直接累加。
    /// 所以三者之和就是总经验，与基线相减得到「累计获得」。
    /// </summary>
    private void SyncSkills()
    {
        Body body = GameStatsHooks.LocalBody;
        if (body == null) return;

        Skills skills = body.skills;
        if (skills == null) return;

        double total = skills.expSTR + skills.expRES + skills.expINT;
        _state.GameStats.NoteSkills(skills.STR, skills.RES, skills.INT, total, false);
        _state.StatsDirty = true;
    }

    private void CollectMovement()
    {
        Body body = GameStatsHooks.LocalBody;
        if (body == null) return;

        Vector3 pos = body.transform.position;
        _state.GameStats.TickMovement(pos.x, pos.y, Time.unscaledDeltaTime);
        _state.StatsDirty = true;

        // 每帧存一份「物品状况指纹」：UseItemInHand 的后置补丁只看到调用**之后**的状态，
        // 「用掉最后一个」时后置状态已是空手，单看它会误判成空挥。
        GameStatsHooks.PrimeHandState(body);
    }

    // ---------------------------------------------------------------

    private void OnDestroy()
    {
        try
        {
            // 必须先解除游戏输入阻断，否则卸载插件后游戏的暂停状态会被留在“已阻断”上
            SetGameInputBlocked(false);

            if (_state != null)
            {
                SaveStats();          // 退出前落盘（加密）
                _state.SaveGeometry();
                _state.Dispose();
                _state = null;
            }

            GameStatsHooks.Stats = null;
            GameStatsHooks.LocalBody = null;

            // 探测缓存跟着插件走：重新加载后 Body 类型可能是新实例，必须重新解析
            ItemUseProbe.Reset();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
        catch (Exception)
        {
            // 销毁阶段不再抛出
        }
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// 输入阻断的开关包装。
    ///
    /// 关闭摆放界面后会**再多阻断一小段时间**：按 Esc 退出时，游戏自己的暂停键处理同样会看到这次按键
    /// （它在"输入短路"那行之前），如果此时立刻把游戏状态恢复回去，游戏就会把 Esc 当成
    /// 「打开暂停菜单」——表现为"关掉摆放界面后游戏被暂停了"。多维持 0.25 秒即可覆盖那一帧。
    /// </summary>
    private void UpdateGameInputBlock(bool editing)
    {
        if (editing) _inputBlockHoldUntil = Time.unscaledTime + InputBlockHoldSeconds;

        bool block = editing || Time.unscaledTime < _inputBlockHoldUntil;
        SetGameInputBlocked(block);
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// 摆放界面期间阻断游戏输入。
    ///
    /// 做法是让游戏的 <c>PauseHandler.paused</c> 返回 true —— 游戏自己的输入处理
    /// （<c>PlayerCamera</c> 里 <c>if (PauseHandler.paused) return;</c> 直接短路全部输入，
    /// 背包键、交互键也各自检查这个标志），所以不需要去拦截 Unity 的 Input。
    ///
    /// 同时禁用 PauseHandler 组件并隐藏暂停菜单容器：否则它会推进暂停动画、
    /// 弹出暂停菜单盖住摆放界面。退出摆放界面时全部还原（含原本就是暂停状态的情况）。
    /// </summary>
    private void SetGameInputBlocked(bool blocked)
    {
        try
        {
            PauseHandler handler = PauseHandler.main;
            if (handler == null)
            {
                _gameInputBlocked = false;
                return;
            }

            if (blocked)
            {
                if (!_gameInputBlocked)
                {
                    _savedPauseState = handler.isPaused;
                    _savedPauseContainer = handler.pauseContainer != null && handler.pauseContainer.activeSelf;

                    handler.enabled = false;      // 不再推进暂停动画、不再接管时间缩放
                    if (handler.pauseContainer != null) handler.pauseContainer.SetActive(false);
                    _gameInputBlocked = true;
                }

                // 每帧维持，而不是只设一次：
                // 游戏的暂停键处理在“输入短路”那行之前，玩家按 ESC 会把 isPaused 切回 false，
                // 所以这里必须持续保持阻断状态。
                if (!handler.isPaused) handler.isPaused = true;
                if (handler.enabled) handler.enabled = false;
                if (handler.pauseContainer != null && handler.pauseContainer.activeSelf)
                {
                    handler.pauseContainer.SetActive(false);
                }
            }
            else if (_gameInputBlocked)
            {
                handler.isPaused = _savedPauseState;
                if (handler.pauseContainer != null) handler.pauseContainer.SetActive(_savedPauseContainer);
                handler.enabled = true;

                _gameInputBlocked = false;
            }
        }
        catch (Exception ex)
        {
            _gameInputBlocked = false;
            Logger.LogWarning("切换游戏输入阻断状态失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 组件卡的可见度：
    ///   1) 没读到本地玩家（主菜单 / 存档未加载）→ 0，完全不画；
    ///   2) 处于暂停界面 → 跟随游戏自己的 <c>PauseHandler.pauseProgress</c> 淡出，
    ///      这样和暂停菜单的入场动画同步，不会突然消失。
    /// </summary>
    private void UpdateCardVisibility()
    {
        float target = _state.Snapshot.Valid ? 1f : 0f;
        if (target > 0f)
        {
            float pauseProgress = PauseProgress();
            if (pauseProgress > 0f) target = 1f - pauseProgress;
        }

        _state.GamePausedVisibility = Mathf.MoveTowards(_state.GamePausedVisibility, target,
            Time.unscaledDeltaTime * 6f);
    }

    /// <summary>游戏暂停动画进度 0..1（0 = 在游戏中，1 = 完全暂停）。</summary>
    private static float PauseProgress()
    {
        try
        {
            PauseHandler handler = PauseHandler.main;
            return handler != null ? Mathf.Clamp01(handler.pauseProgress) : 0f;
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    private void HandleToggleKey()
    {
        if (_state.CapturingKey) return;

        try
        {
            if (_config.ToggleKey.Value.IsDown())
            {
                _state.ToggleVisible();
            }
        }
        catch (Exception)
        {
            // 快捷键解析失败时静默跳过，不影响面板
        }
    }

    /// <summary>组件卡的显示/隐藏快捷键。</summary>
    private void HandleFloatKey()
    {
        if (_state.CapturingKey) return;

        try
        {
            if (!_config.FloatKey.Value.IsDown()) return;

            _state.ToggleFloatCards();
        }
        catch (Exception)
        {
            // 快捷键解析失败时静默跳过
        }
    }

    /// <summary>
    /// 键位捕获：在设置页点击“快捷键”按钮后，下一次按键即写入对应配置。
    /// Esc 或鼠标点击取消。
    /// </summary>
    private void HandleKeyCapture()
    {
        if (!_state.CapturingKey) return;

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                _state.CapturingTarget = 0;
                _state.Note("已取消按键绑定");
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.None) return;

            KeyboardShortcut shortcut = new KeyboardShortcut(e.keyCode);
            if (_state.CapturingTarget == 2)
            {
                _config.FloatKey.Value = shortcut;
                _state.Note("组件卡快捷键已设为 " + shortcut);
            }
            else
            {
                _config.ToggleKey.Value = shortcut;
                _state.Note("快捷键已设为 " + shortcut);
            }

            _state.CapturingTarget = 0;
            e.Use();
        }
        else if (e.type == EventType.MouseDown)
        {
            _state.CapturingTarget = 0;
            _state.Note("已取消按键绑定");
        }
    }
}
