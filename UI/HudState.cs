using BepInEx.Logging;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 插件的全部界面运行状态：窗口几何、当前标签页、滚动位置、动画相位，
    /// 以及随缩放/字号/字体变化而重建的样式与纹理资源。
    /// </summary>
    internal sealed class HudState
    {
        public const int TabVitals = 0;
        public const int TabLimbs = 1;
        public const int TabTeam = 2;
        public const int TabSettings = 3;
        public const int TabStats = 4;
        public const int TabCount = 5;

        private const float PanelFadeSeconds = 0.16f;

        /// <summary>
        /// 供 UI 层记录异常用的日志源。
        /// 绘制层的异常过去只写成 4 秒就消失的底栏提示，出问题极难排查
        /// （例如「某个标签页整页空白」），所以这里同时落进 BepInEx 日志。
        /// </summary>
        // 用完全限定名：UnityEngine 也有 Logger，直接写会二义
        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("CUStatsHud");

        public static readonly string[] TabNames = { "生命体征", "肢体", "队友", "设置", "统计" };

        // ---------- 数据 ----------
        public readonly PluginConfig Config;
        public readonly MpBridge Mp = new MpBridge();
        public readonly BodyReader Reader;
        public readonly BodySnapshot Snapshot = new BodySnapshot();
        public readonly FontProvider Fonts = new FontProvider();
        public readonly AvatarCache Avatars = new AvatarCache();
        public WaveformSet Waves;
        // ---------- 浮动组件卡 ----------
        /// <summary>浮动组件卡宿主（摆放界面在设置页里打开）。</summary>
        public readonly FloatingCardHost FloatingCards = new FloatingCardHost();

        /// <summary>摆放界面右侧组件库的滚动位置。</summary>
        public readonly ScrollState CardLibraryScroll = new ScrollState();

        /// <summary>关于窗口的滚动位置（内容较多）。</summary>
        public readonly ScrollState AboutScroll = new ScrollState();

        /// <summary>摆放界面左栏（属性）的展开度 0..1 —— 抽屉动画，鼠标靠近屏幕边缘展开。</summary>
        public float LeftPaneExpand;
        /// <summary>摆放界面右栏（组件库）的展开度 0..1。</summary>
        public float RightPaneExpand;

        /// <summary>点击把手后锁定展开（不随鼠标离开而收起）。</summary>
        public bool LeftPanePinned;
        public bool RightPanePinned;

        /// <summary>刚收起/选好组件卡后抑制自动展开，直到鼠标离开屏幕边缘。</summary>
        public bool LeftPaneSuppress;
        public bool RightPaneSuppress;

        /// <summary>
        /// 组件卡的可见度 0..1：
        /// 未加载存档（读不到本地玩家）时为 0；进入暂停界面时随游戏自己的暂停动画淡出，回到游戏再淡入。
        /// </summary>
        public float GamePausedVisibility = 1f;

        // ---------- 界面状态 ----------
        public bool Visible;
        public int Tab;
        public float PanelVisibility;
        public float TabAnimStart = -10f;
        public float TabAnimDir = 1f;
        /// <summary>告警呼吸相位 0..1。</summary>
        public float Pulse = 1f;
        /// <summary>正在捕获快捷键的目标：0 = 无，1 = 面板显示/隐藏，2 = 组件卡显示/隐藏。</summary>
        public int CapturingTarget;

        public bool CapturingKey
        {
            get { return CapturingTarget != 0; }
        }
        public string StatusNote = "";
        public float StatusNoteAt = -10f;
        public bool ShowAbout;

        // ---------- 游戏行为统计（本地、加密落盘） ----------
        /// <summary>统计模型。采集点见 Patch/GameStatsHooks，保存前整体加密。</summary>
        public readonly GameStats GameStats = new GameStats();
        /// <summary>统计数据有变化，待写盘。</summary>
        public bool StatsDirty;
        /// <summary>统计文件路径（由插件入口注入）。</summary>
        public string StatsPath;

        /// <summary>打开关于窗口（统一入口）。</summary>
        public void OpenAbout()
        {
            ShowAbout = true;
        }

        /// <summary>切换浮动组件卡的显示（统一入口：快捷键与设置页开关都走这里）。</summary>
        public void ToggleFloatCards()
        {
            Config.FloatCardsEnabled.Value = !Config.FloatCardsEnabled.Value;
            Note(Config.FloatCardsEnabled.Value ? "已显示组件卡" : "已隐藏组件卡");
        }

        // ---------- 队友详情窗口 ----------
        /// <summary>是否正在查看某位队友的详细状态。</summary>
        public bool ShowMateDetail;
        /// <summary>当前查看的队友在快照 Mates 列表中的索引。</summary>
        public int MateDetailIndex = -1;
        /// <summary>打开时的队友名：用于校验列表变化/目标离开后自动关闭或重新定位。</summary>
        private string _mateDetailName;
        /// <summary>详情窗口自己的滚动位置（与主面板各标签页独立）。</summary>
        public readonly ScrollState MateDetailScroll = new ScrollState();

        /// <summary>打开某位队友的详情窗口。</summary>
        public void OpenMateDetail(int index, string name)
        {
            ShowMateDetail = true;
            MateDetailIndex = index;
            _mateDetailName = name;
            MateDetailScroll.Offset = 0f;
            MateDetailScroll.Dragging = false;
        }

        public void CloseMateDetail()
        {
            ShowMateDetail = false;
            MateDetailIndex = -1;
            _mateDetailName = null;
        }

        /// <summary>是否有任何模态窗口开着（模态期间主面板不响应输入）。</summary>
        public bool HasModal
        {
            get { return ShowAbout || ShowMateDetail; }
        }

        /// <summary>
        /// 取当前要查看的队友。列表因离开/死亡/重排而变化时按名字重新定位，找不到就自动关闭。
        /// </summary>
        public MateReading? CurrentMateDetail()
        {
            if (!ShowMateDetail) return null;

            System.Collections.Generic.List<MateReading> mates = Snapshot.Mates;
            if (MateDetailIndex < 0 || MateDetailIndex >= mates.Count)
            {
                CloseMateDetail();
                return null;
            }

            MateReading mate = mates[MateDetailIndex];
            if (string.IsNullOrEmpty(_mateDetailName) || mate.Name == _mateDetailName)
            {
                return mate;
            }

            for (int i = 0; i < mates.Count; i++)
            {
                if (mates[i].Name != _mateDetailName) continue;
                MateDetailIndex = i;
                return mates[i];
            }

            CloseMateDetail();
            return null;
        }

        public Theme Theme;
        public HudStyles Styles;
        public IconProvider Icons;
        public Rect Window;
        /// <summary>
        /// 每个标签页各自的滚动位置。
        /// 长度**由 TabCount 算出**而不是写死：以前写死 4 个，后来加了「统计」页就变成
        /// <c>Scrolls[4]</c> 越界，表现是整页空白（异常被绘制层 try/catch 吞掉）。
        /// </summary>
        public readonly ScrollState[] Scrolls = CreateScrolls();

        private static ScrollState[] CreateScrolls()
        {
            ScrollState[] states = new ScrollState[TabCount];
            for (int i = 0; i < states.Length; i++) states[i] = new ScrollState();
            return states;
        }

        /// <summary>供生命体征页复用的暂存缓冲（避免每帧新建 List，也避免用静态字段）。</summary>
        public readonly System.Collections.Generic.List<int> ScratchPrimary = new System.Collections.Generic.List<int>(24);
        public readonly System.Collections.Generic.List<int> ScratchSecondary = new System.Collections.Generic.List<int>(24);

        public bool Dragging;
        public Vector2 DragOffset;
        public bool Resizing;
        public Vector2 ResizeStart;
        public Rect ResizeOrigin;

        /// <summary>
        /// 波形区的实际像素尺寸：由绘制阶段写入，Update 阶段据此分配纸带纹理，
        /// 这样纹理重建只发生在 Update（数据阶段），绘制阶段不产生副作用。
        /// </summary>
        public int WaveWidth;
        public int WaveHeight;

        public bool GeometryDirty;
        public float GeometryDirtyAt;

        /// <summary>X/Y 是否处于「自动摆放」状态（自动摆放期间不写回配置）。</summary>
        private bool _autoPlacedX = true;
        private bool _autoPlacedY = true;

        private float _lastScale = -1f;
        private int _lastFont = -1;
        private string _lastFontMode;
        private string _lastSkin;
        private bool _windowInitialized;

        public HudState(PluginConfig config)
        {
            Config = config;
            Reader = new BodyReader(Mp);
            Waves = new WaveformSet();
            Tab = Mathf.Clamp(config.Tab.Value, 0, TabCount - 1);
        }

        /// <summary>当前标签页的滚动状态。</summary>
        public ScrollState Scroll
        {
            get { return Scrolls[Mathf.Clamp(Tab, 0, TabCount - 1)]; }
        }

        /// <summary>
        /// 构建或重建样式资源。必须在 OnGUI 内调用（GUIStyle 依赖 GUI.skin），
        /// 当缩放 / 字号 / 字体来源变化时整体重建。
        /// </summary>
        public void EnsureUi()
        {
            float scale = Mathf.Clamp(Config.Scale.Value, 0.7f, 1.5f);
            int fontSize = Mathf.Clamp(Config.FontSize.Value, 11, 20);
            string fontMode = Config.FontMode.Value ?? "game";
            string skin = Config.Skin.Value ?? "fox";

            // emoji 开关每帧同步（不需要重建样式，图标是按需查询的）
            IconProvider.EmojiEnabled = Config.ShowEmoji.Value;

            bool changed = Styles == null
                || Mathf.Abs(scale - _lastScale) > 0.001f
                || fontSize != _lastFont
                || fontMode != _lastFontMode
                || skin != _lastSkin;

            if (!changed)
            {
                if (!_windowInitialized) SyncWindowSize();
                return;
            }

            _lastScale = scale;
            _lastFont = fontSize;
            _lastFontMode = fontMode;
            _lastSkin = skin;

            if (Styles != null)
            {
                Styles.Dispose();
                Styles = null;
            }
            if (Icons != null)
            {
                Icons.Dispose();
                Icons = null;
            }

            // 皮肤必须在构建样式与纹理之前设置：圆角半径、配色都由它决定
            Theme.Skin = string.Equals(skin, "game", System.StringComparison.OrdinalIgnoreCase)
                ? Theme.UiSkin.Game
                : Theme.UiSkin.Fox;

            Theme = new Theme(scale, fontSize);
            Fonts.Invalidate();
            Font font = Fonts.Resolve(fontMode);
            Styles = new HudStyles(Theme, font);
            Icons = new IconProvider(Theme.Pxi(20f));

            SyncWindowSize();
        }

        /// <summary>按配置与当前缩放重算窗口尺寸/位置。</summary>
        public void SyncWindowSize()
        {
            if (Theme == null) return;

            float width = Mathf.Max(Config.ClampedWidth() * Theme.Scale, MinWindowWidth());
            float height = Mathf.Max(Config.ClampedHeight() * Theme.Scale, MinWindowHeight());

            float x;
            float y;
            if (_windowInitialized)
            {
                // 只调整尺寸：保留当前左上角，并保留「自动摆放」标志
                // （Scale 变化时若在此重算标志，会把 -1 哨兵弄丢，导致换分辨率不再自动摆放）
                x = _autoPlacedX ? PluginConfig.AutoPos : Window.x;
                y = _autoPlacedY ? PluginConfig.AutoPos : Window.y;
            }
            else
            {
                x = Config.WindowX.Value;
                y = Config.WindowY.Value;
                _autoPlacedX = x <= PluginConfig.AutoPos;
                _autoPlacedY = y <= PluginConfig.AutoPos;
            }

            if (_autoPlacedX) x = Screen.width - width - Theme.Px(24f);
            if (_autoPlacedY) y = (Screen.height - height) * 0.5f;

            Window = new Rect(x, y, width, height);
            _windowInitialized = true;
            ClampToScreen();
        }

        /// <summary>
        /// 面板最小宽度：两列卡片各自至少能放下一段文字。
        /// </summary>
        public float MinWindowWidth()
        {
            return Theme.Px(360f);
        }

        /// <summary>
        /// 面板最小高度：必须容纳标题栏 + 告警 + 波形 + 标签栏 + 至少一组卡片 + 底栏，
        /// 否则会出现「标签栏被裁到面板外、用户再也点不到」的死局。
        /// </summary>
        public float MinWindowHeight()
        {
            float chrome = Theme.Px(Theme.PadSize) * 2f
                + Theme.HeaderHeight()
                + Theme.TabHeight()
                + Theme.SmallLine()
                + Theme.Px(26f);

            if (Config.AlertsEnabled.Value) chrome += Theme.AlarmHeight() + Theme.Px(5f);
            if (Config.WaveformsEnabled.Value) chrome += 3f * (Theme.StripHeight() + Theme.Px(4f));

            float content = Theme.SectionHeight() + Theme.VitalCardHeight() + Theme.Px(14f);
            return chrome + content;
        }

        public void ClampToScreen()
        {
            if (Theme == null) return;

            float minWidth = Mathf.Min(MinWindowWidth(), Screen.width);
            float minHeight = Mathf.Min(MinWindowHeight(), Screen.height);

            float w = Mathf.Clamp(Window.width, minWidth, Mathf.Max(minWidth, Screen.width));
            float h = Mathf.Clamp(Window.height, minHeight, Mathf.Max(minHeight, Screen.height));

            float x = Mathf.Clamp(Window.x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(Window.y, 0f, Mathf.Max(0f, Screen.height - h));

            Window = new Rect(x, y, w, h);
        }

        /// <summary>把当前窗口几何写回配置（未缩放值）。</summary>
        public void SaveGeometry()
        {
            if (Theme == null) return;

            // 自动摆放状态下不写 X/Y，保留 -1 哨兵，这样换分辨率仍会自动重新摆放
            if (!_autoPlacedX) Config.WindowX.Value = Window.x;
            if (!_autoPlacedY) Config.WindowY.Value = Window.y;

            Config.WindowWidth.Value = Mathf.Clamp(Window.width / Theme.Scale, 360f, 1600f);
            Config.WindowHeight.Value = Mathf.Clamp(Window.height / Theme.Scale, 300f, 1600f);
            GeometryDirty = false;
        }

        /// <summary>标记几何需要落盘。<paramref name="manualPlacement"/> 为 true 表示用户亲手拖动了位置。</summary>
        public void MarkGeometryDirty(bool manualPlacement)
        {
            if (manualPlacement)
            {
                _autoPlacedX = false;
                _autoPlacedY = false;
            }
            GeometryDirty = true;
            GeometryDirtyAt = Time.unscaledTime;
        }

        public void ResetWindowPosition()
        {
            Config.WindowX.Value = PluginConfig.AutoPos;
            Config.WindowY.Value = PluginConfig.AutoPos;
            _windowInitialized = false;
            SyncWindowSize();
            MarkGeometryDirty(false);
        }

        /// <summary>切换面板可见性。</summary>
        public void ToggleVisible()
        {
            Visible = !Visible;

            if (!Visible)
            {
                // 隐藏面板时一并收起模态窗口，避免下次显示时它们突然冒出来
                ShowAbout = false;
                CloseMateDetail();
                CapturingTarget = 0;
            }
        }

        public void SetTab(int tab)
        {
            tab = Mathf.Clamp(tab, 0, TabCount - 1);
            if (tab == Tab) return;

            TabAnimDir = tab > Tab ? 1f : -1f;
            TabAnimStart = Time.unscaledTime;
            Tab = tab;
            Config.Tab.Value = tab;
        }

        /// <summary>1..0 的标签页切换进度（用于滑移动画）。</summary>
        public float TabAnimation()
        {
            float elapsed = Time.unscaledTime - TabAnimStart;
            float t = Mathf.Clamp01(elapsed / 0.18f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>面板淡入淡出推进，返回是否仍需要绘制。</summary>
        public bool AdvanceVisibility(float unscaledDeltaTime)
        {
            float target = Visible ? 1f : 0f;
            PanelVisibility = Mathf.MoveTowards(PanelVisibility, target, unscaledDeltaTime / PanelFadeSeconds);
            return PanelVisibility > 0.002f;
        }

        public void Note(string message)
        {
            StatusNote = message;
            StatusNoteAt = Time.unscaledTime;
        }

        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        /// <summary>分辨率变化后重新夹紧窗口，避免面板跑到屏幕外。</summary>
        public void CheckScreenChange()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            ClampToScreen();
        }

        public void Dispose()
        {
            if (Styles != null)
            {
                Styles.Dispose();
                Styles = null;
            }
            if (Icons != null)
            {
                Icons.Dispose();
                Icons = null;
            }
            if (Waves != null)
            {
                Waves.Dispose();
                Waves = null;
            }

            Avatars.Dispose();
            FloatingCards.Dispose(Config);

            // emoji 缓存跨样式重建共享，插件销毁时统一释放
            IconProvider.DisposeShared();
        }
    }
}
