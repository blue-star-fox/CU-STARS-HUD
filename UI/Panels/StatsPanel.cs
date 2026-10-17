using System.Collections.Generic;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 统计选项卡：展示本地采集的游戏行为数据（**全部为累计值**）。
    ///
    /// 数据在本地采集、加密保存，不联网、不上报。
    /// 采集点见 <see cref="CuStatsHud.Patch.GameStatsHooks"/>，每一项都在反编译源码里核对过。
    /// </summary>
    internal static class StatsPanel
    {
        public static float Draw(HudState s, Rect area, float offset)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;
            GameStats g = s.GameStats;

            Layout l = new Layout(area.x, area.y - offset, area.width);
            float top = area.y - offset;

            Section(ref l, th, st, "交互");

            Row(ref l, th, st, "挖掘方块", GameStats.FormatCount(g.BlocksDug) + " 块");
            Row(ref l, th, st, "放置方块", GameStats.FormatCount(g.BlocksPlaced) + " 块");
            Row(ref l, th, st, "道具使用", GameStats.FormatCount(g.ItemsUsed) + " 次");
            Row(ref l, th, st, "场景物交互", GameStats.FormatCount(g.Interactions) + " 次");
            Row(ref l, th, st, "容器存取", GameStats.FormatCount(g.ContainerOps) + " 次");
            Row(ref l, th, st, "商品购买", GameStats.FormatCount(g.Purchases) + " 次");

            Section(ref l, th, st, "移动");

            Row(ref l, th, st, "累计移动距离", GameStats.FormatDistance(g.DistanceMoved));
            Row(ref l, th, st, "行走距离（水平）", GameStats.FormatDistance(g.DistanceWalked));
            Row(ref l, th, st, "下降高度", GameStats.FormatDistance(g.HeightDescended));
            Row(ref l, th, st, "上升高度", GameStats.FormatDistance(g.HeightAscended));
            Row(ref l, th, st, "最长单次下落", GameStats.FormatDistance(g.LongestFall));
            Row(ref l, th, st, "最大速度", GameStats.FormatSpeed(g.MaxSpeed));
            Row(ref l, th, st, "平均速度", GameStats.FormatSpeed(g.AverageSpeed));

            Section(ref l, th, st, "记录");

            Row(ref l, th, st, "启动次数", GameStats.FormatCount(g.Launches) + " 次");
            Row(ref l, th, st, "累计游玩时长", GameStats.FormatDuration(g.PlaySeconds));
            Row(ref l, th, st, "首次使用", ShortStamp(g.FirstUtc));
            Row(ref l, th, st, "最近使用", ShortStamp(g.LastUtc));

            Section(ref l, th, st, "技能 / 经验");

            Row(ref l, th, st, "力量（STR）", g.LevelSTR + " 级");
            Row(ref l, th, st, "韧性（RES）", g.LevelRES + " 级");
            Row(ref l, th, st, "智力（INT）", g.LevelINT + " 级");
            Row(ref l, th, st, "等级总和", GameStats.FormatCount(g.LevelSTR + g.LevelRES + g.LevelINT) + " 级");
            Row(ref l, th, st, "累计获得经验", GameStats.FormatCount((long)System.Math.Round(g.ExpGained)));
            Row(ref l, th, st, "升级次数", GameStats.FormatCount(g.LevelUps) + " 次");

            // 分类明细：按次数从多到少
            DetailSection(ref l, th, st, "使用最多的道具", g.ItemUses, "还没有使用记录");
            DetailSection(ref l, th, st, "制作记录", g.Crafted, "还没有制作记录",
                g.CraftedTotal > 0 ? "共制作 " + GameStats.FormatCount(g.CraftedTotal) + " 次" : null);

            l.Gap(th.Px(14f));
            Rect reset = l.Take(th.SectionHeight(), 0f);
            if (Widgets.Button(reset, "重置全部统计", false, st))
            {
                g.ResetAll();
                s.StatsDirty = true;
                s.Note("统计数据已重置");
            }

            l.Gap(th.Px(8f));
            GUI.Label(l.Take(th.SmallLine() * 2f),
                "<color=" + Theme.HexMuted + ">以上均为累计值，只保存在本机的加密文件中，插件不会联网上传。</color>",
                st.Tiny);

            return Mathf.Max(0f, l.Y - top);
        }

        // ---------------------------------------------------------------

        private static void Section(ref Layout l, Theme th, HudStyles st, string title)
        {
            l.Gap(th.Px(12f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), title, null, st);
            l.Gap(th.Px(3f));
        }

        /// <summary>分类明细（道具 / 制作）：只显示次数最多的前若干条。</summary>
        private const int DetailRows = 8;

        private static void DetailSection(ref Layout l, Theme th, HudStyles st, string title,
            Dictionary<string, long> map, string emptyText, string note = null)
        {
            l.Gap(th.Px(12f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), title, note, st);
            l.Gap(th.Px(3f));

            if (map == null || map.Count == 0)
            {
                GUI.Label(l.Take(th.LabelLine(), th.Px(3f)),
                    "<color=" + Theme.HexMuted + ">" + emptyText + "</color>", st.Body);
                return;
            }

            List<KeyValuePair<string, long>> top = GameStats.TopOf(map, DetailRows);
            for (int i = 0; i < top.Count; i++)
            {
                Row(ref l, th, st, top[i].Key, GameStats.FormatCount(top[i].Value) + " 次");
            }

            if (map.Count > top.Count)
            {
                GUI.Label(l.Take(th.SmallLine(), th.Px(3f)),
                    "<color=" + Theme.HexMuted + ">（仅显示前 " + top.Count + " 项，共 "
                    + map.Count + " 种）</color>", st.Tiny);
            }
        }

        private static void Row(ref Layout l, Theme th, HudStyles st, string label, string value)
        {
            Rect row = l.Take(th.LabelLine(), th.Px(3f));
            float labelW = row.width * 0.54f;

            GUI.Label(new Rect(row.x, row.y, labelW, row.height),
                "<color=" + Theme.HexSecondary + ">" + label + "</color>", st.Body);
            GUI.Label(new Rect(row.x + labelW, row.y, Mathf.Max(10f, row.width - labelW), row.height),
                "<color=" + Theme.HexPrimary + ">" + value + "</color>", st.Body);
        }

        /// <summary>把 ISO 时间戳截成「2026-10-16 21:30」这样的短形式。</summary>
        private static string ShortStamp(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "—";
            if (iso.Length < 16) return iso;

            // yyyy-MM-ddTHH:mm:ssZ → yyyy-MM-dd HH:mm
            return iso.Substring(0, 10) + " " + iso.Substring(11, 5);
        }
    }
}
