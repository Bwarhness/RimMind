using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class BuildingTools
    {
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
                if (def.MadeFromStuff) entry["stuff"] = true;
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
                return ToolExecutor.JsonError("Building not found: " + args["defName"].Value);

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

            return result.ToString();
        }

        public static string PlaceBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var faction = Faction.OfPlayer;

            var placementsNode = args?["placements"];
            var placements = new List<JSONNode>();

            if (placementsNode != null && placementsNode.IsArray)
            {
                foreach (JSONNode p in placementsNode.AsArray)
                    placements.Add(p);
            }
            else if (args != null && !string.IsNullOrEmpty(args["defName"]?.Value))
            {
                placements.Add(args);
            }
            else
            {
                return ToolExecutor.JsonError("Provide 'defName' + x/z for single placement, or 'placements' array for batch.");
            }

            if (placements.Count > 50)
                return ToolExecutor.JsonError("Maximum 50 placements per call. Got " + placements.Count + ".");

            var results = new JSONArray();
            int placed = 0;
            int failed = 0;

            foreach (var p in placements)
            {
                var entry = new JSONObject();
                string defName = p["defName"]?.Value;
                if (string.IsNullOrEmpty(defName))
                {
                    entry["error"] = "Missing defName";
                    failed++;
                    results.Add(entry);
                    continue;
                }

                var def = ResolveBuildingDef(defName);
                if (def == null)
                {
                    entry["error"] = "Unknown building: " + defName;
                    failed++;
                    results.Add(entry);
                    continue;
                }

                if (def.researchPrerequisites != null)
                {
                    var missing = def.researchPrerequisites.Where(r => !r.IsFinished).ToList();
                    if (missing.Count > 0)
                    {
                        entry["error"] = "Research required: " + string.Join(", ", missing.Select(r => r.label));
                        failed++;
                        results.Add(entry);
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(p["x"]?.Value) || string.IsNullOrEmpty(p["z"]?.Value))
                {
                    entry["error"] = "Missing x/z coordinates";
                    failed++;
                    results.Add(entry);
                    continue;
                }

                int x = p["x"].AsInt;
                int z = p["z"].AsInt;
                var pos = new IntVec3(x, 0, z);

                ThingDef stuff = null;
                if (def.MadeFromStuff)
                {
                    string stuffName = p["stuff"]?.Value;
                    if (string.IsNullOrEmpty(stuffName))
                    {
                        entry["error"] = "Building '" + def.label + "' requires a material. Specify 'stuff' (e.g., 'WoodLog', 'BlocksGranite', 'Steel').";
                        failed++;
                        results.Add(entry);
                        continue;
                    }
                    stuff = ResolveStuffDef(stuffName, def);
                    if (stuff == null)
                    {
                        entry["error"] = "Invalid stuff '" + stuffName + "' for " + def.label + ". Use get_building_info to see available materials.";
                        failed++;
                        results.Add(entry);
                        continue;
                    }
                }

                Rot4 rot = ParseRotation(p["rotation"]);

                var report = GenConstruct.CanPlaceBlueprintAt(def, pos, rot, map, false, null, null, stuff);
                if (!report.Accepted)
                {
                    entry["error"] = "Cannot place at (" + x + "," + z + "): " + (report.Reason ?? "blocked");
                    entry["defName"] = def.defName;
                    failed++;
                    results.Add(entry);
                    continue;
                }

                var blueprint = GenConstruct.PlaceBlueprintForBuild(def, pos, map, rot, faction, stuff);
                if (blueprint == null)
                {
                    entry["error"] = "Failed to place blueprint for " + def.label;
                    failed++;
                    results.Add(entry);
                    continue;
                }

                var forbidComp = blueprint.GetComp<CompForbiddable>();
                if (forbidComp != null)
                {
                    blueprint.SetForbidden(true, false);
                }
                else
                {
                    Log.Warning("[RimMind] Blueprint lacks CompForbiddable: " + blueprint.def.defName);
                }

                string proposalId = ProposalTracker.Track(blueprint);

                entry["proposalId"] = proposalId;
                entry["defName"] = def.defName;
                entry["label"] = def.label;
                entry["x"] = pos.x;
                entry["z"] = pos.z;
                if (stuff != null) entry["stuff"] = stuff.defName;
                entry["rotation"] = rot.AsInt;
                placed++;
                results.Add(entry);
            }

            var result = new JSONObject();
            result["placed"] = placed;
            result["failed"] = failed;
            result["results"] = results;
            return result.ToString();
        }

        public static string RemoveBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool removeAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            bool hasArea = !string.IsNullOrEmpty(args?["x"]?.Value);

            if (!removeAll && (idsNode == null || !idsNode.IsArray) && !hasArea)
                return ToolExecutor.JsonError("Provide 'proposal_ids' array, area (x/z/x2/z2), or 'all: true'.");

            ProposalTracker.CleanupDestroyed(map);

            var toRemove = new List<KeyValuePair<string, Thing>>();

            if (removeAll)
            {
                toRemove = ProposalTracker.GetAll(map);
            }
            else if (idsNode != null && idsNode.IsArray)
            {
                foreach (JSONNode idNode in idsNode.AsArray)
                {
                    string id = idNode.Value;
                    Thing t = ProposalTracker.FindThing(id, map);
                    if (t != null && !t.Destroyed)
                        toRemove.Add(new KeyValuePair<string, Thing>(id, t));
                }
            }
            else if (hasArea)
            {
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                int x2 = args["x2"]?.AsInt ?? x;
                int z2 = args["z2"]?.AsInt ?? z;
                int minX = Math.Min(x, x2), maxX = Math.Max(x, x2);
                int minZ = Math.Min(z, z2), maxZ = Math.Max(z, z2);
                var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                toRemove = ProposalTracker.GetInRect(rect, map);
            }

            int removed = 0;
            foreach (var kvp in toRemove)
            {
                if (!kvp.Value.Destroyed)
                    kvp.Value.Destroy(DestroyMode.Cancel);
                ProposalTracker.Untrack(kvp.Key);
                removed++;
            }

            var result = new JSONObject();
            result["removed"] = removed;
            return result.ToString();
        }

        public static string ApproveBuildings(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool approveAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            bool hasArea = !string.IsNullOrEmpty(args?["x"]?.Value);

            if (!approveAll && (idsNode == null || !idsNode.IsArray) && !hasArea)
                return ToolExecutor.JsonError("Provide 'proposal_ids' array, area (x/z/x2/z2), or 'all: true'.");

            ProposalTracker.CleanupDestroyed(map);

            var toApprove = new List<KeyValuePair<string, Thing>>();

            if (approveAll)
            {
                toApprove = ProposalTracker.GetAll(map);
            }
            else if (idsNode != null && idsNode.IsArray)
            {
                foreach (JSONNode idNode in idsNode.AsArray)
                {
                    string id = idNode.Value;
                    Thing t = ProposalTracker.FindThing(id, map);
                    if (t != null && !t.Destroyed)
                        toApprove.Add(new KeyValuePair<string, Thing>(id, t));
                }
            }
            else if (hasArea)
            {
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                int x2 = args["x2"]?.AsInt ?? x;
                int z2 = args["z2"]?.AsInt ?? z;
                int minX = Math.Min(x, x2), maxX = Math.Max(x, x2);
                int minZ = Math.Min(z, z2), maxZ = Math.Max(z, z2);
                var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                toApprove = ProposalTracker.GetInRect(rect, map);
            }

            int approved = 0;
            foreach (var kvp in toApprove)
            {
                if (!kvp.Value.Destroyed)
                {
                    kvp.Value.SetForbidden(false, false);
                    approved++;
                }
                ProposalTracker.Untrack(kvp.Key);
            }

            var result = new JSONObject();
            result["approved"] = approved;
            return result.ToString();
        }

        private static ThingDef ResolveBuildingDef(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.category != ThingCategory.Building) return null;
            if (typeof(Blueprint).IsAssignableFrom(def.thingClass)) return null;
            if (typeof(Frame).IsAssignableFrom(def.thingClass)) return null;
            return def;
        }

        private static ThingDef ResolveStuffDef(string stuffName, ThingDef buildingDef)
        {
            var stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
            if (stuff == null || !stuff.IsStuff) return null;
            if (buildingDef.stuffCategories == null || stuff.stuffProps?.categories == null) return null;
            foreach (var cat in stuff.stuffProps.categories)
            {
                if (buildingDef.stuffCategories.Contains(cat))
                    return stuff;
            }
            return null;
        }

        private static Rot4 ParseRotation(JSONNode rotNode)
        {
            if (rotNode == null || string.IsNullOrEmpty(rotNode.Value)) return Rot4.North;
            int val = rotNode.AsInt;
            switch (val)
            {
                case 1: return Rot4.East;
                case 2: return Rot4.South;
                case 3: return Rot4.West;
                default: return Rot4.North;
            }
        }
    }
}
