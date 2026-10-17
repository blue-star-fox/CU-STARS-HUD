using BepInEx.Configuration;
using UnityEngine;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 插件全部持久化配置。
    ///
    /// 键名与 v1.2 完全兼容：旧配置文件（[UI] Opacity/Tab/FontSize/FontMode/Scale、
    /// [About] BilibiliName/BilibiliUID）无需改动即可继续使用。
    /// </summary>
    internal sealed class PluginConfig
    {
        // ---------- 界面 ----------
        public readonly ConfigEntry<float> Opacity;
        public readonly ConfigEntry<float> Scale;
        public readonly ConfigEntry<int> FontSize;
        public readonly ConfigEntry<string> FontMode;
        public readonly ConfigEntry<int> Tab;

        /// <summary>界面皮肤：fox = CU-UI（小狐狸 UI，圆角现代风）/ game = 游戏 UI 风格（方角硬边框）。</summary>
        public readonly ConfigEntry<string> Skin;

        /// <summary>是否显示 emoji 图标（关闭后改用程序化绘制的几何图标）。</summary>
        public readonly ConfigEntry<bool> ShowEmoji;

        // ---------- 窗口几何（-1 = 自动摆放）----------
        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<float> WindowWidth;
        public readonly ConfigEntry<float> WindowHeight;

        // ---------- 交互 ----------
        public readonly ConfigEntry<KeyboardShortcut> ToggleKey;
        /// <summary>显示/隐藏浮动组件卡的快捷键。</summary>
        public readonly ConfigEntry<KeyboardShortcut> FloatKey;
        public readonly ConfigEntry<bool> Resizable;

        // ---------- 监护功能 ----------
        public readonly ConfigEntry<bool> WaveformsEnabled;
        public readonly ConfigEntry<bool> AlertsEnabled;
        public readonly ConfigEntry<bool> AlertFlash;
        public readonly ConfigEntry<bool> ShowLimits;
        public readonly ConfigEntry<bool> ShowNormalSecondary;

        // ---------- 浮动副本卡片 ----------
        /// <summary>序列化的浮动卡片列表（格式见 FloatCardRules），由界面自动写入。</summary>
        public readonly ConfigEntry<string> FloatCards;

        /// <summary>组件卡当前是否显示（摆放界面里保存，或由快捷键切换）。</summary>
        public readonly ConfigEntry<bool> FloatCardsEnabled;

        /// <summary>摆放方格的边长（像素），决定网格密度。</summary>
        public readonly ConfigEntry<int> GridSize;

        // ---------- 关于 ----------
        public readonly ConfigEntry<string> AuthorName;
        public readonly ConfigEntry<string> AuthorUid;

        /// <summary>窗口宽度/高度的出厂默认值（未经 Scale 缩放）。</summary>
        public const float DefaultWidth = 566f;
        public const float DefaultHeight = 742f;

        /// <summary>窗口位置的“自动摆放”哨兵值。</summary>
        public const float AutoPos = -1f;

        public PluginConfig(ConfigFile cfg)
        {
            Opacity = cfg.Bind("UI", "Opacity", 0.94f,
                new ConfigDescription("面板不透明度 (0.3 - 1.0)", new AcceptableValueRange<float>(0.3f, 1f)));
            Scale = cfg.Bind("UI", "Scale", 1f,
                new ConfigDescription("界面比例缩放 (0.7 - 1.5)", new AcceptableValueRange<float>(0.7f, 1.5f)));
            FontSize = cfg.Bind("UI", "FontSize", 15,
                new ConfigDescription("基准字号 (11 - 20)，监护仪数值字号由此自动放大", new AcceptableValueRange<int>(11, 20)));
            FontMode = cfg.Bind("UI", "FontMode", "auto",
                new ConfigDescription("字体来源：auto=自动(系统字体优先，字形最全) / system=系统字体 / game=游戏字体"));
            Tab = cfg.Bind("UI", "Tab", 0,
                new ConfigDescription("当前标签页 (0=生命体征 1=肢体 2=队友 3=设置 4=统计)", new AcceptableValueRange<int>(0, 4)));

            Skin = cfg.Bind("UI", "Skin", "fox",
                "界面风格：fox = CU-UI（小狐狸 UI，圆角现代风）/ game = 游戏 UI 风格（方角硬边框）");
            ShowEmoji = cfg.Bind("UI", "ShowEmoji", true,
                "是否显示 emoji 小图标（关闭后改用程序化绘制的几何图标）");

            WindowX = cfg.Bind("Window", "X", AutoPos, "面板左上角 X 坐标（-1 = 自动靠右摆放）");
            WindowY = cfg.Bind("Window", "Y", AutoPos, "面板左上角 Y 坐标（-1 = 自动垂直居中）");
            WindowWidth = cfg.Bind("Window", "Width", DefaultWidth,
                new ConfigDescription("面板宽度（未经 Scale 缩放，最小 360）", new AcceptableValueRange<float>(360f, 1600f)));
            WindowHeight = cfg.Bind("Window", "Height", DefaultHeight,
                new ConfigDescription("面板高度（未经 Scale 缩放，最小 300）", new AcceptableValueRange<float>(300f, 1600f)));

            ToggleKey = cfg.Bind("Keys", "Toggle", new KeyboardShortcut(KeyCode.F1),
                "显示/隐藏面板的快捷键（也可在设置页内点击“重新绑定”后直接按键）");
            FloatKey = cfg.Bind("Keys", "FloatToggle", new KeyboardShortcut(KeyCode.F2),
                "显示/隐藏浮动组件卡的快捷键");
            Resizable = cfg.Bind("Keys", "Resizable", true, "是否允许拖动右下角调整面板大小");

            WaveformsEnabled = cfg.Bind("Monitor", "Waveforms", true, "是否显示波形条（ECG 来自游戏真实心电曲线）");
            AlertsEnabled = cfg.Bind("Monitor", "Alerts", true, "是否显示告警横幅（危急状态判定取自游戏自身 isCriticallyDying）");
            AlertFlash = cfg.Bind("Monitor", "AlertFlash", true, "危急告警时是否闪烁");
            ShowLimits = cfg.Bind("Monitor", "ShowLimits", true, "是否在数值旁显示参考范围");
            ShowNormalSecondary = cfg.Bind("Monitor", "ShowNormalSecondary", false,
                "true = 次要指标（心颤/败血/辐射/血胸等）始终显示；false = 仅在异常时出现");

            AuthorName = cfg.Bind("About", "BilibiliName", "纯种小狐狸", "作者 B 站昵称");
            AuthorUid = cfg.Bind("About", "BilibiliUID", "1951065665", "作者 B 站 UID");

            FloatCards = cfg.Bind("FloatCards", "Cards", string.Empty,
                "浮动副本卡片：kind|key|x|y|opacity，多条用 ; 分隔。在「组件卡摆放」界面里拖拽生成，界面会自动写回这里");
            FloatCardsEnabled = cfg.Bind("FloatCards", "Enabled", true,
                "组件卡是否显示（可在设置页切换，或由快捷键切换）");
            GridSize = cfg.Bind("FloatCards", "GridSize", 24,
                new ConfigDescription("摆放方格边长（像素）：越小网格越密，8 - 96",
                    new AcceptableValueRange<int>(8, 96)));
        }

        /// <summary>窗口宽度换算值：配置 + 下限保护。</summary>
        public float ClampedWidth()
        {
            return Mathf.Max(360f, WindowWidth.Value);
        }

        /// <summary>窗口高度换算值：配置 + 下限保护。</summary>
        public float ClampedHeight()
        {
            return Mathf.Max(300f, WindowHeight.Value);
        }
    }
}
