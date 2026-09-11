using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DadsEZContainers
{
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    internal static class RecipeRequirementPatch
    {
        private static void Postfix(Player __instance, Recipe piece, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (__result || discover || !DadsEZContainersPlugin.Active) return;
            __result = ContainerAccess.HasRecipeRequirements(__instance, piece, qualityLevel, amount);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class BuildRequirementPatch
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (__result || mode != Player.RequirementMode.CanBuild || !DadsEZContainersPlugin.Active || piece == null) return;
            __result = ContainerAccess.HasRequirements(__instance, piece.m_resources, 0, 1,
                piece.m_craftingStation != null ? Utils.GetPrefabName(piece.m_craftingStation.gameObject) : string.Empty);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class ConsumeResourcesPatch
    {
        private static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
        {
            if (!DadsEZContainersPlugin.Active || __instance != Player.m_localPlayer) return true;
            return !ContainerAccess.Consume(__instance, requirements, qualityLevel, itemQuality, multiplier);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class RequirementDisplayPatch
    {
        private static void Postfix(Transform elementRoot, Piece.Requirement req, Player player, int quality, int craftMultiplier)
        {
            if (!DadsEZContainersPlugin.Active || elementRoot == null || req?.m_resItem == null || player == null) return;
            Transform amountTransform = elementRoot.Find("res_amount");
            if (amountTransform == null) return;
            TMP_Text label = amountTransform.GetComponent<TMP_Text>();
            if (label == null) return;
            int required = req.GetAmount(quality) * craftMultiplier;
            if (required <= 0) return;
            string context = ContainerAccess.CurrentCraftingContext(player);
            int inventory = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name, -1, true);
            int total = ContainerAccess.Count(player, req.m_resItem, -1, context);
            string format = DadsEZContainersPlugin.ResourceCostFormat.Value;
            try { label.text = string.Format(format, total, required); }
            catch { label.text = total + "/" + required; }
            if (total < required)
                label.color = DadsEZContainersPlugin.ParseColor(DadsEZContainersPlugin.UnavailableColor.Value, Color.red);
            else if (inventory < required)
                label.color = DadsEZContainersPlugin.ParseColor(DadsEZContainersPlugin.ContainerColor.Value, Color.yellow);
            else
                label.color = DadsEZContainersPlugin.ParseColor(DadsEZContainersPlugin.AvailableColor.Value, Color.white);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class FireplacePatch
    {
        private static void Prefix(Fireplace __instance, Humanoid user)
        {
            Player player = user as Player;
            if (!DadsEZContainersPlugin.Active || player != Player.m_localPlayer || __instance.m_fuelItem == null) return;
            ContainerAccess.TryStage(player, new[] { __instance.m_fuelItem }, Utils.GetPrefabName(__instance.gameObject), out _);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class SmelterFuelPatch
    {
        [System.ThreadStatic] private static bool _filling;

        private static void Prefix(Smelter __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = user as Player;
            if (!DadsEZContainersPlugin.Active || player != Player.m_localPlayer || __instance.m_fuelItem == null) return;
            if (item == null) ContainerAccess.TryStage(player, new[] { __instance.m_fuelItem }, Utils.GetPrefabName(__instance.gameObject), out item);
        }

        private static void Postfix(Smelter __instance, Switch sw, Humanoid user, bool __result)
        {
            Player player = user as Player;
            if (_filling || !__result || !DadsEZContainersPlugin.Active || player != Player.m_localPlayer ||
                !Input.GetKey(DadsEZContainersPlugin.FillAllModifier.Value)) return;
            _filling = true;
            try
            {
                float fuel = Traverse.Create(__instance).Method("GetFuel").GetValue<float>();
                int attempts = Mathf.Max(0, __instance.m_maxFuel - Mathf.CeilToInt(fuel));
                for (int index = 0; index < attempts; index++)
                {
                    if (!ContainerAccess.TryStage(player, new[] { __instance.m_fuelItem }, Utils.GetPrefabName(__instance.gameObject), out ItemDrop.ItemData next)) break;
                    bool added = Traverse.Create(__instance).Method("OnAddFuel", sw, user, next).GetValue<bool>();
                    if (!added) break;
                }
            }
            finally { _filling = false; }
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class SmelterOrePatch
    {
        [System.ThreadStatic] private static bool _filling;

        private static void Prefix(Smelter __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = user as Player;
            if (!DadsEZContainersPlugin.Active || player != Player.m_localPlayer || item != null) return;
            var candidates = new List<ItemDrop>();
            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
                if (conversion?.m_from != null) candidates.Add(conversion.m_from);
            ContainerAccess.TryStage(player, candidates, Utils.GetPrefabName(__instance.gameObject), out item);
        }

        private static void Postfix(Smelter __instance, Switch sw, Humanoid user, bool __result)
        {
            Player player = user as Player;
            if (_filling || !__result || !DadsEZContainersPlugin.Active || player != Player.m_localPlayer ||
                !Input.GetKey(DadsEZContainersPlugin.FillAllModifier.Value)) return;
            var candidates = new List<ItemDrop>();
            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
                if (conversion?.m_from != null) candidates.Add(conversion.m_from);
            _filling = true;
            try
            {
                int queued = Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
                int attempts = Mathf.Max(0, __instance.m_maxOre - queued);
                for (int index = 0; index < attempts; index++)
                {
                    if (!ContainerAccess.TryStage(player, candidates, Utils.GetPrefabName(__instance.gameObject), out ItemDrop.ItemData next)) break;
                    bool added = Traverse.Create(__instance).Method("OnAddOre", sw, user, next).GetValue<bool>();
                    if (!added) break;
                }
            }
            finally { _filling = false; }
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
    internal static class CookingFuelPatch
    {
        [System.ThreadStatic] private static bool _filling;

        private static void Prefix(CookingStation __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = user as Player;
            if (!DadsEZContainersPlugin.Active || player != Player.m_localPlayer || __instance.m_fuelItem == null) return;
            if (item == null) ContainerAccess.TryStage(player, new[] { __instance.m_fuelItem }, Utils.GetPrefabName(__instance.gameObject), out item);
        }

        private static void Postfix(CookingStation __instance, Switch sw, Humanoid user, bool __result)
        {
            Player player = user as Player;
            if (_filling || !__result || !DadsEZContainersPlugin.Active || player != Player.m_localPlayer ||
                !Input.GetKey(DadsEZContainersPlugin.FillAllModifier.Value)) return;
            _filling = true;
            try
            {
                float fuel = Traverse.Create(__instance).Method("GetFuel").GetValue<float>();
                int attempts = Mathf.Max(0, __instance.m_maxFuel - Mathf.CeilToInt(fuel));
                for (int index = 0; index < attempts; index++)
                {
                    if (!ContainerAccess.TryStage(player, new[] { __instance.m_fuelItem }, Utils.GetPrefabName(__instance.gameObject), out ItemDrop.ItemData next)) break;
                    bool added = Traverse.Create(__instance).Method("OnAddFuelSwitch", sw, user, next).GetValue<bool>();
                    if (!added) break;
                }
            }
            finally { _filling = false; }
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnAddFoodSwitch")]
    internal static class CookingFoodPatch
    {
        private static void Prefix(CookingStation __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = user as Player;
            if (!DadsEZContainersPlugin.Active || player != Player.m_localPlayer || item != null) return;
            var candidates = new List<ItemDrop>();
            foreach (CookingStation.ItemConversion conversion in __instance.m_conversion)
                if (conversion?.m_from != null) candidates.Add(conversion.m_from);
            ContainerAccess.TryStage(player, candidates, Utils.GetPrefabName(__instance.gameObject), out item);
        }
    }

    [HarmonyPatch(typeof(Container), "OnContainerChanged")]
    internal static class ContainerChangedPatch
    {
        private static void Postfix() => ContainerAccess.Invalidate();
    }
}
