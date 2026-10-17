using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 字体解析。
    ///
    /// 为什么要做字形探测：游戏实际用 TextMeshPro 渲染文字，而
    /// <c>Resources.FindObjectsOfTypeAll&lt;Font&gt;()</c> 拿到的 TrueType <c>Font</c> 对象
    /// 往往字形不全。IMGUI 遇到缺字会**逐字回退**到另一个字体渲染，
    /// 于是同一行里有的字母明显更粗、字形也不对——这正是「游戏字体显示异常」的成因。
    /// 因此这里按覆盖率挑选最完整的游戏字体，覆盖率不足就直接交给系统字体。
    ///
    /// 模式：
    ///   auto   —— 系统字体优先（中英文覆盖最全、字重一致），拿不到再退游戏字体；
    ///   system —— 只用系统字体；
    ///   game   —— 优先游戏字体，但必须字形够全，否则回退系统字体。
    /// </summary>
    internal sealed class FontProvider
    {
        public const string ModeAuto = "auto";
        public const string ModeSystem = "system";
        public const string ModeGame = "game";

        /// <summary>字形探测样本：ASCII 全量 + 面板里实际会用到的中文与符号。</summary>
        private const string ProbeAscii =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789%/.:-+()[]<>|≥≤";

        private const string ProbeCjk =
            "心率血氧意识状态正常危急警告注意无数据肢体队友设置刷新前体温疼痛度分钟秒血压大脑完整心情饥饿口渴免疫强度负重移动速度力量韧性智力";

        private readonly Dictionary<string, Font> _resolved = new Dictionary<string, Font>();

        /// <summary>
        /// 本插件自己创建过的系统字体。
        ///
        /// 必须记下来：<c>Resources.FindObjectsOfTypeAll&lt;Font&gt;()</c> 会把这些字体也算进去，
        /// 而它们的字形覆盖率是 100%，扫描「游戏字体」时必然胜出——结果就是
        /// 「先切到系统字体、再切回游戏字体」时拿到的仍是系统字体（切换看起来无效）。
        /// 刻意不在 Invalidate 里清空：历史上创建过的都该被排除。
        /// </summary>
        private readonly HashSet<Font> _createdFonts = new HashSet<Font>();

        private Font _systemFont;
        private bool _systemSearched;
        /// <summary>扫描到的覆盖率最高的游戏字体候选（可能为 null）。</summary>
        private Font _gameBest;
        private int _gameBestScore = -1;
        private bool _gameSearched;

        /// <summary>最近一次解析实际生效的字体名（设置页展示，便于确认到底用了哪个字体）。</summary>
        public string ResolvedName { get; private set; }

        /// <summary>当前解析结果（可能为 null，调用方需容忍）。</summary>
        public Font Resolve(string mode)
        {
            string key = Normalize(mode);

            Font cached;
            if (_resolved.TryGetValue(key, out cached) && cached != null)
            {
                // 命中缓存也要刷新展示名，否则设置页显示的会与实际不符
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
                    // 用户明确选了游戏字体，阈值放宽：只有字形残缺得离谱才回退
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

        /// <summary>配置变更后重新查找。</summary>
        public void Invalidate()
        {
            _resolved.Clear();
            _systemSearched = false;
            _systemFont = null;
            _gameSearched = false;
            _gameBest = null;
            _gameBestScore = -1;
            ResolvedName = null;
            // _createdFonts 刻意保留：那些字体仍然在场景里，仍要被排除
        }

        private static string Normalize(string mode)
        {
            if (string.Equals(mode, ModeSystem, StringComparison.OrdinalIgnoreCase)) return ModeSystem;
            if (string.Equals(mode, ModeGame, StringComparison.OrdinalIgnoreCase)) return ModeGame;
            return ModeAuto;
        }

        // ---------------------------------------------------------------

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

        /// <summary>
        /// 系统字体：按名字从「已安装字体列表」里挑，避免为不存在的字体创建空 Font 对象。
        /// 微软雅黑中英文覆盖完整、字重均匀，作首选。
        /// </summary>
        private Font SystemFont()
        {
            if (_systemSearched) return _systemFont;
            _systemSearched = true;

            string[] installed = SafeInstalledNames();
            _systemFont = installed != null
                ? CreateFirstExisting(SystemFontCandidates, installed)   // 只创建确实存在的
                : CreateFirstExisting(SystemFontCandidates, null);       // 拿不到列表时才逐个尝试
            return _systemFont;
        }

        /// <summary>系统字体候选顺序。</summary>
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
                        _createdFonts.Add(font);   // 记下来，免得被当成「游戏字体」
                        return font;
                    }
                }
                catch (Exception)
                {
                    // 试下一个候选字体
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

        /// <summary>
        /// 游戏字体：在场景/资源里的所有 Font 中挑字形覆盖最全的一个。
        ///
        /// 必须排除：插件自己创建的系统字体、Unity 内置字体（Arial / LegacyRuntime）。
        /// <paramref name="force"/> 为 true（用户明确选了「游戏字体」）时阈值放宽到 60%，
        /// 否则（auto 模式）要求 90% —— 宁可用一个完整字体，也不要一行里混着三四种字重。
        /// </summary>
        private Font GameFont(bool force)
        {
            if (!_gameSearched)
            {
                _gameSearched = true;
                ScanGameFonts();
            }

            if (_gameBest == null) return null;

            int required = ProbeAscii.Length + ProbeCjk.Length;
            // 用户明确选了「游戏字体」时阈值放到 50%（游戏字体常缺部分中文，不该因此被回退，
            // 否则表现就是「切了没反应」）；auto 模式仍要求 90%。
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

                    // 关键：排除插件自建的系统字体，否则它的 100% 覆盖率会让「游戏字体」永远是系统字体
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

            // 系统字体候选名也一律排除：它们不是「游戏字体」
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
