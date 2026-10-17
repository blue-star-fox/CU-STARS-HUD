using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
    internal static class EmptyState
    {
        public static void Draw(ref Layout l, HudStyles styles, Theme theme, string message)
        {
            float gap = theme.Px(8f);
            l.Gap(gap);
            Rect box = l.Take(theme.Px(56f), gap);
            styles.DrawBox(box, styles.CardSunken);
            GUI.Label(new Rect(box.x + theme.Px(12f), box.y, box.width - theme.Px(24f), box.height),
                "<color=" + Theme.HexSecondary + ">" + (string.IsNullOrEmpty(message) ? "暂无数据" : message) + "</color>",
                styles.Body);

            l.Gap(theme.Px(4f));
            Rect hint = l.Take(theme.Px(20f));
            GUI.Label(hint, "<color=" + Theme.HexMuted + ">存档载入后才有读数。</color>", styles.Tiny);
        }
    }

    internal static class LimbsPanel
    {
        
        public static float Draw(HudState s, BodySnapshot snap, Rect area, float top, float scrollOffset)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float gap = th.Px(6f);
            float limbH = th.LimbRowHeight();
            float sectionH = th.SectionHeight();

            Layout l = new Layout(area.x, top - scrollOffset, area.width);

            if (snap == null || !snap.Valid)
            {
                EmptyState.Draw(ref l, st, th, snap != null ? snap.Error : "暂无数据");
                return l.Y + scrollOffset;
            }

            if (snap.Limbs.Count == 0)
            {
                EmptyState.Draw(ref l, st, th, "未读取到肢体数据");
                return l.Y + scrollOffset;
            }

            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "肢体总览",
                VitalRules.Int0(snap.AverageLimbHealth) + "% 平均健康", st);
            l.Gap(th.Px(3f));

            Rect summary = l.Take(SummaryHeight(th), gap);
            DrawSummary(summary, snap, s);

            l.Gap(gap);
            Widgets.SectionHeader(l.Take(sectionH), "逐条肢体",
                snap.Limbs.Count + " 条 · 按严重度排序", st);
            l.Gap(th.Px(3f));

            for (int i = 0; i < snap.Limbs.Count; i++)
            {
                Rect row = l.Take(limbH, th.Px(4f));
                Widgets.LimbCard(row, snap.Limbs[i], (i & 1) == 1, st, s.Icons);
            }

            return l.Y + scrollOffset;
        }

        private static float SummaryHeight(Theme th)
        {
            return th.Px(10f) + th.Font(2) * 1.45f + th.Font(3) * 1.4f + th.Px(14f);
        }

        private static void DrawSummary(Rect rect, BodySnapshot snap, HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            st.DrawBox(rect, st.CardSunken);

            float cellW = rect.width * 0.25f;
            DrawCell(new Rect(rect.x, rect.y, cellW, rect.height), "平均健康",
                VitalRules.Pct(snap.AverageLimbHealth),
                VitalRules.Clamp(snap.AverageLimbHealth / 100f, 0f, 1f), th, st);

            DrawCell(new Rect(rect.x + cellW, rect.y, cellW, rect.height), "最伤肢体",
                string.IsNullOrEmpty(snap.WorstLimbName)
                    ? "—"
                    : snap.WorstLimbName + " " + VitalRules.Int0(snap.WorstLimbHealth),
                VitalRules.Clamp(snap.WorstLimbHealth / 100f, 0f, 1f), th, st);

            DrawCell(new Rect(rect.x + cellW * 2f, rect.y, cellW, rect.height), "感染肢体",
                snap.InfectedLimbCount + " 条",
                snap.InfectedLimbCount > 0 ? 0.5f : 0f, th, st);

            DrawCell(new Rect(rect.x + cellW * 3f, rect.y, cellW, rect.height), "重伤/骨折",
                snap.InjuredLimbCount + " 条",
                snap.InjuredLimbCount > 0 ? 0.6f : 0f, th, st);
        }

        private static void DrawCell(Rect rect, string label, string value, float bar, Theme th, HudStyles st)
        {
            float pad = th.Px(8f);
            float labelH = th.Font(2) * 1.45f;
            float valueH = Mathf.Max(th.Font(3) * 1.3f, rect.height - pad - labelH - th.Px(14f));
            float barH = Mathf.Max(3f, th.Px(4f));

            GUI.Label(new Rect(rect.x + pad, rect.y + th.Px(5f), Mathf.Max(6f, rect.width - pad * 2f), labelH),
                "<color=" + Theme.HexMuted + ">" + label + "</color>", st.Tiny);
            GUI.Label(new Rect(rect.x + pad, rect.y + th.Px(5f) + labelH, Mathf.Max(6f, rect.width - pad * 2f), valueH),
                "<color=" + Theme.HexPrimary + ">" + value + "</color>", st.ValueLarge);

            Rect barRect = new Rect(rect.x + pad, rect.yMax - th.Px(6f) - barH, Mathf.Max(6f, rect.width - pad * 2f), barH);
            Color color = bar > 0f ? Theme.Warning : Theme.Normal;
            Widgets.Bar(barRect, bar, color, st);
        }
    }
}
