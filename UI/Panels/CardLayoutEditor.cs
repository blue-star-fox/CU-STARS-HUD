using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    /// <summary>
    /// 组件卡摆放界面。
    ///
    /// 从设置页的「组件卡摆放」按钮进入；打开后主面板让位，屏幕成为画布：
    ///   - **右栏（组件库）**：直接渲染组件卡**真实的样子**（等比预览），拖到屏幕上即在该处生成；
    ///   - **左栏（属性）**：选中组件卡的不透明度、大小、是否按方格摆放，以及删除 / 清空 / 保存 / 取消；
    ///   - **两栏都是抽屉**：平时收在屏幕边只露一条把手，鼠标靠近自动展开、离开自动收起；
    ///     从组件库拖出或选中一张卡后，组件库会自动收起（直到鼠标再靠近屏幕边缘）。
    ///
    /// 未点保存前不会写配置，因此「取消」是完全回滚（含透明度、大小、网格等所有改动）。
    /// </summary>
    internal static class CardLayoutEditor
    {
        /// <summary>栏位完整宽度（未缩放）。</summary>
        private const float PaneWidth = 320f;

        /// <summary>收起时露出的把手宽度（未缩放）。</summary>
        private const float HandleWidth = 30f;

        /// <summary>鼠标靠近屏幕边缘多少像素内展开抽屉（未缩放）。</summary>
        private const float TriggerWidth = 26f;

        /// <summary>点击与拖拽的判定阈值：位移小于它就算“点击”。</summary>
        private const float ClickSlop = 4f;

        /// <summary>抽屉展开/收起速度（1/速度 ≈ 0.14 秒）。</summary>
        private const float DrawerSpeed = 7f;

        private static Vector2 _pressPosition;
        private static bool _pressed;
        private static int _lastSelected = -1;

        // ---------------------------------------------------------------
        // 几何
        // ---------------------------------------------------------------

        private static float PaneWidthPx(Theme th)
        {
            // 组件库要放下卡片预览，所以给得宽一些；小屏上按比例收窄
            return Mathf.Clamp(th.Px(PaneWidth), 220f, Mathf.Max(220f, Screen.width * 0.34f));
        }

        private static float HandleWidthPx(Theme th)
        {
            return Mathf.Min(th.Px(HandleWidth), PaneWidthPx(th) * 0.5f);
        }

        private static float PaneHeightPx(Theme th)
        {
            // 底部要给提示条留位置
            float hint = th.SmallLine() + th.Px(10f);
            return Mathf.Max(200f, Screen.height - th.Px(14f) * 2f - hint - th.Px(8f));
        }

        /// <summary>左栏矩形（按展开度滑入滑出）。</summary>
        public static Rect LeftPaneRect(HudState s, float expand)
        {
            Theme th = s.Theme;
            float width = PaneWidthPx(th);
            float handle = HandleWidthPx(th);
            float x = -(width - handle) * (1f - Mathf.Clamp01(expand));
            return new Rect(x, th.Px(14f), width, PaneHeightPx(th));
        }

        /// <summary>右栏矩形（按展开度滑入滑出）。</summary>
        public static Rect RightPaneRect(HudState s, float expand)
        {
            Theme th = s.Theme;
            float width = PaneWidthPx(th);
            float handle = HandleWidthPx(th);
            float x = Screen.width - handle - (width - handle) * Mathf.Clamp01(expand);
            return new Rect(x, th.Px(14f), width, PaneHeightPx(th));
        }

        /// <summary>抽屉动画：由 Update 阶段推进（每帧一次）。</summary>
        public static void Update(HudState s, float deltaTime)
        {
            FloatingCardHost host = s.FloatingCards;

            if (!host.Editing || s.Theme == null || deltaTime <= 0f)
            {
                s.LeftPaneExpand = 0f;
                s.RightPaneExpand = 0f;
                s.LeftPanePinned = false;
                s.RightPanePinned = false;
                s.LeftPaneSuppress = false;
                s.RightPaneSuppress = false;
                _pressed = false;
                _lastSelected = -1;
                return;
            }

            // Input.mousePosition 原点在左下，转到 IMGUI 的左上原点
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            float trigger = s.Theme.Px(TriggerWidth);

            Rect leftVisible = LeftPaneRect(s, s.LeftPaneExpand);
            Rect rightVisible = RightPaneRect(s, s.RightPaneExpand);

            bool leftTrigger = mouse.x <= trigger;
            if (s.LeftPaneSuppress && !leftTrigger) s.LeftPaneSuppress = false;
            bool leftOpen = s.LeftPanePinned || leftVisible.Contains(mouse) || (leftTrigger && !s.LeftPaneSuppress);
            s.LeftPaneExpand = Advance(s.LeftPaneExpand, deltaTime, leftOpen);

            bool rightTrigger = mouse.x >= Screen.width - trigger;
            if (s.RightPaneSuppress && !rightTrigger) s.RightPaneSuppress = false;
            bool rightOpen = s.RightPanePinned || rightVisible.Contains(mouse) || (rightTrigger && !s.RightPaneSuppress);
            s.RightPaneExpand = Advance(s.RightPaneExpand, deltaTime, rightOpen);

            // 选中画布上的组件卡后，组件库自动收起（此时它的任务已经完成）
            if (host.Selected != _lastSelected)
            {
                _lastSelected = host.Selected;
                if (host.Selected >= 0)
                {
                    s.RightPaneSuppress = true;
                    s.RightPanePinned = false;
                }
            }

            HandleEditorKeys(s, host);
        }

        /// <summary>
        /// 摆放界面的键盘操作：
        ///   Esc        → 保存并关闭（不会丢掉刚摆好的布局；想放弃请点左栏的「取消」）
        ///   Delete/退格 → 删除当前选中的组件卡
        /// </summary>
        private static void HandleEditorKeys(HudState s, FloatingCardHost host)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                host.CommitEditing(s.Config);
                s.Config.FloatCardsEnabled.Value = true;
                s.Note("布局已保存并退出摆放界面");
                return;
            }

            if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) && host.Selected >= 0)
            {
                string removed = host.CardAt(host.Selected).Key;
                host.RemoveAt(host.Selected);
                s.Note("已移除组件卡「" + removed + "」（保存后生效）");
            }
        }

        private static float Advance(float current, float deltaTime, bool shouldOpen)
        {
            float target = shouldOpen ? 1f : 0f;
            float step = deltaTime * DrawerSpeed;
            if (current < target) return Mathf.Min(target, current + step);
            if (current > target) return Mathf.Max(target, current - step);
            return current;
        }

        /// <summary>
        /// 在绘制画布前调用：把可见的栏位登记为“鼠标事件禁区”，
        /// 否则落在栏位下方的组件卡会先抢走点击（IMGUI 先绘制的先响应）。
        /// </summary>
        public static void Prepare(HudState s)
        {
            if (s.Theme == null) return;

            FloatingCardHost host = s.FloatingCards;
            host.PaneAreas.Clear();

            Rect left = LeftPaneRect(s, s.LeftPaneExpand);
            Rect right = RightPaneRect(s, s.RightPaneExpand);
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

            // 只登记屏幕上真正可见的那部分（收起时就是一条把手）
            AddVisible(host, left, screen);
            AddVisible(host, right, screen);

            if (!host.HasDragSource) _pressed = false;
        }

        private static void AddVisible(FloatingCardHost host, Rect pane, Rect screen)
        {
            float xMin = Mathf.Max(pane.xMin, screen.xMin);
            float xMax = Mathf.Min(pane.xMax, screen.xMax);
            if (xMax - xMin < 1f) return;

            host.PaneAreas.Add(new Rect(xMin, pane.yMin, xMax - xMin, pane.height));
        }

        // ---------------------------------------------------------------
        // 绘制
        // ---------------------------------------------------------------

        public static void DrawPanes(HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;
            FloatingCardHost host = s.FloatingCards;

            DrawPropertyPane(s, LeftPaneRect(s, s.LeftPaneExpand), host, th, st);
            DrawLibraryPane(s, RightPaneRect(s, s.RightPaneExpand), host, th, st);

            // 拖拽幽灵跟随鼠标（画在栏位之上，保证看得见）
            host.DrawDragGhost(s);
        }

        /// <summary>
        /// 抽屉把手：收起时露在屏幕边，**始终可见且可点击**——
        /// 点击在「锁定展开」与「跟随鼠标」之间切换。只靠“鼠标靠近才展开”很难被发现，
        /// 所以这里必须留一个明确的点击入口。
        /// </summary>
        private static void DrawHandle(HudState s, Rect pane, bool leftSide, string text, Theme th, HudStyles st,
            float expand, bool pinned)
        {
            float handleW = HandleWidthPx(th);
            Rect handle = leftSide
                ? new Rect(pane.x, pane.y, handleW, pane.height)
                : new Rect(pane.xMax - handleW, pane.y, handleW, pane.height);

            // 展开后把手仍然可见（只是淡一些），否则用户找不到收起入口
            float alpha = 1f - Mathf.Clamp01(expand) * 0.45f;
            bool hover = handle.Contains(Event.current.mousePosition);

            // 整条把手都是按钮
            if (GUI.Button(handle, GUIContent.none, st.Hit))
            {
                if (leftSide)
                {
                    s.LeftPanePinned = !s.LeftPanePinned;
                    if (!s.LeftPanePinned) s.LeftPaneSuppress = true;   // 刚收起时别被边缘触发又弹开
                }
                else
                {
                    s.RightPanePinned = !s.RightPanePinned;
                    if (!s.RightPanePinned) s.RightPaneSuppress = true;
                }

                s.Note(pinned ? "已收起（之后鼠标靠近边缘仍会自动展开）" : "已固定展开（再次点击把手收起）");
            }

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            try
            {
                // 圆角面板底 + 圆角描边，与其它 UI 元素一致（不要用直角色块）
                st.DrawBox(handle, hover ? st.CardRaised : st.Panel);
                st.DrawOutline(handle, Theme.WithAlpha(Theme.Accent, hover ? 0.8f : 0.4f));

                float lineH = th.SmallLine();
                float total = lineH * (text.Length + 1);
                float y = handle.y + (handle.height - total) * 0.5f;

                // 第一行是指示符：固定展开时显示 ●，否则显示可展开/可收起的箭头
                bool expanded = expand > 0.5f;
                string mark = pinned ? "●" : (leftSide ? (expanded ? "◀" : "▶") : (expanded ? "▶" : "◀"));
                GUI.Label(new Rect(handle.x, y, handle.width, lineH),
                    "<color=" + Theme.HexAccent + ">" + mark + "</color>", st.Center);
                y += lineH;

                for (int i = 0; i < text.Length; i++)
                {
                    GUI.Label(new Rect(handle.x, y, handle.width, lineH), text[i].ToString(), st.Center);
                    y += lineH;
                }
            }
            finally
            {
                GUI.color = old;
            }
        }

        // ---------------------------------------------------------------
        // 左栏：选中组件卡的属性
        // ---------------------------------------------------------------

        private static void DrawPropertyPane(HudState s, Rect pane, FloatingCardHost host, Theme th, HudStyles st)
        {
            float expand = s.LeftPaneExpand;

            // 把手始终绘制：它是“怎么展开/收起”的唯一可见入口
            DrawHandle(s, pane, true, "组件属性", th, st, expand, s.LeftPanePinned);

            if (expand <= 0.01f) return;   // 完全收起时只留把手
            if (expand < 0.6f) return;     // 滑入过程未完成时不画内容（避免文字被裁切）

            st.DrawPanel(pane);

            float pad = th.Px(14f);
            float width = Mathf.Max(40f, pane.width - pad * 2f);
            Layout l = new Layout(pane.x + pad, pane.y + pad, width);

            float sectionH = th.SectionHeight();
            float rowH = th.Px(28f);
            float gap = th.Px(6f);

            bool hasSelection = host.Selected >= 0 && host.Selected < host.Count;
            FloatCard card = host.CardAt(host.Selected);

            Widgets.SectionHeader(l.Take(sectionH), "选中组件卡",
                hasSelection ? "第 " + (host.Selected + 1) + " / " + host.Count + " 张" : null, st);
            l.Gap(th.Px(4f));

            GUI.Label(l.Take(th.LabelLine() * 1.2f),
                "<color=" + (hasSelection ? Theme.HexPrimary : Theme.HexMuted) + ">"
                + (hasSelection ? card.Key : "未选中") + "</color>", st.Title);

            GUI.Label(l.Take(th.SmallLine()),
                "<color=" + Theme.HexMuted + ">" + SourceLabel(card.Kind, hasSelection) + "</color>", st.Tiny);

            l.Gap(gap);
            st.DrawHairline(l.Take(1f));
            l.Gap(gap);

            // ---- 不透明度 ----
            GUI.Label(l.Take(th.SmallLine()), "<color=" + Theme.HexMuted + ">不透明度</color>", st.Tiny);
            float opacity = hasSelection ? FloatCardRules.ClampOpacity(card.Opacity) : FloatCardRules.MaxOpacity;
            Rect opacityRow = l.Take(th.Px(20f), gap);
            Rect opacityValue;
            Rect opacitySlider = Layout.SplitColumns(opacityRow, th.Px(46f), out opacityValue);
            float newOpacity = Widgets.Slider(opacitySlider, opacity, FloatCardRules.MinOpacity,
                FloatCardRules.MaxOpacity, st);
            GUI.Label(opacityValue, Mathf.RoundToInt(opacity * 100f) + "%", st.ValueRight);
            if (hasSelection && Mathf.Abs(newOpacity - opacity) > 0.001f) host.SetOpacity(host.Selected, newOpacity);

            l.Gap(gap);

            // ---- 卡片大小 ----
            GUI.Label(l.Take(th.SmallLine()), "<color=" + Theme.HexMuted + ">卡片大小</color>", st.Tiny);
            float scale = hasSelection ? FloatCardRules.ClampScale(card.Scale) : 1f;
            Rect scaleRow = l.Take(th.Px(20f), gap);
            Rect scaleValue;
            Rect scaleSlider = Layout.SplitColumns(scaleRow, th.Px(46f), out scaleValue);
            float newScale = Widgets.Slider(scaleSlider, scale, FloatCardRules.MinScale,
                FloatCardRules.MaxScale, st);
            GUI.Label(scaleValue, Mathf.RoundToInt(scale * 100f) + "%", st.ValueRight);
            if (hasSelection && Mathf.Abs(newScale - scale) > 0.005f) host.SetScale(host.Selected, newScale);

            l.Gap(gap);

            // ---- 按方格摆放 ----
            bool grid = hasSelection && card.GridSnap;
            bool newGrid = Widgets.Toggle(l.Take(rowH, gap), grid, "按方格摆放（严格遵守网格）", st);
            if (hasSelection && newGrid != grid) host.SetGridSnap(host.Selected, newGrid, s);

            l.Gap(gap);

            // ---- 位置（一行显示，小屏/大字号下也不至于把栏撑爆）----
            GUI.Label(l.Take(th.SmallLine()),
                "<color=" + Theme.HexMuted + ">位置 </color><color=" + Theme.HexSecondary + ">"
                + (hasSelection ? Mathf.RoundToInt(card.X) + ", " + Mathf.RoundToInt(card.Y) : "—")
                + "</color>", st.Tiny);

            l.Gap(gap);

            if (Widgets.Button(l.Take(rowH, gap), "删除这张组件卡", false, st) && hasSelection)
            {
                host.RemoveAt(host.Selected);
                s.Note("已移除组件卡（保存后生效）");
            }

            l.Gap(gap);
            st.DrawHairline(l.Take(1f));
            l.Gap(gap);

            // 数量直接写进按钮，省掉一行（属性栏在 720p + 大字号下空间很紧）
            if (Widgets.Button(l.Take(rowH, gap), "清空全部（" + host.Count + " 张）", false, st))
            {
                host.ClearAll(s);
            }

            // ---- 画布设置：网格密度（全局项，与选中哪张卡无关）----
            l.Gap(gap);
            st.DrawHairline(l.Take(1f));
            l.Gap(gap);

            Widgets.SectionHeader(l.Take(sectionH), "画布", null, st);
            l.Gap(th.Px(2f));

            GUI.Label(l.Take(th.SmallLine()), "<color=" + Theme.HexMuted + ">网格密度</color>", st.Tiny);
            int gridSize = Mathf.Clamp(s.Config.GridSize.Value, 8, 96);
            Rect gridRow = l.Take(th.Px(20f), gap);
            Rect gridValue;
            Rect gridSlider = Layout.SplitColumns(gridRow, th.Px(52f), out gridValue);
            float newGridSize = Widgets.Slider(gridSlider, gridSize, 8f, 96f, st);

            // 量化到 4px 步进：ConfigEntry 的 setter 会立刻写盘，拖动时不能每帧写文件
            int quantized = Mathf.Clamp(Mathf.RoundToInt(newGridSize / 4f) * 4, 8, 96);
            GUI.Label(gridValue, quantized + " px", st.ValueRight);
            if (quantized != s.Config.GridSize.Value) s.Config.GridSize.Value = quantized;

            // 保存 / 取消固定在栏底
            float buttonW = (width - gap) * 0.5f;
            Rect saveRect = new Rect(pane.x + pad, pane.yMax - pad - rowH, buttonW, rowH);
            Rect cancelRect = new Rect(pane.x + pad + buttonW + gap, saveRect.y, buttonW, rowH);

            if (Widgets.Button(saveRect, "保存", true, st))
            {
                host.CommitEditing(s.Config);
                s.Config.FloatCardsEnabled.Value = true;
                s.Note("组件卡布局已保存");
            }

            if (Widgets.Button(cancelRect, "取消", false, st))
            {
                host.CancelEditing();
                s.Note("已取消，布局恢复到打开前");
            }
        }

        private static string SourceLabel(FloatCardKind kind, bool hasSelection)
        {
            if (!hasSelection) return "把鼠标移到屏幕边缘可展开组件库";
            switch (kind)
            {
                case FloatCardKind.Vital: return "生命体征";
                case FloatCardKind.Limb: return "肢体";
                default: return "队友";
            }
        }

        // ---------------------------------------------------------------
        // 右栏：组件库（显示组件卡真实的样子）
        // ---------------------------------------------------------------

        private static void DrawLibraryPane(HudState s, Rect pane, FloatingCardHost host, Theme th, HudStyles st)
        {
            float expand = s.RightPaneExpand;

            // 把手始终绘制（与左栏一致）
            DrawHandle(s, pane, false, "组件库", th, st, expand, s.RightPanePinned);

            if (expand <= 0.01f) return;
            if (expand < 0.6f) return;

            st.DrawPanel(pane);

            float pad = th.Px(14f);
            float width = Mathf.Max(40f, pane.width - pad * 2f);

            Widgets.SectionHeader(new Rect(pane.x + pad, pane.y + pad, width, th.SectionHeight()),
                "组件库", "拖到屏幕上摆放", st);

            Rect view = new Rect(pane.x + pad, pane.y + pad + th.SectionHeight() + th.Px(6f),
                width, Mathf.Max(60f, pane.height - pad * 2f - th.SectionHeight() - th.Px(6f)));

            // 内容总高可以直接算出来（条目高度统一），**提前**写入：
            // 否则滚动范围依赖上一帧的高度，滚到底部会因为滞后而抖动/回弹
            s.CardLibraryScroll.ContentHeight = LibraryContentHeight(s, th);

            GUI.BeginGroup(view);
            try
            {
                Rect local = new Rect(0f, 0f, view.width, view.height);
                Widgets.ScrollInput(local, s.CardLibraryScroll, st);

                float contentWidth = Mathf.Max(60f, local.width - th.Px(9f));
                Layout l = new Layout(0f, -s.CardLibraryScroll.Offset, contentWidth);

                DrawLibraryGroup(s, ref l, host, th, st, FloatCardKind.Vital, "生命体征", view.height);
                DrawLibraryGroup(s, ref l, host, th, st, FloatCardKind.Limb, "肢体", view.height);
                DrawLibraryGroup(s, ref l, host, th, st, FloatCardKind.Mate, "队友", view.height);

                Widgets.ScrollBar(local, s.CardLibraryScroll, st);
            }
            catch (Exception ex)
            {
                s.Note("组件库绘制异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        /// <summary>组件库内容总高（与绘制时的排布逐项对应，用于提前确定滚动范围）。</summary>
        private static float LibraryContentHeight(HudState s, Theme th)
        {
            float itemH = LibraryItemHeight(s, th);
            float total = 0f;

            for (int i = 0; i < 3; i++)
            {
                int count = CountSources(s, (FloatCardKind)i);
                total += th.Px(8f) + th.SectionHeight() + th.Px(3f);
                total += count == 0 ? th.CompactRowHeight() : count * (itemH + th.Px(4f));
            }

            return total;
        }

        private static void DrawLibraryGroup(HudState s, ref Layout l, FloatingCardHost host, Theme th,
            HudStyles st, FloatCardKind kind, string title, float viewHeight)
        {
            int count = CountSources(s, kind);

            l.Gap(th.Px(8f));
            Widgets.SectionHeader(l.Take(th.SectionHeight()), title, count + " 项", st);
            l.Gap(th.Px(3f));

            if (count == 0)
            {
                GUI.Label(l.Take(th.CompactRowHeight()), "<color=" + Theme.HexMuted + ">（暂无）</color>", st.Tiny);
                return;
            }

            // 条目高度按最高的一种卡片动态预留：字号大的档位下肢体/队友卡更高，
            // 写死高度会让预览被裁切（排版校验脚本会抓这种问题）。
            float itemH = LibraryItemHeight(s, th);

            // 视口剔除：组件卡预览是完整重绘，整个列表都画会白白浪费。
            // 判断走 FloatCardRules.VisibleInViewport（组内坐标，有单元测试守着）。
            float margin = th.Px(48f);

            for (int i = 0; i < count; i++)
            {
                string key = SourceKeyAt(s, kind, i);
                if (string.IsNullOrEmpty(key)) continue;

                Rect item = l.Take(itemH, th.Px(4f));
                if (!FloatCardRules.VisibleInViewport(item.y, item.yMax, viewHeight, margin)) continue;

                bool isDragging = host.HasDragSource && host.DragSourceKind == kind
                    && string.Equals(host.DragSourceKey, key, StringComparison.Ordinal);
                bool hover = item.Contains(Event.current.mousePosition);

                st.DrawSurface(item, isDragging || hover ? st.CardRaised : st.CardSunken);

                // 真实卡片的样子（等比缩放预览），不再用文字代替
                FloatingCardHost.DrawPreview(s, kind, key, new Rect(item.x + th.Px(4f), item.y + th.Px(4f),
                    item.width - th.Px(8f), item.height - th.Px(8f)), st);

                if (hover || isDragging)
                {
                    st.DrawOutline(item, isDragging ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.6f));
                }

                HandleLibraryDrag(s, host, kind, key, item);
            }
        }

        /// <summary>
        /// 组件库单个条目的高度：取三种卡片等比缩放后的最大高度 + 内边距。
        /// 必须动态算——卡片高度随字号增长，而栏宽随缩放变化，写死必然在某个档位裁切预览。
        /// </summary>
        private static float LibraryItemHeight(HudState s, Theme th)
        {
            float paneWidth = PaneWidthPx(th);
            float itemWidth = Mathf.Max(40f, paneWidth - th.Px(14f) * 2f - th.Px(9f));

            float maxHeight = 0f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 natural = FloatingCardHost.BaseSizeOf((FloatCardKind)i, s);
                if (natural.x < 1f || natural.y < 1f) continue;

                float scale = Mathf.Min(itemWidth / natural.x, 1f);
                maxHeight = Mathf.Max(maxHeight, natural.y * scale);
            }

            return Mathf.Max(th.Px(56f), maxHeight + th.Px(8f));
        }

        /// <summary>组件库条目的拖拽：拖出栏位即生成组件卡；位移很小则视为点击，放到画布中央。</summary>
        private static void HandleLibraryDrag(HudState s, FloatingCardHost host, FloatCardKind kind, string key, Rect item)
        {
            Event e = Event.current;
            if (e == null) return;

            // 同样不用 GUIUtility.hotControl：列表会做视口剔除，可见条目数一变
            // GetControlID 分配到的 id 就变，拖动会被中途打断。这里用 _pressed 自己维护。
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (item.Contains(e.mousePosition))
                    {
                        host.DragSourceKind = kind;
                        host.DragSourceKey = key;
                        host.DragPoint = e.mousePosition;
                        _pressPosition = e.mousePosition;
                        _pressed = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_pressed && host.HasDragSource)
                    {
                        host.DragPoint = e.mousePosition;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (!_pressed) break;

                    _pressed = false;

                    Vector2 mouse = e.mousePosition;
                    bool clicked = (mouse - _pressPosition).sqrMagnitude <= ClickSlop * ClickSlop;
                    Rect right = RightPaneRect(s, s.RightPaneExpand);
                    Rect left = LeftPaneRect(s, s.LeftPaneExpand);

                    if (clicked)
                    {
                        // 点击 = 放到画布中央（两栏之间）
                        Vector2 center = new Vector2((left.xMax + right.x) * 0.5f, Screen.height * 0.42f);
                        host.AddFromLibrary(kind, key, center, s);
                    }
                    else if (!right.Contains(mouse) && !left.Contains(mouse))
                    {
                        host.AddFromLibrary(kind, key, mouse, s);
                    }

                    // 选好一张就收起组件库（鼠标离开屏幕边缘前不再自动展开）
                    s.RightPaneSuppress = true;
                    _lastSelected = host.Selected;
                    host.DragSourceKey = null;
                    e.Use();
                    break;
            }
        }

        // ---------------------------------------------------------------
        // 组件库数据源（直接取自当前快照，因此列出的都是真实存在的条目）
        // ---------------------------------------------------------------

        private static int CountSources(HudState s, FloatCardKind kind)
        {
            BodySnapshot snap = s.Snapshot;
            if (snap == null || !snap.Valid) return 0;

            switch (kind)
            {
                case FloatCardKind.Vital: return snap.Vitals.Count;
                case FloatCardKind.Limb: return snap.Limbs.Count;
                default: return snap.Mates.Count;
            }
        }

        private static string SourceKeyAt(HudState s, FloatCardKind kind, int index)
        {
            BodySnapshot snap = s.Snapshot;
            if (snap == null || !snap.Valid) return null;

            switch (kind)
            {
                case FloatCardKind.Vital:
                    return index < snap.Vitals.Count ? snap.Vitals[index].Label : null;
                case FloatCardKind.Limb:
                    return index < snap.Limbs.Count ? snap.Limbs[index].Name : null;
                default:
                    return index < snap.Mates.Count ? snap.Mates[index].Name : null;
            }
        }
    }
}
