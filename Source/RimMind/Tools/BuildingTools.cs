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
            
            // Check PlaceWorkers for special placement requirements
            var placeWorkerNotes = new List<string>();
            if (def.placeWorkers != null && def.placeWorkers.Count > 0)
            {
                foreach (var pwType in def.placeWorkers)
                {
                    string pwName = pwType.Name;
                    
                    // Detect common special placement requirements
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
                // Indoors/outdoors requirements (if not already set by PlaceWorkers)
                if (!placementRules.HasKey("mustBeOutdoors"))
                {
                    placementRules["mustBeIndoors"] = false;
                    placementRules["mustBeOutdoors"] = false;
                }
                
                if (!placementRules.HasKey("requiresRoof"))
                {
                    placementRules["requiresRoof"] = false;
                }
                
                // Minifiable (can be uninstalled and moved)
                placementRules["minifiable"] = def.minifiedDef != null;
            }

            result["placementRules"] = placementRules;

            // Terrain requirements
            var terrainReqs = new JSONArray();
            if (def.terrainAffordanceNeeded != null && !string.IsNullOrEmpty(def.terrainAffordanceNeeded.defName))
            {
                string affordance = def.terrainAffordanceNeeded.defName;
                
                // Translate affordance to human-readable requirements
                if (affordance.Contains("Heavy"))
                    terrainReqs.Add("Supports heavy structures");
                else if (affordance.Contains("Medium"))
                    terrainReqs.Add("Supports medium structures");
                else if (affordance.Contains("Light"))
                    terrainReqs.Add("Supports light structures");
                    
                // All terrain affordances implicitly require "not water" unless specifically a water building
                if (def.placeWorkers == null || !def.placeWorkers.Any(pw => pw.Name.Contains("Water")))
                    terrainReqs.Add("Not water");
            }
            else
            {
                // Default: most buildings need solid ground
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
                // Stuff-based building (e.g., walls)
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
                // Just return the first research requirement for simplicity
                // (most buildings only have one)
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

        public static string PlaceBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var faction = Faction.OfPlayer;

            var placementsNode = args?["placements"];
            placementsNode = UnwrapStringArray(placementsNode);
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

            if (placements.Count > 100)
                return ToolExecutor.JsonError("Maximum 100 placements per call. Got " + placements.Count + ".");

            bool globalAutoApprove = args?["auto_approve"]?.AsBool == true;

            // Compute bounding box for before/after grid
            int bbMinX = int.MaxValue, bbMinZ = int.MaxValue;
            int bbMaxX = int.MinValue, bbMaxZ = int.MinValue;
            foreach (var p in placements)
            {
                var pxNode = p["x"];
                var pzNode = p["z"];
                if (pxNode == null || pzNode == null) continue;
                int px = pxNode.AsInt;
                int pz = pzNode.AsInt;
                if (px < bbMinX) bbMinX = px;
                if (px > bbMaxX) bbMaxX = px;
                if (pz < bbMinZ) bbMinZ = pz;
                if (pz > bbMaxZ) bbMaxZ = pz;
            }
            JSONArray existingInArea = null;
            if (bbMinX != int.MaxValue)
            {
                int padMinX = System.Math.Max(0, bbMinX - 1);
                int padMinZ = System.Math.Max(0, bbMinZ - 1);
                int padMaxX = System.Math.Min(map.Size.x - 1, bbMaxX + 1);
                int padMaxZ = System.Math.Min(map.Size.z - 1, bbMaxZ + 1);
                if ((padMaxX - padMinX + 1) <= 30 && (padMaxZ - padMinZ + 1) <= 30)
                    existingInArea = ScanBuildingsInArea(map, padMinX, padMinZ, padMaxX, padMaxZ);
            }

            var successEntries = new JSONArray();
            var failures = new JSONArray();
            int placed = 0;
            int failed = 0;

            foreach (var p in placements)
            {
                string defName = p["defName"]?.Value;
                if (string.IsNullOrEmpty(defName))
                {
                    var entry = new JSONObject();
                    entry["error"] = "Missing defName";
                    failed++;
                    failures.Add(entry);
                    continue;
                }

                var def = ResolveBuildingDef(defName);
                if (def == null)
                {
                    var entry = new JSONObject();
                    string suggestions = FindSimilarBuildings(defName);
                    string msg = "Unknown building: " + defName;
                    if (suggestions != null)
                        msg += ". Did you mean: " + suggestions + "?";
                    entry["error"] = msg;
                    entry["defName"] = defName;
                    failed++;
                    failures.Add(entry);
                    continue;
                }

                if (def.researchPrerequisites != null)
                {
                    var missing = def.researchPrerequisites.Where(r => !r.IsFinished).ToList();
                    if (missing.Count > 0)
                    {
                        var entry = new JSONObject();
                        entry["error"] = "Research required: " + string.Join(", ", missing.Select(r => r.label));
                        entry["defName"] = def.defName;
                        failed++;
                        failures.Add(entry);
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(p["x"]?.Value) || string.IsNullOrEmpty(p["z"]?.Value))
                {
                    var entry = new JSONObject();
                    entry["error"] = "Missing x/z coordinates";
                    entry["defName"] = def.defName;
                    failed++;
                    failures.Add(entry);
                    continue;
                }

                int x = p["x"].AsInt;
                int z = p["z"].AsInt;
                var pos = new IntVec3(x, 0, z);

                ThingDef stuff = null;
                if (def.MadeFromStuff)
                {
                    string stuffName = p["stuff"]?.Value;
                    if (stuffName == "null") stuffName = null;
                    if (string.IsNullOrEmpty(stuffName))
                    {
                        var entry = new JSONObject();
                        entry["error"] = "Building '" + def.label + "' requires a material. Specify 'stuff' (e.g., 'WoodLog', 'BlocksGranite', 'Steel').";
                        entry["defName"] = def.defName;
                        entry["x"] = x;
                        entry["z"] = z;
                        failed++;
                        failures.Add(entry);
                        continue;
                    }
                    stuff = ResolveStuffDef(stuffName, def);
                    if (stuff == null)
                    {
                        var entry = new JSONObject();
                        string stuffSuggestions = FindSimilarStuffs(stuffName, def);
                        string msg = "Invalid stuff '" + stuffName + "' for " + def.label;
                        if (stuffSuggestions != null)
                            msg += ". Did you mean: " + stuffSuggestions + "?";
                        else
                            msg += ". Use get_building_info to see available materials.";
                        entry["error"] = msg;
                        entry["defName"] = def.defName;
                        entry["x"] = x;
                        entry["z"] = z;
                        failed++;
                        failures.Add(entry);
                        continue;
                    }
                }

                Rot4 rot = ParseRotation(p["rotation"]);
                bool autoApprove = globalAutoApprove || (p["auto_approve"]?.AsBool == true);

                // Phase 2: Material pre-check
                var materialCheck = CheckMaterials(map, def, stuff);

                var pr = PlaceOneBlueprint(map, faction, def, pos, stuff, rot, autoApprove);
                if (pr.success)
                {
                    placed++;
                    var successEntry = new JSONObject();
                    if (!autoApprove && pr.proposalId != null)
                        successEntry["id"] = pr.proposalId;
                    successEntry["def"] = def.defName;
                    successEntry["x"] = x;
                    successEntry["z"] = z;
                    if (pr.autoRotated)
                    {
                        successEntry["auto_rotated"] = true;
                        successEntry["rotation"] = pr.finalRotation;
                    }
                    // Add material warnings if materials are insufficient
                    if (!materialCheck.hasMaterials)
                    {
                        successEntry["material_warning"] = materialCheck.warning;
                        if (materialCheck.shortages != null)
                            successEntry["material_shortages"] = materialCheck.shortages;
                    }
                    successEntries.Add(successEntry);
                }
                else
                {
                    var entry = new JSONObject();
                    entry["defName"] = def.defName;
                    entry["x"] = x;
                    entry["z"] = z;
                    entry["error"] = pr.error;
                    failed++;
                    failures.Add(entry);
                }
            }

            var result = new JSONObject();
            result["placed"] = placed;
            result["failed"] = failed;
            if (successEntries.Count > 0)
                result["placements"] = successEntries;
            if (failures.Count > 0)
                result["failures"] = failures;

            // Render existing buildings and after area grid so the AI can see what changed
            if (existingInArea != null)
                result["existing_in_area"] = existingInArea;
            if (placed > 0)
            {
                int gridMinX = int.MaxValue, gridMinZ = int.MaxValue;
                int gridMaxX = int.MinValue, gridMaxZ = int.MinValue;
                foreach (var p in placements)
                {
                    int px = p["x"]?.AsInt ?? 0;
                    int pz = p["z"]?.AsInt ?? 0;
                    if (px < gridMinX) gridMinX = px;
                    if (px > gridMaxX) gridMaxX = px;
                    if (pz < gridMinZ) gridMinZ = pz;
                    if (pz > gridMaxZ) gridMaxZ = pz;
                }
                // Add 1-cell padding
                gridMinX = System.Math.Max(0, gridMinX - 1);
                gridMinZ = System.Math.Max(0, gridMinZ - 1);
                gridMaxX = System.Math.Min(map.Size.x - 1, gridMaxX + 1);
                gridMaxZ = System.Math.Min(map.Size.z - 1, gridMaxZ + 1);
                // Cap grid size to avoid huge renders
                if ((gridMaxX - gridMinX + 1) <= 30 && (gridMaxZ - gridMinZ + 1) <= 30)
                {
                    result["area_after"] = MapTools.RenderArea(map, gridMinX, gridMinZ, gridMaxX, gridMaxZ);
                    result["buildings_in_area"] = ScanBuildingsInArea(map, gridMinX, gridMinZ, gridMaxX, gridMaxZ);
                }
            }

            return result.ToString();
        }

        public static string PlaceStructure(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var faction = Faction.OfPlayer;

            string shape = args?["shape"]?.Value;
            if (string.IsNullOrEmpty(shape))
                return ToolExecutor.JsonError("'shape' is required. Valid shapes: room, wall_line, wall_rect.");

            if (string.IsNullOrEmpty(args?["x1"]?.Value) || string.IsNullOrEmpty(args?["z1"]?.Value)
                || string.IsNullOrEmpty(args?["x2"]?.Value) || string.IsNullOrEmpty(args?["z2"]?.Value))
                return ToolExecutor.JsonError("x1, z1, x2, z2 coordinates are required.");

            int x1 = args["x1"].AsInt;
            int z1 = args["z1"].AsInt;
            int x2 = args["x2"].AsInt;
            int z2 = args["z2"].AsInt;

            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);

            string stuffName = args?["stuff"]?.Value;
            if (stuffName == "null") stuffName = null;
            bool autoApprove = args?["auto_approve"]?.AsBool == true;

            // Resolve wall def
            var wallDef = ResolveBuildingDef("Wall");
            if (wallDef == null)
                return ToolExecutor.JsonError("Cannot find Wall building def.");

            // Resolve door def
            var doorDef = ResolveBuildingDef("Door");
            if (doorDef == null)
                return ToolExecutor.JsonError("Cannot find Door building def.");

            // Resolve wall stuff
            ThingDef wallStuff = null;
            if (wallDef.MadeFromStuff)
            {
                if (string.IsNullOrEmpty(stuffName))
                    return ToolExecutor.JsonError("Wall requires a material. Specify 'stuff' (e.g., 'WoodLog', 'BlocksGranite', 'Steel').");

                wallStuff = ResolveStuffDef(stuffName, wallDef);
                if (wallStuff == null)
                {
                    string suggestions = FindSimilarStuffs(stuffName, wallDef);
                    string msg = "Invalid stuff '" + stuffName + "' for Wall";
                    if (suggestions != null)
                        msg += ". Did you mean: " + suggestions + "?";
                    return ToolExecutor.JsonError(msg);
                }
            }

            switch (shape)
            {
                case "room":
                    return PlaceRoom(map, faction, wallDef, doorDef, wallStuff, minX, minZ, maxX, maxZ, args, autoApprove);
                case "wall_line":
                    return PlaceWallLine(map, faction, wallDef, wallStuff, x1, z1, x2, z2, autoApprove);
                case "wall_rect":
                    return PlaceWallRect(map, faction, wallDef, wallStuff, minX, minZ, maxX, maxZ, autoApprove);
                default:
                    return ToolExecutor.JsonError("Unknown shape: " + shape + ". Valid shapes: room, wall_line, wall_rect.");
            }
        }

        private static string PlaceRoom(Map map, Faction faction, ThingDef wallDef, ThingDef doorDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, JSONNode args, bool autoApprove)
        {
            return BuildingHelpers.PlaceRoom(map, faction, wallDef, doorDef, wallStuff, minX, minZ, maxX, maxZ, args, autoApprove);
        }


        private static string PlaceWallLine(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int x1, int z1, int x2, int z2, bool autoApprove)
        {
            return BuildingHelpers.PlaceWallLine(map, faction, wallDef, wallStuff, x1, z1, x2, z2, autoApprove);
        }


        private static string PlaceWallRect(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, bool autoApprove)
        {
            return BuildingHelpers.PlaceWallRect(map, faction, wallDef, wallStuff, minX, minZ, maxX, maxZ, autoApprove);
        }


        public static string RemoveBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool removeAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            idsNode = UnwrapStringArray(idsNode);
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

        /// <summary>
        /// Mark already-built structures for deconstruction using RimWorld's native designation system.
        /// Parameters: x/z (cell), x2/z2 (area), def_name (all of type). At least one required.
        /// </summary>
        public static string DeconstructBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool hasCell = args?["x"] != null && args?["z"] != null;
            bool hasArea = hasCell && args?["x2"] != null && args?["z2"] != null;
            string defName = args?["def_name"]?.Value;

            if (!hasCell && string.IsNullOrEmpty(defName))
                return ToolExecutor.JsonError("At least one parameter required: x/z (cell), x2/z2 (area), or def_name.");

            var targets = new List<Thing>();

            // Collect target buildings
            if (!string.IsNullOrEmpty(defName))
            {
                // Target all buildings of this defName on the map
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (string.Equals(building.def.defName, defName, StringComparison.OrdinalIgnoreCase))
                        targets.Add(building);
                }
                // Also check non-colonist buildings (ancient ruins, ship chunks, etc.)
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                    {
                        if (string.Equals(thing.def.defName, defName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!targets.Contains(thing))
                                targets.Add(thing);
                        }
                    }
                }
            }
            else if (hasArea)
            {
                // Area selection
                int x1 = args["x"].AsInt;
                int z1 = args["z"].AsInt;
                int x2 = args["x2"].AsInt;
                int z2 = args["z2"].AsInt;
                int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) continue;

                        foreach (var thing in cell.GetThingList(map))
                        {
                            if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                            {
                                if (!targets.Contains(thing))
                                    targets.Add(thing);
                            }
                        }
                    }
                }
            }
            else
            {
                // Single cell
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                var cell = new IntVec3(x, 0, z);

                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError($"Position ({x}, {z}) is outside map bounds.");

                foreach (var thing in cell.GetThingList(map))
                {
                    if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                        targets.Add(thing);
                }
            }

            int designated = 0;
            int alreadyDesignated = 0;
            int skipped = 0;
            var structuresList = new JSONArray();

            foreach (var thing in targets)
            {
                // Check if already designated for deconstruction
                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) != null)
                {
                    alreadyDesignated++;
                    continue;
                }

                // Check if it can be deconstructed
                bool canDeconstruct = thing.def.building?.IsDeconstructible ?? false;

                // Also allow ship chunks and mineable things
                if (!canDeconstruct && thing.def.mineable)
                    canDeconstruct = true;

                // Check if it's a real built thing
                bool isBuilt = thing is Building && !(thing is Blueprint) && !(thing is Frame);

                if (!canDeconstruct || !isBuilt)
                {
                    skipped++;
                    continue;
                }

                // Apply the deconstruction designation
                map.designationManager.AddDesignation(new Designation(thing, DesignationDefOf.Deconstruct));
                designated++;

                string label = thing.def.label ?? thing.def.defName;
                structuresList.Add($"{label} at {thing.Position.x},{thing.Position.z}");
            }

            var result = new JSONObject();
            result["designated"] = designated;
            result["already_designated"] = alreadyDesignated;
            result["skipped"] = skipped;
            if (structuresList.Count > 0 && structuresList.Count <= 50)
                result["structures"] = structuresList;
            else if (structuresList.Count > 50)
                result["structures_note"] = $"{structuresList.Count} structures designated (list truncated)";

            return result.ToString();
        }

        public static string ApproveBuildings(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool approveAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            idsNode = UnwrapStringArray(idsNode);
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

        // --- Core placement helper ---

        private static BuildingHelpers.PlacementResult PlaceOneBlueprint(Map map, Faction faction, ThingDef def, IntVec3 pos, ThingDef stuff, Rot4 rot, bool autoApprove, bool allowAutoRotate = true)
        {
            return BuildingHelpers.PlaceOneBlueprint(map, faction, def, pos, stuff, rot, autoApprove, allowAutoRotate);
        }


        // Phase 2: Material pre-check
        private static BuildingHelpers.MaterialCheckResult CheckMaterials(Map map, ThingDef buildingDef, ThingDef stuff)
        {
            return BuildingHelpers.CheckMaterials(map, buildingDef, stuff);
        }


        // --- Utility helpers ---

        // LLMs sometimes send JSON arrays as double-encoded strings -- unwrap them
        private static JSONNode UnwrapStringArray(JSONNode node)
        {
            return BuildingHelpers.UnwrapStringArray(node);
        }


        private static ThingDef ResolveBuildingDef(string defName)
        {
            return BuildingHelpers.ResolveBuildingDef(defName);
        }


        private static ThingDef ResolveStuffDef(string stuffName, ThingDef buildingDef)
        {
            return BuildingHelpers.ResolveStuffDef(stuffName, buildingDef);
        }


        private static bool IsStuffValidForBuilding(ThingDef stuff, ThingDef buildingDef)
        {
            return BuildingHelpers.IsStuffValidForBuilding(stuff, buildingDef);
        }


        private static string FindSimilarBuildings(string defName)
        {
            return BuildingHelpers.FindSimilarBuildings(defName);
        }


        private static string FindSimilarStuffs(string stuffName, ThingDef buildingDef)
        {
            return BuildingHelpers.FindSimilarStuffs(stuffName, buildingDef);
        }


        private static string GetStuffHint(ThingDef def)
        {
            return BuildingHelpers.GetStuffHint(def);
        }


        private static string GetPlacementHint(string reason, ThingDef def, Map map = null, IntVec3 pos = default)
        {
            return BuildingHelpers.GetPlacementHint(reason, def, map, pos);
        }


        private static Rot4 ParseRotation(JSONNode rotNode)
        {
            return BuildingHelpers.ParseRotation(rotNode);
        }


        private static int ParseDoorOffset(JSONNode offsetNode, int innerLen)
        {
            return BuildingHelpers.ParseDoorOffset(offsetNode, innerLen);
        }


        private static bool HasExistingWallOrBlueprint(IntVec3 pos, Map map)
        {
            return BuildingHelpers.HasExistingWallOrBlueprint(pos, map);
        }


        // --- Adjacent wall detection ---

        private static JSONArray DetectAdjacentWalls(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            return BuildingHelpers.DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
        }


        private static bool HasWallLine(Map map, int x1, int z1, int x2, int z2)
        {
            return BuildingHelpers.HasWallLine(map, x1, z1, x2, z2);
        }


        // --- Area scanning helper ---

        private static JSONArray ScanBuildingsInArea(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            return BuildingHelpers.ScanBuildingsInArea(map, minX, minZ, maxX, maxZ);
        }


        // --- Shape helpers ---

        private static List<IntVec3> GetRectOutline(int x1, int z1, int x2, int z2)
        {
            return BuildingHelpers.GetRectOutline(x1, z1, x2, z2);
        }


        private static List<IntVec3> GetLine(int x1, int z1, int x2, int z2)
        {
            return BuildingHelpers.GetLine(x1, z1, x2, z2);
        }


        // --- Placement Validation (Week 2 of #94) ---

        public static string CheckPlacement(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Validate required parameters
            if (string.IsNullOrEmpty(args?["building"]?.Value))
                return ToolExecutor.JsonError("'building' parameter is required.");
            if (args?["x"] == null || args?["z"] == null)
                return ToolExecutor.JsonError("'x' and 'z' coordinates are required.");

            string buildingDefName = args["building"].Value;
            int x = args["x"].AsInt;
            int z = args["z"].AsInt;
            var pos = new IntVec3(x, 0, z);
            
            // Validate position is in bounds
            if (!pos.InBounds(map))
            {
                return ToolExecutor.JsonError($"Position ({x}, {z}) is outside map bounds (map size: {map.Size.x}x{map.Size.z})");
            }

            // Resolve building def
            var def = ResolveBuildingDef(buildingDefName);
            if (def == null)
            {
                string suggestions = FindSimilarBuildings(buildingDefName);
                string msg = "Building not found: " + buildingDefName;
                if (suggestions != null)
                    msg += ". Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            // Parse rotation (default: north)
            string rotationStr = args?["rotation"]?.Value?.ToLower();
            Rot4 rotation = Rot4.North;
            if (!string.IsNullOrEmpty(rotationStr))
            {
                switch (rotationStr)
                {
                    case "north": rotation = Rot4.North; break;
                    case "east": rotation = Rot4.East; break;
                    case "south": rotation = Rot4.South; break;
                    case "west": rotation = Rot4.West; break;
                    default:
                        return ToolExecutor.JsonError("Invalid rotation: " + rotationStr + ". Valid: north, south, east, west.");
                }
            }

            // Calculate occupied cells
            var occupiedCells = GenAdj.CellsOccupiedBy(pos, rotation, def.size).ToList();
            
            if (occupiedCells == null || occupiedCells.Count == 0)
            {
                return ToolExecutor.JsonError("Building size is invalid or position cannot be calculated");
            }

            // Result object
            var result = new JSONObject();
            result["building"] = def.defName;
            result["position"] = new JSONArray { x, z };
            result["rotation"] = rotation.ToStringHuman().ToLower();

            // Size (accounting for rotation)
            int sizeX = rotation.IsHorizontal ? def.size.z : def.size.x;
            int sizeZ = rotation.IsHorizontal ? def.size.x : def.size.z;
            result["size"] = new JSONArray { sizeX, sizeZ };

            // Checks object
            var checks = new JSONObject();
            var warnings = new JSONArray();
            bool valid = true;

            // 1. Check terrain
            var terrainCheck = CheckTerrain(map, def, occupiedCells);
            checks["terrain"] = terrainCheck;
            if (!terrainCheck["ok"].AsBool)
                valid = false;

            // 2. Check space/conflicts
            var spaceCheck = CheckSpace(map, occupiedCells);
            checks["space"] = spaceCheck;
            if (!spaceCheck["ok"].AsBool)
                valid = false;

            // 3. Check power (if required)
            var powerCheck = CheckPower(map, def, pos);
            checks["power"] = powerCheck;
            if (powerCheck["ok"] != null && !powerCheck["ok"].AsBool)
                valid = false;

            // 4. Check roof (for buildings that need it)
            var roofCheck = CheckRoof(map, def, occupiedCells);
            checks["roof"] = roofCheck;
            if (roofCheck["ok"] != null && !roofCheck["ok"].AsBool)
            {
                // Roof is often a warning, not always a blocker
                if (roofCheck["required"]?.AsBool == true)
                    valid = false;
                else
                    warnings.Add(roofCheck["detail"].Value);
            }

            // 5. Check special placement rules
            var specialCheck = CheckSpecialRules(map, def, pos, rotation, occupiedCells);
            checks["special"] = specialCheck;
            if (!specialCheck["ok"].AsBool)
                valid = false;

            // 6. Detect adjacent features
            var adjacentCheck = CheckAdjacent(map, occupiedCells);
            if (adjacentCheck.Count > 0)
            {
                for (int i = 0; i < adjacentCheck.Count; i++)
                    warnings.Add(adjacentCheck[i]);
            }

            result["valid"] = valid;
            result["checks"] = checks;

            if (warnings.Count > 0)
                result["warnings"] = warnings;

            // Suggest alternative if invalid
            if (!valid)
            {
                string suggestion = SuggestAlternative(map, def, pos, rotation);
                if (suggestion != null)
                    result["suggestion"] = suggestion;
            }

            return result.ToString();
        }

        private static JSONObject CheckTerrain(Map map, ThingDef def, List<IntVec3> cells)
        {
            return BuildingHelpers.CheckTerrain(map, def, cells);
        }


        private static JSONObject CheckSpace(Map map, List<IntVec3> cells)
        {
            return BuildingHelpers.CheckSpace(map, cells);
        }


        private static JSONObject CheckPower(Map map, ThingDef def, IntVec3 pos)
        {
            return BuildingHelpers.CheckPower(map, def, pos);
        }


        private static JSONObject CheckRoof(Map map, ThingDef def, List<IntVec3> cells)
        {
            return BuildingHelpers.CheckRoof(map, def, cells);
        }


        private static JSONObject CheckSpecialRules(Map map, ThingDef def, IntVec3 pos, Rot4 rotation, List<IntVec3> cells)
        {
            return BuildingHelpers.CheckSpecialRules(map, def, pos, rotation, cells);
        }


        private static JSONArray CheckAdjacent(Map map, List<IntVec3> cells)
        {
            return BuildingHelpers.CheckAdjacent(map, cells);
        }


        private static string SuggestAlternative(Map map, ThingDef def, IntVec3 pos, Rot4 rotation)
        {
            return BuildingHelpers.SuggestAlternative(map, def, pos, rotation);
        }

    }
}
