using BepInEx.Configuration;
using UnityEngine;

namespace CuStatsHud.Core
{
    
    internal sealed class PluginConfig
    {
        
        public readonly ConfigEntry<float> Opacity;
        public readonly ConfigEntry<float> Scale;
        public readonly ConfigEntry<int> FontSize;
        public readonly ConfigEntry<string> FontMode;
        public readonly ConfigEntry<int> Tab;

        public readonly ConfigEntry<string> Skin;

        public readonly ConfigEntry<float> CustomHue;
        
        public readonly ConfigEntry<float> CustomDark;
        
        public readonly ConfigEntry<float> CustomSaturation;
        
        public readonly ConfigEntry<bool> BackgroundEnabled;
        
        public readonly ConfigEntry<float> BackgroundOpacity;
        
        public readonly ConfigEntry<string> BackgroundFile;

        public readonly ConfigEntry<bool> ShowEmoji;

        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<float> WindowWidth;
        public readonly ConfigEntry<float> WindowHeight;

        public readonly ConfigEntry<KeyboardShortcut> ToggleKey;
        
        public readonly ConfigEntry<KeyboardShortcut> FloatKey;
        public readonly ConfigEntry<bool> Resizable;

        public readonly ConfigEntry<bool> WaveformsEnabled;
        public readonly ConfigEntry<bool> AlertsEnabled;
        public readonly ConfigEntry<bool> AlertFlash;
        public readonly ConfigEntry<bool> ShowLimits;
        public readonly ConfigEntry<bool> ShowNormalSecondary;

        public readonly ConfigEntry<string> FloatCards;

        public readonly ConfigEntry<bool> FloatCardsEnabled;

        public readonly ConfigEntry<int> GridSize;

        public readonly ConfigEntry<string> AuthorName;
        public readonly ConfigEntry<string> AuthorUid;
        
        public readonly ConfigEntry<string> GitHubUrl;

        public const float DefaultWidth = 566f;
        public const float DefaultHeight = 742f;

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
                "界面风格：fox = 原版 UI（深蓝黑 + 紫蓝）/ game = 游戏风格（灰阶 + 青绿）/ custom = 自定义");
            ShowEmoji = cfg.Bind("UI", "ShowEmoji", true,
                "是否显示 emoji 小图标（关闭后改用程序化绘制的几何图标）");

            CustomHue = cfg.Bind("Theme", "Hue", 0.62f,
                new ConfigDescription("自定义配色的色相（0-1；0.0 红 / 0.33 绿 / 0.62 蓝紫 / 0.83 品红）",
                    new AcceptableValueRange<float>(0f, 1f)));
            CustomDark = cfg.Bind("Theme", "Dark", 0.35f,
                new ConfigDescription("自定义配色的底色明度（0 = 很暗 / 1 = 偏亮，但始终是深色界面）",
                    new AcceptableValueRange<float>(0f, 1f)));
            CustomSaturation = cfg.Bind("Theme", "Saturation", 0.5f,
                new ConfigDescription("自定义配色的强调强度（0 = 近乎灰阶 / 1 = 强调色最鲜艳）",
                    new AcceptableValueRange<float>(0f, 1f)));
            BackgroundEnabled = cfg.Bind("Theme", "BackgroundEnabled", true,
                "是否显示自定义底图（关掉后保留已选图片，可随时再打开）");
            BackgroundOpacity = cfg.Bind("Theme", "BackgroundOpacity", 0.35f,
                new ConfigDescription("自定义底图的透明度（1% ~ 100%）",
                    new AcceptableValueRange<float>(0.01f, 1f)));
            BackgroundFile = cfg.Bind("Theme", "BackgroundFile", "CUStatsHud-bg.png",
                "自定义底图的文件名（放在 BepInEx/config 下；支持 png / jpg）");

            WindowX = cfg.Bind("Window", "X", AutoPos, "面板左上角 X 坐标（-1 = 自动靠右摆放）");
            WindowY = cfg.Bind("Window", "Y", AutoPos, "面板左上角 Y 坐标（-1 = 自动垂直居中）");
            WindowWidth = cfg.Bind("Window", "Width", DefaultWidth,
                new ConfigDescription("面板宽度（未经 Scale 缩放，最小 360）", new AcceptableValueRange<float>(360f, 1600f)));
            WindowHeight = cfg.Bind("Window", "Height", DefaultHeight,
                new ConfigDescription("面板高度（未经 Scale 缩放，最小 300）", new AcceptableValueRange<float>(300f, 1600f)));

            ToggleKey = cfg.Bind("Keys", "Toggle", new KeyboardShortcut(KeyCode.N),
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
            GitHubUrl = cfg.Bind("About", "GitHubUrl", "https://github.com/blue-star-fox/CU-STARS-HUD",
                "GitHub 仓库地址（关于窗口里点击可打开浏览器）");

            FloatCards = cfg.Bind("FloatCards", "Cards", string.Empty,
                "浮动副本卡片：kind|key|x|y|opacity，多条用 ; 分隔。在「组件卡摆放」界面里拖拽生成，界面会自动写回这里");
            FloatCardsEnabled = cfg.Bind("FloatCards", "Enabled", true,
                "组件卡是否显示（可在设置页切换，或由快捷键切换）");
            GridSize = cfg.Bind("FloatCards", "GridSize", 24,
                new ConfigDescription("摆放方格边长（像素）：越小网格越密，8 - 96",
                    new AcceptableValueRange<int>(8, 96)));
        }

        public float ClampedWidth()
        {
            return Mathf.Max(360f, WindowWidth.Value);
        }

        public float ClampedHeight()
        {
            return Mathf.Max(300f, WindowHeight.Value);
        }
    }
}
