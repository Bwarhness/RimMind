using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimMind.API;
using static RimMind.Tools.BuildingHelpers;

namespace RimMind.Tools
{
    /// <summary>
    /// Building query tools — read-only operations for listing and inspecting buildings.
    /// These tools are called by the LLM to discover buildings, get details, and check requirements.
    /// </summary>
    public static class BuildingQueryTools
    {
        private static readonly List<string> CachedPlaceWorkerNotes = new List<string>();

        public static string ListBuildable(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string categoryFilter = args?["category"]?.Value;

            var buildings = new List<ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.category != ThingCategory.Building) continue;
                if (def.designationCategory == null) continue;
                if (typeof(Blueprint).IsAssignableFrom(def.thingClass)) continue;
                if (typeof(Frame).IsAssignableFrom(def.thingClass)) continue;
                if (categoryFilter != null && !string.Equals(def.designationCategory.defName, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                buildings.Add(def);
            }

            buildings.Sort((a, b) =>
            {
                int catCmp = string.Compare(a.designationCategory.defName, b.designationCategory.defName, StringComparison.Ordinal);
                if (catCmp != 0) return catCmp;
                return string.Compare(a.label, b.label, StringComparison.Ordinal);
            });

            var result = new JSONObject();
            result["total"] = buildings.Count;

            if (categoryFilter == null)
            {
                var catCounts = new JSONObject();
                foreach (var def in buildings)
                {
                    string cat = def.designationCategory.defName;
                    catCounts[cat] = (catCounts[cat]?.AsInt ?? 0) + 1;
                }
                result["categories"] = catCounts;
            }

            var arr = new JSONArray();
            foreach (var def in buildings)
            {
                var entry = new JSONObject();
                entry["defName"] = def.defName;
                entry["label"] = def.label;
                if (categoryFilter == null)
                    entry["category"] = def.designationCategory.defName;
                string size = def.size.x + "x" + def.size.z;
                if (size != "1x1") entry["size"] = size;
                if (def.MadeFromStuff)
                {
                    entry["stuff"] = true;
                    string hint = GetStuffHint(def);
                    if (hint != null)
                        entry["stuffHint"] = hint;
                }
                if (def.researchPrerequisites != null && def.researchPrerequisites.Count > 0)
                {
                    var missing = def.researchPrerequisites.Where(r => !r.IsFinished).ToList();
                    if (missing.Count > 0)
                    {
                        var research = new JSONArray();
                        foreach (var r in missing)
                            research.Add(r.defName);
                        entry["locked_research"] = research;
                    }
                }
                arr.Add(entry);
            }
            result["buildings"] = arr;
            return result.ToString();
        }

