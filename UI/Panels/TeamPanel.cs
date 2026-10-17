using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
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

                Texture2D avatar = s.Avatars != null
                    ? s.Avatars.Get(mate.SteamId, mate.Avatar, Mathf.RoundToInt(th.AvatarSize()))
                    : null;

                bool hovered = row.Contains(Event.current.mousePosition);
                Widgets.MateCard(row, mate, avatar, (i & 1) == 1, hovered, st);

                if (GUI.Button(row, "mate:" + i + ":" + mate.Name, st.Hit))
                {
                    s.OpenMateDetail(i, mate.Name);
                }
            }

            return l.Y + scrollOffset;
        }
    }
}
