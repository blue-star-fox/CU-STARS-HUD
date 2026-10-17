using System;
using BepInEx.Logging;
using CuStatsHud.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class HudState
    {
        public const int TabVitals = 0;
        public const int TabLimbs = 1;
        public const int TabTeam = 2;
        public const int TabStats = 3;
        public const int TabSettings = 4;
        public const int TabCount = 5;

        private const float PanelFadeSeconds = 0.16f;

        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("CUStatsHud");

        public static readonly string[] TabNames = { "生命体征", "肢体", "队友", "统计", "设置" };

        public readonly PluginConfig Config;
        public readonly MpBridge Mp = new MpBridge();
        public readonly BodyReader Reader;
        public readonly BodySnapshot Snapshot = new BodySnapshot();
        public readonly FontProvider Fonts = new FontProvider();
        public readonly AvatarCache Avatars = new AvatarCache();
        public WaveformSet Waves;
        
        public readonly FloatingCardHost FloatingCards = new FloatingCardHost();

        public readonly ScrollState CardLibraryScroll = new ScrollState();

        public readonly ScrollState AboutScroll = new ScrollState();

        public float LeftPaneExpand;
        
        public float RightPaneExpand;

        public bool LeftPanePinned;
        public bool RightPanePinned;

        public bool LeftPaneSuppress;
        public bool RightPaneSuppress;

        public float GamePausedVisibility = 1f;

        public bool Visible;
        public int Tab;
        public float PanelVisibility;
        public float TabAnimStart = -10f;
        public float TabAnimDir = 1f;
        
        public float Pulse = 1f;
        
        public int CapturingTarget;

        public bool CapturingKey
        {
            get { return CapturingTarget != 0; }
        }
        public string StatusNote = "";
        public float StatusNoteAt = -10f;
        public bool ShowAbout;

        public readonly GameStats GameStats = new GameStats();
        
        public bool StatsDirty;
        
        public string StatsPath;

        public bool StatsResetPending;
        
        public float StatsResetAt = -10f;

        public void OpenAbout()
        {
            ShowAbout = true;
        }

        public void ToggleFloatCards()
        {
            Config.FloatCardsEnabled.Value = !Config.FloatCardsEnabled.Value;
            Note(Config.FloatCardsEnabled.Value ? "已显示组件卡" : "已隐藏组件卡");
        }

        public bool ShowMateDetail;
        
        public int MateDetailIndex = -1;
        
        private string _mateDetailName;
        
        public readonly ScrollState MateDetailScroll = new ScrollState();

        public void OpenMateDetail(int index, string name)
        {
            ShowMateDetail = true;
            MateDetailIndex = index;
            _mateDetailName = name;
            MateDetailScroll.Offset = 0f;
            MateDetailScroll.Dragging = false;
        }

        public void CloseMateDetail()
        {
            ShowMateDetail = false;
            MateDetailIndex = -1;
            _mateDetailName = null;
        }

        public bool HasModal
        {
            get { return ShowAbout || ShowMateDetail; }
        }

        public MateReading? CurrentMateDetail()
        {
            if (!ShowMateDetail) return null;

            System.Collections.Generic.List<MateReading> mates = Snapshot.Mates;
            if (MateDetailIndex < 0 || MateDetailIndex >= mates.Count)
            {
                CloseMateDetail();
                return null;
            }

            MateReading mate = mates[MateDetailIndex];
            if (string.IsNullOrEmpty(_mateDetailName) || mate.Name == _mateDetailName)
            {
                return mate;
            }

            for (int i = 0; i < mates.Count; i++)
            {
                if (mates[i].Name != _mateDetailName) continue;
                MateDetailIndex = i;
                return mates[i];
            }

            CloseMateDetail();
            return null;
        }

        public Theme Theme;
        public HudStyles Styles;
        public IconProvider Icons;
        public Rect Window;
        
        public readonly ScrollState[] Scrolls = CreateScrolls();

        private static ScrollState[] CreateScrolls()
        {
            ScrollState[] states = new ScrollState[TabCount];
            for (int i = 0; i < states.Length; i++) states[i] = new ScrollState();
            return states;
        }

        public readonly System.Collections.Generic.List<int> ScratchPrimary = new System.Collections.Generic.List<int>(24);
        public readonly System.Collections.Generic.List<int> ScratchSecondary = new System.Collections.Generic.List<int>(24);

        public bool Dragging;
        public Vector2 DragOffset;
        public bool Resizing;
        public Vector2 ResizeStart;
        public Rect ResizeOrigin;

        public int WaveWidth;
        public int WaveHeight;

        public bool GeometryDirty;
        public float GeometryDirtyAt;

        private bool _autoPlacedX = true;
        private bool _autoPlacedY = true;

        private float _lastScale = -1f;
        private int _lastFont = -1;
        private string _lastFontMode;
        private string _lastSkin;
        private float _lastHue = -1f;
        private float _lastDark = -1f;
        private float _lastSat = -1f;
        private bool _windowInitialized;

        public HudState(PluginConfig config)
        {
            Config = config;
            Reader = new BodyReader(Mp);
            Waves = new WaveformSet();
            Tab = Mathf.Clamp(config.Tab.Value, 0, TabCount - 1);
        }

        public ScrollState Scroll
        {
            get { return Scrolls[Mathf.Clamp(Tab, 0, TabCount - 1)]; }
        }

        public void EnsureUi()
        {
            float scale = Mathf.Clamp(Config.Scale.Value, 0.7f, 1.5f);
            int fontSize = Mathf.Clamp(Config.FontSize.Value, 11, 20);
            string fontMode = Config.FontMode.Value ?? "game";
            string skin = Config.Skin.Value ?? "fox";

            IconProvider.EmojiEnabled = Config.ShowEmoji.Value;

            float hue = Mathf.Clamp01(Config.CustomHue.Value);
            float dark = Mathf.Clamp01(Config.CustomDark.Value);
            float sat = Mathf.Clamp01(Config.CustomSaturation.Value);

            bool customChanged = Mathf.Abs(hue - _lastHue) > 0.001f
                || Mathf.Abs(dark - _lastDark) > 0.001f
                || Mathf.Abs(sat - _lastSat) > 0.001f;

            bool changed = Styles == null
                || Mathf.Abs(scale - _lastScale) > 0.001f
                || fontSize != _lastFont
                || fontMode != _lastFontMode
                || skin != _lastSkin
                || customChanged;

            if (!changed)
            {
                if (!_windowInitialized) SyncWindowSize();
                return;
            }

            _lastScale = scale;
            _lastFont = fontSize;
            _lastFontMode = fontMode;
            _lastSkin = skin;
            _lastHue = hue;
            _lastDark = dark;
            _lastSat = sat;

            if (Styles != null)
            {
                Styles.Dispose();
                Styles = null;
            }
            if (Icons != null)
            {
                Icons.Dispose();
                Icons = null;
            }

            Theme.RebuildCustom(hue, dark, sat);

            if (string.Equals(skin, "game", System.StringComparison.OrdinalIgnoreCase))
            {
                Theme.Skin = Theme.UiSkin.Game;
            }
            else if (string.Equals(skin, "custom", System.StringComparison.OrdinalIgnoreCase))
            {
                Theme.Skin = Theme.UiSkin.Custom;
            }
            else
            {
                Theme.Skin = Theme.UiSkin.Fox;
            }

            Theme = new Theme(scale, fontSize);
            Fonts.Invalidate();
            Font font = Fonts.Resolve(fontMode);
            Styles = new HudStyles(Theme, font);
            Icons = new IconProvider(Theme.Pxi(20f));

            if (BackgroundImage.Current != null)
            {
                Styles.BackgroundImage = BackgroundImage.Current;
                Styles.BackgroundAlpha = Mathf.Clamp01(Config.BackgroundOpacity.Value);
            }

            SyncWindowSize();
        }

        public string BackgroundDir;

        public bool BackgroundReload;

        public bool ShowFilePicker;
        
        public string PickerDir;
        
        public readonly List<string> PickerEntries = new List<string>();
        
        public string PickerSelected;
        
        public string PickerError;
        
        public readonly ScrollState PickerScroll = new ScrollState();

        public void PickerLoad(string dir)
        {
            PickerDir = dir;
            PickerSelected = null;
            PickerError = null;
            PickerEntries.Clear();
            PickerScroll.Offset = 0f;
            PickerScroll.Dragging = false;

            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                string[] dirs = System.IO.Directory.GetDirectories(dir);
                System.Array.Sort(dirs, System.StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    
                    try
                    {
                        System.IO.FileAttributes attr = System.IO.File.GetAttributes(dirs[i]);
                        if ((attr & System.IO.FileAttributes.Hidden) != 0) continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    PickerEntries.Add(dirs[i]);
                }

                string[] files = System.IO.Directory.GetFiles(dir);
                System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    
                    if (!ImageFileFilter.IsImage(files[i])) continue;
                    PickerEntries.Add(files[i]);
                }
            }
            catch (Exception ex)
            {
                PickerError = ex.Message;
            }
        }

        public static string PickerParent(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;

            try
            {
                System.IO.DirectoryInfo parent = System.IO.Directory.GetParent(dir);
                return parent != null ? parent.FullName : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string[] PickerRoots()
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
                List<string> roots = new List<string>();
                for (int i = 0; i < drives.Length; i++)
                {
                    try
                    {
                        if (!drives[i].IsReady) continue;
                        roots.Add(drives[i].RootDirectory.FullName);
                    }
                    catch (Exception)
                    {
                        
                    }
                }
                return roots.ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        public void SyncWindowSize()
        {
            if (Theme == null) return;

            float width = Mathf.Max(Config.ClampedWidth() * Theme.Scale, MinWindowWidth());
            float height = Mathf.Max(Config.ClampedHeight() * Theme.Scale, MinWindowHeight());

            float x;
            float y;
            if (_windowInitialized)
            {
                
                x = _autoPlacedX ? PluginConfig.AutoPos : Window.x;
                y = _autoPlacedY ? PluginConfig.AutoPos : Window.y;
            }
            else
            {
                x = Config.WindowX.Value;
                y = Config.WindowY.Value;
                _autoPlacedX = x <= PluginConfig.AutoPos;
                _autoPlacedY = y <= PluginConfig.AutoPos;
            }

            if (_autoPlacedX) x = Screen.width - width - Theme.Px(24f);
            if (_autoPlacedY) y = (Screen.height - height) * 0.5f;

            Window = new Rect(x, y, width, height);
            _windowInitialized = true;
            ClampToScreen();
        }

        public float MinWindowWidth()
        {
            return Theme.Px(360f);
        }

        public float MinWindowHeight()
        {
            float chrome = Theme.Px(Theme.PadSize) * 2f
                + Theme.HeaderHeight()
                + Theme.TabHeight()
                + Theme.SmallLine()
                + Theme.Px(26f);

            if (Config.AlertsEnabled.Value) chrome += Theme.AlarmHeight() + Theme.Px(5f);
            if (Config.WaveformsEnabled.Value) chrome += 3f * (Theme.StripHeight() + Theme.Px(4f));

            float content = Theme.SectionHeight() + Theme.VitalCardHeight() + Theme.Px(14f);
            return chrome + content;
        }

        public void ClampToScreen()
        {
            if (Theme == null) return;

            float minWidth = Mathf.Min(MinWindowWidth(), Screen.width);
            float minHeight = Mathf.Min(MinWindowHeight(), Screen.height);

            float w = Mathf.Clamp(Window.width, minWidth, Mathf.Max(minWidth, Screen.width));
            float h = Mathf.Clamp(Window.height, minHeight, Mathf.Max(minHeight, Screen.height));

            float x = Mathf.Clamp(Window.x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(Window.y, 0f, Mathf.Max(0f, Screen.height - h));

            Window = new Rect(x, y, w, h);
        }

        public void SaveGeometry()
        {
            if (Theme == null) return;

            if (!_autoPlacedX) Config.WindowX.Value = Window.x;
            if (!_autoPlacedY) Config.WindowY.Value = Window.y;

            Config.WindowWidth.Value = Mathf.Clamp(Window.width / Theme.Scale, 360f, 1600f);
            Config.WindowHeight.Value = Mathf.Clamp(Window.height / Theme.Scale, 300f, 1600f);
            GeometryDirty = false;
        }

        public void MarkGeometryDirty(bool manualPlacement)
        {
            if (manualPlacement)
            {
                _autoPlacedX = false;
                _autoPlacedY = false;
            }
            GeometryDirty = true;
            GeometryDirtyAt = Time.unscaledTime;
        }

        public void ResetWindowPosition()
        {
            Config.WindowX.Value = PluginConfig.AutoPos;
            Config.WindowY.Value = PluginConfig.AutoPos;
            _windowInitialized = false;
            SyncWindowSize();
            MarkGeometryDirty(false);
        }

        public void ToggleVisible()
        {
            Visible = !Visible;

            if (!Visible)
            {
                
                ShowAbout = false;
                CloseMateDetail();
                CapturingTarget = 0;
            }
        }

        public void SetTab(int tab)
        {
            tab = Mathf.Clamp(tab, 0, TabCount - 1);
            if (tab == Tab) return;

            TabAnimDir = tab > Tab ? 1f : -1f;
            TabAnimStart = Time.unscaledTime;
            Tab = tab;
            Config.Tab.Value = tab;
        }

        public float TabAnimation()
        {
            float elapsed = Time.unscaledTime - TabAnimStart;
            return Easing.OutCubic(elapsed / TabAnimSeconds);
        }

        public const float TabAnimSeconds = 0.22f;

        public bool AdvanceVisibility(float unscaledDeltaTime)
        {
            float target = Visible ? 1f : 0f;
            PanelVisibility = Easing.Approach(PanelVisibility, target, unscaledDeltaTime,
                PanelFadeSeconds * 0.5f, 0.002f);
            return PanelVisibility > 0.002f;
        }

        public void Note(string message)
        {
            StatusNote = message;
            StatusNoteAt = Time.unscaledTime;
        }

        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        public void CheckScreenChange()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            ClampToScreen();
        }

        public void Dispose()
        {
            if (Styles != null)
            {
                Styles.Dispose();
                Styles = null;
            }
            if (Icons != null)
            {
                Icons.Dispose();
                Icons = null;
            }
            if (Waves != null)
            {
                Waves.Dispose();
                Waves = null;
            }

            Avatars.Dispose();
            FloatingCards.Dispose(Config);

            IconProvider.DisposeShared();
        }
    }
}
