using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 监护仪配色与尺寸度量。
    ///
    /// 视觉语言参考床旁监护仪：近黑底 + 淡网格 + 每条生命体征一个通道色，
    /// 数值正常时保持中性白，异常时按分级染成 青(注意) / 琥珀(警告) / 红(危急)，
    /// 这样“平静时安静、危急时刺眼”，而不是把所有数值都涂成彩色。
    /// </summary>
    internal sealed class Theme
    {
        /// <summary>界面皮肤。</summary>
        public enum UiSkin
        {
            /// <summary>CU-UI（小狐狸 UI）：现代圆角深色。</summary>
            Fox = 0,
            /// <summary>游戏 UI 风格：方角硬边框、灰阶冷调。</summary>
            Game = 1
        }

        /// <summary>
        /// 一套配色 + 圆角参数。
        /// 配色全部走属性转发，所以切换皮肤时**所有调用点都不用改**。
        /// </summary>
        private struct Palette
        {
            public Color PanelBg, PanelBgTop, CardBg, CardBgAlt, HeaderBg, Line, LineSoft, Outline;
            public Color GridMinor, GridMajor;
            public Color TextPrimary, TextSecondary, TextMuted, TextOnAccent;
            public Color Accent;
            public string HexPrimary, HexSecondary, HexMuted, HexAccent;
            public float Radius, PanelRadius, MinRadius;
            public int BarRadius;
        }

        private static readonly Palette FoxPalette = BuildFoxPalette();
        private static readonly Palette GamePalette = BuildGamePalette();
        private static bool _gameSkin;

        /// <summary>当前皮肤（由 HudState 在构建样式前设置）。</summary>
        public static UiSkin Skin
        {
            get { return _gameSkin ? UiSkin.Game : UiSkin.Fox; }
            set { _gameSkin = value == UiSkin.Game; }
        }

        private static Palette P
        {
            get { return _gameSkin ? GamePalette : FoxPalette; }
        }

        // ---------- 底色 ----------
        public static Color PanelBg { get { return P.PanelBg; } }
        public static Color PanelBgTop { get { return P.PanelBgTop; } }
        public static Color CardBg { get { return P.CardBg; } }
        public static Color CardBgAlt { get { return P.CardBgAlt; } }
        public static Color HeaderBg { get { return P.HeaderBg; } }
        public static Color Line { get { return P.Line; } }
        public static Color LineSoft { get { return P.LineSoft; } }
        /// <summary>卡片描边：现代系统设计里的 1px 轮廓。</summary>
        public static Color Outline { get { return P.Outline; } }
        public static Color GridMinor { get { return P.GridMinor; } }
        public static Color GridMajor { get { return P.GridMajor; } }

        // ---------- 文字 ----------
        public static Color TextPrimary { get { return P.TextPrimary; } }
        public static Color TextSecondary { get { return P.TextSecondary; } }
        public static Color TextMuted { get { return P.TextMuted; } }
        public static Color TextOnAccent { get { return P.TextOnAccent; } }

        // ---------- 强调 / 分级 ----------
        public static Color Accent { get { return P.Accent; } }
        // 分级色与通道色是**功能色**，不随皮肤变化（红黄绿在任何风格下含义都一样）
        public static readonly Color Normal = Hex("#36D399");
        public static readonly Color Notice = Hex("#4FC3F7");
        public static readonly Color Warning = Hex("#FFB020");
        public static readonly Color Critical = Hex("#FF4D4F");

        // ---------- 波形通道色（与床旁监护仪一致）----------
        public static readonly Color ChEcg = Hex("#3BE081");
        public static readonly Color ChPleth = Hex("#38D2E8");
        public static readonly Color ChResp = Hex("#FFD166");

        public static string HexPrimary { get { return P.HexPrimary; } }
        public static string HexSecondary { get { return P.HexSecondary; } }
        public static string HexMuted { get { return P.HexMuted; } }
        public static string HexAccent { get { return P.HexAccent; } }
        public const string HexNormal = "#36D399";
        public const string HexNotice = "#4FC3F7";
        public const string HexWarning = "#FFB020";
        public const string HexCritical = "#FF4D4F";

        // ---------- 尺寸（未经缩放，与字体无关的部分）----------
        public const float PadSize = 14f;
        public const float GapSize = 8f;
        /// <summary>卡片圆角半径（随皮肤变化：游戏风格几乎是直角）。</summary>
        public static float RadiusSize { get { return P.Radius; } }
        /// <summary>面板圆角半径（现代系统设计里外框圆角更大）。</summary>
        public static float PanelRadiusSize { get { return P.PanelRadius; } }
        /// <summary>圆角下限：游戏风格下要能真正画到接近直角。</summary>
        public static float MinRadius { get { return P.MinRadius; } }
        /// <summary>细条（进度条/滑块轨道）的圆角半径。</summary>
        public static int BarRadius { get { return P.BarRadius; } }

        // ---------------------------------------------------------------

        private static Palette BuildFoxPalette()
        {
            Palette p = new Palette();
            p.PanelBg = Hex("#0C1219");
            p.PanelBgTop = Hex("#141D27");
            p.CardBg = Hex("#161F2A");
            p.CardBgAlt = Hex("#121A23");
            p.HeaderBg = Hex("#101821");
            p.Line = Hex("#26343F");
            p.LineSoft = Hex("#1A242E");
            p.Outline = Hex("#2A3844");
            p.GridMinor = new Color(1f, 1f, 1f, 0.022f);
            p.GridMajor = new Color(1f, 1f, 1f, 0.05f);

            p.TextPrimary = Hex("#E8F0F7");
            p.TextSecondary = Hex("#93A6B8");
            p.TextMuted = Hex("#5C6E7E");
            p.TextOnAccent = Hex("#0B1015");

            p.Accent = Hex("#7C9EFF");

            p.HexPrimary = "#E8F0F7";
            p.HexSecondary = "#93A6B8";
            p.HexMuted = "#5C6E7E";
            p.HexAccent = "#7C9EFF";

            p.Radius = 12f;
            p.PanelRadius = 18f;
            p.MinRadius = 4f;
            p.BarRadius = 2;
            return p;
        }

        /// <summary>
        /// 游戏 UI 风格：只用配色区分（灰阶冷调 + 更亮的 1px 硬边框 + 青绿强调色）。
        /// **圆角与 CU-UI 保持一致** —— 圆角是这套界面的基本语言，换风格不该把它改掉。
        /// </summary>
        private static Palette BuildGamePalette()
        {
            Palette p = new Palette();
            p.PanelBg = Hex("#121419");
            p.PanelBgTop = Hex("#1B1E25");
            p.CardBg = Hex("#1B1E25");
            p.CardBgAlt = Hex("#0D0F13");
            p.HeaderBg = Hex("#171A20");
            p.Line = Hex("#33383F");
            p.LineSoft = Hex("#23272D");
            p.Outline = Hex("#4C535C");
            p.GridMinor = new Color(1f, 1f, 1f, 0.024f);
            p.GridMajor = new Color(1f, 1f, 1f, 0.055f);

            p.TextPrimary = Hex("#E2E6EC");
            p.TextSecondary = Hex("#98A2B0");
            p.TextMuted = Hex("#616B78");
            p.TextOnAccent = Hex("#0A0C10");

            // 游戏 UI 的强调色偏青绿，比 CU-UI 的蓝紫更“硬”
            p.Accent = Hex("#5FD3C0");

            p.HexPrimary = "#E2E6EC";
            p.HexSecondary = "#98A2B0";
            p.HexMuted = "#616B78";
            p.HexAccent = "#5FD3C0";

            // 圆角与 CU-UI 相同（换风格只换配色）
            p.Radius = 12f;
            p.PanelRadius = 18f;
            p.MinRadius = 4f;
            p.BarRadius = 2;
            return p;
        }


        // ---------- 行高换算 ----------
        // 所有含文字的行高都必须由字体度量推导：字号可在 11-20 之间调整、
        // 缩放可在 0.7-1.5 之间调整，写死常量必然在某个档位重叠或裁切。

        /// <summary>正文行高。</summary>
        public float LabelLine()
        {
            return Font(1) * 1.35f;
        }

        /// <summary>小字行高。</summary>
        public float SmallLine()
        {
            return Font(2) * 1.5f;
        }

        /// <summary>大数值行高。</summary>
        public float ValueLine()
        {
            return Font(3) * 1.5f;
        }

        public float BarHeight()
        {
            // 下限也要跟着绘制期缩放走，否则缩小的卡片里进度条会显得过粗
            return Mathf.Max(3f * UiScale, Px(4f));
        }

        /// <summary>标题栏：左侧标题+副标题，右侧关闭按钮。数值不在这里显示（见波形区通道块）。</summary>
        public float HeaderHeight()
        {
            float left = Px(6f) + Font(0) * 1.3f + Font(2) * 1.5f;
            float right = Px(6f) + CloseButtonSize();
            return Mathf.Max(left, right) + Px(6f);
        }

        /// <summary>右上角关闭按钮尺寸。</summary>
        public float CloseButtonSize()
        {
            return Px(22f);
        }

        /// <summary>主要体征卡片：标签行（含右侧参考范围）+ 大数值 + 范围条。</summary>
        public float VitalCardHeight()
        {
            return Px(14f) + LabelLine() + ValueLine() + BarHeight() + Px(6f);
        }

        /// <summary>肢体检视卡：名称行 + 皮肤/肌肉双条 + 指标行 + 状态标签行。</summary>
        public float LimbRowHeight()
        {
            float barsLine = Mathf.Max(SmallLine(), BarHeight() + Px(5f));
            return Px(14f) + LabelLine() + barsLine + SmallLine() + SmallLine() + Px(8f);
        }

        /// <summary>队友卡：头像/姓名 + 指标 + 摘要 + 血条。</summary>
        public float MateCardHeight()
        {
            float text = LabelLine() + SmallLine() * 1.2f + SmallLine();
            return Px(14f) + Mathf.Max(AvatarSize(), text) + BarHeight() + Px(10f);
        }

        /// <summary>队友头像直径（圆形）。</summary>
        public float AvatarSize()
        {
            return Px(40f);
        }

        /// <summary>紧凑单行。</summary>
        public float CompactRowHeight()
        {
            return Px(6f) + SmallLine();
        }

        /// <summary>分组小标题。</summary>
        public float SectionHeight()
        {
            return Px(3f) + SmallLine();
        }

        /// <summary>告警横幅。</summary>
        public float AlarmHeight()
        {
            return Px(7f) + SmallLine();
        }

        /// <summary>标签栏。</summary>
        public float TabHeight()
        {
            return Px(9f) + SmallLine();
        }

        /// <summary>波形条高度：左侧通道块需要「通道名 + 大数值」两行。</summary>
        public float StripHeight()
        {
            return Px(8f) + SmallLine() + ValueLine();
        }

        /// <summary>波形条左侧通道块宽度。</summary>
        public float ChannelBlockWidth()
        {
            return Px(70f);
        }

        /// <summary>当前缩放系数（0.7 - 1.5）。</summary>
        public readonly float Scale;

        /// <summary>基准字号（来自配置）。</summary>
        public readonly int BaseFont;

        public Theme(float scale, int baseFont)
        {
            Scale = Mathf.Clamp(scale, 0.7f, 1.5f);
            BaseFont = Mathf.Clamp(baseFont, 11, 20);
        }

        /// <summary>
        /// 绘制期的附加缩放，专供浮动组件卡 / 组件库预览这类「整卡等比缩放」的场合。
        ///
        /// 所有尺寸入口（<see cref="Px"/> / <see cref="Pxi"/> / <see cref="Font"/> 及各行高方法）
        /// 都会乘上它，所以卡片里的图形元素自动等比缩放，而且**绘制坐标仍是真实屏幕坐标**。
        ///
        /// 之所以不用 <c>GUI.matrix</c>：矩阵缩放与滚动区的 <c>GUI.BeginGroup</c> 裁剪会互相干扰，
        /// 落在 group 内的矩阵分支会整个画不出来（体征卡刚好是 1:1 才幸免）。
        ///
        /// 注意 Theme 是全局共享的，必须在 try/finally 里设置与恢复。
        /// </summary>
        public float UiScale = 1f;

        /// <summary>把设计像素换算为实际像素。</summary>
        public float Px(float designPixels)
        {
            return designPixels * Scale * UiScale;
        }

        public int Pxi(float designPixels)
        {
            return Mathf.RoundToInt(designPixels * Scale * UiScale);
        }

        /// <summary>按层级取字号：0=标题 1=正文 2=小字 3=数值(大) 4=数值(特大)。</summary>
        public int Font(int level)
        {
            int size;
            switch (level)
            {
                case 0: size = Mathf.RoundToInt(BaseFont * 1.05f) + 1; break;
                case 1: size = BaseFont; break;
                case 2: size = Mathf.Max(9, BaseFont - 2); break;
                case 3: size = Mathf.RoundToInt(BaseFont * 1.35f); break;
                case 4: size = Mathf.RoundToInt(BaseFont * 1.75f); break;
                default: size = BaseFont; break;
            }

            // GUIStyle 的字号是构建时固化的、改不了，所以缩放后的字号要由调用方
            // 用 rich text 的 <size> 覆盖（见 Widgets.Fit）。
            if (UiScale != 1f) size = Mathf.Max(6, Mathf.RoundToInt(size * UiScale));
            return size;
        }

        /// <summary>分级对应的显示色。</summary>
        public static Color SeverityColor(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return Critical;
                case Severity.Warning: return Warning;
                case Severity.Notice: return Notice;
                case Severity.Unknown: return TextMuted;
                default: return Normal;
            }
        }

        /// <summary>数值文字色：正常保持中性白，异常才上色。</summary>
        public static Color ValueColor(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return Critical;
                case Severity.Warning: return Warning;
                case Severity.Notice: return Notice;
                case Severity.Unknown: return TextMuted;
                default: return TextPrimary;
            }
        }

        /// <summary>分级对应的富文本色标签（用于 GUILayout 之外的拼接）。</summary>
        public static string SeverityHex(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return HexCritical;
                case Severity.Warning: return HexWarning;
                case Severity.Notice: return HexNotice;
                case Severity.Unknown: return HexMuted;
                default: return HexNormal;
            }
        }

        public static string ValueHex(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return HexCritical;
                case Severity.Warning: return HexWarning;
                case Severity.Notice: return HexNotice;
                case Severity.Unknown: return HexMuted;
                default: return HexPrimary;
            }
        }

        /// <summary>分级的中文短名，用于徽章。</summary>
        public static string SeverityLabel(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return "危急";
                case Severity.Warning: return "警告";
                case Severity.Notice: return "注意";
                case Severity.Unknown: return "未知";
                default: return "正常";
            }
        }

        public static Color Hex(string hex)
        {
            return HexColor(hex);
        }

        /// <summary>#RRGGBB / #RRGGBBAA → Color。</summary>
        public static Color HexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;

            string h = hex[0] == '#' ? hex.Substring(1) : hex;
            if (h.Length != 6 && h.Length != 8) return Color.white;

            try
            {
                float r = System.Convert.ToInt32(h.Substring(0, 2), 16) / 255f;
                float g = System.Convert.ToInt32(h.Substring(2, 2), 16) / 255f;
                float b = System.Convert.ToInt32(h.Substring(4, 2), 16) / 255f;
                float a = h.Length == 8 ? System.Convert.ToInt32(h.Substring(6, 2), 16) / 255f : 1f;
                return new Color(r, g, b, a);
            }
            catch (System.Exception)
            {
                return Color.white;
            }
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>
        /// 按系数淡化一个颜色（只动 alpha）。
        ///
        /// IMGUI 的 <c>GUI.color</c> 是直接赋值、并非相乘，所以凡是内部要改 GUI.color 的绘制，
        /// 都必须把外层 alpha 乘进来；否则浮动副本的整卡透明度会在这些块上失效
        /// （底纹淡了、色块和图标还是实心）。
        /// </summary>
        public static Color Fade(Color c, float factor)
        {
            if (factor >= 1f) return c;
            if (factor <= 0f) return new Color(c.r, c.g, c.b, 0f);
            return new Color(c.r, c.g, c.b, c.a * factor);
        }
    }
}
