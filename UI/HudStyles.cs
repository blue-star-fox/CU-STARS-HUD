using System;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class HudStyles : IDisposable
    {
        public readonly Theme Theme;

        public Texture2D Panel;
        
        public Texture2D PanelOutline;
        
        public Texture2D Outline;
        public Texture2D CardNormal;
        
        public Texture2D CardPositive;
        public Texture2D CardNotice;
        public Texture2D CardWarning;
        public Texture2D CardCritical;
        public Texture2D CardSunken;
        public Texture2D CardRaised;
        public Texture2D PillAccent;
        public Texture2D PillIdle;
        public Texture2D PillOn;
        
        public Texture2D PillWhite;
        
        public Texture2D CardWhite;
        
        public Texture2D OutlineAccent;
        
        public Texture2D BarTrack;
        
        public Texture2D BarFill;
        
        public Texture2D KnobCircle;
        
        public Texture2D GlowWhite;
        public Texture2D WaveBackground;
        public Texture2D AlarmGlow;
        public Texture2D GridMinor;
        public Texture2D GridMajor;
        public Texture2D Hairline;
        public Texture2D Overlay;

        public GUIStyle Title;
        public GUIStyle Body;
        public GUIStyle Label;
        public GUIStyle Tiny;
        public GUIStyle Section;
        public GUIStyle ValueLarge;
        
        public GUIStyle ValueLargeLeft;
        public GUIStyle WaveLabel;
        public GUIStyle WaveValue;
        public GUIStyle AlarmText;
        
        public GUIStyle Center;
        
        public GUIStyle CenterAccent;
        public GUIStyle Right;
        
        public GUIStyle ChipOn;
        
        public GUIStyle ChipIdle;
        
        public GUIStyle ValueRight;
        public GUIStyle MateName;
        
        public GUIStyle MateMeta;
        
        public GUIStyle TinyRight;
        
        public GUIStyle SectionNote;
        
        public GUIStyle MateDistance;
        
        public GUIStyle BodyWrap;

        public GUIStyle Box;
        
        public GUIStyle PanelBox;
        
        public GUIStyle Pill;
        
        public GUIStyle BarBox;
        
        public GUIStyle Hit;
        
        public GUIStyle Thumb;

        private readonly Font _font;
        private readonly int _radius;

        public HudStyles(Theme theme, Font font)
        {
            Theme = theme;
            _font = font;
            
            _radius = Mathf.Max(Theme.Pxi(Theme.MinRadius), theme.Pxi(Theme.RadiusSize));

            BuildTextures();
            BuildTextStyles();
            BuildDrawStyles();
        }

        private void BuildTextures()
        {
            int r = _radius;
            int size = r * 2 + 2;
            int panelRadius = Mathf.Max(Theme.Pxi(Theme.MinRadius), Mathf.Max(r, Theme.Pxi(Theme.PanelRadiusSize)));
            int panelSize = panelRadius * 2 + 2;
            int pillH = Mathf.Max(12, Theme.Pxi(18));
            int pillW = pillH * 2 + 4;

            Panel = TextureFactory.Rounded(panelSize, panelRadius,
                new Color(Theme.PanelBg.r, Theme.PanelBg.g, Theme.PanelBg.b, 0.98f));
            PanelOutline = TextureFactory.Rounded(panelSize, panelRadius,
                new Color(Theme.Outline.r, Theme.Outline.g, Theme.Outline.b, 0.85f));
            Outline = TextureFactory.Rounded(size, r,
                new Color(Theme.Outline.r, Theme.Outline.g, Theme.Outline.b, 0.75f));
            
            OutlineAccent = TextureFactory.RoundedOutline(size, r, Mathf.Max(1.5f, Theme.Px(1.5f)), Color.white);

            BarTrack = TextureFactory.Rounded(6, Theme.BarRadius, new Color(1f, 1f, 1f, 0.07f));
            BarFill = TextureFactory.Rounded(6, Theme.BarRadius, Color.white);

            KnobCircle = TextureFactory.Circle(32, Color.white);

            CardNormal = TextureFactory.Rounded(size, r, Theme.CardBg);
            CardPositive = TextureFactory.Rounded(size, r, Blend(Theme.CardBg, Theme.Normal, 0.16f));
            CardNotice = TextureFactory.Rounded(size, r, Blend(Theme.CardBg, Theme.Notice, 0.08f));
            CardWarning = TextureFactory.Rounded(size, r, Blend(Theme.CardBg, Theme.Warning, 0.13f));
            CardCritical = TextureFactory.Rounded(size, r, Blend(Theme.CardBg, Theme.Critical, 0.20f));
            CardSunken = TextureFactory.Rounded(size, r, Theme.CardBgAlt);
            CardRaised = TextureFactory.Rounded(size, r, Blend(Theme.CardBg, Color.white, 0.05f));

            PillAccent = TextureFactory.Capsule(pillW, pillH, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.92f));
            PillIdle = TextureFactory.Capsule(pillW, pillH, new Color(1f, 1f, 1f, 0.07f));
            PillOn = TextureFactory.Capsule(pillW, pillH, Blend(Theme.CardBg, Theme.Accent, 0.35f));
            PillWhite = TextureFactory.Capsule(pillW, pillH, Color.white);
            CardWhite = TextureFactory.Rounded(size, r, Color.white);
            GlowWhite = TextureFactory.Glow(96, Color.white);
            
            WaveBackground = TextureFactory.Rounded(size, r, new Color(0f, 0f, 0f, 0.08f));

            AlarmGlow = TextureFactory.Glow(96, new Color(Theme.Critical.r, Theme.Critical.g, Theme.Critical.b, 0.45f));
            GridMinor = TextureFactory.Grid(Mathf.Max(3, Theme.Pxi(9)), Theme.GridMinor);
            GridMajor = TextureFactory.Grid(Mathf.Max(9, Theme.Pxi(54)), Theme.GridMajor);
            Hairline = TextureFactory.Hairline(64, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.9f));
            Overlay = TextureFactory.Solid(new Color(0f, 0f, 0f, 0.55f));
        }

        private void BuildTextStyles()
        {
            Title = Text(Theme.Font(0), Theme.TextPrimary, TextAnchor.MiddleLeft, true);
            Body = Text(Theme.Font(1), Theme.TextPrimary, TextAnchor.MiddleLeft, false);
            Label = Text(Theme.Font(1), Theme.TextSecondary, TextAnchor.MiddleLeft, false);
            Tiny = Text(Theme.Font(2), Theme.TextMuted, TextAnchor.MiddleLeft, false);
            Section = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.MiddleLeft, true);
            ValueLarge = Text(Theme.Font(3), Theme.TextPrimary, TextAnchor.MiddleRight, true);
            ValueLargeLeft = Text(Theme.Font(3), Theme.TextPrimary, TextAnchor.MiddleLeft, true);
            WaveLabel = Text(Theme.Font(2), Theme.TextMuted, TextAnchor.MiddleLeft, false);
            WaveValue = Text(Theme.Font(3), Theme.TextPrimary, TextAnchor.MiddleRight, true);
            AlarmText = Text(Theme.Font(2), Theme.TextPrimary, TextAnchor.MiddleCenter, true);
            Center = Text(Theme.Font(1), Theme.TextPrimary, TextAnchor.MiddleCenter, false);
            CenterAccent = Text(Theme.Font(1), Theme.TextOnAccent, TextAnchor.MiddleCenter, true);
            Right = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.MiddleRight, false);
            ChipOn = Text(Theme.Font(2), Theme.TextOnAccent, TextAnchor.MiddleCenter, true);
            ChipIdle = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.MiddleCenter, false);
            ValueRight = Text(Theme.Font(2), Theme.TextPrimary, TextAnchor.MiddleRight, true);
            MateName = Text(Theme.Font(1), Theme.TextPrimary, TextAnchor.MiddleLeft, true);
            MateMeta = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.MiddleLeft, false);
            TinyRight = Text(Theme.Font(2), Theme.TextMuted, TextAnchor.MiddleRight, false);
            SectionNote = Text(Theme.Font(2), Theme.TextMuted, TextAnchor.MiddleRight, false);
            MateDistance = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.MiddleRight, false);
            BodyWrap = Text(Theme.Font(2), Theme.TextSecondary, TextAnchor.UpperLeft, false);
            BodyWrap.wordWrap = true;
            BodyWrap.clipping = TextClipping.Clip;
        }

        private void BuildDrawStyles()
        {
            Box = new GUIStyle();
            Box.normal.background = CardNormal;
            Box.border = new RectOffset(_radius + 1, _radius + 1, _radius + 1, _radius + 1);
            Box.padding = new RectOffset(0, 0, 0, 0);
            Box.margin = new RectOffset(0, 0, 0, 0);
            Box.richText = true;

            int panelRadius = Mathf.Max(Theme.Pxi(Theme.MinRadius), Mathf.Max(_radius, Theme.Pxi(Theme.PanelRadiusSize)));
            PanelBox = new GUIStyle(Box);
            PanelBox.normal.background = Panel;
            PanelBox.border = new RectOffset(panelRadius + 1, panelRadius + 1, panelRadius + 1, panelRadius + 1);

            int pillBorder = Mathf.Max(2, Theme.Pxi(18) / 2 + 1);
            Pill = new GUIStyle(Box);
            Pill.normal.background = PillIdle;
            Pill.border = new RectOffset(pillBorder, pillBorder, pillBorder, pillBorder);

            BarBox = new GUIStyle(Box);
            BarBox.normal.background = BarTrack;
            BarBox.border = new RectOffset(2, 2, 2, 2);

            Hit = new GUIStyle();
            Hit.normal.textColor = new Color(0f, 0f, 0f, 0f);
            Hit.hover.textColor = new Color(0f, 0f, 0f, 0f);
            Hit.active.textColor = new Color(0f, 0f, 0f, 0f);
            Hit.alignment = TextAnchor.MiddleCenter;
            Hit.margin = new RectOffset(0, 0, 0, 0);
            Hit.padding = new RectOffset(0, 0, 0, 0);
            Hit.clipping = TextClipping.Clip;

            Thumb = new GUIStyle(Box);
            Thumb.normal.background = PillAccent;
        }

        public Texture2D BackgroundImage;
        
        public float BackgroundAlpha;

        public void DrawPanel(Rect rect)
        {
            PanelBox.normal.background = PanelOutline;
            GUI.Box(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GUIContent.none, PanelBox);

            PanelBox.normal.background = Panel;
            GUI.Box(rect, GUIContent.none, PanelBox);

            if (BackgroundImage != null && BackgroundAlpha > 0.002f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, BackgroundAlpha * old.a);
                GUI.DrawTexture(rect, BackgroundImage, ScaleMode.ScaleAndCrop, true);
                GUI.color = old;
            }
        }

        public void DrawSurface(Rect rect, Texture2D background)
        {
            Box.normal.background = Outline;
            GUI.Box(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GUIContent.none, Box);

            Box.normal.background = background != null ? background : CardNormal;
            GUI.Box(rect, GUIContent.none, Box);
        }

        public void DrawBox(Rect rect, Texture2D background)
        {
            Box.normal.background = background != null ? background : CardNormal;
            GUI.Box(rect, GUIContent.none, Box);
        }

        public void DrawPill(Rect rect, Texture2D background)
        {
            Pill.normal.background = background != null ? background : PillIdle;
            GUI.Box(rect, GUIContent.none, Pill);
        }

        public void DrawBar(Rect rect, Texture2D background)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            BarBox.normal.background = background != null ? background : BarTrack;
            GUI.Box(rect, GUIContent.none, BarBox);
        }

        public void DrawGrid(Rect rect, Texture2D tile, int cellPixels)
        {
            if (tile == null || cellPixels <= 0 || rect.width <= 0f || rect.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(rect, tile,
                new Rect(0f, 0f, rect.width / cellPixels, rect.height / cellPixels));
        }

        public void DrawHairline(Rect rect)
        {
            if (rect.height <= 0f) return;
            GUI.DrawTexture(rect, Hairline);
        }

        public void DrawOutline(Rect rect, Color color)
        {
            if (rect.width <= 3f || rect.height <= 3f) return;

            Color old = GUI.color;
            GUI.color = Theme.Fade(color, old.a);
            Box.normal.background = OutlineAccent;
            GUI.Box(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GUIContent.none, Box);
            GUI.color = old;
        }

        public void Dispose()
        {
            TextureFactory.Destroy(ref Panel);
            TextureFactory.Destroy(ref PanelOutline);
            TextureFactory.Destroy(ref Outline);
            TextureFactory.Destroy(ref OutlineAccent);
            TextureFactory.Destroy(ref BarTrack);
            TextureFactory.Destroy(ref BarFill);
            TextureFactory.Destroy(ref KnobCircle);
            TextureFactory.Destroy(ref CardNormal);
            TextureFactory.Destroy(ref CardPositive);
            TextureFactory.Destroy(ref CardNotice);
            TextureFactory.Destroy(ref CardWarning);
            TextureFactory.Destroy(ref CardCritical);
            TextureFactory.Destroy(ref CardSunken);
            TextureFactory.Destroy(ref CardRaised);
            TextureFactory.Destroy(ref PillAccent);
            TextureFactory.Destroy(ref PillIdle);
            TextureFactory.Destroy(ref PillOn);
            TextureFactory.Destroy(ref PillWhite);
            TextureFactory.Destroy(ref CardWhite);
            TextureFactory.Destroy(ref GlowWhite);
            TextureFactory.Destroy(ref WaveBackground);
            TextureFactory.Destroy(ref AlarmGlow);
            TextureFactory.Destroy(ref GridMinor);
            TextureFactory.Destroy(ref GridMajor);
            TextureFactory.Destroy(ref Hairline);
            TextureFactory.Destroy(ref Overlay);
        }

        public Texture2D CardFor(Core.Severity severity)
        {
            switch (severity)
            {
                case Core.Severity.Critical: return CardCritical;
                case Core.Severity.Warning: return CardWarning;
                case Core.Severity.Notice: return CardNotice;
                default: return CardNormal;
            }
        }

        private GUIStyle Text(int size, Color color, TextAnchor anchor, bool bold)
        {
            GUIStyle style = new GUIStyle();
            if (_font != null) style.font = _font;
            style.fontSize = Mathf.Max(8, size);
            style.normal.textColor = color;
            style.alignment = anchor;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.richText = true;
            style.wordWrap = false;
            style.clipping = TextClipping.Clip;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static Color Blend(Color from, Color over, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(
                Mathf.Lerp(from.r, over.r, t),
                Mathf.Lerp(from.g, over.g, t),
                Mathf.Lerp(from.b, over.b, t),
                from.a);
        }
    }
}