        public static string GetBuildingInfo(JSONNode args)
        {
            if (args == null || string.IsNullOrEmpty(args["defName"]?.Value))
                return ToolExecutor.JsonError("'defName' is required.");

            var def = ResolveBuildingDef(args["defName"].Value);
            if (def == null)
            {
                string suggestions = FindSimilarBuildings(args["defName"].Value);
                string msg = "Building not found: " + args["defName"].Value;
                if (suggestions != null)
                    msg += ". Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            var result = new JSONObject();
            result["defName"] = def.defName;
            result["label"] = def.label;
            if (!string.IsNullOrEmpty(def.description))
                result["description"] = def.description;
            result["size"] = def.size.x + "x" + def.size.z;
            if (def.designationCategory != null)
                result["category"] = def.designationCategory.defName;
            result["rotatable"] = def.rotatable;

            if (def.MadeFromStuff)
            {
                result["madeFromStuff"] = true;
                result["costStuffCount"] = def.costStuffCount;
                if (def.stuffCategories != null)
                {
                    var cats = new JSONArray();
                    foreach (var sc in def.stuffCategories)
                        cats.Add(sc.defName);
                    result["stuffCategories"] = cats;
                }
                var stuffList = new JSONArray();
                foreach (var stuffDef in DefDatabase<ThingDef>.AllDefs)
                {
                    if (!stuffDef.IsStuff) continue;
                    if (stuffDef.stuffProps?.categories == null) continue;
                    if (def.stuffCategories != null)
                    {
                        foreach (var cat in stuffDef.stuffProps.categories)
                        {
                            if (def.stuffCategories.Contains(cat))
                            {
                                stuffList.Add(stuffDef.defName);
                                break;
                            }
                        }
                    }
                }
                result["availableStuffs"] = stuffList;
            }

            if (def.costList != null && def.costList.Count > 0)
            {
                var costs = new JSONObject();
                foreach (var cost in def.costList)
                    costs[cost.thingDef.defName] = cost.count;
                result["costList"] = costs;
            }

            if (def.statBases != null && def.statBases.Count > 0)
            {
                var stats = new JSONObject();
                foreach (var stat in def.statBases)
                    stats[stat.stat.defName] = (float)Math.Round(stat.value, 2);
                result["stats"] = stats;
            }

            if (def.researchPrerequisites != null && def.researchPrerequisites.Count > 0)
            {
                var research = new JSONArray();
                foreach (var r in def.researchPrerequisites)
                {
                    var rObj = new JSONObject();
                    rObj["defName"] = r.defName;
                    rObj["label"] = r.label;
                    rObj["completed"] = r.IsFinished;
                    research.Add(rObj);
                }
                result["researchPrerequisites"] = research;
            }

            result["passability"] = def.passability.ToString();
            if (def.terrainAffordanceNeeded != null)
                result["terrainNeeded"] = def.terrainAffordanceNeeded.defName;
            if (def.minifiedDef != null)
                result["canUninstall"] = true;

            if (def.hasInteractionCell)
            {
                result["has_interaction_cell"] = true;
                result["interaction_cell_note"] = "Requires 1 clear cell in front (facing direction) for pawn access. Don't place facing a wall.";
            }

            return result.ToString();
        }

        /// <summary>
        /// Get comprehensive placement requirements for a building.
        /// Returns size, power, placement rules, terrain requirements, resources, research, and build work.
        /// </summary>
        public static string GetRequirements(JSONNode args)
        {
            if (args == null || string.IsNullOrEmpty(args["building"]?.Value))
                return ToolExecutor.JsonError("'building' parameter is required.");

            string buildingName = args["building"].Value;
            var def = ResolveBuildingDef(buildingName);
            if (def == null)
            {
                string suggestions = FindSimilarBuildings(buildingName);
                string msg = "Building not found: " + buildingName;
                if (suggestions != null)
                    msg += ". Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            var result = new JSONObject();
            result["building"] = def.defName;
            result["label"] = def.label;

            // Size
            var sizeObj = new JSONObject();
            sizeObj["width"] = def.size.x;
            sizeObj["height"] = def.size.z;
            result["size"] = sizeObj;

            // Power stats
            var powerComp = def.GetCompProperties<CompProperties_Power>();
            if (powerComp != null)
            {
                result["powerOutput"] = (int)Math.Round(powerComp.PowerConsumption > 0 ? 0 : Math.Abs(powerComp.PowerConsumption));
                result["powerConsumption"] = (int)Math.Round(powerComp.PowerConsumption > 0 ? powerComp.PowerConsumption : 0);
            }
            else
            {
                result["powerOutput"] = 0;
                result["powerConsumption"] = 0;
            }

            // Placement rules
            var placementRules = new JSONObject();
            var placeWorkerNotes = new List<string>();

            if (def.placeWorkers != null && def.placeWorkers.Count > 0)
            {
                foreach (var pwType in def.placeWorkers)
                {
                    string pwName = pwType.Name;

                    if (pwName.Contains("OnSteamGeyser"))
                    {
                        placementRules["mustBeOnSteamGeyser"] = true;
                        placeWorkerNotes.Add("Must be placed directly on a steam geyser");
                    }
                    else if (pwName.Contains("WatchForGrowth"))
                    {
                        placementRules["mustWatchGrowingPlants"] = true;
                        placeWorkerNotes.Add("Must face growing plants (sun lamps, etc.)");
                    }
                    else if (pwName.Contains("WaterDepth"))
                    {
                        placementRules["mustBeInWater"] = true;
                        placeWorkerNotes.Add("Must be placed in water");
                    }
                    else if (pwName.Contains("NotUnderRoof"))
                    {
                        placementRules["mustBeOutdoors"] = true;
                        placementRules["requiresRoof"] = false;
                        placeWorkerNotes.Add("Must be outdoors (unroofed)");
                    }
                }
            }

            // Standard placement flags
            if (def.building != null)
            {
                if (!placementRules.HasKey("mustBeOutdoors"))
                {
                    placementRules["mustBeIndoors"] = false;
                    placementRules["mustBeOutdoors"] = false;
                }

                if (!placementRules.HasKey("requiresRoof"))
                {
                    placementRules["requiresRoof"] = false;
                }

                placementRules["minifiable"] = def.minifiedDef != null;
            }

            result["placementRules"] = placementRules;

            // Terrain requirements
            var terrainReqs = new JSONArray();
            if (def.terrainAffordanceNeeded != null && !string.IsNullOrEmpty(def.terrainAffordanceNeeded.defName))
            {
                string affordance = def.terrainAffordanceNeeded.defName;

                if (affordance.Contains("Heavy"))
                    terrainReqs.Add("Supports heavy structures");
                else if (affordance.Contains("Medium"))
                    terrainReqs.Add("Supports medium structures");
                else if (affordance.Contains("Light"))
                    terrainReqs.Add("Supports light structures");

                if (def.placeWorkers == null || !def.placeWorkers.Any(pw => pw.Name.Contains("Water")))
                    terrainReqs.Add("Not water");
            }
            else
            {
                terrainReqs.Add("Not water");
            }

            result["terrainRequirements"] = terrainReqs;

            // Work to build
            if (def.statBases != null)
            {
                var workStat = def.statBases.FirstOrDefault(s => s.stat.defName == "WorkToBuild");
                if (workStat != null)
                    result["workToBuild"] = (int)Math.Round(workStat.value);
            }

            // Resources required
            var resources = new JSONArray();
            if (def.MadeFromStuff)
            {
                var resObj = new JSONObject();
                resObj["thing"] = "Stuff (any material)";
                resObj["count"] = def.costStuffCount;
                resources.Add(resObj);
            }

            if (def.costList != null && def.costList.Count > 0)
            {
                foreach (var cost in def.costList)
                {
                    var resObj = new JSONObject();
                    resObj["thing"] = cost.thingDef.defName;
                    resObj["count"] = cost.count;
                    resources.Add(resObj);
                }
            }

            result["resources"] = resources;

            // Research required
            if (def.researchPrerequisites != null && def.researchPrerequisites.Count > 0)
            {
                result["researchRequired"] = def.researchPrerequisites[0].defName;
            }
            else
            {
                result["researchRequired"] = "None";
            }

            // Notes (from PlaceWorker analysis)
            if (placeWorkerNotes.Count > 0)
            {
                result["notes"] = string.Join("; ", placeWorkerNotes);
            }
            else
            {
                result["notes"] = "";
            }

            return result.ToString();
        }
    }
}
