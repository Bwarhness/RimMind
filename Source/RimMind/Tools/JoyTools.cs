using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class JoyTools
    {
        /// <summary>
        /// Get joy type saturation for all colonists
        /// Parameters: pawn_name (optional, defaults to all)
        /// Returns: Current joy level, joy category, per-JoyKind saturation levels, saturated categories, recommended joy types
        /// </summary>
        public static string GetJoySaturation(string pawnName = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var colonists = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.needs?.joy == null) continue;
                if (pawnName != null && pawn.Name?.ToStringShort != pawnName) continue;

                var needJoy = pawn.needs.joy;
                var joyObject = new JSONObject();
                joyObject["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                joyObject["currentJoyLevel"] = needJoy.CurLevelPercentage.ToString("P0");
                joyObject["currentJoyValue"] = needJoy.CurLevel.ToString("0.00");

                // Joy category (Low/Satisfied/High)
                // RimWorld JoyCategory values: Empty, VeryLow, Low, Satisfied, High, Extreme
                string category = "Satisfied";
                if (needJoy.CurCategory == JoyCategory.Empty || needJoy.CurCategory == JoyCategory.VeryLow)
                    category = "Low";
                else if (needJoy.CurCategory == JoyCategory.High || needJoy.CurCategory == JoyCategory.Extreme)
                    category = "High";
                joyObject["joyCategory"] = category;

                // Joy tolerances (saturation levels per JoyKind) - enumerate all defined joy kinds
                var tolerances = new JSONObject();
                var tolerancesSet = needJoy.tolerances;
                if (tolerancesSet != null)
                {
                    foreach (var jk in DefDatabase<JoyKindDef>.AllDefsListForReading)
                    {
                        tolerances[jk.defName] = tolerancesSet[jk].ToString("P0");
                    }
                }
                joyObject["joyTolerances"] = tolerances;

                // Find saturated categories (>80%)
                var saturatedTypes = new JSONArray();
                var nonSaturatedTypes = new JSONArray();
                if (tolerancesSet != null)
                {
                    // Check each joy kind
                    foreach (var jk in DefDatabase<JoyKindDef>.AllDefsListForReading)
                    {
                        float tolerance = tolerancesSet[jk];
                        if (tolerance >= 0.8f)
                            saturatedTypes.Add(jk.defName);
                        else
                            nonSaturatedTypes.Add(jk.defName);
                    }
                }
                joyObject["saturatedJoyTypes"] = saturatedTypes;
                joyObject["availableJoyTypes"] = nonSaturatedTypes;

                // Traits affecting joy preferences
                var joyTraits = new JSONArray();
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait.def.defName == "Ascetic" || trait.def.defName == "Greedy" ||
                            trait.def.defName == "Neurotic" || trait.def.defName == "Joywalker")
                        {
                            joyTraits.Add(trait.LabelCap.ToString());
                        }
                    }
                }
                if (joyTraits.Count > 0)
                    joyObject["joyAffectedTraits"] = joyTraits;

                colonists.Add(joyObject);
            }

            result["colonists"] = colonists;
            return result.ToString();
        }

        /// <summary>
        /// Analyze colony recreation diversity and gaps
        /// Returns: Available joy sources by type, joy type coverage gaps, colonists with high saturation, recommended buildings
        /// </summary>
        public static string AnalyzeRecreationDiversity()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Find all joy-giving buildings
            var availableJoySources = new JSONObject();
            var presentJoyKindNames = new HashSet<string>();
            var joyBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.joyKind != null)
                .GroupBy(b => b.def.building.joyKind.defName);

            foreach (var group in joyBuildings)
            {
                presentJoyKindNames.Add(group.Key);
                var buildings = new JSONArray();
                foreach (var b in group)
                {
                    var buildingInfo = new JSONObject();
                    buildingInfo["defName"] = b.def.defName;
                    buildingInfo["position"] = string.Format("{0}, {1}", b.Position.x, b.Position.z);
                    buildings.Add(buildingInfo);
                }
                availableJoySources[group.Key] = buildings;
            }
            result["availableJoySources"] = availableJoySources;

            // Find missing joy types
            var allJoyKinds = DefDatabase<JoyKindDef>.AllDefsListForReading;
            var missingJoyKinds = new JSONArray();

            foreach (var jk in allJoyKinds)
            {
                if (!presentJoyKindNames.Contains(jk.defName))
                    missingJoyKinds.Add(jk.defName);
            }
            result["missingJoyTypes"] = missingJoyKinds;

            // Colonists with high saturation
            var saturatedColonists = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.needs?.joy?.tolerances == null) continue;

                var tolerancesSet = pawn.needs.joy.tolerances;
                var highSaturation = new JSONArray();
                foreach (var jk in allJoyKinds)
                {
                    if (tolerancesSet[jk] >= 0.8f)
                        highSaturation.Add(jk.defName);
                }

                if (highSaturation.Count > 0)
                {
                    var colonist = new JSONObject();
                    colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    colonist["saturatedTypes"] = highSaturation;
                    saturatedColonists.Add(colonist);
                }
            }
            result["saturatedColonists"] = saturatedColonists;

            // Recommendations
            var recommendations = new JSONArray();
            if (missingJoyKinds.Count > 0)
            {
                foreach (var missing in missingJoyKinds)
                {
                    string joyType = missing.ToString();
                    var suggestion = new JSONObject();
                    suggestion["joyType"] = joyType;
                    suggestion["buildings"] = GetRecommendedBuildingsForJoyKind(joyType);
                    recommendations.Add(suggestion);
                }
            }
            result["recommendations"] = recommendations;

            return result.ToString();
        }

        /// <summary>
        /// Recommend specific joy activities for a colonist
        /// Parameters: pawn_name
        /// Returns: Current saturation levels, non-saturated joy types, specific available activities, missing recreation types
        /// </summary>
        public static string RecommendJoyActivities(string pawnName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var pawn = map.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name?.ToStringShort == pawnName);

            if (pawn == null)
                return ToolExecutor.JsonError(string.Format("Colonist '{0}' not found.", pawnName));

            if (pawn.needs?.joy == null)
                return ToolExecutor.JsonError(string.Format("Colonist '{0}' has no joy need.", pawnName));

            var result = new JSONObject();
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";

            var needJoy = pawn.needs.joy;
            result["currentJoyLevel"] = needJoy.CurLevelPercentage.ToString("P0");

            // Current saturation
            var tolerancesSet = needJoy.tolerances;
            var allJoyKinds = DefDatabase<JoyKindDef>.AllDefsListForReading;
            var currentSaturation = new JSONObject();
            var saturated = new JSONArray();
            var available = new JSONArray();

            foreach (var jk in allJoyKinds)
            {
                float tolerance = tolerancesSet[jk];
                currentSaturation[jk.defName] = tolerance.ToString("P0");
                if (tolerance >= 0.8f)
                    saturated.Add(jk.defName);
                else
                    available.Add(jk.defName);
            }
            result["saturationLevels"] = currentSaturation;
            result["saturatedJoyTypes"] = saturated;
            result["recommendedJoyTypes"] = available;

            // Available activities for non-saturated types
            var recommendedActivities = new JSONArray();
            var buildings = map.listerBuildings.allBuildingsColonist;

            foreach (string joyType in available)
            {
                var joyKind = allJoyKinds.FirstOrDefault(jk => jk.defName == joyType);
                if (joyKind == null) continue;

                var relevantBuildings = buildings
                    .Where(b => b.def.building?.joyKind == joyKind)
                    .ToList();

                if (relevantBuildings.Count > 0)
                {
                    foreach (var b in relevantBuildings)
                    {
                        var activity = new JSONObject();
                        activity["joyType"] = joyType;
                        activity["building"] = b.def.defName;
                        activity["position"] = string.Format("{0}, {1}", b.Position.x, b.Position.z);
                        recommendedActivities.Add(activity);
                    }
                }
            }
            result["availableActivities"] = recommendedActivities;

            // Missing recreation types for this colonist
            var missingTypes = new JSONArray();
            foreach (string joyType in available)
            {
                var joyKind = allJoyKinds.FirstOrDefault(jk => jk.defName == joyType);
                if (joyKind == null) continue;

                bool hasBuilding = buildings.Any(b => b.def.building?.joyKind == joyKind);
                if (!hasBuilding)
                {
                    var missing = new JSONObject();
                    missing["joyType"] = joyType;
                    missing["suggestedBuildings"] = GetRecommendedBuildingsForJoyKind(joyType);
                    missingTypes.Add(missing);
                }
            }
            result["missingRecreationTypes"] = missingTypes;

            return result.ToString();
        }

        private static string GetRecommendedBuildingsForJoyKind(string joyKind)
        {
            // Map joy types to recommended buildings
            var buildingMap = new Dictionary<string, string[]>
            {
                ["Cerebral"] = new[] { "ChessTable", "Bookshelf" },
                ["Chemical"] = new[] { "DrugLab", "Brewery" },
                ["Dexterity"] = new[] { "BilliardsTable", "Horseshoes", "BoxingRing" },
                ["Gluttonous"] = new[] { "Stove", "Brewery" },
                ["GluttonousMulti"] = new[] { "DiningChair", "Table" },
                ["Gaming"] = new[] { "PokerTable", "Hoopstone", "BilliardsTable" },
                ["Social"] = new[] { "Brewery", "Campfire", "PartySpot" },
                ["Study"] = new[] { "ResearchBench", "DeepDrill" },
                ["Television"] = new[] { "Television", "Telescope" },
                ["Work"] = new[] { "HandLoom", "TailoringBench" }
            };

            if (buildingMap.TryGetValue(joyKind, out var buildings))
            {
                var result = new JSONArray();
                foreach (var b in buildings) result.Add(b);
                return result.ToString();
            }
            return "[]";
        }
    }
}
