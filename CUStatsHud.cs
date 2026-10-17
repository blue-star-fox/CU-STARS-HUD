using System;
using BepInEx;
using BepInEx.Configuration;
using CuStatsHud.Core;
using CuStatsHud.Patch;
using CuStatsHud.UI;
using CuStatsHud.UI.Panels;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(Guid, "CU Stats HUD", Version)]
public class CUStatsHud : BaseUnityPlugin
{
    public const string Guid = "com.fox.custatshud";
    public const string Version = "2.0.0";

    private const float RefreshInterval = 0.2f;
    private const float GeometrySaveDelay = 0.8f;
    
    private const float FloatCardSaveDelay = 0.6f;

    private const float InputBlockHoldSeconds = 0.25f;

    private const float StatsSaveInterval = 60f;

    private Harmony _harmony;
    private float _statsSaveTimer;

    private PluginConfig _config;
    private HudState _state;
    private float _refreshTimer;

    private bool _gameInputBlocked;
    private bool _savedPauseState;
    private bool _savedPauseContainer;
    
    private float _inputBlockHoldUntil;

    private void Awake()
    {
        try
        {
            _config = new PluginConfig(Config);
            _state = new HudState(_config);

            try
            {
                _state.BackgroundDir = Paths.ConfigPath;
            }
            catch (Exception)
            {
                _state.BackgroundDir = null;
            }

            LoadStats();
            _state.GameStats.NoteLaunch(DateTime.UtcNow);
            _state.StatsDirty = true;
            GameStatsHooks.Stats = _state.GameStats;
            ApplyHarmonyPatches();

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

            RefreshLocalBody();
            CollectMovement();
            SyncSkills();
            SyncAppearance();

            _statsSaveTimer += Time.unscaledDeltaTime;
            if (_statsSaveTimer >= StatsSaveInterval)
            {
                _statsSaveTimer = 0f;
                if (_state.StatsDirty) SaveStats();
            }

            UpdateCardVisibility();

            CardLayoutEditor.Update(_state, Time.unscaledDeltaTime);

            UpdateGameInputBlock(_state.FloatingCards.Editing);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                _state.Reader.Read(_state.Snapshot, _state.Visible, _config.ShowNormalSecondary.Value);
            }

            if (_state.Visible && _config.WaveformsEnabled.Value && _state.Waves != null)
            {
                
                if (_state.WaveWidth > 8 && _state.WaveHeight > 8)
                {
                    _state.Waves.EnsureSize(_state.WaveWidth, _state.WaveHeight);
                }
                _state.Waves.Advance(Time.unscaledDeltaTime, _state.Reader.LocalBody, _state.Snapshot);
            }

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

            _state.FloatingCards.SyncRects(_state);

            bool panelOn = _state.PanelVisibility > 0.002f;
            bool editing = _state.FloatingCards.Editing;

            if (_state.Waves != null) _state.Waves.Flush();

            if (editing)
            {
                
                CardLayoutEditor.Prepare(_state);
                FloatingCardHost.DrawGridOverlay(_state);
            }

            float opacity = Mathf.Clamp(_config.Opacity.Value, 0.3f, 1f);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _state.PanelVisibility * opacity);

            if (panelOn && !editing)
            {
                
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

            if (editing || _config.FloatCardsEnabled.Value)
            {
                _state.FloatingCards.Draw(_state, editing);
            }

            if (editing)
            {
                CardLayoutEditor.DrawPanes(_state);
                FloatingCardHost.DrawHintBar(_state);
            }

            if (panelOn && _state.ShowMateDetail)
            {
                MateDetailDialog.Draw(_state);
            }

            if (panelOn && _state.ShowAbout)
            {
                AboutDialog.Draw(_state);
            }

            if (panelOn && _state.ShowFilePicker)
            {
                FilePickerDialog.Draw(_state);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("CU Stats HUD 绘制异常：" + ex);
        }
    }

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
            if (!System.IO.File.Exists(path)) return;   

            string plain = StatsCipher.Decrypt(System.IO.File.ReadAllText(path));
            if (plain == null)
            {
                Logger.LogWarning("统计数据无法解密（可能被改动过或来自其它版本），本次将重新开始统计");
                return;
            }

            _state.GameStats.Deserialize(plain);
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
        
        _state.GameStats.ResetSkillBaseline();
    }

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
    }
    
    private void SyncAppearance()
    {
        HudStyles styles = _state.Styles;
        if (styles == null) return;

        styles.BackgroundAlpha = _config.BackgroundEnabled.Value
            ? UnityEngine.Mathf.Clamp(_config.BackgroundOpacity.Value, 0.01f, 1f)
            : 0f;

        bool force = _state.BackgroundReload;
        if (force) _state.BackgroundReload = false;

        string path = BackgroundFilePath();
        bool loaded = BackgroundImage.Ensure(path, force);
        styles.BackgroundImage = loaded ? BackgroundImage.Current : null;
    }

    private string BackgroundFilePath()
    {
        string name = _config != null ? _config.BackgroundFile.Value : null;
        if (string.IsNullOrEmpty(name)) return null;

        string dir = _state != null ? _state.BackgroundDir : null;
        if (string.IsNullOrEmpty(dir)) return null;

        try
        {
            return System.IO.Path.Combine(dir, name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnDestroy()
    {
        try
        {
            
            SetGameInputBlocked(false);

            if (_state != null)
            {
                SaveStats();          
                _state.SaveGeometry();
                _state.Dispose();
                _state = null;
            }

            GameStatsHooks.Stats = null;
            GameStatsHooks.LocalBody = null;

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
        catch (Exception)
        {
            
        }
    }

    private void UpdateGameInputBlock(bool editing)
    {
        if (editing) _inputBlockHoldUntil = Time.unscaledTime + InputBlockHoldSeconds;

        bool block = editing || Time.unscaledTime < _inputBlockHoldUntil;
        SetGameInputBlocked(block);
    }

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

                    handler.enabled = false;      
                    if (handler.pauseContainer != null) handler.pauseContainer.SetActive(false);
                    _gameInputBlocked = true;
                }

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
            
        }
    }

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
            
        }
    }

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
