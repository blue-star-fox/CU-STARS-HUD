using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 图标总入口：优先用内嵌 emoji，缺失时回退到程序化几何图标。
    ///
    /// emoji 自带颜色、不做着色（分级由卡片底色/左侧色条/数值颜色表达）；
    /// 程序化图标则是按分级色现场生成的，回退时仍然保持可辨识。
    /// </summary>
    internal sealed class IconProvider : IDisposable
    {
        /// <summary>图标种类 → Twemoji 码点。键名与 Core 层写入的 Icon 字段一致。</summary>
        private static readonly Dictionary<string, string> KindToEmoji = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // 循环系统
            { "heart", "1fac0" },        // 🫀 解剖心脏
            { "bp", "1fa7a" },           // 🩺 听诊器（血压）
            { "oxygen", "1f4a8" },       // 💨 气流（血氧）
            { "blood", "1fa78" },        // 🩸 血滴（血量/出血）
            { "viscosity", "1f9ea" },    // 🧪 试管（血粘度）
            { "fibrillation", "26a1" },  // ⚡ 高压（心颤）
            { "lungs", "1fac1" },        // 🫁 肺（呼吸/血胸/肺栓塞）
            // 神经 / 感知
            { "conscious", "1f440" },    // 👀 眼睛（意识）
            { "brain", "1f9e0" },        // 🧠 脑（大脑完整度/中风）
            { "pain", "1f62b" },         // 😫 痛苦表情（疼痛）
            { "painshock", "1f915" },    // 🤕 头缠绷带（疼痛性休克）
            { "shock", "1f4a5" },        // 💥 冲击（休克）
            { "mood", "1f60a" },         // 😊 微笑（心情）
            { "energy", "1f50b" },       // 🔋 电池（精力/体力）
            { "bandage", "1fa79" },      // 🩹 创可贴（组织损伤）
            // 代谢 / 内环境
            { "food", "1f35e" },         // 🍞 面包（饥饿）
            { "water", "1f4a7" },        // 💧 水滴（口渴/潮湿）
            { "thermo", "1f321" },       // 🌡 温度计（体温）
            { "sick", "1f912" },         // 🤒 病容（疾病程度）
            { "sepsis", "1f9a0" },       // 🦠 病菌（败血性休克）
            { "rad", "2622" },           // ☢ 辐射（辐射病）
            { "skull", "2620" },         // ☠ 骷髅（毒素）
            { "dirty", "1f9fc" },        // 🧼 皂（污浊）
            { "hearing", "1f9bb" },      // 🦻 助听耳（听力损伤）
            { "immune", "1f489" },       // 💉 注射器（免疫）
            // 体能 / 技能
            { "strength", "1f4aa" },     // 💪 肌肉（力量 STR）
            { "shield", "1f6e1" },       // 🛡 盾（韧性 RES / 躯干）
            { "intellect", "1f4a1" },    // 💡 灯泡（智力 INT）
            { "sparkle", "2728" },       // ✨ 闪光（技能通用）
            { "weight", "2696" },        // ⚖ 天平（负重）
            { "walk", "1f3c3" },         // 🏃 跑者（移动速度）
            // 肢体
            { "arm", "1f9be" },          // 🦾 机械臂
            { "leg", "1f9b5" },          // 🦵 腿
            { "bone", "1f9b4" },         // 🦴 骨头
            // 界面
            { "vitals", "1f4ca" },       // 📊 图表（生命体征）
            { "limbs", "1f4cb" },        // 📋 剪贴板（肢体）
            { "mates", "1f465" },        // 👥 人群（队友）
            { "settings", "2699" },      // ⚙ 齿轮（设置）
            { "stats", "1f4ca" },        // 📊 条形图（统计）
            { "warning", "26a0" }        // ⚠ 警告（告警横幅）
        };

        private readonly EmojiIcons _emoji;
        private readonly IconFactory _procedural;

        /// <summary>
        /// emoji 是 128×128 的独立贴图，与字号/缩放无关，
        /// 因此跨「样式重建」共享一份缓存：拖动缩放滑条时不会反复解码 PNG，
        /// 只在插件销毁时统一释放。
        /// </summary>
        private static EmojiIcons _sharedEmoji;

        public IconProvider(int proceduralIconSize)
        {
            if (_sharedEmoji == null)
            {
                _sharedEmoji = new EmojiIcons();
                _sharedEmoji.Preload(KindToEmoji.Values);
            }

            _emoji = _sharedEmoji;
            _procedural = new IconFactory(proceduralIconSize);
        }

        /// <summary>插件销毁时释放共享的 emoji 缓存。</summary>
        public static void DisposeShared()
        {
            if (_sharedEmoji == null) return;
            _sharedEmoji.Dispose();
            _sharedEmoji = null;
        }

        /// <summary>
        /// 是否显示 emoji 图标。关闭后**不显示任何图标**（也不回退到程序化图标），
        /// 由设置页的「显示 emoji 图标」开关控制。
        /// </summary>
        public static bool EmojiEnabled = true;

        /// <summary>取 emoji 图标；没有对应 emoji（或已关闭图标）时返回 null。</summary>
        public Texture2D GetEmoji(string kind)
        {
            if (!EmojiEnabled) return null;

            string code;
            if (string.IsNullOrEmpty(kind) || !KindToEmoji.TryGetValue(kind, out code)) return null;
            return _emoji.Get(code);
        }

        /// <summary>
        /// 取图标：优先 emoji，缺失时回退程序化图标（按 <paramref name="proceduralTint"/> 着色）。
        /// 关闭 emoji 后直接返回 null —— 调用方对 null 都是“不画图标”，因此界面会整体无图标。
        /// </summary>
        public Texture2D Get(string kind, Color proceduralTint)
        {
            if (!EmojiEnabled) return null;

            Texture2D emoji = GetEmoji(kind);
            if (emoji != null) return emoji;
            return _procedural.Get(kind, proceduralTint);
        }

        /// <summary>
        /// 取程序化图标，**不受 emoji 开关影响**。
        /// 专供关闭按钮的 ✕ 这类功能图标：它们不属于「装饰性小图标」，
        /// 关掉 emoji 后也必须照常显示。
        /// </summary>
        public Texture2D GetProcedural(string kind, Color tint)
        {
            return _procedural.Get(kind, tint);
        }

        /// <summary>该种类是否用的是 emoji（emoji 不应再被着色）。</summary>
        public bool IsEmoji(string kind)
        {
            if (!EmojiEnabled) return false;
            return GetEmoji(kind) != null;
        }

        public void Dispose()
        {
            // 只释放随缩放/字号变化重建的程序化图标；共享 emoji 由 DisposeShared 统一释放
            _procedural.Dispose();
        }
    }
}
