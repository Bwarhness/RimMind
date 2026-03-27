using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimMind.API;

namespace RimMind.Tools
{
    /// <summary>
    /// Shared helper methods used by multiple building tool classes.
    /// Extracted to avoid duplication between BuildingTools, BuildingQueryTools, and BuildingDeconstructTools.
    /// </summary>
    public static class BuildingHelpers
    {
        /// <summary>
        /// LLMs sometimes send JSON arrays as double-encoded strings -- unwrap them.
        /// Used by RemoveBuilding and ApproveBuildings.
        /// </summary>
        public static JSONNode UnwrapStringArray(JSONNode node)
        {
            if (node != null && node.IsString)
            {
                try
                {
                    var parsed = JSONNode.Parse(node.Value);
                    if (parsed != null && parsed.IsArray) return parsed;
                }
                catch { }
            }
            return node;
        }

        /// <summary>
        /// Resolve a building def by exact or fuzzy name match.
        /// </summary>
        public static ThingDef ResolveBuildingDef(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && def.category == ThingCategory.Building
                && !typeof(Blueprint).IsAssignableFrom(def.thingClass)
                && !typeof(Frame).IsAssignableFrom(def.thingClass))
            {
                return def;
            }

            // Fuzzy: case-insensitive match across all building defs
            foreach (var candidate in DefDatabase<ThingDef>.AllDefs)
            {
                if (candidate.category != ThingCategory.Building) continue;
                if (typeof(Blueprint).IsAssignableFrom(candidate.thingClass)) continue;
                if (typeof(Frame).IsAssignableFrom(candidate.thingClass)) continue;
                if (string.Equals(candidate.defName, defName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Find similar building defs when a lookup fails.
        /// </summary>
        public static string FindSimilarBuildings(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;

            var matches = new List<string>();
            string lower = defName.ToLower();

            foreach (var candidate in DefDatabase<ThingDef>.AllDefs)
            {
                if (candidate.category != ThingCategory.Building) continue;
                if (candidate.designationCategory == null) continue;
                if (typeof(Blueprint).IsAssignableFrom(candidate.thingClass)) continue;
                if (typeof(Frame).IsAssignableFrom(candidate.thingClass)) continue;

                if (candidate.defName.ToLower().Contains(lower)
                    || (candidate.label != null && candidate.label.ToLower().Contains(lower)))
                {
                    matches.Add(candidate.defName);
                    if (matches.Count >= 3) break;
                }
            }

            return matches.Count > 0 ? string.Join(", ", matches) : null;
        }

        /// <summary>
        /// Suggest common stuff types for a building that requires materials.
        /// </summary>
        public static string GetStuffHint(ThingDef def)
        {
            if (def.stuffCategories == null || def.stuffCategories.Count == 0)
                return null;

            var hints = new List<string>();
            foreach (var cat in def.stuffCategories)
            {
                string catName = cat.defName;
                if (catName.Contains("Stony"))
                    hints.AddRange(new[] { "BlocksGranite", "BlocksSandstone", "BlocksMarble" });
                else if (catName.Contains("Metallic"))
                    hints.AddRange(new[] { "Steel", "Plasteel", "Silver" });
                else if (catName.Contains("Woody"))
                    hints.Add("WoodLog");
            }

            if (hints.Count == 0) return null;
            // Deduplicate and take up to 3
            var unique = new List<string>();
            foreach (var h in hints)
            {
                if (!unique.Contains(h))
                    unique.Add(h);
                if (unique.Count >= 3) break;
            }
            return string.Join(", ", unique);
        }
    }
}
