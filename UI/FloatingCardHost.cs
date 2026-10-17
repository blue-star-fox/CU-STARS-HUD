using System;
using System.Collections.Generic;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 浮动组件卡的运行时宿主。
    ///
    /// 交互模型：**不占用任何游戏按键**。摆放通过设置页的「组件卡摆放」界面进行——
    /// 该界面打开时屏幕变成画布（方格 + 边界），右栏组件库直接显示**组件卡真实的样子**，
    /// 拖到屏幕上即生成；左栏调整该卡的不透明度、大小、是否按方格摆放；
    /// 「保存」写盘，「取消」完全还原。
    ///
    /// 显示开关：由快捷键（Keys/FloatToggle）或设置页开关控制，与摆放界面无关。
    ///
    /// 坐标与边界：位置以屏幕坐标保存，每帧夹进屏幕内（含换分辨率后自我修正）；
    /// 未开网格时做屏幕边缘吸附，开启「按方格摆放」后位置严格遵守网格。
    /// </summary>
    internal sealed class FloatingCardHost
    {
        private readonly List<FloatCard> _cards = new List<FloatCard>(FloatCardRules.MaxCards);
        private readonly List<Rect> _rects = new List<Rect>(FloatCardRules.MaxCards);
        /// <summary>进入摆放界面时的布局快照，「取消」用它还原。</summary>
        private readonly List<FloatCard> _backup = new List<FloatCard>(FloatCardRules.MaxCards);

        private bool _loaded;
        private int _dragIndex = -1;
        private Vector2 _dragOffset;
        /// <summary>是否正在拖动某张组件卡（自己维护，不用 hotControl，见 DrawOne 的说明）。</summary>
        private bool _dragging;
        private int _hoverIndex = -1;
        private bool _dirty;
        private float _dirtyAt;

        // ---------- 摆放界面会话 ----------
        /// <summary>摆放界面是否打开。</summary>
        public bool Editing;
        /// <summary>左侧属性栏当前选中的组件卡；-1 表示未选中。</summary>
        public int Selected = -1;
        /// <summary>正在从组件库拖出的源（<see cref="DragSourceKey"/> 为空表示没有）。</summary>
        public string DragSourceKey;
        public FloatCardKind DragSourceKind;
        public Vector2 DragPoint;

        /// <summary>
        /// 摆放界面左右栏的矩形。落在其中的鼠标事件不交给画布上的组件卡——
        /// IMGUI 是先绘制的先响应，若不作禁区，栏位下方的组件卡会抢走栏上的点击。
        /// </summary>
        public readonly List<Rect> PaneAreas = new List<Rect>(2);

        public int Count
        {
            get { return _cards.Count; }
        }

        public FloatCard CardAt(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index] : default(FloatCard);
        }

        public bool HasDragSource
        {
            get { return !string.IsNullOrEmpty(DragSourceKey); }
        }

        /// <summary>摆放方格的边长（像素）。由设置里的「网格密度」决定。</summary>
        public static float GridCell(HudState s)
        {
            return Mathf.Clamp(s.Config.GridSize.Value, 6f, 200f);
        }

        // ---------------------------------------------------------------
        // 生命周期
        // ---------------------------------------------------------------

        /// <summary>从配置载入（只做一次）。</summary>
        public void EnsureLoaded(PluginConfig config)
        {
            if (_loaded) return;
            _loaded = true;
            FloatCardRules.Deserialize(config.FloatCards.Value, _cards);
        }

        /// <summary>
        /// 重算所有组件卡的屏幕矩形。每帧绘制前调用一次，
        /// 保证 Layout 与 Repaint 两次绘制用的是同一套矩形（否则拾取与视觉会错位）。
        /// </summary>
        public void SyncRects(HudState s)
        {
            _rects.Clear();
            if (s.Theme == null) return;

            for (int i = 0; i < _cards.Count; i++)
            {
                FloatCard card = _cards[i];
                Vector2 size = SizeOf(card, s);
                float x = FloatCardRules.ClampAxis(card.X, size.x, Screen.width);
                float y = FloatCardRules.ClampAxis(card.Y, size.y, Screen.height);
                _rects.Add(new Rect(x, y, size.x, size.y));

                // 分辨率变小等情况下坐标会被夹回来，这里写回，避免“越拖越偏”
                if (Mathf.Abs(x - card.X) > 0.5f || Mathf.Abs(y - card.Y) > 0.5f)
                {
                    card.X = x;
                    card.Y = y;
                    _cards[i] = card;
                    MarkDirty(Time.unscaledTime);
                }
            }
        }

        /// <summary>落盘（延时）。摆放界面未点保存前一律不落盘。</summary>
        public void Flush(PluginConfig config, float now, float delay)
        {
            if (Editing) return;   // 未保存前不写配置，保证「取消」是彻底回滚
            if (!_dirty || now - _dirtyAt < delay) return;
            SaveNow(config);
        }

        public void SaveNow(PluginConfig config)
        {
            config.FloatCards.Value = FloatCardRules.Serialize(_cards);
            _dirty = false;
        }

        public void MarkDirty(float now)
        {
            _dirty = true;
            _dirtyAt = now;
        }

        public void Dispose(PluginConfig config)
        {
            if (config != null && _dirty) SaveNow(config);
            _dragIndex = -1;
            Editing = false;
        }

        // ---------------------------------------------------------------
        // 摆放界面会话
        // ---------------------------------------------------------------

        public void BeginEditing()
        {
            if (Editing) return;
            Editing = true;
            Selected = -1;
            DragSourceKey = null;
            _dragIndex = -1;
            _backup.Clear();
            _backup.AddRange(_cards);
        }

        /// <summary>保存并退出摆放界面。</summary>
        public void CommitEditing(PluginConfig config)
        {
            if (!Editing) return;
            Editing = false;
            Selected = -1;
            DragSourceKey = null;
            _dragIndex = -1;
            _backup.Clear();
            SaveNow(config);
        }

        /// <summary>取消并还原到进入摆放界面时的布局。</summary>
        public void CancelEditing()
        {
            if (!Editing) return;
            Editing = false;
            Selected = -1;
            DragSourceKey = null;
            _dragIndex = -1;

            _cards.Clear();
            _cards.AddRange(_backup);
            _backup.Clear();
            _rects.Clear();
        }

        // ---------------------------------------------------------------
        // 增删改
        // ---------------------------------------------------------------

        /// <summary>从组件库拖出：<paramref name="dropPoint"/> 为松手位置（作为卡片中心）。</summary>
        public void AddFromLibrary(FloatCardKind kind, string key, Vector2 dropPoint, HudState s)
        {
            if (string.IsNullOrEmpty(key)) return;

            FloatCard fresh;
            fresh.Kind = kind;
            fresh.Key = key;
            fresh.X = 0f;
            fresh.Y = 0f;
            fresh.Opacity = FloatCardRules.MaxOpacity;
            fresh.Scale = 1f;
            fresh.GridSnap = true;      // 新生成的组件卡默认按方格摆放

            Vector2 size = SizeOf(fresh, s);
            float snap = s.Theme.Px(FloatCardRules.SnapDistance);
            float cell = GridCell(s);

            float dropX;
            float dropY;
            FloatCardRules.DropAt(dropPoint.x, dropPoint.y, size.x, size.y, Screen.width, Screen.height, snap,
                out dropX, out dropY);

            // 默认吸网格，所以落点也直接对齐到网格
            float x = FloatCardRules.PlaceAxis(dropX, size.x, Screen.width, snap, cell);
            float y = FloatCardRules.PlaceAxis(dropY, size.y, Screen.height, snap, cell);

            // 同一来源只保留一张：已存在就挪到这次放下的位置
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Kind != kind || !string.Equals(_cards[i].Key, key, StringComparison.Ordinal)) continue;

                FloatCard existing = _cards[i];
                Vector2 existingSize = SizeOf(existing, s);
                float grid = existing.GridSnap ? cell : 0f;
                existing.X = FloatCardRules.PlaceAxis(x, existingSize.x, Screen.width, snap, grid);
                existing.Y = FloatCardRules.PlaceAxis(y, existingSize.y, Screen.height, snap, grid);
                _cards[i] = existing;
                Selected = i;
                MarkDirty(Time.unscaledTime);
                s.Note("这张组件卡已在屏幕上，已移动到放下的位置");
                return;
            }

            if (_cards.Count >= FloatCardRules.MaxCards)
            {
                s.Note("组件卡已达上限 " + FloatCardRules.MaxCards + " 张");
                return;
            }

            fresh.X = x;
            fresh.Y = y;
            _cards.Add(fresh);
            Selected = _cards.Count - 1;
            MarkDirty(Time.unscaledTime);
        }

        public void SetOpacity(int index, float value)
        {
            if (index < 0 || index >= _cards.Count) return;

            FloatCard card = _cards[index];
            card.Opacity = FloatCardRules.ClampOpacity(value);
            _cards[index] = card;
            MarkDirty(Time.unscaledTime);
        }

        /// <summary>调整该组件卡的尺寸倍率（内容一起等比缩放）。</summary>
        public void SetScale(int index, float value)
        {
            if (index < 0 || index >= _cards.Count) return;

            FloatCard card = _cards[index];
            card.Scale = FloatCardRules.ClampScale(value);
            _cards[index] = card;
            _rects.Clear();   // 尺寸变了，矩形需要重算
            MarkDirty(Time.unscaledTime);
        }

        /// <summary>切换「按方格摆放」；开启时立刻把当前位置对齐到最近的格点。</summary>
        public void SetGridSnap(int index, bool value, HudState s)
        {
            if (index < 0 || index >= _cards.Count) return;

            FloatCard card = _cards[index];
            card.GridSnap = value;

            if (value && s.Theme != null)
            {
                Vector2 size = SizeOf(card, s);
                float cell = GridCell(s);
                card.X = FloatCardRules.PlaceAxis(card.X, size.x, Screen.width, 0f, cell);
                card.Y = FloatCardRules.PlaceAxis(card.Y, size.y, Screen.height, 0f, cell);
            }

            _cards[index] = card;
            _rects.Clear();
            MarkDirty(Time.unscaledTime);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _cards.Count) return;

            _cards.RemoveAt(index);
            _rects.Clear();
            if (Selected == index) Selected = -1;
            else if (Selected > index) Selected--;

            MarkDirty(Time.unscaledTime);
        }

        public void ClearAll(HudState s)
        {
            if (_cards.Count == 0) return;
            _cards.Clear();
            _rects.Clear();
            Selected = -1;
            MarkDirty(Time.unscaledTime);
            s.Note("已清空全部组件卡");
        }

        // ---------------------------------------------------------------
        // 尺寸与矩形
        // ---------------------------------------------------------------

        /// <summary>该卡片的自然尺寸（不含用户的尺寸倍率）。</summary>
        public static Vector2 BaseSizeOf(FloatCardKind kind, HudState s)
        {
            Theme th = s.Theme;
            float content = Mathf.Max(160f, s.Window.width - th.Px(Theme.PadSize) * 2f - th.Px(9f));

            switch (kind)
            {
                case FloatCardKind.Vital:
                    return new Vector2(Mathf.Max(140f, (content - th.Px(8f)) * 0.5f), th.VitalCardHeight());
                case FloatCardKind.Limb:
                    return new Vector2(content, th.LimbRowHeight());
                default:
                    return new Vector2(content, th.MateCardHeight());
            }
        }

        /// <summary>该卡片的实际尺寸 = 自然尺寸 × 自己的倍率。</summary>
        public static Vector2 SizeOf(FloatCard card, HudState s)
        {
            Vector2 natural = BaseSizeOf(card.Kind, s);
            float scale = FloatCardRules.ClampScale(card.Scale);
            return new Vector2(natural.x * scale, natural.y * scale);
        }

        /// <summary>某个屏幕坐标是否落在任一组件卡上。</summary>
        public bool Covers(Vector2 point)
        {
            for (int i = 0; i < _rects.Count; i++)
            {
                if (_rects[i].Contains(point)) return true;
            }
            return false;
        }

        public Rect RectAt(int index)
        {
            return index >= 0 && index < _rects.Count ? _rects[index] : new Rect(0f, 0f, 0f, 0f);
        }

        /// <summary>该点是否落在摆放界面的栏位上。</summary>
        public bool InPane(Vector2 point)
        {
            for (int i = 0; i < PaneAreas.Count; i++)
            {
                if (PaneAreas[i].Contains(point)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // 绘制
        // ---------------------------------------------------------------

        /// <summary>绘制全部组件卡；<paramref name="interactive"/> = 摆放界面打开中（可拖动/可选中）。</summary>
        public void Draw(HudState s, bool interactive)
        {
            if (s.Theme == null || s.Styles == null) return;
            if (_cards.Count == 0) return;

            // 未加载存档 / 暂停界面：不绘制也不响应鼠标
            // （摆放界面里始终可见，否则没法编辑）
            float visibility = interactive ? 1f : s.GamePausedVisibility;
            if (visibility <= 0.01f) return;

            if (_rects.Count != _cards.Count) SyncRects(s);

            BodySnapshot snap = s.Snapshot;
            Event e = Event.current;
            Vector2 mouse = e != null ? e.mousePosition : new Vector2(-1f, -1f);

            // 鼠标抬起统一收尾：拖动可能结束在任何位置（甚至卡片之外）
            if (e != null && e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
                _dragIndex = -1;
                MarkDirty(Time.unscaledTime);
            }

            _hoverIndex = -1;
            if (interactive)
            {
                for (int i = 0; i < _rects.Count; i++)
                {
                    if (!_rects[i].Contains(mouse)) continue;
                    if (InPane(mouse)) continue;
                    _hoverIndex = i;   // 后画的在上层，取最后命中的
                }
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                if (interactive && i == _dragIndex) continue;
                DrawOne(s, snap, i, interactive, mouse, visibility);
            }

            // 正在拖动的卡片最后画，保证压在最上层
            if (interactive && _dragIndex >= 0 && _dragIndex < _cards.Count)
            {
                DrawOne(s, snap, _dragIndex, interactive, mouse, visibility);
            }
        }

        private void DrawOne(HudState s, BodySnapshot snap, int index, bool interactive, Vector2 mouse,
            float visibility)
        {
            FloatCard card = _cards[index];
            Rect rect = _rects[index];
            bool hovered = interactive && _hoverIndex == index;
            bool selected = interactive && Selected == index;
            HudStyles st = s.Styles;
            Theme th = s.Theme;

            // 组件卡自己的透明度与尺寸：整卡（底纹、色块、图标、文字）一起缩放与淡出
            float alpha = Mathf.Clamp01(FloatCardRules.ClampOpacity(card.Opacity) * visibility);
            DrawCardVisual(s, snap, card, rect, alpha, st);

            if (!interactive) return;

            if (selected || hovered)
            {
                st.DrawOutline(rect, selected ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.55f));
            }

            // 摆放界面里每张卡右上角都有 ✕（左栏也有删除按钮）
            float closeSize = th.CloseButtonSize();
            Rect closeRect = new Rect(rect.xMax - closeSize - th.Px(3f), rect.y + th.Px(3f), closeSize, closeSize);

            if (Widgets.CloseButton(closeRect, st, s.Icons))
            {
                RemoveAt(index);
                s.Note("已移除组件卡");
                return;
            }

            Event e = Event.current;
            if (e == null) return;

            // 拖动状态完全由自己的标志维护，**不用 GUIUtility.hotControl**：
            // 拖动中的卡片会被移到最后绘制，绘制顺序一变、GetControlID 分配到的 id 就变了，
            // 于是 hotControl == id 的校验必然失败——表现就是"放了第二张之后第一张拖不动"。
            // 鼠标抬起在 Draw 里统一收尾（无论在哪张卡上松开）。
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (InPane(e.mousePosition)) break;
                    if (rect.Contains(e.mousePosition) && !closeRect.Contains(e.mousePosition))
                    {
                        Selected = index;                       // 点一下即选中（左栏显示属性）
                        _dragIndex = index;
                        _dragOffset = e.mousePosition - new Vector2(rect.x, rect.y);
                        _dragging = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_dragging && _dragIndex == index)
                    {
                        MoveTo(index, e.mousePosition - _dragOffset, s);
                        e.Use();
                    }
                    break;
            }
        }

        private void MoveTo(int index, Vector2 topLeft, HudState s)
        {
            if (index < 0 || index >= _cards.Count || index >= _rects.Count) return;

            Theme th = s.Theme;
            Rect rect = _rects[index];
            FloatCard card = _cards[index];

            // 按方格摆放时严格遵守网格（不做边缘吸附）；否则夹进屏幕并吸附边缘
            float grid = card.GridSnap ? GridCell(s) : 0f;
            float snap = th.Px(FloatCardRules.SnapDistance);

            card.X = FloatCardRules.PlaceAxis(topLeft.x, rect.width, Screen.width, snap, grid);
            card.Y = FloatCardRules.PlaceAxis(topLeft.y, rect.height, Screen.height, snap, grid);
            _cards[index] = card;
            MarkDirty(Time.unscaledTime);
        }

        /// <summary>
        /// 按自己的倍率绘制一张组件卡：内容用主面板的同一套渲染，
        /// 因此组件卡与原件完全一致（含数据实时性）。
        ///
        /// 缩放通过 <see cref="Theme.UiScale"/> 完成（所有尺寸入口整体缩放），
        /// **不使用 GUI.matrix** —— 矩阵缩放与滚动区的 BeginGroup 裁剪会互相干扰，
        /// 表现为内容整个画不出来、描选框与卡片错位。这样绘制坐标始终是真实屏幕坐标。
        /// </summary>
        private static void DrawCardVisual(HudState s, BodySnapshot snap, FloatCard card, Rect rect,
            float alpha, HudStyles st)
        {
            Theme th = s.Theme;
            Vector2 natural = BaseSizeOf(card.Kind, s);
            float scale = natural.x > 1f ? rect.width / natural.x : 1f;

            Color oldColor = GUI.color;
            float oldUiScale = th.UiScale;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            bool hasData;
            try
            {
                th.UiScale = scale;
                hasData = DrawContent(s, snap, card, new Rect(rect.x, rect.y, natural.x * scale, natural.y * scale), st);
            }
            finally
            {
                th.UiScale = oldUiScale;
                GUI.color = oldColor;
            }

            if (!hasData)
            {
                DrawPlaceholder(rect, card.Key, st);
            }
        }

        /// <summary>
        /// 组件库里的预览：显示组件卡**真实的样子**（等比缩放，最多到自然尺寸，不放大）。
        /// 同样走 <see cref="Theme.UiScale"/>，在滚动区的 group 内可以正常工作。
        /// </summary>
        public static void DrawPreview(HudState s, FloatCardKind kind, string key, Rect area, HudStyles st)
        {
            if (s.Theme == null || area.width < 8f || area.height < 8f) return;

            Theme th = s.Theme;
            Vector2 natural = BaseSizeOf(kind, s);
            if (natural.x < 1f || natural.y < 1f) return;

            float scale = Mathf.Min(area.width / natural.x, area.height / natural.y);
            if (scale > 1f) scale = 1f;
            if (scale < 0.05f) return;

            float width = natural.x * scale;
            float height = natural.y * scale;
            Rect target = new Rect(area.x + (area.width - width) * 0.5f,
                area.y + (area.height - height) * 0.5f, width, height);

            FloatCard probe;
            probe.Kind = kind;
            probe.Key = key;
            probe.X = 0f;
            probe.Y = 0f;
            probe.Opacity = 1f;
            probe.Scale = 1f;
            probe.GridSnap = false;

            float oldUiScale = th.UiScale;
            try
            {
                th.UiScale = scale;
                if (!DrawContent(s, s.Snapshot, probe, target, st))
                {
                    DrawPlaceholder(target, key, st);
                }
            }
            finally
            {
                th.UiScale = oldUiScale;
            }
        }

        /// <summary>拖拽幽灵：跟随鼠标的半透明预览（位置已按当前摆放规则吸附）。</summary>
        public void DrawDragGhost(HudState s)
        {
            if (!HasDragSource || s.Theme == null) return;

            FloatCard probe;
            probe.Kind = DragSourceKind;
            probe.Key = DragSourceKey;
            probe.X = 0f;
            probe.Y = 0f;
            probe.Opacity = 1f;
            probe.Scale = 1f;
            probe.GridSnap = false;

            Vector2 size = SizeOf(probe, s);
            float x;
            float y;
            FloatCardRules.DropAt(DragPoint.x, DragPoint.y, size.x, size.y, Screen.width, Screen.height,
                s.Theme.Px(FloatCardRules.SnapDistance), out x, out y);

            Rect rect = new Rect(x, y, size.x, size.y);
            DrawCardVisual(s, s.Snapshot, probe, rect, 0.6f, s.Styles);
            s.Styles.DrawOutline(rect, Theme.Accent);
        }

        private static bool DrawContent(HudState s, BodySnapshot snap, FloatCard card, Rect rect, HudStyles st)
        {
            switch (card.Kind)
            {
                case FloatCardKind.Vital:
                {
                    VitalReading? reading = FindVital(snap, card.Key);
                    if (reading == null) return false;
                    Widgets.VitalCard(rect, reading.Value, st, s.Icons, s.Config.ShowLimits.Value);
                    return true;
                }
                case FloatCardKind.Limb:
                {
                    LimbReading? limb = FindLimb(snap, card.Key);
                    if (limb == null) return false;
                    Widgets.LimbCard(rect, limb.Value, false, st, s.Icons);
                    return true;
                }
                default:
                {
                    MateReading? mate = FindMate(snap, card.Key);
                    if (mate == null) return false;

                    Texture2D avatar = s.Avatars != null
                        ? s.Avatars.Get(mate.Value.SteamId, mate.Value.Avatar, Mathf.RoundToInt(s.Theme.AvatarSize()))
                        : null;
                    Widgets.MateCard(rect, mate.Value, avatar, false, false, st);
                    return true;
                }
            }
        }

        private static void DrawPlaceholder(Rect rect, string key, HudStyles st)
        {
            Theme th = st.Theme;
            st.DrawSurface(rect, st.CardSunken);
            GUI.Label(new Rect(rect.x + th.Px(12f), rect.y, Mathf.Max(20f, rect.width - th.Px(24f)), rect.height),
                "<color=" + Theme.HexMuted + ">" + key + "：暂无数据</color>", st.Body);
        }

        // ---------------------------------------------------------------
        // 数据查找（组件卡按 key 从快照里取，因此始终是实时数据）
        // ---------------------------------------------------------------

        private static VitalReading? FindVital(BodySnapshot snap, string key)
        {
            if (snap == null || !snap.Valid) return null;
            for (int i = 0; i < snap.Vitals.Count; i++)
            {
                if (string.Equals(snap.Vitals[i].Label, key, StringComparison.Ordinal)) return snap.Vitals[i];
            }
            return null;
        }

        private static LimbReading? FindLimb(BodySnapshot snap, string key)
        {
            if (snap == null || !snap.Valid) return null;
            for (int i = 0; i < snap.Limbs.Count; i++)
            {
                if (string.Equals(snap.Limbs[i].Name, key, StringComparison.Ordinal)) return snap.Limbs[i];
            }
            return null;
        }

        private static MateReading? FindMate(BodySnapshot snap, string key)
        {
            if (snap == null || !snap.Valid) return null;
            for (int i = 0; i < snap.Mates.Count; i++)
            {
                if (string.Equals(snap.Mates[i].Name, key, StringComparison.Ordinal)) return snap.Mates[i];
            }
            return null;
        }

        // ---------------------------------------------------------------
        // 摆放界面的画布提示
        // ---------------------------------------------------------------

        /// <summary>摆放界面的操作提示。</summary>
        public const string EditorHintText =
            "鼠标靠近屏幕左右边缘展开两侧面板（离开自动收起）· 从组件库拖出组件卡摆放 · 选中后左栏调透明度 / 大小 / 方格 · Delete 删除选中 · Esc 保存并关闭";

        /// <summary>画布背景层：全屏方格 + 屏幕边界。</summary>
        public static void DrawGridOverlay(HudState s)
        {
            if (s.Theme == null || s.Styles == null) return;

            float cell = GridCell(s);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.055f);
            for (float x = 0f; x < Screen.width; x += cell)
            {
                GUI.DrawTexture(new Rect(Mathf.Floor(x), 0f, 1f, Screen.height), Texture2D.whiteTexture);
            }
            for (float y = 0f; y < Screen.height; y += cell)
            {
                GUI.DrawTexture(new Rect(0f, Mathf.Floor(y), Screen.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = old;

            // 屏幕边界：直观说明“拖不出去”
            Color border = Theme.WithAlpha(Theme.Accent, 0.5f);
            Widgets.Fill(new Rect(0f, 0f, Screen.width, 2f), border);
            Widgets.Fill(new Rect(0f, Screen.height - 2f, Screen.width, 2f), border);
            Widgets.Fill(new Rect(0f, 0f, 2f, Screen.height), border);
            Widgets.Fill(new Rect(Screen.width - 2f, 0f, 2f, Screen.height), border);
        }

        /// <summary>底部提示条（画在最上层，避免被抽屉盖住）。</summary>
        public static void DrawHintBar(HudState s)
        {
            if (s.Theme == null || s.Styles == null) return;

            Theme th = s.Theme;
            HudStyles st = s.Styles;

            // 不贴屏幕边缘：留出边距做成圆角条，与其它 UI 元素保持一致的圆角语言
            float margin = th.Px(12f);
            float hintH = th.SmallLine() + th.Px(10f);
            Rect hint = new Rect(margin, Screen.height - hintH - margin,
                Mathf.Max(40f, Screen.width - margin * 2f), hintH);

            st.DrawBox(hint, st.Panel);
            st.DrawOutline(hint, Theme.WithAlpha(Theme.Accent, 0.35f));
            GUI.Label(new Rect(hint.x + th.Px(14f), hint.y, Mathf.Max(20f, hint.width - th.Px(28f)), hint.height),
                "<color=" + Theme.HexAccent + ">" + EditorHintText + "</color>", st.Body);
        }
    }
}
