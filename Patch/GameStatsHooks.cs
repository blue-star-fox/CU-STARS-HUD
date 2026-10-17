using System;
using CuStatsHud.Core;
using HarmonyLib;
using UnityEngine;

namespace CuStatsHud.Patch
{
    /// <summary>
    /// 游戏行为采集的桥接点。
    ///
    /// Hook 里抛异常会直接打断游戏主循环，所以这里每个入口都整体 try/catch：
    /// 统计拿不到数据也不能影响玩家游玩。
    ///
    /// Hook 点全部在反编译源码里核对过：
    ///   <c>WorldGeneration.SetBlock(Vector2Int pos, ushort block)</c> —— 方块写入的唯一入口，
    ///       block == 0 是挖掉、&gt; 0 是放置（玩家放置方块走 <c>Item</c> 的 useAction 调用它）。
    ///   <c>Body.UseItem(Item)</c> / <c>Body.UseItemInHand()</c> —— 使用道具。
    ///   <c>OnUse()</c> —— 可交互场景物的统一入口，全游戏共 10 个类实现了它。
    ///   <c>Container.LoadItem/UnloadItem</c> —— 容器存取。
    ///   <c>TraderScript.TryPurchase</c> —— 向商人购买。
    /// </summary>
    internal static class GameStatsHooks
    {
        /// <summary>统计模型（由插件入口注入）。</summary>
        public static GameStats Stats;

        /// <summary>本地玩家的 Body（由 Update 每帧刷新；null 表示当前没有玩家）。</summary>
        public static Body LocalBody;

        /// <summary>
        /// <c>UseItemInHand</c> 里拿不到「被用掉的那一个」的 id，
        /// 这类使用统一归到这个键名，让「明细之和 == 总数」始终成立。
        ///
        /// <b>不能用尖括号</b>：明细行是用富文本拼接的（<c>&lt;color=...&gt;</c>），
        /// <c>&lt;未分类&gt;</c> 会被 Unity 当成未知标签吞掉，界面上直接显示不出这一行。
        /// 这里用全角括号，同时也保证与真实 <c>Item.id</c>（小写标识符）不冲突。
        /// </summary>
        public const string UnknownItemId = "（未分类）";

        /// <summary>上一帧的物品状况指纹，供 <c>UseItemInHand</c> 前后比对。</summary>
        private static string _lastHandSignature;

        /// <summary>最近一次记录真实道具使用的时间（秒），用于与 <c>UseItem</c> 去重。</summary>
        private static float _lastItemUseAt = -999f;

        /// <summary>
        /// <c>UseItem</c> 与 <c>UseItemInHand</c> 若属同一次动作，
        /// 两次调用的间隔通常在同一帧或相邻帧内；0.25 秒足够覆盖，
        /// 又远小于玩家两次真实使用的间隔（连续按键最快也要 0.1 秒以上，
        /// 而这两个方法成对出现时几乎无间隔）。
        /// </summary>
        private const float DuplicateWindow = 0.25f;

        /// <summary>探测失败提示只记一次，避免每帧刷日志。</summary>
        private static bool _probeFailureLogged;

        /// <summary>
        /// 方块统计的有效半径（单位是方块；<c>WorldToBlockPos</c> 是平移映射，1 方块 = 1 世界单位）。
        /// 玩家挖掘 / 放置都在手边（源码里取 5 单位），而世界生成、爆炸造成的方块变化发生在别处，
        /// 用这个半径可以把它们排除掉。
        /// </summary>
        private const float BlockRadius = 20f;

        /// <summary>场景交互的玩家距离上限：再远就不是玩家在操作了（例如 NPC 自己用容器）。</summary>
        private const float InteractRadius = 60f;

        public static void NoteBlock(Vector2Int pos, ushort block)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;
                if (!NearLocalPlayer(pos)) return;

