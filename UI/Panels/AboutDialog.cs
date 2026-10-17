using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
    internal static class AboutDialog
    {
        
        private const string Purpose =
            "适用于游戏《Casualties Unknown》的角色数据面板插件，同时你也可以查看队友的角色状态数据，同时支持统计游戏游玩数据，如果觉得此插件好用，请给GitHub点个小星星吧！";

        public static void Draw(HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float pad = th.Px(16f);
            float titleH = th.Font(0) * 1.5f;
            float buttonH = th.Px(28f);

            float w = Mathf.Min(Screen.width - th.Px(48f), th.Px(520f));
            float h = Mathf.Min(Screen.height - th.Px(48f), th.Px(400f));
            Rect rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), st.Overlay);

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && !rect.Contains(e.mousePosition))
            {
                s.ShowAbout = false;
                e.Use();
                return;
            }
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                s.ShowAbout = false;
                e.Use();
                return;
            }

            st.DrawPanel(rect);

            GUI.BeginGroup(rect);
            try
            {
                GUI.Label(new Rect(pad, pad, w - pad * 2f, titleH), "关于", st.Title);
                GUI.Label(new Rect(pad, pad, w - pad * 2f, titleH),
                    "<color=" + Theme.HexMuted + ">CU Stats HUD</color>", st.TinyRight);

                float contentTop = pad + titleH + th.Px(8f);
                float footerH = buttonH + th.Px(8f);
                Rect view = new Rect(0f, 0f, w - pad * 2f,
                    Mathf.Max(80f, h - contentTop - footerH - pad));
                Rect viewScreen = new Rect(pad, contentTop, view.width, view.height);

                GUI.BeginGroup(viewScreen);
                try
                {
                    Rect local = new Rect(0f, 0f, view.width, view.height);

                    s.AboutScroll.ContentHeight = MeasureContent(s, st, th, local.width - th.Px(9f));
                    Widgets.ScrollInput(local, s.AboutScroll, st);

                    float contentW = Mathf.Max(80f, local.width - th.Px(9f));
                    Layout l = new Layout(0f, -s.AboutScroll.Offset, contentW);

                    DrawAuthorCard(s, ref l, st, th);
                    DrawPurpose(ref l, st, th, contentW);
                    DrawRepoRow(s, ref l, st, th);

                    Widgets.ScrollBar(local, s.AboutScroll, st);
                }
                finally
                {
                    GUI.EndGroup();
                }

                Rect button = new Rect(w - pad - th.Px(96f), h - pad - buttonH, th.Px(96f), buttonH);
                if (Widgets.Button(button, "关闭", true, st))
                {
                    s.ShowAbout = false;
                }
            }
            catch (Exception ex)
            {
                s.Note("关于窗口绘制异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private static void DrawAuthorCard(HudState s, ref Layout l, HudStyles st, Theme th)
        {
            PluginConfig cfg = s.Config;

            Rect card = l.Take(CardHeight(th), th.Px(12f));
            st.DrawSurface(card, st.CardRaised);

            float pad = th.Px(12f);
            float x = card.x + pad;
            float width = Mathf.Max(40f, card.width - pad * 2f);
            float y = card.y + pad;

            Widgets.Fill(new Rect(card.x + 1f, card.y + pad * 0.6f, Mathf.Max(2f, th.Px(3f)),
                card.height - pad * 1.2f), Theme.WithAlpha(Theme.Accent, 0.9f));

            GUI.Label(new Rect(x, y, width, th.LabelLine() * 1.25f),
                "<color=" + Theme.HexPrimary + ">CU Stats HUD  <size=" + th.Font(2) + ">v"
                + CUStatsHud.Version + "</size></color>", st.Title);
            y += th.LabelLine() * 1.25f + th.Px(2f);

            string author = string.IsNullOrEmpty(cfg.AuthorName.Value) ? "未填写" : cfg.AuthorName.Value;
            string uid = string.IsNullOrEmpty(cfg.AuthorUid.Value) ? "未填写" : cfg.AuthorUid.Value;

            InfoLine(new Rect(x, y, width, th.SmallLine()), "作者", author, st);
            y += th.SmallLine();
            InfoLine(new Rect(x, y, width, th.SmallLine()), "B 站 UID", uid, st);
        }

        private static void DrawPurpose(ref Layout l, HudStyles st, Theme th, float contentW)
        {
            l.Gap(th.Px(4f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), "插件用途", null, st);
            l.Gap(th.Px(3f));

            float height = st.BodyWrap.CalcHeight(new GUIContent(Purpose), contentW);
            GUI.Label(l.Take(height, th.Px(4f)),
                "<color=" + Theme.HexSecondary + ">" + Purpose + "</color>", st.BodyWrap);
        }

        private static void DrawRepoRow(HudState s, ref Layout l, HudStyles st, Theme th)
        {
            string url = s.Config.GitHubUrl.Value;
            if (string.IsNullOrEmpty(url)) url = "https://github.com/blue-star-fox/CU-STARS-HUD";

            l.Gap(th.Px(12f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), "GitHub 仓库", null, st);
            l.Gap(th.Px(4f));

            Rect row = l.Take(th.SectionHeight(), 0f);
            if (Widgets.Button(row, url, false, st))
            {
                try
                {
                    Application.OpenURL(url);
                }
                catch (Exception)
                {
                    s.Note("无法打开浏览器，请手动访问：" + url);
                }
            }
        }

        private static void InfoLine(Rect rect, string label, string value, HudStyles st)
        {
            float labelW = Mathf.Min(rect.width * 0.3f, st.Theme.Px(76f));
            GUI.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                "<color=" + Theme.HexMuted + ">" + label + "</color>", st.Tiny);
            GUI.Label(new Rect(rect.x + labelW, rect.y, Mathf.Max(20f, rect.width - labelW), rect.height),
                "<color=" + Theme.HexSecondary + ">" + value + "</color>", st.Tiny);
        }

        private static float CardHeight(Theme th)
        {
            return th.Px(12f) * 2f + th.LabelLine() * 1.25f + th.Px(2f) + th.SmallLine() * 2f;
        }

        private static float MeasureContent(HudState s, HudStyles st, Theme th, float contentW)
        {
            float total = CardHeight(th) + th.Px(12f);

            total += th.Px(4f) + th.SectionHeight() + th.Px(3f);
            total += st.BodyWrap.CalcHeight(new GUIContent(Purpose), contentW) + th.Px(4f);

            total += th.Px(12f) + th.SectionHeight() + th.Px(4f) + th.SectionHeight();

            return total;
        }
    }
}
