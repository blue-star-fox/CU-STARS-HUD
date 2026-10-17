using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 监护仪主窗口：外框、标题栏（含关闭按钮）、状态条、波形区、标签栏、内容区与底栏。
    ///
    /// 不继承 GUI.Window：拖动/缩放/滚动全部自行处理，
    /// 这样坐标确定、几何可持久化，也不会和游戏自身的 IMGUI 窗口 ID 冲突。
    /// 所有竖向尺寸都来自 <see cref="Theme"/> 的字体度量，任意字号/缩放组合下都不会重叠。
    ///
    /// 信息不重复：标题栏只放名称与关闭按钮，数值只在波形区通道块里出现一次，
    /// 状态条只写状态本身、不列举指标名（指标在下方卡片中）。
    /// </summary>
    internal static class MonitorWindow
    {
        // 走属性而不是 const：配色随皮肤变化，const 在编译期就固化了
        private static string MutedHex { get { return Theme.HexMuted; } }
        private const string NoValue = "—";

        /// <summary>标签栏图标种类，与 <see cref="HudState.TabNames"/> 一一对应。</summary>
        private static readonly string[] TabIconKinds = { "vitals", "limbs", "mates", "settings", "stats" };

        public static void Draw(HudState s)
        {
            if (s.Styles == null || s.Theme == null) return;

            HudStyles st = s.Styles;
            Theme th = s.Theme;

            // 模态窗口（关于 / 队友详情）打开时，主窗口不再响应拖动与缩放
            if (!s.HasModal) HandleDragAndResize(s);

            Rect win = s.Window;

            // ---- 投影 + 面板本体 ----
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.38f * old.a);
            st.DrawBox(new Rect(win.x - 2f, win.y + 3f, win.width + 4f, win.height + 4f), st.CardWhite);
            GUI.color = old;

            st.DrawPanel(win);

            bool closeClicked = false;

            // ---- 面板内部：统一使用局部坐标 ----
            GUI.BeginGroup(win);
            try
            {
                float width = win.width;
                float height = win.height;
                float pad = th.Px(Theme.PadSize);
                float gap = th.Px(Theme.GapSize);
                float innerWidth = Mathf.Max(10f, width - pad * 2f);

                float y = 0f;

                float headerH = th.HeaderHeight();
                closeClicked = DrawHeader(s, new Rect(0f, y, width, headerH), st, th);
                y += headerH;
                st.DrawHairline(new Rect(pad, y, innerWidth, 1f));
                y += 1f + gap * 0.55f;

                // 状态条：始终显示，颜色与闪烁表达当前档位
                if (s.Config.AlertsEnabled.Value)
                {
                    float bannerH = th.AlarmHeight();
                    Widgets.StatusBanner(new Rect(pad, y, innerWidth, bannerH),
                        s.Snapshot.Overall,
                        string.IsNullOrEmpty(s.Snapshot.AlertText) ? "等待数据" : s.Snapshot.AlertText,
                        s.Config.AlertFlash.Value, s.Pulse, st,
                        s.Snapshot.Overall == Severity.Critical && s.Icons != null ? s.Icons.GetEmoji("warning") : null);
                    y += bannerH + gap * 0.7f;
                }

                if (s.Config.WaveformsEnabled.Value && s.Waves != null)
                {
                    float stripH = th.StripHeight();
                    float stripGap = th.Px(5f);
                    for (int i = 0; i < 3; i++)
                    {
                        DrawWaveRow(s, new Rect(pad, y, innerWidth, stripH), i, st, th);
                        y += stripH + stripGap;
                    }
                    y += gap * 0.25f;
                }

                float tabH = th.TabHeight();
                DrawTabs(s, new Rect(pad, y, innerWidth, tabH), st, th);
                y += tabH + gap * 0.6f;

                float footerH = th.SmallLine();
                float contentH = height - y - footerH - pad * 0.6f;
                Rect contentRect = new Rect(pad, y, innerWidth, Mathf.Max(0f, contentH));

                if (contentH >= th.CompactRowHeight())
                {
                    DrawContent(s, contentRect, st, th);
                }
                else if (contentH > 1f)
                {
                    GUI.Label(contentRect, "<color=" + MutedHex + ">面板高度不足，请拖大窗口</color>", st.Tiny);
                }

                DrawFooter(s, new Rect(pad, height - footerH - pad * 0.3f, innerWidth, footerH), st, th);

                if (s.Config.Resizable.Value)
                {
                    float grip = th.Px(16f);
                    Color faint = new Color(1f, 1f, 1f, 0.22f);
                    Widgets.Fill(new Rect(width - grip - 3f, height - 4f, grip, 1f), faint);
                    Widgets.Fill(new Rect(width - 4f, height - grip - 3f, 1f, grip), faint);
                }
            }
            catch (Exception ex)
            {
                s.Note("界面绘制异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }

            if (closeClicked) s.Visible = false;
        }

        // ===============================================================
        // 标题栏：状态灯 + 名称 + 关闭按钮
        // ===============================================================

        private static bool DrawHeader(HudState s, Rect rect, HudStyles st, Theme th)
        {
            BodySnapshot snap = s.Snapshot;

            // 标题栏做成面板内的独立圆角表面：
            // 既符合现代系统的分层表面设计，也避免全宽直角从面板圆角外露出。
            Rect surface = new Rect(rect.x + th.Px(6f), rect.y + th.Px(6f),
                Mathf.Max(20f, rect.width - th.Px(12f)), Mathf.Max(20f, rect.height - th.Px(10f)));
            st.DrawSurface(surface, st.CardRaised);

            float closeSize = th.CloseButtonSize();
            Rect closeRect = new Rect(surface.xMax - th.Px(6f) - closeSize,
                surface.y + (surface.height - closeSize) * 0.5f, closeSize, closeSize);

            // 左侧状态指示灯（圆形）
            float dot = Mathf.Max(6f, th.Px(8f));
            float textX = surface.x + th.Px(10f);
            Color old = GUI.color;
            GUI.color = Theme.SeverityColor(snap.Overall);
            st.DrawPill(new Rect(textX, surface.y + (surface.height - dot) * 0.5f, dot, dot), st.PillWhite);
            GUI.color = old;

            textX += dot + th.Px(8f);
            float textWidth = Mathf.Max(20f, closeRect.x - textX - th.Px(8f));

            float titleH = th.Font(0) * 1.3f;
            float subH = th.Font(2) * 1.5f;
            float top = surface.y + (surface.height - (titleH + subH)) * 0.5f;

            GUI.Label(new Rect(textX, top, textWidth, titleH),
                "<color=" + Theme.HexAccent + ">CU Stats HUD</color>", st.Title);

            string who = string.IsNullOrEmpty(snap.PlayerName)
                ? (snap.InMultiplayer ? "本机玩家" : "本地幸存者")
                : snap.PlayerName;
            GUI.Label(new Rect(textX, top + titleH, textWidth, subH),
                "<color=" + MutedHex + ">" + who + " · " + (snap.InMultiplayer ? "联机" : "单机") + "</color>", st.Tiny);

            return Widgets.CloseButton(closeRect, st, s.Icons);
        }

        // ===============================================================
        // 波形区：每行 = 通道块（通道名 + 大数值） + 波形
        // ===============================================================

        private static void DrawWaveRow(HudState s, Rect row, int channel, HudStyles st, Theme th)
        {
            BodySnapshot snap = s.Snapshot;
            bool valid = snap.Valid;

            string label;
            string value;
            string unit;
            Color color;
            Severity severity;

            switch (channel)
            {
                case 0:
                    label = "II ECG";
                    value = valid ? VitalRules.Int0(snap.HeartRate) : NoValue;
                    unit = "bpm";
                    color = Theme.ChEcg;
                    severity = valid ? VitalRules.HeartRate(snap.HeartRate) : Severity.Unknown;
                    break;
                case 1:
                    label = "SpO2";
                    value = valid ? VitalRules.Int0(snap.BloodOxygen) : NoValue;
                    unit = "%";
                    color = Theme.ChPleth;
                    severity = valid ? VitalRules.BloodOxygen(snap.BloodOxygen) : Severity.Unknown;
                    break;
                default:
                    label = "RESP";
                    value = valid ? VitalRules.Int0(snap.RespiratoryPerMin) : NoValue;
                    unit = "/m";
                    color = Theme.ChResp;
                    severity = valid
                        ? VitalRules.RespirationPerMin(snap.RespiratoryPerMin, snap.Breathing)
                        : Severity.Unknown;
                    break;
            }

            float blockW = th.ChannelBlockWidth();
            Rect blockRect = new Rect(row.x, row.y, blockW, row.height);
            Rect waveRect = new Rect(row.x + blockW + th.Px(4f), row.y,
                Mathf.Max(10f, row.width - blockW - th.Px(4f)), row.height);

            // 通道块：通道名与数值都用通道色；指标异常时数值转为分级色
            string hex = ColorHex(color);
            string valueHex = severity >= Severity.Notice ? Theme.ValueHex(severity) : hex;

            float labelH = th.SmallLine();
            GUI.Label(new Rect(blockRect.x, blockRect.y, blockW, labelH),
                "<color=" + hex + ">" + label + "</color>", st.Tiny);

            string valueText = "<color=" + valueHex + ">" + value + "</color>";
            if (!string.IsNullOrEmpty(unit))
            {
                valueText += "<color=" + MutedHex + "><size=" + th.Font(2) + "> " + unit + "</size></color>";
            }
            GUI.Label(new Rect(blockRect.x, blockRect.y + labelH, blockW,
                    Mathf.Max(th.ValueLine(), blockRect.height - labelH)),
                valueText, st.ValueLargeLeft);

            if (channel == 1)
            {
                // 血氧是浓度，用浓度条而不是波形（波形看不出浓度高低）
                Widgets.Spo2Gauge(waveRect, valid, snap.BloodOxygen, st);
                return;
            }

            // 波形纸带：尺寸由绘制阶段记录，纹理分配在 Update 阶段完成
            s.WaveWidth = Mathf.RoundToInt(Mathf.Max(0f, waveRect.width - 2f));
            s.WaveHeight = Mathf.RoundToInt(Mathf.Max(0f, waveRect.height - 2f));

            WaveformStrip strip = channel == 0 ? s.Waves.Ecg : s.Waves.Resp;

            st.DrawBox(waveRect, st.WaveBackground);
            st.DrawGrid(new Rect(waveRect.x + 1f, waveRect.y + 1f, waveRect.width - 2f, waveRect.height - 2f),
                st.GridMinor, th.Pxi(9f));
            Widgets.Fill(new Rect(waveRect.x + 1f, waveRect.y + waveRect.height * 0.5f, waveRect.width - 2f, 1f),
                new Color(1f, 1f, 1f, 0.05f));

            if (valid && strip != null && strip.Texture != null)
            {
                GUI.DrawTexture(new Rect(waveRect.x + 1f, waveRect.y + 1f, waveRect.width - 2f, waveRect.height - 2f),
                    strip.Texture, ScaleMode.StretchToFill, true);
            }
            else
            {
                GUI.Label(waveRect, "<color=" + MutedHex + ">无信号</color>", st.Center);
            }
        }

        private static string ColorHex(Color c)
        {
            return "#" + ((int)(c.r * 255f)).ToString("X2")
                + ((int)(c.g * 255f)).ToString("X2")
                + ((int)(c.b * 255f)).ToString("X2");
        }

        // ===============================================================
        // 标签栏
        // ===============================================================

        private static void DrawTabs(HudState s, Rect rect, HudStyles st, Theme th)
        {
            float gap = th.Px(6f);
            float tabW = Mathf.Max(20f, (rect.width - gap * (HudState.TabCount - 1)) / HudState.TabCount);

            for (int i = 0; i < HudState.TabCount; i++)
            {
                string label = HudState.TabNames[i];
                if (i == HudState.TabTeam && s.Snapshot.Mates.Count > 0)
                {
                    label += " (" + s.Snapshot.Mates.Count + ")";
                }

                Rect tab = new Rect(rect.x + i * (tabW + gap), rect.y, tabW, rect.height);
                Texture2D icon = s.Icons != null ? s.Icons.GetEmoji(TabIconKinds[i]) : null;
                if (Widgets.SegmentWithIcon(tab, icon, label, i == s.Tab, st))
                {
                    s.SetTab(i);
                }
            }
        }

        // ===============================================================
        // 内容区
        // ===============================================================

        private static void DrawContent(HudState s, Rect area, HudStyles st, Theme th)
        {
            ScrollState scroll = s.Scroll;

            GUI.BeginGroup(area);
            try
            {
                // 组内坐标以 area 左上角为原点，鼠标位置同样是组内坐标
                Rect view = new Rect(0f, 0f, area.width, area.height);
                Widgets.ScrollInput(view, scroll, st);

                float ease = s.TabAnimation();
                float offX = (1f - ease) * th.Px(18f) * s.TabAnimDir;
                Color old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * ease);

                float contentWidth = Mathf.Max(80f, view.width - th.Px(9f));
                Rect panelArea = new Rect(offX, 0f, contentWidth, 0f);
                float measured;

                switch (s.Tab)
                {
                    case HudState.TabLimbs:
                        measured = Panels.LimbsPanel.Draw(s, s.Snapshot, panelArea, 0f, scroll.Offset);
                        break;
                    case HudState.TabTeam:
                        measured = Panels.TeamPanel.Draw(s, panelArea, scroll.Offset);
                        break;
                    case HudState.TabSettings:
                        measured = Panels.SettingsPanel.Draw(s, panelArea, scroll.Offset);
                        break;
                    case HudState.TabStats:
                        measured = Panels.StatsPanel.Draw(s, panelArea, scroll.Offset);
                        break;
                    default:
                        // 主面板的波形区已常显心率/血氧/呼吸，列表里跳过以免重复
                        measured = Panels.VitalsPanel.Draw(s, s.Snapshot, panelArea, 0f, scroll.Offset,
                            s.Config.WaveformsEnabled.Value);
                        break;
                }

                GUI.color = old;
                scroll.ContentHeight = measured;
                Widgets.ScrollBar(view, scroll, st);
            }
            catch (Exception ex)
            {
                // 同时落进日志：底栏提示 4 秒就消失，出问题时基本抓不到
                s.Note("内容绘制异常：" + ex.Message);
                HudState.Log.LogError("标签页内容绘制异常：" + ex);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        // ===============================================================
        // 底栏：只放临时提示与刷新时间（操作说明在关于窗口里）
        // ===============================================================

        private static void DrawFooter(HudState s, Rect rect, HudStyles st, Theme th)
        {
            if (!string.IsNullOrEmpty(s.StatusNote) && Time.unscaledTime - s.StatusNoteAt < 4f)
            {
                GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.66f, rect.height),
                    "<color=" + MutedHex + ">" + s.StatusNote + "</color>", st.Tiny);
            }

            string right = "";
            if (s.Snapshot.Valid)
            {
                right = "刷新 " + VitalRules.Num1(Time.unscaledTime - s.Snapshot.SampledAt) + "s 前";
            }
            else if (!string.IsNullOrEmpty(s.Snapshot.Error))
            {
                right = s.Snapshot.Error;
            }

            GUI.Label(new Rect(rect.x + rect.width * 0.6f, rect.y, rect.width * 0.4f, rect.height),
                "<color=" + MutedHex + ">" + right + "</color>", st.TinyRight);
        }

        // ===============================================================
        // 拖动 / 缩放
        // ===============================================================

        private static void HandleDragAndResize(HudState s)
        {
            Event e = Event.current;
            if (e == null) return;

            Theme th = s.Theme;
            Rect win = s.Window;
            float headerH = th.HeaderHeight();
            Rect header = new Rect(win.x, win.y, win.width, headerH);
            float grip = th.Px(18f);
            Rect gripRect = new Rect(win.xMax - grip, win.yMax - grip, grip, grip);

            // 两个控件 ID 每帧都按固定顺序申请，保证跨帧稳定
            int resizeId = GUIUtility.GetControlID(FocusType.Passive);
            int dragId = GUIUtility.GetControlID(FocusType.Passive);

            float minW = Mathf.Min(s.MinWindowWidth(), Screen.width);
            float minH = Mathf.Min(s.MinWindowHeight(), Screen.height);

            // ---- 右下角缩放 ----
            // 注意：MouseUp 的收尾必须无条件执行，否则一旦拖动中途 Resizable 被改成 false，
            // hotControl 会永久钉在本控件上，面板内所有控件都失去响应。
            switch (e.GetTypeForControl(resizeId))
            {
                case EventType.MouseDown:
                    if (s.Config.Resizable.Value && gripRect.Contains(e.mousePosition))
                    {
                        s.Resizing = true;
                        s.ResizeStart = e.mousePosition;
                        s.ResizeOrigin = win;
                        GUIUtility.hotControl = resizeId;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (s.Resizing && GUIUtility.hotControl == resizeId)
                    {
                        float w = Mathf.Clamp(s.ResizeOrigin.width + (e.mousePosition.x - s.ResizeStart.x),
                            minW, Mathf.Max(minW, Screen.width - win.x));
                        float h = Mathf.Clamp(s.ResizeOrigin.height + (e.mousePosition.y - s.ResizeStart.y),
                            minH, Mathf.Max(minH, Screen.height - win.y));
                        s.Window = new Rect(win.x, win.y, w, h);
                        s.MarkGeometryDirty(false);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (s.Resizing)
                    {
                        s.Resizing = false;
                        if (GUIUtility.hotControl == resizeId) GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }

            if (s.Resizing) return;

            // ---- 标题栏拖动 ----
            // 关闭按钮所在区域不参与拖动，避免点关闭时把面板拖走
            float closeSize = th.CloseButtonSize();
            Rect closeRect = new Rect(win.xMax - th.Px(6f) - th.Px(6f) - closeSize,
                win.y + (headerH - closeSize) * 0.5f, closeSize, closeSize);

            bool canStart = header.Contains(e.mousePosition)
                && !gripRect.Contains(e.mousePosition)
                && !closeRect.Contains(e.mousePosition);

            switch (e.GetTypeForControl(dragId))
            {
                case EventType.MouseDown:
                    if (canStart)
                    {
                        s.Dragging = true;
                        s.DragOffset = e.mousePosition - new Vector2(win.x, win.y);
                        GUIUtility.hotControl = dragId;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (s.Dragging && GUIUtility.hotControl == dragId)
                    {
                        s.Window = new Rect(
                            Mathf.Clamp(e.mousePosition.x - s.DragOffset.x, 0f, Mathf.Max(0f, Screen.width - win.width)),
                            Mathf.Clamp(e.mousePosition.y - s.DragOffset.y, 0f, Mathf.Max(0f, Screen.height - win.height)),
                            win.width, win.height);
                        s.MarkGeometryDirty(true);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (s.Dragging)
                    {
                        s.Dragging = false;
                        if (GUIUtility.hotControl == dragId) GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }
    }
}
