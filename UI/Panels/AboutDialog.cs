using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 关于窗口：作者信息 + 快速上手 + 功能说明 + 数据说明 + 图标许可。
    ///
    /// 设置页只放可操作项，所有解释性文字都集中到这里；内容较多因此**可滚动**。
    /// 内容总高由纯计算测出（不依赖绘制顺序），所以滚动范围不会滞后。
    /// </summary>
    internal static class AboutDialog
    {
        /// <summary>分区标题。</summary>
        private static readonly string[] SectionTitles =
        {
            "快速上手",
            "功能说明",
            "数据说明",
            "图标与许可"
        };

        /// <summary>各分区的正文段落（与 SectionTitles 一一对应）。</summary>
        private static readonly string[][] SectionLines =
        {
            new[]
            {
                "按 [F1] 显示 / 隐藏面板，按 [F2] 显示 / 隐藏组件卡（两个快捷键都能在设置页重新绑定）。",
                "拖动标题栏移动面板，拖右下角调整大小，滚轮滚动内容，点右上角 ✕ 关闭面板。"
            },
            new[]
            {
                "页面：生命体征 / 肢体 / 队友 / 设置 四个标签页。次要指标（心颤、败血、辐射、血胸、内出血等）默认只在异常时出现，可在设置页改为始终显示。",
                "队友：点队友卡片打开详情，里面是与本人相同的全部指标（四组体征 + 逐条肢体）。头像与名字来自联机 MOD 的 Steam 信息，拿不到时只显示名字。",
                "组件卡：设置页点「打开组件卡摆放界面」——两侧是抽屉面板，鼠标靠近屏幕左右边缘自动展开、离开自动收起，把手可点击用于固定展开。从组件库（直接显示卡片真实的样子）拖到屏幕上摆放；左栏可调不透明度、卡片大小、是否按方格摆放（新卡默认开启，网格密度也在左栏），Delete 删除选中，Esc 保存并关闭且不影响游戏状态，「取消」完全还原。摆放期间会阻断游戏输入以免误触。",
                "字体：可在「自动 / 系统 / 游戏」之间切换；游戏字体字形不全时会自动回退到系统字体。",
                "界面风格：可在「CU-UI（小狐狸 UI，深蓝黑 + 蓝紫强调色）」与「游戏风格（灰阶冷调 + 青绿强调色）」之间切换；两者圆角一致，只有配色不同。分级色与波形通道色是功能色，两套风格下含义一致。",
                "emoji 图标：可在设置页关闭，关掉后界面不再显示任何装饰图标，文字会自动左移填补空位；关闭按钮的 ✕ 等功能图标不受影响。",
                "记忆：面板位置 / 尺寸 / 缩放 / 字体 / 组件卡布局都会保存，换分辨率后越界的坐标会自动夹回屏幕内。"
            },
            new[]
            {
                "数据来源：游戏的 Body / Limb / Skills 字段；告警红线取自游戏自身的 isDying 与 isCriticallyDying 判定——面板变红即接近游戏判定中的垂危。",
                "ECG：游戏真实心电曲线（Body.GetECGHeight）。打开面板时会一次性回填过去约 4.6 秒的真实曲线（一屏 5~8 个心跳），不会从空白慢慢长。",
                "血氧：它是「浓度」而不是节律，波形看不出高低，因此用带 90% 危险刻度的浓度条表示。",
                "呼吸波：游戏没有对外暴露呼吸曲线，波形形状由呼吸频率推算（示意波形），旁边的数值是真实读数。",
                "队友距离：世界坐标直线距离，1 Unity 单位 ≈ 1 米。"
            },
            new[]
            {
                "图标来自 Twemoji（CC-BY 4.0，© Twitter/X）；其余界面元素（圆角、描边、胶囊、网格纸、辉光、图标、圆形头像遮罩）均由插件在运行时绘制。"
            }
        };

        public static void Draw(HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float pad = th.Px(16f);
            float titleH = th.Font(0) * 1.5f;
            float buttonH = th.Px(28f);

            float w = Mathf.Min(Screen.width - th.Px(48f), th.Px(560f));
            float h = Mathf.Min(Screen.height - th.Px(48f), th.Px(640f));
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
                float headerY = pad;
                GUI.Label(new Rect(pad, headerY, w - pad * 2f, titleH), "关于", st.Title);
                GUI.Label(new Rect(pad, headerY, w - pad * 2f, titleH),
                    "<color=" + Theme.HexMuted + ">CU Stats HUD</color>", st.TinyRight);

                float contentTop = headerY + titleH + th.Px(8f);
                float footerH = buttonH + th.Px(8f);
                Rect view = new Rect(0f, 0f, w - pad * 2f, Mathf.Max(80f, h - contentTop - footerH - pad));
                Rect viewScreen = new Rect(pad, contentTop, view.width, view.height);

                GUI.BeginGroup(viewScreen);
                try
                {
                    Rect local = new Rect(0f, 0f, view.width, view.height);

                    // 内容总高先算好再交给滚动：不依赖绘制顺序，范围不会滞后
                    s.AboutScroll.ContentHeight = MeasureContent(s, st, th, local.width - th.Px(9f));
                    Widgets.ScrollInput(local, s.AboutScroll, st);

                    float contentW = Mathf.Max(80f, local.width - th.Px(9f));
                    Layout l = new Layout(0f, -s.AboutScroll.Offset, contentW);

                    DrawAuthorCard(s, ref l, st, th, contentW);
                    for (int i = 0; i < SectionTitles.Length; i++)
                    {
                        DrawSection(ref l, st, th, contentW, SectionTitles[i], SectionLines[i]);
                    }

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

        // ---------------------------------------------------------------
        // 内容
        // ---------------------------------------------------------------

        /// <summary>顶部作者卡片：作者信息放在最显眼的位置。</summary>
        private static void DrawAuthorCard(HudState s, ref Layout l, HudStyles st, Theme th, float contentW)
        {
            PluginConfig cfg = s.Config;
            float cardH = CardHeight(th);

            Rect card = l.Take(cardH, th.Px(10f));
            st.DrawSurface(card, st.CardRaised);

            float pad = th.Px(12f);
            float x = card.x + pad;
            float width = Mathf.Max(40f, card.width - pad * 2f);
            float y = card.y + pad;

            // 强调色在左边缘竖条，让作者卡片一眼可辨
            Widgets.Fill(new Rect(card.x + 1f, card.y + pad * 0.6f, Mathf.Max(2f, th.Px(3f)),
                card.height - pad * 1.2f), Theme.WithAlpha(Theme.Accent, 0.9f));

            GUI.Label(new Rect(x, y, width, th.LabelLine() * 1.25f),
                "<color=" + Theme.HexPrimary + ">CU Stats HUD  <size=" + th.Font(2) + ">v"
                + CUStatsHud.Version + "</size></color>", st.Title);
            y += th.LabelLine() * 1.25f + th.Px(2f);

            string author = string.IsNullOrEmpty(cfg.AuthorName.Value) ? "未填写" : cfg.AuthorName.Value;
            string uid = string.IsNullOrEmpty(cfg.AuthorUid.Value) ? "未填写" : cfg.AuthorUid.Value;
            string font = string.IsNullOrEmpty(s.Fonts.ResolvedName) ? "默认字体" : s.Fonts.ResolvedName;

            InfoLine(new Rect(x, y, width, th.SmallLine()), "作者", author + "（B 站）", st);
            y += th.SmallLine();
            InfoLine(new Rect(x, y, width, th.SmallLine()), "B 站 UID", uid, st);
            y += th.SmallLine();
            InfoLine(new Rect(x, y, width, th.SmallLine()), "当前字体", font, st);
        }

        private static void InfoLine(Rect rect, string label, string value, HudStyles st)
        {
            float labelW = Mathf.Min(rect.width * 0.3f, st.Theme.Px(76f));
            GUI.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                "<color=" + Theme.HexMuted + ">" + label + "</color>", st.Tiny);
            GUI.Label(new Rect(rect.x + labelW, rect.y, Mathf.Max(20f, rect.width - labelW), rect.height),
                "<color=" + Theme.HexSecondary + ">" + value + "</color>", st.Tiny);
        }

        private static void DrawSection(ref Layout l, HudStyles st, Theme th, float contentW,
            string title, string[] lines)
        {
            l.Gap(th.Px(12f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), title, null, st);
            l.Gap(th.Px(3f));

            for (int i = 0; i < lines.Length; i++)
            {
                float height = st.BodyWrap.CalcHeight(new GUIContent(lines[i]), contentW);
                GUI.Label(l.Take(height, th.Px(4f)),
                    "<color=" + Theme.HexSecondary + ">" + lines[i] + "</color>", st.BodyWrap);
            }
        }

        // ---------------------------------------------------------------
        // 测量（与绘制走同一份数据，保证滚动范围一致）
        // ---------------------------------------------------------------

        private static float CardHeight(Theme th)
        {
            return th.Px(12f) * 2f + th.LabelLine() * 1.25f + th.Px(2f) + th.SmallLine() * 3f;
        }

        private static float MeasureContent(HudState s, HudStyles st, Theme th, float contentW)
        {
            float total = CardHeight(th) + th.Px(10f);

            for (int i = 0; i < SectionLines.Length; i++)
            {
                total += th.Px(12f) + th.SectionHeight() + th.Px(3f);
                string[] lines = SectionLines[i];
                for (int k = 0; k < lines.Length; k++)
                {
                    total += st.BodyWrap.CalcHeight(new GUIContent(lines[k]), contentW) + th.Px(4f);
                }
            }

            return total;
        }
    }
}
