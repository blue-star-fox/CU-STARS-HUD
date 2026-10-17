using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    
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

        public Rect Take(float height)
        {
            Rect rect = new Rect(X, Y, Width, height);
            Y += height;
            return rect;
        }

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

        public static Rect SplitColumns(Rect row, float gutter, out Rect right)
        {
            float w = Mathf.Max(0f, (row.width - gutter) * 0.5f);
            Rect left = new Rect(row.x, row.y, w, row.height);
            right = new Rect(row.x + w + gutter, row.y, w, row.height);
            return left;
        }
    }

    internal sealed class ScrollState
    {
        public float Offset;
        public bool Dragging;
        
        public float ContentHeight;
    }

    internal static class Widgets
    {
        
        private static string MutedHex { get { return Theme.HexMuted; } }
        private static string SecondaryHex { get { return Theme.HexSecondary; } }

        private static string Fit(string text, Theme th, int fontLevel)
        {
            if (th.UiScale >= 0.999f && th.UiScale <= 1.001f) return text;
            return "<size=" + th.Font(fontLevel) + ">" + text + "</size>";
        }

        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = Theme.Fade(color, old.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

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
            
            GUI.DrawTexture(new Rect(knobX, track.y + 1f, knob, knob), styles.KnobCircle);
            GUI.color = knobOld;

            GUI.Label(new Rect(rect.x + 10f, rect.y, Mathf.Max(20f, rect.width - switchW - 24f), rect.height),
                label, styles.Body);

            return GUI.Button(rect, label + (value ? "|on" : "|off"), styles.Hit) ? !value : value;
        }

        public static float Slider(Rect rect, float value, float min, float max, HudStyles styles)
        {
            
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

        public static void VitalCard(Rect rect, VitalReading reading, HudStyles styles, IconProvider icons, bool showLimits)
        {
            Theme th = styles.Theme;
            styles.DrawSurface(rect, styles.CardFor(reading.Severity));

            float pad = th.Px(7f);
            
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

        public static void CompactRow(Rect rect, VitalReading reading, bool striped, HudStyles styles, IconProvider icons, bool showLimits)
        {
            Theme th = styles.Theme;
            if (striped) styles.DrawBox(rect, styles.CardSunken);

            float pad = th.Px(5f);
            
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

        public static void Bar(Rect rect, float t, Color color, HudStyles styles)
        {
            
            styles.DrawBar(rect, styles.BarTrack);

            float clamped = Mathf.Clamp01(t);
            if (clamped <= 0f) return;

            Color old = GUI.color;
            GUI.color = Theme.Fade(color, old.a);
            styles.DrawBar(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * clamped), rect.height),
                styles.BarFill);
            GUI.color = old;
        }

        public static void Badge(Rect rect, Severity severity, string text, HudStyles styles)
        {
            Color old = GUI.color;
            GUI.color = Theme.Fade(Theme.WithAlpha(Theme.SeverityColor(severity), 0.92f), old.a);
            styles.DrawPill(rect, styles.PillWhite);
            GUI.color = old;

            GUI.Label(rect, Fit(text, styles.Theme, 2), styles.ChipOn);
        }

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

            float tickX = innerX + innerW * Threshold;
            Fill(new Rect(tickX, track.y + th.Px(3f), Mathf.Max(1f, th.Px(1.5f)), track.height - th.Px(6f)),
                Theme.WithAlpha(Theme.Warning, 0.6f));

            if (!valid)
            {
                GUI.Label(track, "<color=" + MutedHex + ">无信号</color>", styles.Center);
            }

            GUI.Label(new Rect(labels.x, labels.y, labels.width * 0.25f, labels.height),
                "<color=" + MutedHex + ">0%</color>", styles.Tiny);
            GUI.Label(new Rect(tickX - th.Px(18f), labels.y, th.Px(36f), labels.height),
                "<color=" + Theme.HexWarning + ">90%</color>", styles.ChipIdle);
            GUI.Label(new Rect(labels.xMax - labels.width * 0.25f, labels.y, labels.width * 0.25f, labels.height),
                "<color=" + MutedHex + ">100%</color>", styles.TinyRight);
        }

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

        public static void LimbCard(Rect rect, LimbReading limb, bool striped, HudStyles styles, IconProvider icons)
        {
            Theme th = styles.Theme;
            styles.DrawSurface(rect, striped ? styles.CardSunken : styles.CardFor(limb.Severity));

            float pad = th.Px(7f);
            float line1 = th.LabelLine();
            float barsLine = Mathf.Max(th.SmallLine(), th.BarHeight() + th.Px(5f));
            float line3 = th.SmallLine();
            float line4 = th.SmallLine();
            
            Texture2D icon = icons.Get(LimbIcon(limb), IconTint(limb.Severity));
            float iconSize = icon != null ? Mathf.Max(13f * th.UiScale, line1 * 0.95f) : 0f;
            float iconGap = icon != null ? th.Px(6f) : 0f;

            float innerX = rect.x + pad;
            float innerW = Mathf.Max(20f, rect.width - pad * 2f);
            float y = rect.y + pad;

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

            float half = (innerW - th.Px(10f)) * 0.5f;
            DrawHealthBar(new Rect(innerX, y, half, barsLine), "皮肤", limb.SkinHealth, styles);
            DrawHealthBar(new Rect(innerX + half + th.Px(10f), y, half, barsLine), "肌肉", limb.MuscleHealth, styles);
            y += barsLine;

            string metrics = "疼痛 " + VitalRules.Num1(limb.Pain)
                + " · 感染 " + VitalRules.Num1(limb.Infection) + "%"
                + " · 出血 " + VitalRules.Num2(limb.BleedLpm) + " l/m"
                + " · 弹片 " + limb.Shrapnel;
            GUI.Label(new Rect(innerX, y, innerW, line3),
                Fit("<color=" + MutedHex + ">" + metrics + "</color>", th, 2), styles.Tiny);
            y += line3;

            DrawLimbChips(new Rect(innerX, y, innerW, line4), limb, styles);
        }

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

            if (limb.Bleed > 0.1f)
            {
                drawn += PushChip(rect, ref x, gap, true,
                    limb.BlockedBleeding
                        ? "出血·已止血"
                        : "出血 " + VitalRules.Num2(limb.BleedLpm) + " l/m",
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

        public static float ChipWidth(string text, HudStyles styles)
        {
            
            return (styles.ChipOn.CalcSize(new GUIContent(text)).x + styles.Theme.Px(16f))
                * styles.Theme.UiScale;
        }

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

            GUI.Label(new Rect(textX, y, Mathf.Max(20f, textW - badgeW - th.Px(6f)), line1),
                Fit(mate.Name, th, 1), styles.MateName);
            Badge(new Rect(rect.xMax - pad - badgeW, y + (line1 - line2) * 0.5f, badgeW, line2),
                mate.Severity, Theme.SeverityLabel(mate.Severity), styles);
            y += line1;

            string metrics = "心率 " + VitalRules.Int0(mate.HeartRate)
                + " · 血氧 " + VitalRules.Int0(mate.BloodOxygen) + "%"
                + " · 意识 " + VitalRules.Int0(mate.Consciousness) + "%";
            GUI.Label(new Rect(textX, y, Mathf.Max(20f, textW - th.Px(74f)), line2),
                Fit("<color=" + MutedHex + ">" + metrics + "</color>", th, 2), styles.Tiny);
            GUI.Label(new Rect(rect.xMax - pad - th.Px(72f), y, th.Px(72f), line2),
                Fit(mate.Distance >= 0f ? VitalRules.Num1(mate.Distance) + " m" : "距离未知", th, 2),
                styles.MateDistance);
            y += line2;

            string summary = string.IsNullOrEmpty(mate.WorstLimb) ? mate.Summary : mate.Summary + " · " + mate.WorstLimb;
            GUI.Label(new Rect(textX, y, textW, line3),
                Fit("<color=" + SummaryHex(mate.Severity) + ">" + summary + "</color>", th, 2), styles.MateMeta);

            float barY = rect.yMax - pad - barH;
            Bar(new Rect(rect.x + pad, barY, Mathf.Max(10f, rect.width - pad * 2f - th.Px(46f)), barH),
                mate.BloodBar, Theme.SeverityColor(mate.Severity), styles);
            GUI.Label(new Rect(rect.xMax - pad - th.Px(42f), barY - th.Px(1f), th.Px(42f), barH + th.Px(2f)),
                Fit("<color=" + MutedHex + ">血量</color>", th, 2), styles.Tiny);

            if (hovered)
            {
                Fill(new Rect(rect.x + pad, rect.yMax - th.Px(3f), Mathf.Max(10f, rect.width - pad * 2f), th.Px(2f)),
                    Theme.WithAlpha(Theme.Accent, 0.9f));
            }
        }

        private static string SummaryHex(Severity severity)
        {
            return severity >= Severity.Warning ? Theme.SeverityHex(severity) : SecondaryHex;
        }

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

        public static void ScrollInput(Rect view, ScrollState state, HudStyles styles)
        {
            
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

        public static void ScrollBar(Rect view, ScrollState state, HudStyles styles)
        {
            float trackWidth = Mathf.Max(4f, styles.Theme.Px(5f));
            Rect track;
            float maxOffset;
            Rect thumb = ThumbRect(view, state, trackWidth, out track, out maxOffset);
            if (maxOffset <= 0f) return;

            styles.DrawBar(track, styles.BarTrack);

            Color old = GUI.color;
            GUI.color = state.Dragging ? Theme.Accent : new Color(0.78f, 0.81f, 0.85f, 0.9f);
            styles.DrawBar(thumb, styles.BarFill);
            GUI.color = old;
        }

        private static Color IconTint(Severity severity)
        {
            if (severity == Severity.Normal) return Theme.WithAlpha(Theme.TextSecondary, 0.85f);
            if (severity == Severity.Unknown) return Theme.WithAlpha(Theme.TextMuted, 0.85f);
            return Theme.WithAlpha(Theme.SeverityColor(severity), 0.95f);
        }

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
