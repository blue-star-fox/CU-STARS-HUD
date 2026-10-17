using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 设置页：只保留可操作项本身，不放任何说明文字
    /// （说明与版本信息统一收进「关于」窗口）。
    /// 所有改动即时写入 BepInEx 配置，键名与 v1.2 兼容。
    /// </summary>
    internal static class SettingsPanel
    {
        public static float Draw(HudState s, Rect area, float scrollOffset)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;
            PluginConfig cfg = s.Config;

            float gap = th.Px(6f);
            float sectionH = th.SectionHeight();
            float rowH = th.Px(28f);

            Layout l = new Layout(area.x, -scrollOffset, area.width);

            // ---------------- 显示 ----------------
            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "显示", null, st);
            l.Gap(th.Px(3f));

            // 注意：ConfigEntry.Value 的 setter 会立刻把配置写盘，
            // 所以先把滑块结果量化到 0.05 步进，避免拖动时每帧写文件。
            float opacity = SliderRow(ref l, "面板不透明度", cfg.Opacity.Value, 0.3f, 1f,
                VitalRules.Pct01(cfg.Opacity.Value), th, st);
            float opacityStep = Mathf.Round(opacity / 0.05f) * 0.05f;
            if (Mathf.Abs(opacityStep - cfg.Opacity.Value) > 0.001f) cfg.Opacity.Value = opacityStep;

            float scale = SliderRow(ref l, "界面缩放", cfg.Scale.Value, 0.7f, 1.5f,
                cfg.Scale.Value.ToString("0.00"), th, st);
            float scaleStep = Mathf.Round(scale / 0.05f) * 0.05f;
            if (Mathf.Abs(scaleStep - cfg.Scale.Value) > 0.001f)
            {
                cfg.Scale.Value = scaleStep;
                s.MarkGeometryDirty(false);
            }

            float fontSize = SliderRow(ref l, "基准字号", cfg.FontSize.Value, 11f, 20f,
                Mathf.RoundToInt(cfg.FontSize.Value).ToString(), th, st);
            int newFont = Mathf.RoundToInt(fontSize);
            if (newFont != cfg.FontSize.Value) cfg.FontSize.Value = newFont;

            l.Gap(gap * 0.5f);
            GUI.Label(l.Take(th.Font(2) * 1.4f), "<color=" + Theme.HexMuted + ">字体</color>", st.Tiny);
            Rect fontRow = l.Take(rowH, gap);
            string mode = cfg.FontMode.Value ?? FontProvider.ModeAuto;
            float third = (fontRow.width - gap * 2f) / 3f;

            if (Widgets.Segment(new Rect(fontRow.x, fontRow.y, third, fontRow.height), "自动", mode == FontProvider.ModeAuto, st)
                && mode != FontProvider.ModeAuto)
            {
                cfg.FontMode.Value = FontProvider.ModeAuto;
            }
            if (Widgets.Segment(new Rect(fontRow.x + third + gap, fontRow.y, third, fontRow.height), "系统", mode == FontProvider.ModeSystem, st)
                && mode != FontProvider.ModeSystem)
            {
                cfg.FontMode.Value = FontProvider.ModeSystem;
            }
            if (Widgets.Segment(new Rect(fontRow.x + (third + gap) * 2f, fontRow.y, third, fontRow.height), "游戏", mode == FontProvider.ModeGame, st)
                && mode != FontProvider.ModeGame)
            {
                cfg.FontMode.Value = FontProvider.ModeGame;
            }

            // 当前实际生效的字体：切换字体后能立刻确认有没有生效
            GUI.Label(l.Take(th.SmallLine()),
                "<color=" + Theme.HexMuted + ">当前使用："
                + (string.IsNullOrEmpty(s.Fonts.ResolvedName) ? "默认字体" : s.Fonts.ResolvedName)
                + "</color>", st.Tiny);

            // 界面风格：CU-UI / 游戏风格（只换配色，圆角一致）
            l.Gap(gap * 0.5f);
            GUI.Label(l.Take(th.Font(2) * 1.4f), "<color=" + Theme.HexMuted + ">界面风格</color>", st.Tiny);
            Rect skinRow = l.Take(rowH, gap);
            string skin = cfg.Skin.Value ?? "fox";
            bool gameSkin = string.Equals(skin, "game", System.StringComparison.OrdinalIgnoreCase);
            float half = (skinRow.width - gap) * 0.5f;

            if (Widgets.Segment(new Rect(skinRow.x, skinRow.y, half, skinRow.height), "CU-UI", !gameSkin, st)
                && gameSkin)
            {
                cfg.Skin.Value = "fox";
            }
            if (Widgets.Segment(new Rect(skinRow.x + half + gap, skinRow.y, half, skinRow.height), "游戏风格", gameSkin, st)
                && !gameSkin)
            {
                cfg.Skin.Value = "game";
            }

            l.Gap(gap);
            cfg.ShowEmoji.Value = Widgets.Toggle(l.Take(rowH, gap), cfg.ShowEmoji.Value,
                "显示 emoji 图标", st);

            // ---------------- 交互 ----------------
            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "交互", null, st);
            l.Gap(th.Px(3f));

            KeybindRow(ref l, s, "显示/隐藏快捷键", 1, rowH, gap, th, st);
            KeybindRow(ref l, s, "组件卡快捷键", 2, rowH, gap, th, st);

            cfg.Resizable.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.Resizable.Value,
                "允许拖动右下角调整大小", st);

            if (Widgets.Button(l.Take(rowH, gap), "复位面板位置与大小", false, st))
            {
                s.ResetWindowPosition();
                s.Note("面板位置已复位");
            }

            // ---------------- 组件卡 ----------------
            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "组件卡", null, st);
            l.Gap(th.Px(3f));

            cfg.FloatCardsEnabled.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.FloatCardsEnabled.Value,
                "显示组件卡", st);

            if (Widgets.Button(l.Take(rowH, gap), "打开组件卡摆放界面", true, st))
            {
                s.FloatingCards.BeginEditing();
                s.Note("从右侧拖动组件卡到屏幕上，完成后点保存");
            }

            // ---------------- 监护 ----------------
            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "监护", null, st);
            l.Gap(th.Px(3f));

            cfg.WaveformsEnabled.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.WaveformsEnabled.Value,
                "显示波形条", st);
            cfg.AlertsEnabled.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.AlertsEnabled.Value,
                "显示告警横幅", st);
            cfg.AlertFlash.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.AlertFlash.Value,
                "危急时闪烁提醒", st);
            cfg.ShowLimits.Value = Widgets.Toggle(l.Take(rowH, th.Px(2f)), cfg.ShowLimits.Value,
                "显示参考范围", st);
            cfg.ShowNormalSecondary.Value = Widgets.Toggle(l.Take(rowH, gap), cfg.ShowNormalSecondary.Value,
                "始终显示次要指标", st);

            // ---------------- 关于 ----------------
            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "关于", null, st);
            l.Gap(th.Px(3f));

            if (Widgets.Button(l.Take(rowH), "查看关于与说明", false, st))
            {
                s.OpenAbout();
            }

            return l.Y + scrollOffset;
        }

        /// <summary>一行快捷键绑定：标题 + 按键按钮。</summary>
        private static void KeybindRow(ref Layout l, HudState s, string label, int target, float rowH, float gap,
            Theme th, HudStyles st)
        {
            Rect row = l.Take(rowH, gap);
            Rect right;
            Rect left = Layout.SplitColumns(row, gap, out right);

            GUI.Label(new Rect(left.x, left.y, left.width, left.height), label, st.Body);

            bool capturing = s.CapturingTarget == target;
            string text = capturing
                ? "按键…"
                : (target == 2 ? s.Config.FloatKey.Value.ToString() : s.Config.ToggleKey.Value.ToString());

            if (Widgets.KeybindButton(right, text, capturing, st))
            {
                s.CapturingTarget = target;
                s.Note("请按下新的快捷键（Esc 取消）");
            }
        }

        /// <summary>一行滑块：标题 + 当前值 + 滑轨。返回新值。</summary>
        private static float SliderRow(ref Layout l, string label, float value, float min, float max,
            string display, Theme th, HudStyles st)
        {
            float labelH = th.Font(2) * 1.5f;
            Rect row = l.Take(labelH + th.Px(18f), th.Px(4f));

            GUI.Label(new Rect(row.x, row.y, row.width * 0.7f, labelH),
                "<color=" + Theme.HexMuted + ">" + label + "</color>", st.Tiny);
            GUI.Label(new Rect(row.x + row.width * 0.7f, row.y, row.width * 0.3f, labelH), display, st.TinyRight);

            return Widgets.Slider(new Rect(row.x, row.y + labelH, row.width, th.Px(16f)), value, min, max, st);
        }
    }
}
