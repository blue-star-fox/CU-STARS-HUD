using System;
using System.Collections.Generic;
using System.IO;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI.Panels
{
    
    internal static class FilePickerDialog
    {
        
        private static float RowHeight(Theme th)
        {
            return th.LabelLine() + th.Px(4f);
        }

        public static void Draw(HudState s)
        {
            Theme th = s.Theme;
            HudStyles st = s.Styles;

            float pad = th.Px(16f);
            float titleH = th.Font(0) * 1.5f;
            float buttonH = th.Px(28f);

            float w = Mathf.Min(Screen.width - th.Px(48f), th.Px(620f));
            float h = Mathf.Min(Screen.height - th.Px(48f), th.Px(500f));
            Rect rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), st.Overlay);

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && !rect.Contains(e.mousePosition))
            {
                Close(s);
                e.Use();
                return;
            }
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close(s);
                e.Use();
                return;
            }

            st.DrawPanel(rect);

            GUI.BeginGroup(rect);
            try
            {
                GUI.Label(new Rect(pad, pad, w - pad * 2f, titleH), "选择底图", st.Title);

                Rect closeRect = new Rect(w - pad - th.Px(26f), pad, th.Px(22f), th.Px(22f));
                if (Widgets.CloseButton(closeRect, st, s.Icons)) Close(s);

                float y = pad + titleH + th.Px(4f);
                string dir = s.PickerDir ?? string.Empty;
                GUI.Label(new Rect(pad, y, w - pad * 2f, th.SmallLine()),
                    "<color=" + Theme.HexMuted + ">" + ShortPath(dir, w - pad * 2f, st) + "</color>", st.Tiny);
                y += th.SmallLine() + th.Px(6f);

                Rect tools = new Rect(pad, y, w - pad * 2f, th.Px(24f));
                y += tools.height + th.Px(6f);

                float btnW = th.Px(76f);
                string parent = HudState.PickerParent(dir);
                GUI.enabled = parent != null;
                if (Widgets.Button(new Rect(tools.x, tools.y, btnW, tools.height), "↑ 上一级", false, st)
                    && parent != null)
                {
                    s.PickerLoad(parent);
                }
                GUI.enabled = true;

                string[] roots = HudState.PickerRoots();
                float rootX = tools.x + btnW + th.Px(6f);
                for (int i = 0; i < roots.Length && rootX + btnW < tools.xMax; i++)
                {
                    if (Widgets.Button(new Rect(rootX, tools.y, btnW * 0.7f, tools.height),
                        roots[i].TrimEnd('\\', '/'), false, st))
                    {
                        s.PickerLoad(roots[i]);
                    }
                    rootX += btnW * 0.7f + th.Px(4f);
                }

                float footerH = buttonH + th.Px(10f);
                Rect view = new Rect(pad, y, w - pad * 2f, Mathf.Max(60f, h - y - footerH - pad));
                GUI.BeginGroup(view);
                try
                {
                    Rect local = new Rect(0f, 0f, view.width, view.height);
                    float rowH = RowHeight(th);
                    float previewW = Mathf.Max(90f, local.width * 0.34f);
                    float listW = Mathf.Max(60f, local.width - previewW - th.Px(17f));

                    int count = s.PickerEntries.Count;
                    s.PickerScroll.ContentHeight = count * rowH + th.Px(4f);
                    Widgets.ScrollInput(local, s.PickerScroll, st);

                    float rowY = -s.PickerScroll.Offset;
                    for (int i = 0; i < count; i++)
                    {
                        
                        if (rowY + rowH >= -2f && rowY <= local.height + 2f)
                        {
                            DrawEntry(s, st, th, new Rect(0f, rowY, listW, rowH - th.Px(2f)),
                                s.PickerEntries[i]);
                        }
                        rowY += rowH;
                    }

                    if (count == 0 && string.IsNullOrEmpty(s.PickerError))
                    {
                        GUI.Label(new Rect(0f, 0f, listW, th.LabelLine()),
                            "<color=" + Theme.HexMuted + ">（这个目录里没有子文件夹或图片）</color>", st.Tiny);
                    }
                    if (!string.IsNullOrEmpty(s.PickerError))
                    {
                        GUI.Label(new Rect(0f, 0f, listW, th.LabelLine()),
                            "<color=" + Theme.HexCritical + ">无法读取：</color>" + s.PickerError, st.Tiny);
                    }

                    Widgets.ScrollBar(local, s.PickerScroll, st);

                    Rect previewRect = new Rect(listW + th.Px(8f), 0f, previewW, local.height - th.Px(2f));
                    DrawPreview(s, st, th, previewRect);
                }
                finally
                {
                    GUI.EndGroup();
                }

                Rect footer = new Rect(pad, h - pad - buttonH, w - pad * 2f, buttonH);
                if (Widgets.Button(new Rect(footer.x, footer.y, th.Px(90f), buttonH), "取消", false, st))
                {
                    Close(s);
                }

                bool canOpen = !string.IsNullOrEmpty(s.PickerSelected);
                GUI.enabled = canOpen;
                if (Widgets.Button(new Rect(footer.xMax - th.Px(110f), footer.y, th.Px(110f), buttonH),
                    canOpen ? "使用这张图" : "未选择文件", true, st) && canOpen)
                {
                    Apply(s, s.PickerSelected);
                }
                GUI.enabled = true;
            }
            catch (Exception ex)
            {
                s.Note("文件浏览器绘制异常：" + ex.Message);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private static void DrawPreview(HudState s, HudStyles st, Theme th, Rect rect)
        {
            st.DrawSurface(rect, st.CardSunken);

            float pad = th.Px(8f);
            string path = s.PickerSelected;

            if (string.IsNullOrEmpty(path))
            {
                GUI.Label(new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, th.LabelLine()),
                    "<color=" + Theme.HexMuted + ">选一张图\n查看预览</color>", st.Tiny);
                return;
            }

            bool ok = BackgroundImage.EnsurePreview(path);
            Texture2D tex = ok ? BackgroundImage.Preview : null;

            float captionH = th.SmallLine() * 2f;
            Rect box = new Rect(rect.x + pad, rect.y + pad,
                rect.width - pad * 2f, Mathf.Max(20f, rect.height - pad * 2f - captionH));

            if (tex == null)
            {
                GUI.Label(new Rect(box.x, box.y, box.width, th.LabelLine()),
                    "<color=" + Theme.HexCritical + ">无法预览</color>", st.Tiny);
            }
            else
            {
                
                GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(box.x, box.yMax, box.width, th.SmallLine()),
                    "<color=" + Theme.HexMuted + ">" + tex.width + " × " + tex.height + "</color>", st.Tiny);
            }

            GUI.Label(new Rect(box.x, box.yMax + th.SmallLine(), box.width, th.SmallLine()),
                "<color=" + Theme.HexSecondary + ">" + SafeName(path) + "</color>", st.Tiny);
        }

        private static void DrawEntry(HudState s, HudStyles st, Theme th, Rect row, string path)
        {
            bool isDir;
            try
            {
                isDir = Directory.Exists(path);
            }
            catch (Exception)
            {
                isDir = false;
            }

            bool selected = !isDir && string.Equals(path, s.PickerSelected, StringComparison.OrdinalIgnoreCase);
            bool hover = row.Contains(Event.current.mousePosition);

            if (selected)
            {
                st.DrawSurface(row, st.CardRaised);
            }
            else if (hover)
            {
                st.DrawSurface(row, st.CardSunken);
            }

            string name = SafeName(path);
            
            string icon = isDir ? "▸ " : "▪ ";
            string color = isDir ? Theme.HexAccent : (selected ? Theme.HexPrimary : Theme.HexSecondary);

            GUI.Label(new Rect(row.x + th.Px(8f), row.y, row.width - th.Px(16f), row.height),
                icon + "<color=" + color + ">" + name + "</color>", st.Body);

            if (row.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0)
            {
                if (isDir)
                {
                    s.PickerLoad(path);
                }
                else
                {
                    s.PickerSelected = path;
                }
                Event.current.Use();
            }
        }

        private static string SafeName(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(name)) return name;

                return path;
            }
            catch (Exception)
            {
                return path;
            }
        }

        private static string ShortPath(string path, float width, HudStyles st)
        {
            if (string.IsNullOrEmpty(path)) return "";

            GUIContent content = new GUIContent(path);
            if (st.Tiny.CalcSize(content).x <= width) return path;

            int cut = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (st.Tiny.CalcSize(new GUIContent("…" + path.Substring(i))).x <= width)
                {
                    cut = i;
                    break;
                }
            }
            return "…" + path.Substring(cut);
        }

        private static void Apply(HudState s, string sourcePath)
        {
            
            if (!ImageFileFilter.IsImage(sourcePath))
            {
                s.Note("只能使用图片文件（png / jpg / jpeg / bmp）");
                return;
            }

            try
            {
                string dir = s.BackgroundDir;
                if (string.IsNullOrEmpty(dir))
                {
                    s.Note("无法定位 BepInEx/config 目录");
                    return;
                }

                string name = "CUStatsHud-bg" + Path.GetExtension(sourcePath).ToLowerInvariant();
                File.Copy(sourcePath, Path.Combine(dir, name), true);

                s.Config.BackgroundFile.Value = name;
                s.BackgroundReload = true;
                s.ShowFilePicker = false;
                s.Note("已加载底图：" + name);
            }
            catch (Exception ex)
            {
                s.Note("复制底图失败：" + ex.Message);
            }
        }

        private static void Close(HudState s)
        {
            s.ShowFilePicker = false;
        }
    }
}
