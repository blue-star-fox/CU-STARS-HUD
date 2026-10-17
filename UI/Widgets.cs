using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>竖向排布游标，让面板代码像写清单一样自上而下堆叠。</summary>
    internal struct Layout
    {
        public float X;
        public float Y;
        public float Width;

        public Layout(float x, float y, float width)
        {
            X = x;
            Y = y;
            Width = width;
        }

        /// <summary>占用一行，返回其矩形并下移游标。</summary>
        public Rect Take(float height)
        {
            Rect rect = new Rect(X, Y, Width, height);
            Y += height;
            return rect;
        }

        /// <summary>占用一行并追加额外间距。</summary>
        public Rect Take(float height, float gap)
        {
            Rect rect = new Rect(X, Y, Width, height);
            Y += height + gap;
            return rect;
        }

        public void Gap(float height)
        {
            Y += height;
        }

        /// <summary>左右分栏，返回左列并输出右列。</summary>
        public static Rect SplitColumns(Rect row, float gutter, out Rect right)
        {
            float w = Mathf.Max(0f, (row.width - gutter) * 0.5f);
            Rect left = new Rect(row.x, row.y, w, row.height);
            right = new Rect(row.x + w + gutter, row.y, w, row.height);
            return left;
        }
    }

    /// <summary>一个标签页的滚动状态。</summary>
    internal sealed class ScrollState
    {
        public float Offset;
        public bool Dragging;
        /// <summary>上一次绘制得到的实际内容高度（下一帧用于计算滚动范围）。</summary>
        public float ContentHeight;
    }

    /// <summary>
    /// 自绘控件集合。所有控件都基于显式 Rect，布局完全确定，
    /// 不受 GUILayout 隐式测量的影响，这样监护仪式对齐才不会漂。
    ///
    /// 两条硬性约定：
    ///   1) 纯色块一律「白色底纹 + GUI.color 着色」，避免有色底纹再乘颜色导致偏色；
    ///   2) **绝不就地修改共享 GUIStyle**（需要变色时用富文本 &lt;color&gt;），
    ///      否则不同调用点之间会互相串色。
    /// 行内所有竖向分带都由 <see cref="Theme"/> 的字体度量推导，保证任意字号/缩放下不重叠。
    /// </summary>
    internal static class Widgets
    {
        // 走属性而不是 const：配色随皮肤变化，const 在编译期就固化了
        private static string MutedHex { get { return Theme.HexMuted; } }
        private static string SecondaryHex { get { return Theme.HexSecondary; } }

        /// <summary>
        /// 给文字套上 rich text 字号，用于「整卡等比缩放」（<see cref="Theme.UiScale"/>）。
        ///
        /// GUIStyle 的字号是构建时固化的、运行时改不了（改了会污染共享样式），
        /// 所以缩放后的字号只能由 rich text 的 size 标签覆盖。
        /// UiScale = 1 时原样返回，主面板的绘制完全不受影响。
        /// </summary>
        private static string Fit(string text, Theme th, int fontLevel)
        {
            if (th.UiScale >= 0.999f && th.UiScale <= 1.001f) return text;
            return "<size=" + th.Font(fontLevel) + ">" + text + "</size>";
        }

        /// <summary>
        /// 用纯色填充矩形。
        /// 颜色会乘上当前 <see cref="GUI.color"/> 的 alpha —— IMGUI 的 GUI.color 是**直接赋值**而不是相乘，
        /// 若这里直接覆盖，浮动副本的整卡透明度就会在填充块上失效。
        /// </summary>
        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = Theme.Fade(color, old.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---------------------------------------------------------------
        // 基础控件
        // ---------------------------------------------------------------

        /// <summary>胶囊分段按钮（标签页）。</summary>
        public static bool Segment(Rect rect, string label, bool active, HudStyles styles)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (active)
            {
                Color old = GUI.color;
                GUI.color = hover ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.88f);
                styles.DrawPill(rect, styles.PillWhite);
                GUI.color = old;
            }
            else
            {
                styles.DrawPill(rect, hover ? styles.CardRaised : styles.PillIdle);
            }

            GUI.Label(rect, label, active ? styles.ChipOn : styles.ChipIdle);
            return GUI.Button(rect, label, styles.Hit);
        }

        /// <summary>带图标的胶囊分段按钮（标签页）：图标 + 文字整体居中。</summary>
        public static bool SegmentWithIcon(Rect rect, Texture2D icon, string label, bool active, HudStyles styles)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (active)
            {
                Color old = GUI.color;
                GUI.color = hover ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.88f);
                styles.DrawPill(rect, styles.PillWhite);
                GUI.color = old;
            }
            else
            {
                styles.DrawPill(rect, hover ? styles.CardRaised : styles.PillIdle);
            }

            GUIStyle text = active ? styles.ChipOn : styles.ChipIdle;
            float textWidth = text.CalcSize(new GUIContent(label)).x;
            float iconSize = icon != null ? Mathf.Min(rect.height * 0.62f, styles.Theme.Px(15f)) : 0f;
            float iconGap = icon != null ? styles.Theme.Px(5f) : 0f;
            float blockWidth = iconSize + iconGap + textWidth;
            float startX = rect.x + (rect.width - blockWidth) * 0.5f;

            if (icon != null)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, active ? 1f : 0.85f);
                GUI.DrawTexture(new Rect(startX, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize),
                    icon, ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }

            GUI.Label(new Rect(startX + iconSize + iconGap, rect.y, textWidth + 2f, rect.height), label, text);
            return GUI.Button(rect, label, styles.Hit);
        }

        /// <summary>圆角按钮。</summary>
        public static bool Button(Rect rect, string label, bool primary, HudStyles styles)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (primary)
            {
                Color old = GUI.color;
                GUI.color = hover ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.86f);
                styles.DrawBox(rect, styles.CardWhite);
                GUI.color = old;
            }
            else
            {
                styles.DrawBox(rect, hover ? styles.CardRaised : styles.CardSunken);
            }

            GUI.Label(rect, label, primary ? styles.CenterAccent : styles.Center);
            return GUI.Button(rect, label + (primary ? "|p" : "|s"), styles.Hit);
        }

        /// <summary>开关行。返回新值。</summary>
        public static bool Toggle(Rect rect, bool value, string label, HudStyles styles)
        {
            styles.DrawBox(rect, styles.CardSunken);

            float switchW = rect.height * 1.7f;
            Rect track = new Rect(rect.xMax - switchW - 6f, rect.y + 3f, switchW, rect.height - 6f);

            if (value)
            {
                Color old = GUI.color;
                GUI.color = Theme.WithAlpha(Theme.Accent, 0.9f);
                styles.DrawPill(track, styles.PillWhite);
                GUI.color = old;
            }
            else
            {
                styles.DrawPill(track, styles.PillIdle);
            }

            float knob = track.height - 2f;
            float knobX = value ? track.xMax - knob - 1f : track.x + 1f;
            Color knobOld = GUI.color;
            GUI.color = value ? Color.white : new Color(0.78f, 0.81f, 0.85f, 0.95f);
            // 手柄必须是正圆：胶囊纹理的 border 一旦超过尺寸一半就会被压成直角方块
            GUI.DrawTexture(new Rect(knobX, track.y + 1f, knob, knob), styles.KnobCircle);
            GUI.color = knobOld;

            GUI.Label(new Rect(rect.x + 10f, rect.y, Mathf.Max(20f, rect.width - switchW - 24f), rect.height),
                label, styles.Body);

            return GUI.Button(rect, label + (value ? "|on" : "|off"), styles.Hit) ? !value : value;
        }

        /// <summary>滑块。点击轨道即定位，拖动连续调整。返回新值。</summary>
        public static float Slider(Rect rect, float value, float min, float max, HudStyles styles)
        {
            // 模态窗口打开时主面板整体禁用：手动处理的控件也必须跟着失效
            if (!GUI.enabled) return value;

            float trackH = Mathf.Max(4f, rect.height * 0.30f);
            Rect track = new Rect(rect.x, rect.y + (rect.height - trackH) * 0.5f, rect.width, trackH);
            float t = Mathf.Clamp01((value - min) / Mathf.Max(0.0001f, max - min));

            styles.DrawBar(track, styles.BarTrack);

            if (t > 0f)
            {
                Color old = GUI.color;
                GUI.color = Theme.Accent;
                styles.DrawBar(new Rect(track.x, track.y, Mathf.Max(trackH, track.width * t), track.height),
                    styles.BarFill);
                GUI.color = old;
            }

            float knobSize = Mathf.Max(10f, rect.height * 0.62f);
            float knobX = Mathf.Clamp(track.x + track.width * t - knobSize * 0.5f, track.x,
                Mathf.Max(track.x, track.xMax - knobSize));
            Color knobOld = GUI.color;
            GUI.color = Color.white;
            // 手柄必须是正圆：滑块行高 16px 时手柄约 10×16，而胶囊纹理的 border 是 10，
            // 会被 9 宫格压成直角方块。圆形纹理 + 缩放绘制就没有这个问题。
            GUI.DrawTexture(new Rect(knobX, rect.y + (rect.height - knobSize) * 0.5f, knobSize, knobSize),
                styles.KnobCircle);
            GUI.color = knobOld;

            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        // 单击轨道也要立刻定位，否则用户会以为滑块坏了
                        value = ValueAt(e.mousePosition.x, track, min, max);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = ValueAt(e.mousePosition.x, track, min, max);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }

            return value;
        }

        private static float ValueAt(float mouseX, Rect track, float min, float max)
        {
            float local = Mathf.Clamp01((mouseX - track.x) / Mathf.Max(1f, track.width));
            return min + local * (max - min);
        }

        /// <summary>键位捕获按钮。返回是否被点击（点击后由调用方开启捕获状态）。</summary>
        public static bool KeybindButton(Rect rect, string current, bool capturing, HudStyles styles)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (capturing)
            {
                Color old = GUI.color;
                GUI.color = Theme.WithAlpha(Theme.Accent, 0.9f);
                styles.DrawBox(rect, styles.CardWhite);
                GUI.color = old;
                GUI.Label(rect, "请按下新按键…", styles.CenterAccent);
            }
            else
            {
                styles.DrawBox(rect, hover ? styles.CardRaised : styles.CardSunken);
                GUI.Label(rect, current, styles.Center);
            }

            return GUI.Button(rect, "keybind:" + current + (capturing ? "|cap" : ""), styles.Hit);
        }

        // ---------------------------------------------------------------
        // 监护仪卡片
        // ---------------------------------------------------------------

        /// <summary>
        /// 主要生命体征卡片。竖向分带：
        /// [图标 + 名称 | 参考范围] / [大数值 + 单位] / [范围条]，
        /// 总高度由 <c>Theme.VitalCardHeight()</c> 保证刚好容纳。
        /// </summary>
        public static void VitalCard(Rect rect, VitalReading reading, HudStyles styles, IconProvider icons, bool showLimits)
        {
            Theme th = styles.Theme;
            styles.DrawSurface(rect, styles.CardFor(reading.Severity));

            float pad = th.Px(7f);
            // 图标隐藏时（关闭 emoji）不留空位，文字左移填补
            Texture2D icon = icons.Get(reading.Icon, IconTint(reading.Severity));
            float iconSize = icon != null ? Mathf.Max(th.Px(14f), th.Font(1) * 1.2f) : 0f;
            float iconGap = icon != null ? th.Px(6f) : 0f;
            float labelLine = th.LabelLine();
            float valueLine = th.ValueLine();
            float barH = th.BarHeight();

            if (reading.Severity != Severity.Normal && reading.Severity != Severity.Unknown)
            {
                Fill(new Rect(rect.x + 1f, rect.y + pad, Mathf.Max(2f, th.Px(3f)), rect.height - pad * 2f),
                    Theme.WithAlpha(Theme.SeverityColor(reading.Severity), 0.9f));
            }

            if (icon != null)
            {
                DrawIcon(new Rect(rect.x + pad, rect.y + pad, iconSize, iconSize), icon, reading.Severity);
            }

            bool hasLimits = showLimits && !string.IsNullOrEmpty(reading.Limits);
            float limitsW = hasLimits ? rect.width * 0.42f : 0f;
            float textX = rect.x + pad + iconSize + iconGap;

            GUI.Label(new Rect(textX, rect.y + pad, Mathf.Max(10f, rect.width - (textX - rect.x) - pad - limitsW), labelLine),
                Fit(reading.Label, th, 1), styles.Label);

            if (hasLimits)
            {
                GUI.Label(new Rect(rect.xMax - pad - limitsW, rect.y + pad, limitsW, labelLine),
                    Fit("<color=" + MutedHex + ">" + reading.Limits + "</color>", th, 2), styles.TinyRight);
            }

            string value = "<color=" + Theme.ValueHex(reading.Severity) + ">" + reading.Value + "</color>";
            if (!string.IsNullOrEmpty(reading.Unit))
            {
                value += "<color=" + MutedHex + "><size=" + th.Font(2) + "> " + reading.Unit + "</size></color>";
            }
            GUI.Label(new Rect(rect.x + pad, rect.y + pad + labelLine + th.Px(2f), rect.width - pad * 2f, valueLine),
                Fit(value, th, 3), styles.ValueLarge);

            if (reading.HasBar)
            {
                Bar(new Rect(rect.x + pad, rect.yMax - pad - barH, rect.width - pad * 2f, barH),
                    reading.Bar, Theme.SeverityColor(reading.Severity), styles);
            }
        }

        /// <summary>紧凑单行：图标 + 名称 + 参考范围 + 数值。</summary>
        public static void CompactRow(Rect rect, VitalReading reading, bool striped, HudStyles styles, IconProvider icons, bool showLimits)
        {
            Theme th = styles.Theme;
            if (striped) styles.DrawBox(rect, styles.CardSunken);

            float pad = th.Px(5f);
            // 图标隐藏时不留空位
            Texture2D icon = icons.Get(reading.Icon, IconTint(reading.Severity));
            float iconSize = icon != null ? Mathf.Max(11f, rect.height * 0.62f) : 0f;
            float iconGap = icon != null ? th.Px(5f) : 0f;

            if (icon != null)
            {
                DrawIcon(new Rect(rect.x + pad, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize),
                    icon, reading.Severity);
            }

            float textX = rect.x + pad + iconSize + iconGap;
            float valueWidth = Mathf.Max(56f, rect.width * 0.32f);
            float limitsW = showLimits && !string.IsNullOrEmpty(reading.Limits) ? Mathf.Max(40f, rect.width * 0.18f) : 0f;

            GUI.Label(new Rect(textX, rect.y, Mathf.Max(10f, rect.width - (textX - rect.x) - valueWidth - limitsW - pad), rect.height),
                reading.Label, styles.Label);

            if (limitsW > 0f)
            {
                GUI.Label(new Rect(rect.xMax - pad - valueWidth - limitsW, rect.y, limitsW - 6f, rect.height),
                    "<color=" + MutedHex + ">" + reading.Limits + "</color>", styles.Tiny);
            }

            string value = "<color=" + Theme.ValueHex(reading.Severity) + ">" + reading.Value + "</color>";
            if (!string.IsNullOrEmpty(reading.Unit))
            {
                value += "<color=" + MutedHex + "> " + reading.Unit + "</color>";
            }
            GUI.Label(new Rect(rect.xMax - pad - valueWidth, rect.y, valueWidth, rect.height), value, styles.ValueRight);
        }

        /// <summary>分组小标题：左标题 + 右说明 + 一条剩余宽度的分隔线。</summary>
        public static void SectionHeader(Rect rect, string title, string note, HudStyles styles)
        {
            GUI.Label(rect, title, styles.Section);

            float titleWidth = styles.Section.CalcSize(new GUIContent(title)).x + 8f;
            float noteWidth = 0f;
            if (!string.IsNullOrEmpty(note))
            {
                GUI.Label(rect, note, styles.SectionNote);
                noteWidth = styles.SectionNote.CalcSize(new GUIContent(note)).x + 8f;
            }

            float startX = rect.x + titleWidth;
            float endX = rect.xMax - noteWidth;
            if (endX - startX > 8f)
            {
                styles.DrawHairline(new Rect(startX, rect.y + rect.height * 0.5f, endX - startX, 1f));
            }
        }

        /// <summary>横向进度/范围条。</summary>
        public static void Bar(Rect rect, float t, Color color, HudStyles styles)
        {
            // 用细条样式（2px 圆角）而不是胶囊：进度条只有 4px 高，
            // 胶囊纹理的 border≈10 会被 9 宫格压扁成直角，看起来就是「方角的数值条」。
            styles.DrawBar(rect, styles.BarTrack);

            float clamped = Mathf.Clamp01(t);
            if (clamped <= 0f) return;

            Color old = GUI.color;
            GUI.color = Theme.Fade(color, old.a);
            styles.DrawBar(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * clamped), rect.height),
                styles.BarFill);
            GUI.color = old;
        }

        /// <summary>分级徽章。</summary>
        public static void Badge(Rect rect, Severity severity, string text, HudStyles styles)
        {
            Color old = GUI.color;
            GUI.color = Theme.Fade(Theme.WithAlpha(Theme.SeverityColor(severity), 0.92f), old.a);
            styles.DrawPill(rect, styles.PillWhite);
            GUI.color = old;

            GUI.Label(rect, Fit(text, styles.Theme, 2), styles.ChipOn);
        }

        /// <summary>
        /// 右上角关闭按钮：悬停时红色高亮，图标为程序化绘制的 ✕（不依赖字体，避免字形问题）。
        /// </summary>
        public static bool CloseButton(Rect rect, HudStyles styles, IconProvider icons)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (hover)
            {
                Color old = GUI.color;
                GUI.color = Theme.WithAlpha(Theme.Critical, 0.9f);
                styles.DrawPill(rect, styles.PillWhite);
                GUI.color = old;
            }

            // 功能图标：走程序化图标，不受「显示 emoji」开关影响，否则关掉 emoji 后 ✕ 会消失
            Texture2D icon = icons != null
                ? icons.GetProcedural("close", hover ? Theme.TextOnAccent : Theme.TextSecondary)
                : null;
            if (icon != null)
            {
                float size = rect.height * 0.60f;
                GUI.DrawTexture(new Rect(rect.x + (rect.width - size) * 0.5f, rect.y + (rect.height - size) * 0.5f, size, size),
                    icon, ScaleMode.ScaleToFit, true);
            }

            return GUI.Button(rect, "close", styles.Hit);
        }

        /// <summary>
        /// 状态条（原告警横幅）：始终显示当前角色状态。
        /// 正常=绿色常亮，非致命问题=黄色闪烁，致命=红色闪烁。
        /// 只显示状态本身，不列举指标名（指标名在下面的卡片里，避免重复）。
        /// </summary>
        public static void StatusBanner(Rect rect, Severity severity, string text, bool flash, float pulse,
            HudStyles styles, Texture2D alarmIcon)
        {
            bool lethal = severity == Severity.Critical;
            bool nonLethalProblem = severity == Severity.Warning || severity == Severity.Notice;
            bool steady = !lethal && !nonLethalProblem;
            Color color = BannerColor(severity);

            Texture2D background;
            if (lethal) background = styles.CardCritical;
            else if (nonLethalProblem) background = styles.CardWarning;
            else if (severity == Severity.Unknown) background = styles.CardSunken;
            else background = styles.CardPositive;

            // 闪烁：致命闪得更深，非致命闪烁更柔
            float alpha = 1f;
            if (flash)
            {
                if (lethal) alpha = 0.55f + 0.45f * pulse;
                else if (nonLethalProblem) alpha = 0.70f + 0.30f * pulse;
            }

            Color old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            styles.DrawSurface(rect, background);

            if (lethal)
            {
                GUI.color = new Color(1f, 1f, 1f, (flash ? 0.10f + 0.22f * pulse : 0.16f) * old.a);
                GUI.DrawTexture(rect, styles.GlowWhite);
            }
            GUI.color = old;

            // 左侧状态色条：一眼看出当前档位
            Fill(new Rect(rect.x + 1f, rect.y + styles.Theme.Px(3f),
                Mathf.Max(2f, styles.Theme.Px(3f)), rect.height - styles.Theme.Px(6f)),
                Theme.WithAlpha(color, 0.95f));

            float inset = styles.Theme.Px(8f);
            if (lethal && alarmIcon != null)
            {
                float size = Mathf.Min(rect.height * 0.62f, styles.Theme.Px(15f));
                GUI.DrawTexture(new Rect(rect.x + styles.Theme.Px(9f), rect.y + (rect.height - size) * 0.5f, size, size),
                    alarmIcon, ScaleMode.ScaleToFit, true);
                inset = styles.Theme.Px(28f);
            }

            GUI.Label(new Rect(rect.x + inset, rect.y, Mathf.Max(10f, rect.width - inset * 2f), rect.height),
                "<color=" + Theme.SeverityHex(severity) + ">" + text + "</color>", styles.AlarmText);
        }

        /// <summary>
        /// 血氧浓度条。
        ///
        /// 血氧是「浓度」而不是节律，用波形反而看不出数值含义，
        /// 所以这里用一条带 90% 危险阈值的水平浓度条：填充长度 = 血氧饱和度，
        /// 阈值以下区域有淡红底，低于阈值时填充色转为琥珀/红。
        /// </summary>
        public static void Spo2Gauge(Rect rect, bool valid, float spo2, HudStyles styles)
        {
            Theme th = styles.Theme;
            const float Threshold = 0.90f;

            Severity severity = valid ? VitalRules.BloodOxygen(spo2) : Severity.Unknown;
            float value01 = valid ? Mathf.Clamp01(spo2 / 100f) : 0f;

            float labelH = th.SmallLine();
            float trackH = Mathf.Min(rect.height * 0.46f, th.Px(24f));
            float trackY = rect.y + (rect.height - trackH - labelH) * 0.5f;
            float left = rect.x + th.Px(4f);
            float width = Mathf.Max(30f, rect.width - th.Px(8f));

            Rect track = new Rect(left, trackY, width, trackH);
            Rect labels = new Rect(left, track.yMax + th.Px(2f), width, labelH);

            styles.DrawPill(track, styles.PillIdle);

            float innerX = track.x + th.Px(2f);
            float innerW = Mathf.Max(6f, track.width - th.Px(4f));
            float innerH = Mathf.Max(3f, track.height - th.Px(4f));

            // 危险区（阈值以下）淡红底：填得越满，露出的红底越少
            Color old = GUI.color;
            GUI.color = Theme.WithAlpha(Theme.Critical, 0.13f);
            styles.DrawPill(new Rect(innerX, track.y + th.Px(2f), innerW * Threshold, innerH), styles.PillWhite);
            GUI.color = old;

            if (value01 > 0f)
            {
                Color fill = severity == Severity.Normal || severity == Severity.Unknown
                    ? Theme.ChPleth
                    : Theme.SeverityColor(severity);

                old = GUI.color;
                GUI.color = fill;
                styles.DrawPill(new Rect(innerX, track.y + th.Px(2f), Mathf.Max(innerH, innerW * value01), innerH),
                    styles.PillWhite);
                GUI.color = old;
            }

            // 90% 危险阈值刻度
            float tickX = innerX + innerW * Threshold;
            Fill(new Rect(tickX, track.y + th.Px(3f), Mathf.Max(1f, th.Px(1.5f)), track.height - th.Px(6f)),
                Theme.WithAlpha(Theme.Warning, 0.6f));

            if (!valid)
            {
                GUI.Label(track, "<color=" + MutedHex + ">无信号</color>", styles.Center);
            }

            // 刻度文字
            GUI.Label(new Rect(labels.x, labels.y, labels.width * 0.25f, labels.height),
                "<color=" + MutedHex + ">0%</color>", styles.Tiny);
            GUI.Label(new Rect(tickX - th.Px(18f), labels.y, th.Px(36f), labels.height),
                "<color=" + Theme.HexWarning + ">90%</color>", styles.ChipIdle);
            GUI.Label(new Rect(labels.xMax - labels.width * 0.25f, labels.y, labels.width * 0.25f, labels.height),
                "<color=" + MutedHex + ">100%</color>", styles.TinyRight);
        }

        /// <summary>顶部状态条配色：致命红、非致命黄、正常绿、无数据灰。</summary>
        private static Color BannerColor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical: return Theme.Critical;
                case Severity.Warning:
                case Severity.Notice: return Theme.Warning;
                case Severity.Normal: return Theme.Normal;
                default: return Theme.TextMuted;
            }
        }

        // ---------------------------------------------------------------
        // 肢体与队友
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // 肢体卡片：尽可能多地展示肢体信息
        // ---------------------------------------------------------------

        /// <summary>
        /// 肢体检视卡。竖向分带：
        /// [图标 + 名称（要害）｜健康%] / [皮肤条 ｜ 肌肉条] / [疼痛·感染·出血·弹片] / [状态标签胶囊]
        /// </summary>
        public static void LimbCard(Rect rect, LimbReading limb, bool striped, HudStyles styles, IconProvider icons)
        {
            Theme th = styles.Theme;
            styles.DrawSurface(rect, striped ? styles.CardSunken : styles.CardFor(limb.Severity));

            float pad = th.Px(7f);
            float line1 = th.LabelLine();
            float barsLine = Mathf.Max(th.SmallLine(), th.BarHeight() + th.Px(5f));
            float line3 = th.SmallLine();
            float line4 = th.SmallLine();
            // 图标隐藏时不留空位
            Texture2D icon = icons.Get(LimbIcon(limb), IconTint(limb.Severity));
            float iconSize = icon != null ? Mathf.Max(13f * th.UiScale, line1 * 0.95f) : 0f;
            float iconGap = icon != null ? th.Px(6f) : 0f;

            float innerX = rect.x + pad;
            float innerW = Mathf.Max(20f, rect.width - pad * 2f);
            float y = rect.y + pad;

            // 行1：图标 + 名称 | 健康%
            if (icon != null)
            {
                DrawIcon(new Rect(innerX, y + (line1 - iconSize) * 0.5f, iconSize, iconSize), icon, limb.Severity);
            }

            float nameX = innerX + iconSize + iconGap;
            string name = limb.IsVital ? limb.Name + "（要害）" : limb.Name;
            GUI.Label(new Rect(nameX, y, Mathf.Max(10f, innerX + innerW - nameX - th.Px(64f)), line1),
                Fit(name, th, 1), styles.Label);
            GUI.Label(new Rect(innerX + innerW - th.Px(62f), y, th.Px(62f), line1),
                Fit("<color=" + Theme.ValueHex(limb.Severity) + ">" + VitalRules.Pct(limb.Health) + "</color>", th, 3),
                styles.ValueRight);
            y += line1;

            // 行2：皮肤 / 肌肉分别显示（这两项临床上区别很大）
            float half = (innerW - th.Px(10f)) * 0.5f;
            DrawHealthBar(new Rect(innerX, y, half, barsLine), "皮肤", limb.SkinHealth, styles);
            DrawHealthBar(new Rect(innerX + half + th.Px(10f), y, half, barsLine), "肌肉", limb.MuscleHealth, styles);
            y += barsLine;

            // 行3：疼痛 / 感染 / 出血 / 弹片（数值全部摆出来）
            string metrics = "疼痛 " + VitalRules.Num1(limb.Pain)
                + " · 感染 " + VitalRules.Num1(limb.Infection) + "%"
                + " · 出血 " + VitalRules.Num2(limb.Bleed)
                + " · 弹片 " + limb.Shrapnel;
            GUI.Label(new Rect(innerX, y, innerW, line3),
                Fit("<color=" + MutedHex + ">" + metrics + "</color>", th, 2), styles.Tiny);
            y += line3;

            // 行4：状态标签
            DrawLimbChips(new Rect(innerX, y, innerW, line4), limb, styles);
        }

        /// <summary>皮肤/肌肉健康分条：标签 + 进度条 + 数值。</summary>
        private static void DrawHealthBar(Rect rect, string label, float value, HudStyles styles)
        {
            Theme th = styles.Theme;
            float labelW = th.Px(30f);
            float valueW = th.Px(28f);
            float barH = th.BarHeight();
            Severity severity = HealthSeverity(value);

            GUI.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                Fit("<color=" + MutedHex + ">" + label + "</color>", th, 2), styles.Tiny);

            Rect barRect = new Rect(rect.x + labelW, rect.y + (rect.height - barH) * 0.5f,
                Mathf.Max(6f, rect.width - labelW - valueW - th.Px(4f)), barH);
            Bar(barRect, value / 100f, Theme.SeverityColor(severity), styles);

            GUI.Label(new Rect(rect.xMax - valueW, rect.y, valueW, rect.height),
                Fit("<color=" + Theme.ValueHex(severity) + ">" + VitalRules.Int0(value) + "</color>", th, 2),
                styles.TinyRight);
        }

        private static Severity HealthSeverity(float value)
        {
            if (value < 40f) return Severity.Critical;
            if (value < 60f) return Severity.Warning;
            if (value < 80f) return Severity.Notice;
            return Severity.Normal;
        }

        /// <summary>肢体状态标签；完全没有异常时显示一个绿色的「完好」。</summary>
        private static void DrawLimbChips(Rect rect, LimbReading limb, HudStyles styles)
        {
            Theme th = styles.Theme;
            float gap = th.Px(4f);
            float x = rect.x;
            int drawn = 0;

            drawn += PushChip(rect, ref x, gap, limb.Dismembered, "断肢", Theme.Critical, styles);
            drawn += PushChip(rect, ref x, gap, limb.Broken, limb.Splinted ? "骨折·已固定" : "骨折",
                limb.Splinted ? Theme.Notice : Theme.Warning, styles);
            drawn += PushChip(rect, ref x, gap, limb.Dislocated, "脱臼", Theme.Warning, styles);

            if (limb.Bleed > 0.01f)
            {
                drawn += PushChip(rect, ref x, gap, true,
                    limb.BlockedBleeding ? "出血·已止血" : "出血 " + VitalRules.Num2(limb.Bleed),
                    limb.BlockedBleeding ? Theme.Notice : Theme.Critical, styles);
            }
            if (limb.Infected || limb.Infection > 0.001f)
            {
                drawn += PushChip(rect, ref x, gap, true, "感染 " + VitalRules.Num1(limb.Infection) + "%",
                    Theme.SeverityColor(VitalRules.Infection(limb.Infection)), styles);
            }

            drawn += PushChip(rect, ref x, gap, limb.Shrapnel > 0, "弹片 " + limb.Shrapnel, Theme.Notice, styles);

            if (limb.Pain > VitalRules.PainNotice)
            {
                drawn += PushChip(rect, ref x, gap, true, "疼痛 " + VitalRules.Num1(limb.Pain),
                    Theme.SeverityColor(VitalRules.Pain(limb.Pain)), styles);
            }

            if (drawn == 0)
            {
                Chip(new Rect(rect.x, rect.y, ChipWidth("完好", styles), rect.height), "完好", Theme.Normal, styles);
            }
        }

        /// <summary>条件成立且仍有余量时画一个胶囊标签；返回画出数量（0 或 1）。</summary>
        private static int PushChip(Rect bounds, ref float x, float gap, bool condition, string text, Color color,
            HudStyles styles)
        {
            if (!condition) return 0;

            float width = ChipWidth(text, styles);
            if (x + width > bounds.xMax) return 0;

            Chip(new Rect(x, bounds.y, width, bounds.height), text, color, styles);
            x += width + gap;
            return 1;
        }

        /// <summary>胶囊标签宽度。</summary>
        public static float ChipWidth(string text, HudStyles styles)
        {
            // CalcSize 用的是未缩放的字号，所以整体再乘一次绘制期缩放
            return (styles.ChipOn.CalcSize(new GUIContent(text)).x + styles.Theme.Px(16f))
                * styles.Theme.UiScale;
        }

        /// <summary>彩色胶囊标签（深色文字），用于状态标记。</summary>
        public static void Chip(Rect rect, string text, Color color, HudStyles styles)
        {
            Color old = GUI.color;
            GUI.color = Theme.Fade(Theme.WithAlpha(color, 0.9f), old.a);
            styles.DrawPill(rect, styles.PillWhite);
            GUI.color = old;

            GUI.Label(rect, Fit(text, styles.Theme, 2), styles.ChipOn);
        }

        private static string LimbIcon(LimbReading limb)
        {
            if (limb.IsHead) return "brain";
            if (limb.IsLeg) return "leg";
            if (limb.IsAbdomen) return "shield";
            if (limb.IsArm) return "arm";
            return "bone";
        }

        /// <summary>
        /// 队友卡片。竖向分带：[头像] + [姓名 | 分级徽章] / [指标 | 距离] / [摘要]，底部整宽血量条。
        /// <paramref name="avatar"/> 为 null 时不画头像（文字自动左移占满）。
        /// <paramref name="hovered"/> 为 true 时整卡提亮并画出可点击提示线（点击查看详情）。
        /// </summary>
        public static void MateCard(Rect rect, MateReading mate, Texture2D avatar, bool striped, bool hovered,
            HudStyles styles)
        {
            Theme th = styles.Theme;
            if (hovered)
            {
                styles.DrawSurface(rect, styles.CardRaised);
            }
            else
            {
                styles.DrawSurface(rect, striped ? styles.CardSunken : styles.CardFor(mate.Severity));
            }

            float pad = th.Px(7f);
            float line1 = th.LabelLine();
            float line2 = th.SmallLine() * 1.2f;
            float line3 = th.SmallLine();
            float barH = th.BarHeight();
            float badgeW = th.Px(54f);
            float textBlock = line1 + line2 + line3;

            float textX = rect.x + pad;
            if (avatar != null)
            {
                float size = th.AvatarSize();
                float avatarY = rect.y + pad + Mathf.Max(0f, (textBlock - size) * 0.5f);
                GUI.DrawTexture(new Rect(textX, avatarY, size, size), avatar, ScaleMode.ScaleToFit, true);
                textX += size + th.Px(8f);
            }

            float textW = Mathf.Max(20f, rect.xMax - pad - textX);
            float y = rect.y + pad;

            // 行1：姓名 + 分级徽章
            GUI.Label(new Rect(textX, y, Mathf.Max(20f, textW - badgeW - th.Px(6f)), line1),
                Fit(mate.Name, th, 1), styles.MateName);
            Badge(new Rect(rect.xMax - pad - badgeW, y + (line1 - line2) * 0.5f, badgeW, line2),
                mate.Severity, Theme.SeverityLabel(mate.Severity), styles);
            y += line1;

            // 行2：指标 + 距离
            string metrics = "心率 " + VitalRules.Int0(mate.HeartRate)
                + " · 血氧 " + VitalRules.Int0(mate.BloodOxygen) + "%"
                + " · 意识 " + VitalRules.Int0(mate.Consciousness) + "%";
            GUI.Label(new Rect(textX, y, Mathf.Max(20f, textW - th.Px(74f)), line2),
                Fit("<color=" + MutedHex + ">" + metrics + "</color>", th, 2), styles.Tiny);
            GUI.Label(new Rect(rect.xMax - pad - th.Px(72f), y, th.Px(72f), line2),
                Fit(mate.Distance >= 0f ? VitalRules.Num1(mate.Distance) + " m" : "距离未知", th, 2),
                styles.MateDistance);
            y += line2;

            // 行3：摘要 + 最伤肢体
            string summary = string.IsNullOrEmpty(mate.WorstLimb) ? mate.Summary : mate.Summary + " · " + mate.WorstLimb;
            GUI.Label(new Rect(textX, y, textW, line3),
                Fit("<color=" + SummaryHex(mate.Severity) + ">" + summary + "</color>", th, 2), styles.MateMeta);

            // 底部：整宽血量条
            float barY = rect.yMax - pad - barH;
            Bar(new Rect(rect.x + pad, barY, Mathf.Max(10f, rect.width - pad * 2f - th.Px(46f)), barH),
                mate.BloodBar, Theme.SeverityColor(mate.Severity), styles);
            GUI.Label(new Rect(rect.xMax - pad - th.Px(42f), barY - th.Px(1f), th.Px(42f), barH + th.Px(2f)),
                Fit("<color=" + MutedHex + ">血量</color>", th, 2), styles.Tiny);

            // 可点击提示：悬停时底部出现一条强调色细线（点击整卡进入详情）
            if (hovered)
            {
                Fill(new Rect(rect.x + pad, rect.yMax - th.Px(3f), Mathf.Max(10f, rect.width - pad * 2f), th.Px(2f)),
                    Theme.WithAlpha(Theme.Accent, 0.9f));
            }
        }

        /// <summary>摘要文字色：异常时用分级色，正常时用次亮色。</summary>
        private static string SummaryHex(Severity severity)
        {
            return severity >= Severity.Warning ? Theme.SeverityHex(severity) : SecondaryHex;
        }

        // ---------------------------------------------------------------
        // 滚动
        // ---------------------------------------------------------------

        private static Rect ThumbRect(Rect view, ScrollState state, float trackWidth, out Rect track, out float maxOffset)
        {
            maxOffset = Mathf.Max(0f, state.ContentHeight - view.height);
            track = new Rect(view.xMax - trackWidth - 1f, view.y, trackWidth, view.height);
            if (maxOffset <= 0f || state.ContentHeight <= 0f)
            {
                return new Rect(track.x, track.y, track.width, track.height);
            }

            float thumbH = Mathf.Max(view.height * 0.12f, view.height * (view.height / state.ContentHeight));
            float thumbY = view.y + (view.height - thumbH) * (state.Offset / maxOffset);
            return new Rect(track.x, thumbY, track.width, thumbH);
        }

        /// <summary>
        /// 滚动输入处理：滚轮 + 滚动条拖动/点击。<b>必须在绘制内容之前调用</b>，
        /// 这样被消费掉的事件不会再被内容里的按钮响应。
        /// 方向固定为「滚轮向上 = 内容向上移动（看更后面的内容）」。
        /// </summary>
        public static void ScrollInput(Rect view, ScrollState state, HudStyles styles)
        {
            // 模态窗口打开时不允许滚动被压在下面的面板
            if (!GUI.enabled) return;

            Event e = Event.current;
            if (e == null) return;

            float trackWidth = Mathf.Max(4f, styles.Theme.Px(5f));
            Rect track;
            float maxOffset;
            Rect thumb = ThumbRect(view, state, trackWidth, out track, out maxOffset);

            if (maxOffset <= 0f)
            {
                state.Offset = 0f;
                state.Dragging = false;
                return;
            }

            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
            {
                state.Offset = Mathf.Clamp(state.Offset + e.delta.y * styles.Theme.Px(30f), 0f, maxOffset);
                e.Use();
                return;
            }

            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (thumb.Contains(e.mousePosition))
                    {
                        state.Dragging = true;
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    else if (track.Contains(e.mousePosition))
                    {
                        // 按滑块高度补偿，让滑块中心落到鼠标处
                        float denom = Mathf.Max(1f, view.height - thumb.height);
                        state.Offset = Mathf.Clamp(
                            (e.mousePosition.y - view.y - thumb.height * 0.5f) / denom * maxOffset, 0f, maxOffset);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (state.Dragging && GUIUtility.hotControl == id)
                    {
                        float denom = Mathf.Max(1f, view.height - thumb.height);
                        state.Offset = Mathf.Clamp(
                            (e.mousePosition.y - view.y - thumb.height * 0.5f) / denom * maxOffset, 0f, maxOffset);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (state.Dragging)
                    {
                        state.Dragging = false;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        /// <summary>绘制滚动条（内容绘制之后调用）。</summary>
        public static void ScrollBar(Rect view, ScrollState state, HudStyles styles)
        {
            float trackWidth = Mathf.Max(4f, styles.Theme.Px(5f));
            Rect track;
            float maxOffset;
            Rect thumb = ThumbRect(view, state, trackWidth, out track, out maxOffset);
            if (maxOffset <= 0f) return;

            // 滚动条很细（约 5px），胶囊纹理的 border 同样会被压扁，统一用细条样式
            styles.DrawBar(track, styles.BarTrack);

            Color old = GUI.color;
            GUI.color = state.Dragging ? Theme.Accent : new Color(0.78f, 0.81f, 0.85f, 0.9f);
            styles.DrawBar(thumb, styles.BarFill);
            GUI.color = old;
        }

        // ---------------------------------------------------------------

        /// <summary>图标配色：正常时用中性灰蓝，异常时用分级色（仅程序化图标使用）。</summary>
        private static Color IconTint(Severity severity)
        {
            if (severity == Severity.Normal) return Theme.WithAlpha(Theme.TextSecondary, 0.85f);
            if (severity == Severity.Unknown) return Theme.WithAlpha(Theme.TextMuted, 0.85f);
            return Theme.WithAlpha(Theme.SeverityColor(severity), 0.95f);
        }

        /// <summary>
        /// 画图标。emoji 自带颜色，无法再着色，因此用透明度表达状态：
        /// 危急/警告全亮，正常略降（避免彩色图标抢走数值的视觉焦点），无数据时压暗。
        /// </summary>
        private static void DrawIcon(Rect rect, Texture2D icon, Severity severity)
        {
            if (icon == null) return;

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, IconAlpha(severity) * old.a);
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        private static float IconAlpha(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical:
                case Severity.Warning:
                    return 1f;
                case Severity.Unknown:
                    return 0.45f;
                default:
                    return 0.9f;
            }
        }
    }
}
