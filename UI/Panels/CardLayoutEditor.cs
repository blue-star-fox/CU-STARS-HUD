using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
    internal static class CardLayoutEditor
    {
        
        private const float PaneWidth = 320f;

        private const float HandleWidth = 30f;

        private const float TriggerWidth = 26f;

        private const float ClickSlop = 4f;

        private const float DrawerSpeed = 7f;

        private static Vector2 _pressPosition;
        private static bool _pressed;
        private static int _lastSelected = -1;

        private static float PaneWidthPx(Theme th)
        {
            
            return Mathf.Clamp(th.Px(PaneWidth), 220f, Mathf.Max(220f, Screen.width * 0.34f));
        }

        private static float HandleWidthPx(Theme th)
        {
            return Mathf.Min(th.Px(HandleWidth), PaneWidthPx(th) * 0.5f);
        }

        private static float PaneHeightPx(Theme th)
        {
            
            float hint = th.SmallLine() + th.Px(10f);
            return Mathf.Max(200f, Screen.height - th.Px(14f) * 2f - hint - th.Px(8f));
        }

        public static Rect LeftPaneRect(HudState s, float expand)
        {
            Theme th = s.Theme;
            float width = PaneWidthPx(th);
            float handle = HandleWidthPx(th);
            float x = -(width - handle) * (1f - Mathf.Clamp01(expand));
            return new Rect(x, th.Px(14f), width, PaneHeightPx(th));
        }

        public static Rect RightPaneRect(HudState s, float expand)
        {
            Theme th = s.Theme;
            float width = PaneWidthPx(th);
            float handle = HandleWidthPx(th);
            float x = Screen.width - handle - (width - handle) * Mathf.Clamp01(expand);
            return new Rect(x, th.Px(14f), width, PaneHeightPx(th));
        }

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
            return Easing.Approach(current, target, deltaTime, 0.075f, 0.003f);
        }

        public static void Prepare(HudState s)
        {
            if (s.Theme == null) return;

            FloatingCardHost host = s.FloatingCards;
            host.PaneAreas.Clear();

            Rect left = LeftPaneRect(s, s.LeftPaneExpand);
            Rect right = RightPaneRect(s, s.RightPaneExpand);
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

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

        public static void DrawPanes(HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;
            FloatingCardHost host = s.FloatingCards;

            DrawPropertyPane(s, LeftPaneRect(s, s.LeftPaneExpand), host, th, st);
            DrawLibraryPane(s, RightPaneRect(s, s.RightPaneExpand), host, th, st);

            host.DrawDragGhost(s);
        }

        private static void DrawHandle(HudState s, Rect pane, bool leftSide, string text, Theme th, HudStyles st,
            float expand, bool pinned)
        {
            float handleW = HandleWidthPx(th);
            Rect handle = leftSide
                ? new Rect(pane.x, pane.y, handleW, pane.height)
                : new Rect(pane.xMax - handleW, pane.y, handleW, pane.height);

            float alpha = 1f - Mathf.Clamp01(expand) * 0.45f;
            bool hover = handle.Contains(Event.current.mousePosition);

            if (GUI.Button(handle, GUIContent.none, st.Hit))
            {
                if (leftSide)
                {
                    s.LeftPanePinned = !s.LeftPanePinned;
                    if (!s.LeftPanePinned) s.LeftPaneSuppress = true;   
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
                
                st.DrawBox(handle, hover ? st.CardRaised : st.Panel);
                st.DrawOutline(handle, Theme.WithAlpha(Theme.Accent, hover ? 0.8f : 0.4f));

                float lineH = th.SmallLine();
                float total = lineH * (text.Length + 1);
                float y = handle.y + (handle.height - total) * 0.5f;

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

        private static void DrawPropertyPane(HudState s, Rect pane, FloatingCardHost host, Theme th, HudStyles st)
        {
            float expand = s.LeftPaneExpand;

            DrawHandle(s, pane, true, "组件属性", th, st, expand, s.LeftPanePinned);

            if (expand <= 0.01f) return;   
            if (expand < 0.6f) return;     

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

            bool grid = hasSelection && card.GridSnap;
            bool newGrid = Widgets.Toggle(l.Take(rowH, gap), grid, "按方格摆放（严格遵守网格）", st);
            if (hasSelection && newGrid != grid) host.SetGridSnap(host.Selected, newGrid, s);

            l.Gap(gap);

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

            if (Widgets.Button(l.Take(rowH, gap), "清空全部（" + host.Count + " 张）", false, st))
            {
                host.ClearAll(s);
            }

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

            int quantized = Mathf.Clamp(Mathf.RoundToInt(newGridSize / 4f) * 4, 8, 96);
            GUI.Label(gridValue, quantized + " px", st.ValueRight);
            if (quantized != s.Config.GridSize.Value) s.Config.GridSize.Value = quantized;

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

        private static void DrawLibraryPane(HudState s, Rect pane, FloatingCardHost host, Theme th, HudStyles st)
        {
            float expand = s.RightPaneExpand;

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

            float itemH = LibraryItemHeight(s, th);

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

                FloatingCardHost.DrawPreview(s, kind, key, new Rect(item.x + th.Px(4f), item.y + th.Px(4f),
                    item.width - th.Px(8f), item.height - th.Px(8f)), st);

                if (hover || isDragging)
                {
                    st.DrawOutline(item, isDragging ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.6f));
                }

                HandleLibraryDrag(s, host, kind, key, item);
            }
        }

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

        private static void HandleLibraryDrag(HudState s, FloatingCardHost host, FloatCardKind kind, string key, Rect item)
        {
            Event e = Event.current;
            if (e == null) return;

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
                        
                        Vector2 center = new Vector2((left.xMax + right.x) * 0.5f, Screen.height * 0.42f);
                        host.AddFromLibrary(kind, key, center, s);
                    }
                    else if (!right.Contains(mouse) && !left.Contains(mouse))
                    {
                        host.AddFromLibrary(kind, key, mouse, s);
                    }

                    s.RightPaneSuppress = true;
                    _lastSelected = host.Selected;
                    host.DragSourceKey = null;
                    e.Use();
                    break;
            }
        }

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