                stats.NoteBlock(block);
            }
            catch (Exception)
            {
                // 统计出问题不影响游戏
            }
        }

        /// <summary>
        /// 道具使用。<paramref name="itemId"/> 为 <c>Item.id</c>（道具的字符串 ID），
        /// 用于按种类统计。
        /// </summary>
        public static void NoteItemUse(Body user, string itemId)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;

                // 只统计本地玩家：NPC / 其他玩家的操作不该计入
                if (LocalBody == null || user != LocalBody) return;

                // 记下时间戳：UseItemInHand 若是同一次动作的另一个阶段，
                // 会据此跳过，避免一次使用被记两次。
                _lastItemUseAt = Time.unscaledTime;

                stats.NoteItemUse(itemId);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// <c>Body.UseItemInHand()</c> 的判定入口：区分「真的用了道具」与「空手挥击」。
        ///
        /// <b>为什么不能只看后置状态：</b>本方法是**后置**补丁，此时动作已经完成。
        /// 「用掉最后一个绷带」的调用结束后，手持槽本来就是空的 ——
        /// 若直接把 <c>ReadHand</c> 的 <c>Empty</c> 当作空挥，就会把真实使用误判掉，
        /// 而这恰恰是最需要记上的那一次。
        ///
        /// 因此判定顺序是：
        ///   ① <b>前后指纹比对优先</b>：<see cref="PrimeHandState"/> 每帧缓存指纹，
        ///      本次调用后重新取一份，两者不同即说明物品真的被动过 → 真实使用；
        ///      两者相同则说明什么都没动 → **空手挥击，直接丢弃、不计数**。
        ///   ② 指纹比对不可用（两级成员都没探到，或取指纹失败）→ 退回读手持成员：
        ///      <c>Present</c> 说明手上还留着东西 → 真实使用；
        ///      <c>Empty</c> 则无法区分「本来就是空手」与「刚把最后一个用掉」，
        ///      按更常见的情形（空手挥击远远多于用掉最后一个）判为空挥。
        ///   ③ 两级都不可用 → 保守放行（记一次使用并归到 <see cref="UnknownItemId"/>），
        ///      宁可偏多也不永久丢光统计，同时只提示一次。
        ///
        /// <b>空手挥击不计数</b>：它既不是道具使用，也不再单列成「空手挥击」，
        /// 直接丢弃，所以「道具使用」恒等于分类明细之和。
        ///
        /// <b>去重：</b><c>UseItem</c> 与 <c>UseItemInHand</c> 可能是同一次动作的两个阶段
        /// （前者带具体 <c>Item</c>，后者是「用手上的东西」的入口）。若两者都无条件计数，
        /// 一次使用会被记两次。因此检查 <see cref="_lastItemUseAt"/>：刚刚记过一次真实
        /// 使用就跳过本次，只把它当作同一次动作的收尾。
        /// </summary>
        public static void NoteItemUseInHand(Body user)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;
                if (LocalBody == null || user != LocalBody) return;

                string after = ItemUseProbe.Snapshot(user);

                // ---- ① 指纹比对优先 ----
                // _lastHandSignature 由本方法自己回写（下方）而不是只靠 Update 采样：
                // 玩家可能在同一帧内连按两次，Update 来不及插入采样，
                // 那样第二次比对会拿「第一次之后」当基准而检测不到变化。
                bool? touched = ItemUseProbe.Changed(_lastHandSignature, after);
                if (after != null) _lastHandSignature = after;

                bool used;
                if (touched == true)
                {
                    // 物品真的被动过（被消耗 / 被替换）→ 真实使用
                    used = true;
                }
                else if (touched == false)
                {
                    // 什么都没变 → 空手挥击
                    used = false;
                }
                else
                {
                    // ---- ② 退回读手持成员 ----
                    HandItemState state = ItemUseProbe.ReadHand(user);

                    if (state == HandItemState.Present)
                    {
                        used = true;
                    }
                    else if (state == HandItemState.Empty)
                    {
                        // 无法区分「本来就空手」与「刚用掉最后一个」，
                        // 按更常见的情形判为空挥。
                        used = false;
                    }
                    else
                    {
                        // ---- ③ 两级都不可用：保守放行 ----
                        used = true;

                        if (!_probeFailureLogged)
                        {
                            _probeFailureLogged = true;
                            Debug.LogWarning(
                                "CU Stats HUD：未能定位 Body 的手持物品成员，"
                                + "「空手挥击」与「道具使用」暂时无法区分（道具使用会偏多）。"
                                + "可在 Core/ItemUseProbe.cs 的 HandSlotNames 里补充字段名。");
                        }
                    }
                }

                // 空手挥击：直接丢弃，既不计数也不分类
                if (!used) return;

                // 与 UseItem 的同动作去重
                float now = Time.unscaledTime;
                if (now - _lastItemUseAt < DuplicateWindow)
                {
                    return;
                }

                _lastItemUseAt = now;
                stats.NoteItemUse(UnknownItemId);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 每帧由插件入口调用，缓存一份「当前的物品状况指纹」。
        ///
        /// 用途：<c>UseItemInHand</c> 的后置补丁只能看到**调用之后**的状态，
        /// 「用掉最后一个」时后置状态已是空手，单看后置状态会误判成空挥。
        /// 这里持续保留最近一次采样的指纹，比对时用它作为「动作前」的近似值。
        ///
        /// <b>时间精度说明：</b>采样发生在 <c>Update</c>，而玩家的使用动作可能在同一帧的
        /// 稍后时刻（或下一帧）才发生，因此缓存值表示的是「上一次 Update 之后」的状态。
        /// 对判定没有实质影响：空手挥击不会改变任何物品状态，指纹必然相同；
        /// 真实使用必然改变指纹。两种情况都不需要严格的动作前快照。
        /// </summary>
        public static void PrimeHandState(Body body)
        {
            try
            {
                if (body == null)
                {
                    _lastHandSignature = null;
                    return;
                }
                _lastHandSignature = ItemUseProbe.Snapshot(body);
            }
            catch (Exception)
            {
                _lastHandSignature = null;
            }
        }

        /// <summary>制作（配方名用于分类）。</summary>
        public static void NoteCraft(string recipeName)
        {
            try
            {
                GameStats stats = Stats;
                if (stats != null) stats.NoteCraft(recipeName);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>可交互场景物（各类机器的 OnUse）。</summary>
        public static void NoteInteraction(Component source)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;
                if (!NearLocalPlayer(source)) return;

                stats.NoteInteraction();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>容器存取。</summary>
        public static void NoteContainerOp(Component source)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;
                if (!NearLocalPlayer(source)) return;

                stats.NoteContainerOp();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>向商人购买。</summary>
        public static void NotePurchase(Component source)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;

                stats.NotePurchase();
            }
            catch (Exception)
            {
            }
        }

        // ---------------------------------------------------------------

        private static bool NearLocalPlayer(Vector2Int blockPos)
        {
            Body body = LocalBody;
            if (body == null) return false;

            WorldGeneration world = WorldGeneration.world;
            if (world == null) return false;

            Vector2Int player = world.WorldToBlockPos(body.transform.position);
            float dx = blockPos.x - player.x;
            float dy = blockPos.y - player.y;
            return dx * dx + dy * dy <= BlockRadius * BlockRadius;
        }

        private static bool NearLocalPlayer(Component source)
        {
            Body body = LocalBody;
            if (body == null) return false;
            if (source == null) return false;

            Vector3 target = source.transform.position;
            Vector3 self = body.transform.position;
            float dx = target.x - self.x;
            float dy = target.y - self.y;
            return dx * dx + dy * dy <= InteractRadius * InteractRadius;
        }
    }

    // ===================================================================
    // 方块：挖掘 / 放置
    // ===================================================================

    [HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
    internal static class SetBlockPatch
    {
        // 参数名必须与游戏源码一致：SetBlock(Vector2Int pos, ushort block)
        [HarmonyPostfix]
        private static void Postfix(Vector2Int pos, ushort block)
        {
            GameStatsHooks.NoteBlock(pos, block);
        }
    }

    // ===================================================================
    // 道具使用
    // ===================================================================

    [HarmonyPatch(typeof(Body), "UseItem")]
    internal static class UseItemPatch
    {
        // 参数名必须与游戏源码一致：UseItem(Item item)
        [HarmonyPostfix]
        private static void Postfix(Body __instance, Item item)
        {
            // item 为非 null 才是「真的用了一个道具」。
            // item 为 null 说明这次没有动用任何物品（空手挥击走的就是这条路），
            // 直接丢弃、不计数 —— 空挥既不算道具使用，也不再单独统计。
            if (item == null) return;

            GameStatsHooks.NoteItemUse(__instance, item.id);
        }
    }

    /// <summary>
    /// 使用手上的物品。
    ///
    /// <b>关键：没有可用物品时游戏会把这次调用变成一次空手挥击。</b>
    /// 方法照常返回、后置补丁照常触发，但那一次并没有动用任何道具。
    /// 如果无差别计数，「道具使用」总数里就会掺进大量空挥，
    /// 与按 id 分类的明细之和永远对不上。
    ///
    /// 判定依据见 <see cref="ItemUseProbe"/>：<c>Body.handSlot</c> 只是槽位索引，
    /// 必须配合 <c>Body.GetItem(int)</c> 才能拿到物品。
    /// </summary>
    [HarmonyPatch(typeof(Body), "UseItemInHand")]
    internal static class UseItemInHandPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Body __instance)
        {
            GameStatsHooks.NoteItemUseInHand(__instance);
        }
    }

    /// <summary>制作：<c>Recipe.TryMake()</c> 是制作的实际执行点。</summary>
    [HarmonyPatch(typeof(Recipe), "TryMake")]
    internal static class RecipeTryMakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Recipe __instance)
        {
            if (__instance == null) return;

            // fullName 为空时退回 simpleName，保证明细里始终有可读名字
            string name = !string.IsNullOrEmpty(__instance.fullName)
                ? __instance.fullName
                : __instance.simpleName;
            GameStatsHooks.NoteCraft(name);
        }
    }

    // ===================================================================
    // 可交互场景物：全游戏实现 OnUse() 的一共这 10 个类
    // ===================================================================

    [HarmonyPatch(typeof(BatteryRecharger), "OnUse")]
    internal static class OnUseBatteryRechargerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BatteryRecharger __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(BioTerminalScript), "OnUse")]
    internal static class OnUseBioTerminalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BioTerminalScript __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(DrillPod), "OnUse")]
    internal static class OnUseDrillPodPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DrillPod __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(LifepodButton), "OnUse")]
    internal static class OnUseLifepodPatch
    {
        [HarmonyPostfix]
        private static void Postfix(LifepodButton __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(Openable), "OnUse")]
    internal static class OnUseOpenablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Openable __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(ScrapEaterScript), "OnUse")]
    internal static class OnUseScrapEaterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ScrapEaterScript __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(SurvivorNote), "OnUse")]
    internal static class OnUseSurvivorNotePatch
    {
        [HarmonyPostfix]
        private static void Postfix(SurvivorNote __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(TerminalScript), "OnUse")]
    internal static class OnUseTerminalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TerminalScript __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(TraderScript), "OnUse")]
    internal static class OnUseTraderPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TraderScript __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    [HarmonyPatch(typeof(TutorialCraftButtonBehaviour), "OnUse")]
    internal static class OnUseTutorialCraftPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TutorialCraftButtonBehaviour __instance) { GameStatsHooks.NoteInteraction(__instance); }
    }

    // ===================================================================
    // 容器 / 交易
    // ===================================================================

    [HarmonyPatch(typeof(Container), "LoadItem")]
    internal static class ContainerLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Container __instance) { GameStatsHooks.NoteContainerOp(__instance); }
    }

    [HarmonyPatch(typeof(Container), "UnloadItem")]
    internal static class ContainerUnloadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Container __instance) { GameStatsHooks.NoteContainerOp(__instance); }
    }

    [HarmonyPatch(typeof(TraderScript), "TryPurchase")]
    internal static class TraderPurchasePatch
    {
        [HarmonyPostfix]
        private static void Postfix(TraderScript __instance) { GameStatsHooks.NotePurchase(__instance); }
    }
}
