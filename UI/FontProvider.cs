using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class FontProvider
    {
        public const string ModeAuto = "auto";
        public const string ModeSystem = "system";
        public const string ModeGame = "game";

        private const string ProbeAscii =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789%/.:-+()[]<>|≥≤";

        private const string ProbeCjk =
            "心率血氧意识状态正常危急警告注意无数据肢体队友设置刷新前体温疼痛度分钟秒血压大脑完整心情饥饿口渴免疫强度负重移动速度力量韧性智力";

        private readonly Dictionary<string, Font> _resolved = new Dictionary<string, Font>();

        private readonly HashSet<Font> _createdFonts = new HashSet<Font>();

        private Font _systemFont;
        private bool _systemSearched;
        
        private Font _gameBest;
        private int _gameBestScore = -1;
        private bool _gameSearched;

        public string ResolvedName { get; private set; }

        public Font Resolve(string mode)
        {
            string key = Normalize(mode);

            Font cached;
            if (_resolved.TryGetValue(key, out cached) && cached != null)
            {
                
                ResolvedName = DisplayName(cached);
                return cached;
            }

            Font font;
            switch (key)
            {
                case ModeSystem:
                    font = SystemFont() ?? SkinFont();
                    break;
                case ModeGame:
                    
                    font = GameFont(true) ?? SystemFont() ?? SkinFont();
                    break;
                default:
                    font = SystemFont() ?? GameFont(false) ?? SkinFont();
                    break;
            }

            _resolved[key] = font;
            ResolvedName = DisplayName(font);
            return font;
        }

        private static string DisplayName(Font font)
        {
            return font != null && !string.IsNullOrEmpty(font.name) ? font.name : "默认字体";
        }

        public void Invalidate()
        {
            _resolved.Clear();
            _systemSearched = false;
            _systemFont = null;
            _gameSearched = false;
            _gameBest = null;
            _gameBestScore = -1;
            ResolvedName = null;
            
        }

        private static string Normalize(string mode)
        {
            if (string.Equals(mode, ModeSystem, StringComparison.OrdinalIgnoreCase)) return ModeSystem;
            if (string.Equals(mode, ModeGame, StringComparison.OrdinalIgnoreCase)) return ModeGame;
            return ModeAuto;
        }

        private static Font SkinFont()
        {
            try
            {
                return GUI.skin != null ? GUI.skin.font : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Font SystemFont()
        {
            if (_systemSearched) return _systemFont;
            _systemSearched = true;

            string[] installed = SafeInstalledNames();
            _systemFont = installed != null
                ? CreateFirstExisting(SystemFontCandidates, installed)   
                : CreateFirstExisting(SystemFontCandidates, null);       
            return _systemFont;
        }

        private static readonly string[] SystemFontCandidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "Noto Sans CJK SC", "Source Han Sans SC", "思源黑体",
            "SimHei", "SimSun", "Arial Unicode MS", "Arial"
        };

        private Font CreateFirstExisting(string[] candidates, string[] installed)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = candidates[i];
                if (installed != null && Array.IndexOf(installed, name) < 0) continue;

                try
                {
                    Font font = Font.CreateDynamicFontFromOSFont(name, 14);
                    if (font != null)
                    {
                        _createdFonts.Add(font);   
                        return font;
                    }
                }
                catch (Exception)
                {
                    
                }
            }
            return null;
        }

        private static string[] SafeInstalledNames()
        {
            try
            {
                return Font.GetOSInstalledFontNames();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Font GameFont(bool force)
        {
            if (!_gameSearched)
            {
                _gameSearched = true;
                ScanGameFonts();
            }

            if (_gameBest == null) return null;

            int required = ProbeAscii.Length + ProbeCjk.Length;
            
            int ratio = force ? 5 : 9;
            return _gameBestScore >= required * ratio / 10 ? _gameBest : null;
        }

        private void ScanGameFonts()
        {
            _gameBest = null;
            _gameBestScore = -1;

            try
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                for (int i = 0; i < fonts.Length; i++)
                {
                    Font font = fonts[i];
                    if (font == null) continue;

                    if (_createdFonts.Contains(font)) continue;
                    if (IsBuiltInFont(font.name)) continue;

                    int score = Coverage(font, ProbeAscii) + Coverage(font, ProbeCjk);
                    if (score > _gameBestScore)
                    {
                        _gameBestScore = score;
                        _gameBest = font;
                    }
                }
            }
            catch (Exception)
            {
                _gameBest = null;
            }
        }

        private static bool IsBuiltInFont(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name == "Arial" || name == "LegacyRuntime") return true;
            if (name.IndexOf("Arial", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return Array.IndexOf(SystemFontCandidates, name) >= 0;
        }

        private static int Coverage(Font font, string probe)
        {
            int covered = 0;
            for (int i = 0; i < probe.Length; i++)
            {
                try
                {
                    if (font.HasCharacter(probe[i])) covered++;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            return covered;
        }
    }
}
