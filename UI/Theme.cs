using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class Theme
    {
        
        public enum UiSkin
        {
            
            Fox = 0,
            
            Game = 1,
            
            Custom = 2
        }

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
        private static Palette _customPalette = BuildCustomPalette(0.62f, 0.35f, 0.5f);
        private static UiSkin _skin = UiSkin.Fox;

        public static float CustomHue = 0.62f;
        public static float CustomDark = 0.35f;
        public static float CustomSaturation = 0.5f;

        public static UiSkin Skin
        {
            get { return _skin; }
            set { _skin = value; }
        }

        public static void RebuildCustom(float hue, float dark, float saturation)
        {
            CustomHue = ColorMath.Clamp01(hue);
            CustomDark = ColorMath.Clamp01(dark);
            CustomSaturation = ColorMath.Clamp01(saturation);
            _customPalette = BuildCustomPalette(CustomHue, CustomDark, CustomSaturation);
        }

        private static Palette P
        {
            get
            {
                if (_skin == UiSkin.Custom) return _customPalette;
                return _skin == UiSkin.Game ? GamePalette : FoxPalette;
            }
        }

        public static Color PanelBg { get { return P.PanelBg; } }
        public static Color PanelBgTop { get { return P.PanelBgTop; } }
        public static Color CardBg { get { return P.CardBg; } }
        public static Color CardBgAlt { get { return P.CardBgAlt; } }
        public static Color HeaderBg { get { return P.HeaderBg; } }
        public static Color Line { get { return P.Line; } }
        public static Color LineSoft { get { return P.LineSoft; } }
        
        public static Color Outline { get { return P.Outline; } }
        public static Color GridMinor { get { return P.GridMinor; } }
        public static Color GridMajor { get { return P.GridMajor; } }

        public static Color TextPrimary { get { return P.TextPrimary; } }
        public static Color TextSecondary { get { return P.TextSecondary; } }
        public static Color TextMuted { get { return P.TextMuted; } }
        public static Color TextOnAccent { get { return P.TextOnAccent; } }

        public static Color Accent { get { return P.Accent; } }
        
        public static readonly Color Normal = Hex("#36D399");
        public static readonly Color Notice = Hex("#4FC3F7");
        public static readonly Color Warning = Hex("#FFB020");
        public static readonly Color Critical = Hex("#FF4D4F");

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

        public const float PadSize = 14f;
        public const float GapSize = 8f;
        
        public static float RadiusSize { get { return P.Radius; } }
        
        public static float PanelRadiusSize { get { return P.PanelRadius; } }
        
        public static float MinRadius { get { return P.MinRadius; } }
        
        public static int BarRadius { get { return P.BarRadius; } }

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

        private static Palette BuildGamePalette()
        {
            Palette p = new Palette();

            p.PanelBg = Hex("#000000");
            p.PanelBgTop = Hex("#000000");
            p.CardBg = Hex("#000000");
            p.CardBgAlt = Hex("#000000");
            p.HeaderBg = Hex("#000000");

            p.Line = Hex("#2E2E2E");
            p.LineSoft = Hex("#1C1C1C");

            p.Outline = Hex("#FFFFFF");

            p.GridMinor = new Color(1f, 1f, 1f, 0.05f);
            p.GridMajor = new Color(1f, 1f, 1f, 0.1f);

            p.TextPrimary = Hex("#FFFFFF");
            p.TextSecondary = Hex("#C4C4C4");
            p.TextMuted = Hex("#8A8A8A");
            p.TextOnAccent = Hex("#000000");

            p.Accent = Hex("#FFFFFF");

            p.HexPrimary = "#FFFFFF";
            p.HexSecondary = "#C4C4C4";
            p.HexMuted = "#8A8A8A";
            p.HexAccent = "#FFFFFF";

            p.Radius = 12f;
            p.PanelRadius = 18f;
            p.MinRadius = 4f;
            p.BarRadius = 2;
            return p;
        }

        private static Palette BuildCustomPalette(float hue, float dark, float saturation)
        {
            Palette p = new Palette();

            float sat = ColorMath.Clamp01(saturation);
            float shade = ColorMath.Clamp01(dark);
            float baseSat = ColorMath.BaseSaturation(sat, 1f);

            p.PanelBg = Hsv(hue, baseSat * 0.80f, ColorMath.BaseValue(shade, 0.32f));
            p.PanelBgTop = Hsv(hue, baseSat * 0.90f, ColorMath.BaseValue(shade, 0.95f));
            p.CardBg = Hsv(hue, baseSat * 0.85f, ColorMath.BaseValue(shade, 0.70f));
            p.CardBgAlt = Hsv(hue, baseSat * 0.80f, ColorMath.BaseValue(shade, 0.42f));
            p.HeaderBg = Hsv(hue, baseSat * 0.70f, ColorMath.BaseValue(shade, 0.55f));
            p.Line = Hsv(hue, baseSat * 1.15f, ColorMath.Clamp01(0.16f + 0.26f * shade));
            p.LineSoft = Hsv(hue, baseSat * 0.90f, ColorMath.Clamp01(0.09f + 0.14f * shade));

            p.Outline = Hsv(hue, baseSat * 1.30f, ColorMath.Clamp01(0.30f + 0.34f * shade));
            p.GridMinor = new Color(1f, 1f, 1f, 0.022f);
            p.GridMajor = new Color(1f, 1f, 1f, 0.05f);

            p.TextPrimary = Hsv(hue, 0.05f, 0.93f);
            p.TextSecondary = Hsv(hue, 0.11f, 0.68f);
            p.TextMuted = Hsv(hue, 0.13f, 0.42f);
            p.TextOnAccent = Hsv(hue, 0.35f, 0.07f);

            p.Accent = Hsv(hue, 0.50f + 0.45f * sat, 0.92f);

            p.HexPrimary = ToHex(p.TextPrimary);
            p.HexSecondary = ToHex(p.TextSecondary);
            p.HexMuted = ToHex(p.TextMuted);
            p.HexAccent = ToHex(p.Accent);

            p.Radius = 12f;
            p.PanelRadius = 18f;
            p.MinRadius = 4f;
            p.BarRadius = 2;
            return p;
        }

        private static Color Hsv(float h, float s, float v)
        {
            float r, g, b;
            ColorMath.HsvToRgb(h, s, v, out r, out g, out b);
            return new Color(r, g, b, 1f);
        }

        private static string ToHex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return "#" + r.ToString("x2") + g.ToString("x2") + b.ToString("x2");
        }

        public float LabelLine()
        {
            return Font(1) * 1.35f;
        }

        public float SmallLine()
        {
            return Font(2) * 1.5f;
        }

        public float ValueLine()
        {
            return Font(3) * 1.5f;
        }

        public float BarHeight()
        {
            
            return Mathf.Max(3f * UiScale, Px(4f));
        }

        public float HeaderHeight()
        {
            float left = Px(6f) + Font(0) * 1.3f + Font(2) * 1.5f;
            float right = Px(6f) + CloseButtonSize();
            return Mathf.Max(left, right) + Px(6f);
        }

        public float CloseButtonSize()
        {
            return Px(22f);
        }

        public float VitalCardHeight()
        {
            return Px(14f) + LabelLine() + ValueLine() + BarHeight() + Px(6f);
        }

        public float LimbRowHeight()
        {
            float barsLine = Mathf.Max(SmallLine(), BarHeight() + Px(5f));
            return Px(14f) + LabelLine() + barsLine + SmallLine() + SmallLine() + Px(8f);
        }

        public float MateCardHeight()
        {
            float text = LabelLine() + SmallLine() * 1.2f + SmallLine();
            return Px(14f) + Mathf.Max(AvatarSize(), text) + BarHeight() + Px(10f);
        }

        public float AvatarSize()
        {
            return Px(40f);
        }

        public float CompactRowHeight()
        {
            return Px(6f) + SmallLine();
        }

        public float SectionHeight()
        {
            return Px(3f) + SmallLine();
        }

        public float AlarmHeight()
        {
            return Px(7f) + SmallLine();
        }

        public float TabHeight()
        {
            return Px(9f) + SmallLine();
        }

        public float StripHeight()
        {
            return Px(8f) + SmallLine() + ValueLine();
        }

        public float ChannelBlockWidth()
        {
            return Px(70f);
        }

        public readonly float Scale;

        public readonly int BaseFont;

        public Theme(float scale, int baseFont)
        {
            Scale = Mathf.Clamp(scale, 0.7f, 1.5f);
            BaseFont = Mathf.Clamp(baseFont, 11, 20);
        }

        public float UiScale = 1f;

        public float Px(float designPixels)
        {
            return designPixels * Scale * UiScale;
        }

        public int Pxi(float designPixels)
        {
            return Mathf.RoundToInt(designPixels * Scale * UiScale);
        }

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

            if (UiScale != 1f) size = Mathf.Max(6, Mathf.RoundToInt(size * UiScale));
            return size;
        }

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

        public static Color Fade(Color c, float factor)
        {
            if (factor >= 1f) return c;
            if (factor <= 0f) return new Color(c.r, c.g, c.b, 0f);
            return new Color(c.r, c.g, c.b, c.a * factor);
        }
    }
}
