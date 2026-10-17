using System;
using CuStatsHud.Core;
using HarmonyLib;
using UnityEngine;

namespace CuStatsHud.Patch
{
    
    internal static class GameStatsHooks
    {
        
        public static GameStats Stats;

        public static Body LocalBody;

        private const float BlockRadius = 20f;

        private const float InteractRadius = 60f;

        public static void NoteBlock(Vector2Int pos, ushort block)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;

                WorldGeneration world = WorldGeneration.world;
                if (world != null && world.generatingWorld) return;

                if (!NearLocalPlayer(pos)) return;

                stats.NoteBlock(block);
            }
            catch (Exception)
            {
                
            }
        }

        public static void NoteItemUse(Body user, string itemId)
        {
            try
            {
                GameStats stats = Stats;
                if (stats == null) return;

                if (LocalBody == null || user != LocalBody) return;

                stats.NoteItemUse(itemId);
            }
            catch (Exception)
            {
            }
        }

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

    [HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
    internal static class SetBlockPatch
    {
        
        [HarmonyPostfix]
        private static void Postfix(Vector2Int pos, ushort block)
        {
            GameStatsHooks.NoteBlock(pos, block);
        }
    }

    [HarmonyPatch(typeof(Body), "UseItem")]
    internal static class UseItemPatch
    {
        
        [HarmonyPostfix]
        private static void Postfix(Body __instance, Item item)
        {
            GameStatsHooks.NoteItemUse(__instance, item != null ? item.id : null);
        }
    }

    [HarmonyPatch(typeof(Body), "UseItemInHand")]
    internal static class UseItemInHandPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Body __instance)
        {
            GameStatsHooks.NoteItemUse(__instance, null);
        }
    }

    [HarmonyPatch(typeof(Recipe), "TryMake")]
    internal static class RecipeTryMakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Recipe __instance)
        {
            if (__instance == null || __instance.result == null) return;

            GameStatsHooks.NoteCraft(__instance.result.id);
        }
    }

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
