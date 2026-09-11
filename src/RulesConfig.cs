using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DadsEZContainers
{
    internal enum RuleDecision
    {
        Unspecified,
        Allow,
        Deny
    }

    internal sealed class PullRule
    {
        internal readonly List<string> Exclude = new List<string>();
        internal readonly List<string> Include = new List<string>();
    }

    internal static class RulesConfig
    {
        private static readonly Dictionary<string, List<string>> Groups =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PullRule> Rules =
            new Dictionary<string, PullRule>(StringComparer.Ordinal);

        private const string DefaultRules = @"# DadsEZContainers pull rules.
# Unlisted containers and stations allow all items.
# Entries may be item prefab names or group names.

groups:
  BuildingMaterials:
    - Wood
    - Stone
    - FineWood
    - CoreWood

# Example:
# piece_chest_wood:
#   exclude:
#     - Food
#   includeOverride:
#     - Carrot
";

        internal static void EnsureFile(string path)
        {
            if (File.Exists(path))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, DefaultRules);
        }

        internal static void Load(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var rules = new Dictionary<string, PullRule>(StringComparer.Ordinal);
            string section = null;
            string group = null;
            string target = null;
            string list = null;

            foreach (string original in lines)
            {
                string withoutComment = StripComment(original);
                if (string.IsNullOrWhiteSpace(withoutComment))
                {
                    continue;
                }
                int indent = withoutComment.TakeWhile(char.IsWhiteSpace).Count();
                string value = withoutComment.Trim();

                if (indent == 0 && value.EndsWith(":"))
                {
                    string key = value.Substring(0, value.Length - 1).Trim();
                    if (string.Equals(key, "groups", StringComparison.OrdinalIgnoreCase))
                    {
                        section = "groups";
                        target = null;
                    }
                    else
                    {
                        section = "rules";
                        target = key;
                        rules[target] = new PullRule();
                    }
                    group = null;
                    list = null;
                    continue;
                }

                if (section == "groups" && indent >= 2 && !value.StartsWith("-") && value.EndsWith(":"))
                {
                    group = value.Substring(0, value.Length - 1).Trim();
                    groups[group] = new List<string>();
                    continue;
                }
                if (section == "groups" && group != null && value.StartsWith("-"))
                {
                    AddValue(groups[group], value.Substring(1));
                    continue;
                }

                if (section == "rules" && target != null && indent >= 2 && value.Contains(":"))
                {
                    int separator = value.IndexOf(':');
                    list = value.Substring(0, separator).Trim();
                    string inline = value.Substring(separator + 1).Trim().Trim('[', ']');
                    if (inline.Length > 0)
                    {
                        foreach (string entry in inline.Split(','))
                        {
                            AddRuleValue(rules[target], list, entry);
                        }
                    }
                    continue;
                }
                if (section == "rules" && target != null && list != null && value.StartsWith("-"))
                {
                    AddRuleValue(rules[target], list, value.Substring(1));
                }
            }

            Groups.Clear();
            foreach (var pair in groups) Groups[pair.Key] = pair.Value;
            Rules.Clear();
            foreach (var pair in rules) Rules[pair.Key] = pair.Value;
            DadsEZContainersPlugin.ModLogger?.LogInfo($"Loaded {Rules.Count} container/station rules and {Groups.Count} custom groups.");
        }

        internal static RuleDecision Evaluate(string target, ItemDrop.ItemData item)
        {
            if (string.IsNullOrEmpty(target) || item == null || !Rules.TryGetValue(target, out PullRule rule))
            {
                return RuleDecision.Unspecified;
            }
            if (MatchesAny(rule.Include, item)) return RuleDecision.Allow;
            if (MatchesAny(rule.Exclude, item)) return RuleDecision.Deny;
            return RuleDecision.Unspecified;
        }

        private static bool MatchesAny(IEnumerable<string> entries, ItemDrop.ItemData item)
        {
            foreach (string entry in entries)
            {
                if (Matches(entry, item, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Matches(string entry, ItemDrop.ItemData item, HashSet<string> visited)
        {
            string prefab = item.m_dropPrefab != null ? Utils.GetPrefabName(item.m_dropPrefab) : string.Empty;
            if (string.Equals(entry, prefab, StringComparison.Ordinal)) return true;
            if (string.Equals(entry, "All", StringComparison.OrdinalIgnoreCase)) return true;
            if (Groups.TryGetValue(entry, out List<string> members) && visited.Add(entry))
            {
                return members.Any(member => Matches(member, item, visited));
            }

            ItemDrop.ItemData.SharedData data = item.m_shared;
            switch (entry.ToLowerInvariant())
            {
                case "food": return data.m_food > 0f && data.m_foodStamina > 0f;
                case "potion": return data.m_food > 0f && data.m_foodStamina == 0f;
                case "fish": return data.m_itemType == ItemDrop.ItemData.ItemType.Fish;
                case "equipment": return data.m_itemType == ItemDrop.ItemData.ItemType.Torch;
                case "trinkets": return data.m_itemType == ItemDrop.ItemData.ItemType.Trinket;
                case "swords": return data.m_skillType == Skills.SkillType.Swords;
                case "bows": return data.m_skillType == Skills.SkillType.Bows;
                case "crossbows": return data.m_skillType == Skills.SkillType.Crossbows;
                case "axes": return data.m_skillType == Skills.SkillType.Axes;
                case "clubs": return data.m_skillType == Skills.SkillType.Clubs;
                case "knives": return data.m_skillType == Skills.SkillType.Knives;
                case "pickaxes": return data.m_skillType == Skills.SkillType.Pickaxes;
                case "polearms": return data.m_skillType == Skills.SkillType.Polearms;
                case "spears": return data.m_skillType == Skills.SkillType.Spears;
                case "boss trophy": return data.m_itemType == ItemDrop.ItemData.ItemType.Trophy && IsBossTrophy(data.m_name);
                case "trophy": return data.m_itemType == ItemDrop.ItemData.ItemType.Trophy && !IsBossTrophy(data.m_name);
                case "ores": return IsSmelterMaterial(item, true, "smelter", "blastfurnace");
                case "metals": return IsSmelterMaterial(item, false, "smelter", "blastfurnace");
                case "woods": return IsSmelterMaterial(item, true, "charcoal_kiln");
                case "crops": return IsCultivationMaterial(item, true);
                case "seeds": return IsCultivationMaterial(item, false);
                default: return false;
            }
        }

        private static bool IsBossTrophy(string sharedName)
        {
            string name = (sharedName ?? string.Empty).ToLowerInvariant();
            return new[] { "eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "seekerqueen", "fader" }
                .Any(name.EndsWith);
        }

        private static bool IsSmelterMaterial(ItemDrop.ItemData item, bool input, params string[] stationPrefabs)
        {
            if (ZNetScene.instance == null || item == null) return false;
            string itemName = item.m_shared.m_name;
            foreach (string stationPrefab in stationPrefabs)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(stationPrefab);
                Smelter smelter = prefab != null ? prefab.GetComponent<Smelter>() : null;
                if (smelter == null) continue;
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                {
                    ItemDrop candidate = input ? conversion?.m_from : conversion?.m_to;
                    if (candidate != null && candidate.m_itemData.m_shared.m_name == itemName) return true;
                }
            }
            return false;
        }

        private static bool IsCultivationMaterial(ItemDrop.ItemData item, bool multipleYield)
        {
            if (ObjectDB.instance == null || item == null) return false;
            string itemName = item.m_shared.m_name;
            var tables = new HashSet<PieceTable>();
            foreach (GameObject itemPrefab in ObjectDB.instance.m_items)
            {
                ItemDrop drop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
                if (drop?.m_itemData?.m_shared?.m_buildPieces != null) tables.Add(drop.m_itemData.m_shared.m_buildPieces);
            }
            foreach (PieceTable table in tables)
            {
                foreach (GameObject piecePrefab in table.m_pieces)
                {
                    Piece piece = piecePrefab != null ? piecePrefab.GetComponent<Piece>() : null;
                    Plant plant = piecePrefab != null ? piecePrefab.GetComponent<Plant>() : null;
                    if (piece == null || plant == null || !piece.m_resources.Any(requirement =>
                            requirement?.m_resItem?.m_itemData?.m_shared?.m_name == itemName)) continue;
                    foreach (GameObject grownPrefab in plant.m_grownPrefabs)
                    {
                        Pickable pickable = grownPrefab != null ? grownPrefab.GetComponent<Pickable>() : null;
                        if (pickable != null && (multipleYield ? pickable.m_amount > 1 : pickable.m_amount == 1)) return true;
                    }
                }
            }
            return false;
        }

        private static string StripComment(string value)
        {
            int index = value.IndexOf('#');
            return index >= 0 ? value.Substring(0, index) : value;
        }

        private static void AddValue(List<string> destination, string value)
        {
            value = value.Trim().Trim('"', '\'');
            if (value.Length > 0) destination.Add(value);
        }

        private static void AddRuleValue(PullRule rule, string list, string value)
        {
            if (string.Equals(list, "exclude", StringComparison.OrdinalIgnoreCase)) AddValue(rule.Exclude, value);
            else if (string.Equals(list, "includeOverride", StringComparison.OrdinalIgnoreCase)) AddValue(rule.Include, value);
        }
    }
}
