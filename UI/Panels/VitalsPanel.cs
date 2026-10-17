using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
    internal static class VitalsPanel
    {
        private static readonly string[] GroupOrder = { "循环系统", "神经 / 感知", "代谢 / 内环境", "体能 / 技能" };
        private static readonly string[] GroupNotes = { "心肺与血液", "意识、疼痛与情绪", "代谢、体温与免疫", "体力、负重与技能" };

        public static float Draw(HudState s, BodySnapshot snap, Rect area, float top, float scrollOffset,
            bool channelsCovered)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float gap = th.Px(6f);
            float cardH = th.VitalCardHeight();
            float cardGap = th.Px(8f);
            float rowH = th.CompactRowHeight();
            float sectionH = th.SectionHeight();

            Layout l = new Layout(area.x, top - scrollOffset, area.width);

            if (snap == null || !snap.Valid)
            {
                EmptyState.Draw(ref l, st, th, snap != null ? snap.Error : "暂无数据");
                return l.Y + scrollOffset;
            }

            bool showLimits = s.Config.ShowLimits.Value;
            
            System.Collections.Generic.List<int> primary = s.ScratchPrimary;
            System.Collections.Generic.List<int> secondary = s.ScratchSecondary;

            for (int g = 0; g < GroupOrder.Length; g++)
            {
                string group = GroupOrder[g];
                primary.Clear();
                secondary.Clear();

                for (int i = 0; i < snap.Vitals.Count; i++)
                {
                    VitalReading reading = snap.Vitals[i];
                    if (reading.Group != group) continue;
                    if (reading.ChannelCovered && channelsCovered) continue;
                    if (reading.Secondary) secondary.Add(i);
                    else primary.Add(i);
                }

                if (primary.Count == 0 && secondary.Count == 0) continue;

                l.Gap(gap);
                Widgets.SectionHeader(l.Take(sectionH), group,
                    GroupNotes[g] + " · " + (primary.Count + secondary.Count) + " 项", st);
                l.Gap(th.Px(3f));

                for (int k = 0; k < primary.Count; k += 2)
                {
                    Rect row = l.Take(cardH, cardGap);
                    Rect right;
                    Rect left = Layout.SplitColumns(row, cardGap, out right);

                    Widgets.VitalCard(left, snap.Vitals[primary[k]], st, s.Icons, showLimits);
                    if (k + 1 < primary.Count)
                    {
                        Widgets.VitalCard(right, snap.Vitals[primary[k + 1]], st, s.Icons, showLimits);
                    }
                }

                for (int k = 0; k < secondary.Count; k++)
                {
                    Rect row = l.Take(rowH + th.Px(2f));
                    Widgets.CompactRow(row, snap.Vitals[secondary[k]], (k & 1) == 1, st, s.Icons, showLimits);
                }
            }

            return l.Y + scrollOffset;
        }
    }
}
