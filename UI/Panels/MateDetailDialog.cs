using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 队友详情窗口（模态）：
    /// 顶部是身份（圆形头像 / 名字 / 距离）+ 当前状态条，下面是该队友的**完整**状态——
    /// 与本地玩家页面复用同一套渲染代码，所以四个生理分组的全部体征、
    /// 逐条肢体的皮肤/肌肉/疼痛/感染/出血/弹片等内容项完全一致。
    ///
    /// 交互：右上角 ✕、点击遮罩、Esc 关闭；内容可滚轮滚动。
    /// </summary>
    internal static class MateDetailDialog
    {
        private static string MutedHex { get { return Theme.HexMuted; } }

        public static void Draw(HudState s)
        {
            MateReading? maybeMate = s.CurrentMateDetail();
            if (maybeMate == null) return;

            MateReading mate = maybeMate.Value;
            BodySnapshot detail = mate.Detail;

            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float margin = th.Px(20f);
            float width = Mathf.Min(Screen.width - margin * 2f, th.Px(620f));
            float height = Mathf.Min(Screen.height - margin * 2f, th.Px(660f));
            Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), st.Overlay);

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && !rect.Contains(e.mousePosition))
            {
                s.CloseMateDetail();
                e.Use();
                return;
            }
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                s.CloseMateDetail();
                e.Use();
                return;
            }

            st.DrawPanel(rect);

            GUI.BeginGroup(rect);
            try
            {
                float pad = th.Px(Theme.PadSize);
                float innerWidth = Mathf.Max(40f, rect.width - pad * 2f);

                float headerH = DrawHeader(s, mate, new Rect(pad, pad, innerWidth,
                    Mathf.Max(th.AvatarSize(), th.LabelLine() + th.SmallLine())), st, th);

                // 状态条：与主面板同一套三态配色/闪烁
                float bannerH = th.AlarmHeight();
                Rect banner = new Rect(pad, pad + headerH + th.Px(6f), innerWidth, bannerH);
                string statusText = detail != null && !string.IsNullOrEmpty(detail.AlertText)
                    ? detail.AlertText
                    : (string.IsNullOrEmpty(mate.Summary) ? "等待数据" : mate.Summary);
                Texture2D alarmIcon = mate.Severity == Severity.Critical && s.Icons != null
                    ? s.Icons.GetEmoji("warning")
                    : null;
                Widgets.StatusBanner(banner, mate.Severity, statusText,
                    s.Config.AlertFlash.Value, s.Pulse, st, alarmIcon);

                float contentTop = banner.yMax + th.Px(8f);
                st.DrawHairline(new Rect(pad, contentTop, innerWidth, 1f));
                contentTop += th.Px(4f);

                float viewHeight = Mathf.Max(40f, rect.height - contentTop - pad);
                DrawContent(s, detail, new Rect(pad, contentTop, innerWidth, viewHeight), st, th);
            }
            catch (Exception ex)
            {
                s.Note("队友详情绘制异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        /// <summary>身份头部：头像、名字、距离、关闭按钮。返回头部高度。</summary>
        private static float DrawHeader(HudState s, MateReading mate, Rect rect, HudStyles st, Theme th)
        {
            float avatarSize = th.AvatarSize();
            float textX = rect.x;

            Texture2D avatar = s.Avatars != null
                ? s.Avatars.Get(mate.SteamId, mate.Avatar, Mathf.RoundToInt(avatarSize))
                : null;
            if (avatar != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y, avatarSize, avatarSize), avatar, ScaleMode.ScaleToFit, true);
                textX += avatarSize + th.Px(10f);
            }

            float closeSize = th.CloseButtonSize();
            Rect closeRect = new Rect(rect.xMax - closeSize, rect.y, closeSize, closeSize);

            float line1 = th.LabelLine();
            float line2 = th.SmallLine();
            float textWidth = Mathf.Max(20f, closeRect.x - textX - th.Px(8f));

            // 头部只放身份信息；体征数值一律在下面的卡片里，避免重复
            GUI.Label(new Rect(textX, rect.y, textWidth, line1), mate.Name, st.Title);

            string meta = "距离 " + (mate.Distance >= 0f ? VitalRules.Num1(mate.Distance) + " m" : "未知");
            if (mate.SteamId != 0UL) meta += " · Steam 玩家";
            GUI.Label(new Rect(textX, rect.y + line1, textWidth, line2),
                "<color=" + MutedHex + ">" + meta + "</color>", st.Tiny);

            if (Widgets.CloseButton(closeRect, st, s.Icons))
            {
                s.CloseMateDetail();
            }

            return Mathf.Max(avatarSize, line1 + line2);
        }

        /// <summary>内容区：体征 + 肢体，复用主面板的同一套渲染方法。</summary>
        private static void DrawContent(HudState s, BodySnapshot detail, Rect area, HudStyles st, Theme th)
        {
            GUI.BeginGroup(area);
            try
            {
                Rect view = new Rect(0f, 0f, area.width, area.height);
                Widgets.ScrollInput(view, s.MateDetailScroll, st);

                float contentWidth = Mathf.Max(80f, view.width - th.Px(9f));
                Rect panelArea = new Rect(0f, 0f, contentWidth, 0f);
                float offset = s.MateDetailScroll.Offset;

                if (detail == null || !detail.Valid)
                {
                    Layout empty = new Layout(0f, -offset, contentWidth);
                    EmptyState.Draw(ref empty, st, th, detail != null ? detail.Error : "未读取到该玩家的数据");
                    s.MateDetailScroll.ContentHeight = empty.Y + offset;
                    Widgets.ScrollBar(view, s.MateDetailScroll, st);
                    return;
                }

                // 详情页没有波形区常显，因此 channelsCovered 传 false：列出全部指标
                float afterVitals = VitalsPanel.Draw(s, detail, panelArea, th.Px(2f), offset, false);
                float total = LimbsPanel.Draw(s, detail, panelArea, afterVitals + th.Px(12f), offset);

                s.MateDetailScroll.ContentHeight = total;
                Widgets.ScrollBar(view, s.MateDetailScroll, st);
            }
            catch (Exception ex)
            {
                s.Note("队友详情内容异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }
        }
    }
}
