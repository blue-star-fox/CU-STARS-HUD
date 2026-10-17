using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 队友页：每人一张摘要卡片（头像 / 姓名 / 距离 / 分级 / 三项指标 / 摘要 / 血量条）。
    /// **点击整张卡片**会打开该队友的详情窗口，里面是与本地玩家相同的一整套指标
    /// （四个生理分组的全部体征 + 逐条肢体）。
    ///
    /// 联机时来自 KrokoshaCasualtiesMP 的 AllLivingPlayers；
    /// 单机时退化为场景内其它存活 Body（无法区分 NPC，会在名字里标注来源）。
    /// </summary>
    internal static class TeamPanel
    {
        public static float Draw(HudState s, Rect area, float scrollOffset)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;
            BodySnapshot snap = s.Snapshot;

            float gap = th.Px(6f);
            float cardH = th.MateCardHeight();
            float sectionH = th.SectionHeight();

            Layout l = new Layout(area.x, -scrollOffset, area.width);

            if (!snap.Valid)
            {
                EmptyState.Draw(ref l, st, th, snap.Error);
                return l.Y + scrollOffset;
            }

            l.Gap(gap);
            string note = snap.InMultiplayer ? "联机模式" : "单机/未加载联机 MOD";
            Widgets.SectionHeader(l.Take(sectionH), "其他玩家与幸存者",
                snap.Mates.Count + " 位 · " + note, st);
            l.Gap(th.Px(3f));

            if (snap.Mates.Count == 0)
            {
                Rect box = l.Take(th.Px(52f), gap);
                st.DrawBox(box, st.CardSunken);
                GUI.Label(new Rect(box.x + th.Px(12f), box.y, box.width - th.Px(24f), box.height),
                    "<color=" + Theme.HexSecondary + ">未发现队友；联机模式下会显示每名存活玩家。</color>",
                    st.Body);
            }

            for (int i = 0; i < snap.Mates.Count; i++)
            {
                MateReading mate = snap.Mates[i];
                Rect row = l.Take(cardH, th.Px(5f));

                // 头像：拿不到就返回 null，卡片自动不画头像（文字左移占满）
                Texture2D avatar = s.Avatars != null
                    ? s.Avatars.Get(mate.SteamId, mate.Avatar, Mathf.RoundToInt(th.AvatarSize()))
                    : null;

                bool hovered = row.Contains(Event.current.mousePosition);
                Widgets.MateCard(row, mate, avatar, (i & 1) == 1, hovered, st);

                // 整卡热区：点击进入该队友的详情（透明样式，不重复绘制内容）
                if (GUI.Button(row, "mate:" + i + ":" + mate.Name, st.Hit))
                {
                    s.OpenMateDetail(i, mate.Name);
                }
            }

            return l.Y + scrollOffset;
        }
    }
}
