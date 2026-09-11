using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DadsEZContainers
{
    internal sealed class ContainerSource
    {
        internal Container Container;
        internal Inventory Inventory;
        internal string Prefab;
        internal float Distance;
    }

    internal static class ContainerAccess
    {
        private static readonly List<ContainerSource> Cache = new List<ContainerSource>();
        private static float _cacheUntil;
        private static Vector3 _cacheCenter;
        private static string _cacheContext;

        internal static string CurrentCraftingContext(Player player)
        {
            CraftingStation station = player?.GetCurrentCraftingStation();
            return station != null ? Utils.GetPrefabName(station.gameObject) : string.Empty;
        }

        internal static int Count(Player player, ItemDrop item, int quality, string context)
        {
            if (player == null || item == null) return 0;
            string sharedName = item.m_itemData.m_shared.m_name;
            int total = player.GetInventory().CountItems(sharedName, quality, true);
            foreach (ContainerSource source in GetSources(player, context))
            {
                int count = CountAllowed(source, sharedName, quality, context);
                if (DadsEZContainersPlugin.LeaveOneItem.Value && count > 0) count--;
                total += Math.Max(0, count);
            }
            return total;
        }

        internal static bool HasRequirements(Player player, Piece.Requirement[] requirements, int quality, int multiplier, string context)
        {
            if (requirements == null) return true;
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (!AppliesToStation(requirement, station) || requirement.m_resItem == null) continue;
                int needed = requirement.GetAmount(quality) * multiplier;
                if (needed > 0 && Count(player, requirement.m_resItem, -1, context) < needed) return false;
            }
            return true;
        }

        internal static bool HasRecipeRequirements(Player player, Recipe recipe, int quality, int multiplier)
        {
            if (recipe == null) return false;
            CraftingStation station = player.GetCurrentCraftingStation();
            string context = CurrentCraftingContext(player);
            bool foundOne = false;
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (!AppliesToStation(requirement, station) || requirement.m_resItem == null) continue;
                int needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;
                int bestQualityCount = 0;
                for (int itemQuality = 1; itemQuality <= requirement.m_resItem.m_itemData.m_shared.m_maxQuality; itemQuality++)
                {
                    bestQualityCount = Math.Max(bestQualityCount, Count(player, requirement.m_resItem, itemQuality, context));
                }
                if (bestQualityCount >= needed)
                {
                    foundOne = true;
                    if (recipe.m_requireOnlyOneIngredient) return true;
                }
                else if (!recipe.m_requireOnlyOneIngredient)
                {
                    return false;
                }
            }
            return !recipe.m_requireOnlyOneIngredient || foundOne;
        }

        internal static bool Consume(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
        {
            string context = CurrentCraftingContext(player);
            if (!HasRequirementsAtQuality(player, requirements, qualityLevel, itemQuality, multiplier, context)) return false;
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (!AppliesToStation(requirement, station) || requirement.m_resItem == null) continue;
                int remaining = requirement.GetAmount(qualityLevel) * multiplier;
                if (remaining <= 0) continue;
                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                int inPlayer = player.GetInventory().CountItems(sharedName, itemQuality, true);
                int fromPlayer = Math.Min(remaining, inPlayer);
                if (fromPlayer > 0)
                {
                    player.GetInventory().RemoveItem(sharedName, fromPlayer, itemQuality, true);
                    remaining -= fromPlayer;
                }
                foreach (ContainerSource source in GetSources(player, context))
                {
                    if (remaining <= 0) break;
                    int inContainer = CountAllowed(source, sharedName, itemQuality, context);
                    int reserve = DadsEZContainersPlugin.LeaveOneItem.Value && inContainer > 0 ? 1 : 0;
                    int take = Math.Min(remaining, Math.Max(0, inContainer - reserve));
                    if (take <= 0) continue;
                    ClaimOwnership(source.Container);
                    source.Inventory.RemoveItem(sharedName, take, itemQuality, true);
                    remaining -= take;
                    Log($"Pulled {take} {sharedName} from {source.Prefab} at {source.Distance:0.0}m.");
                }
            }
            Invalidate();
            return true;
        }

        internal static bool TryStage(Player player, IEnumerable<ItemDrop> candidates, string context, out ItemDrop.ItemData staged)
        {
            staged = null;
            if (player == null || candidates == null) return false;
            var names = new HashSet<string>(candidates.Where(item => item != null).Select(item => item.m_itemData.m_shared.m_name));
            ItemDrop.ItemData existing = player.GetInventory().GetAllItems().FirstOrDefault(item => names.Contains(item.m_shared.m_name));
            if (existing != null)
            {
                staged = existing;
                return true;
            }
            foreach (ContainerSource source in GetSources(player, context))
            {
                ItemDrop.ItemData item = source.Inventory.GetAllItems().FirstOrDefault(candidate =>
                    names.Contains(candidate.m_shared.m_name) && Allowed(source, candidate, context) &&
                    (!DadsEZContainersPlugin.LeaveOneItem.Value || source.Inventory.CountItems(candidate.m_shared.m_name, candidate.m_quality, true) > 1));
                if (item == null) continue;
                ItemDrop.ItemData transfer = item.Clone();
                transfer.m_stack = 1;
                if (!player.GetInventory().CanAddItem(transfer, 1)) continue;
                ClaimOwnership(source.Container);
                if (!source.Inventory.RemoveItem(item, 1)) continue;
                if (!player.GetInventory().AddItem(transfer))
                {
                    source.Inventory.AddItem(transfer);
                    continue;
                }
                staged = transfer;
                Invalidate();
                Log($"Staged {transfer.m_shared.m_name} from {source.Prefab} for {context}.");
                return true;
            }
            return false;
        }

        internal static void Invalidate()
        {
            _cacheUntil = 0f;
        }

        private static bool HasRequirementsAtQuality(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier, string context)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (!AppliesToStation(requirement, station) || requirement.m_resItem == null) continue;
                int needed = requirement.GetAmount(qualityLevel) * multiplier;
                if (needed > 0 && Count(player, requirement.m_resItem, itemQuality, context) < needed) return false;
            }
            return true;
        }

        private static bool AppliesToStation(Piece.Requirement requirement, CraftingStation station)
        {
            if (station != null) return station.m_upgrader == requirement.m_upgraderResource;
            return !requirement.m_upgraderResource;
        }

        private static List<ContainerSource> GetSources(Player player, string context)
        {
            Vector3 center = player.transform.position;
            float range = Math.Max(0f, DadsEZContainersPlugin.ContainerRange.Value);
            if (Time.unscaledTime < _cacheUntil && Vector3.Distance(center, _cacheCenter) < 0.5f && _cacheContext == context)
            {
                return Cache;
            }
            Cache.Clear();
            foreach (Container container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (container == null) continue;
                float distance = Vector3.Distance(center, container.transform.position);
                if (distance > range || !Traverse.Create(container).Method("CheckAccess", player.GetPlayerID()).GetValue<bool>()) continue;
                Inventory inventory = container.GetInventory();
                if (inventory == null || ReferenceEquals(inventory, player.GetInventory())) continue;
                Cache.Add(new ContainerSource
                {
                    Container = container,
                    Inventory = inventory,
                    Prefab = Utils.GetPrefabName(container.gameObject),
                    Distance = distance
                });
            }
            Cache.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            _cacheCenter = center;
            _cacheContext = context;
            _cacheUntil = Time.unscaledTime + 0.25f;
            return Cache;
        }

        private static int CountAllowed(ContainerSource source, string sharedName, int quality, string context)
        {
            int total = 0;
            foreach (ItemDrop.ItemData item in source.Inventory.GetAllItems())
            {
                if (item.m_shared.m_name != sharedName || (quality >= 0 && item.m_quality != quality) || !Allowed(source, item, context)) continue;
                total += item.m_stack;
            }
            return total;
        }

        private static bool Allowed(ContainerSource source, ItemDrop.ItemData item, string context)
        {
            RuleDecision station = RulesConfig.Evaluate(context, item);
            if (station == RuleDecision.Allow) return true;
            if (station == RuleDecision.Deny) return false;
            RuleDecision container = RulesConfig.Evaluate(source.Prefab, item);
            return container != RuleDecision.Deny;
        }

        private static void ClaimOwnership(Container container)
        {
            if (container.IsOwner()) return;
            ZNetView view = Traverse.Create(container).Field("m_nview").GetValue<ZNetView>();
            if (view != null && view.IsValid()) view.ClaimOwnership();
        }

        private static void Log(string message)
        {
            if (DadsEZContainersPlugin.DebugLogging.Value) DadsEZContainersPlugin.ModLogger.LogInfo(message);
        }
    }
}
