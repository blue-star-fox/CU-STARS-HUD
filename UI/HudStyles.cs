using System;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 一次性构建全部纹理与 GUIStyle。
    ///
    /// 约定：所有圆角/胶囊底纹都通过 <see cref="Box"/>（圆角）与 <see cref="Pill"/>（胶囊）
    /// 这两个可变样式绘制——绘制前换 <c>normal.background</c> 即可，
    /// 因此不需要为每个状态各建一个 GUIStyle。
    ///
    /// 必须在 OnGUI 内构造（GUIStyle 依赖 GUI.skin），缩放或字号变化时整体重建。
    /// </summary>
    internal sealed class HudStyles : IDisposable
    {
        public readonly Theme Theme;

        // ---------- 底纹 ----------
        public Texture2D Panel;
        /// <summary>面板外描边（比面板大 1px 的圆角）。</summary>
        public Texture2D PanelOutline;
        /// <summary>卡片外描边（比卡片大 1px 的圆角）——现代系统设计的 1px 轮廓。</summary>
        public Texture2D Outline;
        public Texture2D CardNormal;
        /// <summary>正常状态的状态条底色（绿色调）。</summary>
        public Texture2D CardPositive;
        public Texture2D CardNotice;
        public Texture2D CardWarning;
        public Texture2D CardCritical;
        public Texture2D CardSunken;
        public Texture2D CardRaised;
        public Texture2D PillAccent;
        public Texture2D PillIdle;
        public Texture2D PillOn;
        /// <summary>纯白胶囊：配合 GUI.color 着色，可得到任意纯色胶囊（进度条/徽章）。</summary>
        public Texture2D PillWhite;
        /// <summary>纯白圆角：配合 GUI.color 着色。</summary>
        public Texture2D CardWhite;
        /// <summary>纯白圆角描边：配合 GUI.color 得到任意颜色的圆角高亮框。</summary>
        public Texture2D OutlineAccent;
        /// <summary>细条轨道（进度条/滑块轨道）：border 很小，见 BarBox 的说明。</summary>
        public Texture2D BarTrack;
        /// <summary>纯白细条填充：配合 GUI.color 着色。</summary>
        public Texture2D BarFill;
        /// <summary>纯白实心圆：滑块 / 开关手柄（缩放绘制，保证是正圆）。</summary>
        public Texture2D KnobCircle;
        /// <summary>纯白径向辉光：配合 GUI.color 着色。</summary>
        public Texture2D GlowWhite;
        public Texture2D WaveBackground;
        public Texture2D AlarmGlow;
        public Texture2D GridMinor;
        public Texture2D GridMajor;
        public Texture2D Hairline;
        public Texture2D Overlay;

        // ---------- 文本样式 ----------
        public GUIStyle Title;
        public GUIStyle Body;
        public GUIStyle Label;
        public GUIStyle Tiny;
        public GUIStyle Section;
        public GUIStyle ValueLarge;
        /// <summary>左对齐的大数值（波形区通道块用）。</summary>
        public GUIStyle ValueLargeLeft;
        public GUIStyle WaveLabel;
        public GUIStyle WaveValue;
        public GUIStyle AlarmText;
        /// <summary>居中白色文字（按钮/提示）。</summary>
        public GUIStyle Center;
        /// <summary>居中深色文字（强调底上的按钮）。</summary>
        public GUIStyle CenterAccent;
        public GUIStyle Right;
        /// <summary>强调底上的小字（深色）。</summary>
        public GUIStyle ChipOn;
        /// <summary>中性底上的小字（次亮）。</summary>
        public GUIStyle ChipIdle;
        /// <summary>右对齐的数值（颜色由调用方用富文本指定）。</summary>
        public GUIStyle ValueRight;
        public GUIStyle MateName;
        /// <summary>队友摘要（颜色由调用方用富文本指定）。</summary>
        public GUIStyle MateMeta;
        /// <summary>右上角小字（底栏/说明），右对齐。</summary>
        public GUIStyle TinyRight;
        /// <summary>分组标题右侧的说明文字。</summary>
        public GUIStyle SectionNote;
        /// <summary>队友卡片右上角的距离。</summary>
        public GUIStyle MateDistance;
        /// <summary>自动换行的说明文字（关于面板用）。</summary>
        public GUIStyle BodyWrap;

        // ---------- 可复用绘制样式 ----------
        /// <summary>圆角 9 宫格：绘制前设置 normal.background。</summary>
        public GUIStyle Box;
        /// <summary>面板专用 9 宫格（圆角更大）。</summary>
        public GUIStyle PanelBox;
        /// <summary>胶囊 9 宫格：绘制前设置 normal.background。</summary>
        public GUIStyle Pill;
        /// <summary>细条样式（进度条 / 滑块轨道）：border 只有 2px。</summary>
        public GUIStyle BarBox;
        /// <summary>完全透明的点击热区（文字由调用方另行绘制，避免双重绘制）。</summary>
        public GUIStyle Hit;
        /// <summary>可换背景的滚动条轨道/滑块。</summary>
        public GUIStyle Thumb;

        private readonly Font _font;
        private readonly int _radius;

        public HudStyles(Theme theme, Font font)
        {
            Theme = theme;
            _font = font;
            // 圆角下限也随皮肤变化：游戏 UI 风格要能真正画到接近直角
            _radius = Mathf.Max(Theme.Pxi(Theme.MinRadius), theme.Pxi(Theme.RadiusSize));

            BuildTextures();
            BuildTextStyles();
            BuildDrawStyles();
        }

        // ---------------------------------------------------------------

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
            // 白色圆角描边：配合 GUI.color 着色，用于选中高亮等任意颜色的描边
            OutlineAccent = TextureFactory.RoundedOutline(size, r, Mathf.Max(1.5f, Theme.Px(1.5f)), Color.white);

            // 细条专用：圆角半径固定 2px、border 也是 2px。
            // 进度条只有 4px 高，若用胶囊纹理（border≈10）9 宫格的边框会被压扁成直角，
            // 这正是「数值条看起来是方的」的原因。
            BarTrack = TextureFactory.Rounded(6, Theme.BarRadius, new Color(1f, 1f, 1f, 0.07f));
            BarFill = TextureFactory.Rounded(6, Theme.BarRadius, Color.white);

            // 手柄用实心圆：尺寸随缩放变化，靠缩放绘制保持正圆
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
            WaveBackground = TextureFactory.Rounded(size, r, new Color(0f, 0f, 0f, 0.28f));

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

            // 细条样式：border 只有 2px，4px 高的进度条也能画出圆头而不是被压成直角
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

        // ---------------------------------------------------------------

        /// <summary>面板：1px 描边 + 圆角面板底（描边在面板外 1px，形成现代 UI 的轮廓感）。</summary>
        public void DrawPanel(Rect rect)
        {
            PanelBox.normal.background = PanelOutline;
            GUI.Box(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GUIContent.none, PanelBox);

            PanelBox.normal.background = Panel;
            GUI.Box(rect, GUIContent.none, PanelBox);
        }

        /// <summary>卡片/分块表面：1px 描边 + 圆角底。</summary>
        public void DrawSurface(Rect rect, Texture2D background)
        {
            Box.normal.background = Outline;
            GUI.Box(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GUIContent.none, Box);

            Box.normal.background = background != null ? background : CardNormal;
            GUI.Box(rect, GUIContent.none, Box);
        }

        /// <summary>用当前 <see cref="Box"/> 画一个圆角底（会临时替换 background）。</summary>
        public void DrawBox(Rect rect, Texture2D background)
        {
            Box.normal.background = background != null ? background : CardNormal;
            GUI.Box(rect, GUIContent.none, Box);
        }

        /// <summary>用当前 <see cref="Pill"/> 画一个胶囊底。</summary>
        public void DrawPill(Rect rect, Texture2D background)
        {
            Pill.normal.background = background != null ? background : PillIdle;
            GUI.Box(rect, GUIContent.none, Pill);
        }

        /// <summary>画细条（进度条轨道 / 填充）：用 2px 圆角，避免细条被压成直角。</summary>
        public void DrawBar(Rect rect, Texture2D background)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            BarBox.normal.background = background != null ? background : BarTrack;
            GUI.Box(rect, GUIContent.none, BarBox);
        }

        /// <summary>平铺网格。</summary>
        public void DrawGrid(Rect rect, Texture2D tile, int cellPixels)
        {
            if (tile == null || cellPixels <= 0 || rect.width <= 0f || rect.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(rect, tile,
                new Rect(0f, 0f, rect.width / cellPixels, rect.height / cellPixels));
        }

        /// <summary>1px 分隔线。</summary>
        public void DrawHairline(Rect rect)
        {
            if (rect.height <= 0f) return;
            GUI.DrawTexture(rect, Hairline);
        }

        /// <summary>画一圈圆角描边（编辑态高亮、选中框等）。</summary>
        public void DrawOutline(Rect rect, Color color)
        {
            if (rect.width <= 3f || rect.height <= 3f) return;

            // 用圆角描边纹理而不是四条直线：直线拼出来是直角框，
            // 套在圆角卡片上会明显不匹配。
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

        /// <summary>取分级对应的卡片底纹。</summary>
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
