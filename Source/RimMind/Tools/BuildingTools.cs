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
        private struct PlacementResult
        {
            public bool success;
            public string proposalId;
            public string error;
            public Thing blueprint;
            public bool autoRotated;
            public int finalRotation;
        }

        private struct MaterialCheckResult
        {
            public bool hasMaterials;
            public string warning;
            public JSONArray shortages;
        }

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
            if (def.terrainAffordanceNeeded != null)
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
                if (!def.placeWorkers.Any(pw => pw.Name.Contains("Water")))
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
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            if (width > 25 || height > 25)
                return ToolExecutor.JsonError("Maximum room size is 25x25. Got " + width + "x" + height + ".");

            if (width < 3 || height < 3)
                return ToolExecutor.JsonError("Minimum room size is 3x3 (1x1 interior + walls). Got " + width + "x" + height + ".");

            // Resolve door stuff
            string doorStuffName = args?["door_stuff"]?.Value;
            if (string.IsNullOrEmpty(doorStuffName) || doorStuffName == "null")
                doorStuffName = args?["stuff"]?.Value;
            if (doorStuffName == "null") doorStuffName = null;
            ThingDef doorStuff = wallStuff; // default to wall stuff
            if (!string.IsNullOrEmpty(doorStuffName))
            {
                doorStuff = ResolveStuffDef(doorStuffName, doorDef);
                if (doorStuff == null)
                {
                    string suggestions = FindSimilarStuffs(doorStuffName, doorDef);
                    string msg = "Invalid stuff '" + doorStuffName + "' for Door";
                    if (suggestions != null)
                        msg += ". Did you mean: " + suggestions + "?";
                    return ToolExecutor.JsonError(msg);
                }
            }
            else if (doorDef.MadeFromStuff && doorStuff != null)
            {
                // Validate that wall stuff works for door too
                var checkDoorStuff = ResolveStuffDef(doorStuff.defName, doorDef);
                if (checkDoorStuff == null)
                {
                    // Wall stuff doesn't work for door, try WoodLog as fallback
                    doorStuff = ResolveStuffDef("WoodLog", doorDef);
                    if (doorStuff == null)
                        return ToolExecutor.JsonError("Wall stuff '" + wallStuff.defName + "' is not valid for doors. Specify 'door_stuff'.");
                }
            }

            // Determine door position
            string doorSide = args?["door_side"]?.Value ?? "south";
            doorSide = doorSide.ToLower();

            // Calculate wall cells for the perimeter
            var wallCells = GetRectOutline(minX, minZ, maxX, maxZ);

            // Determine the door cell
            IntVec3 doorCell;
            Rot4 doorRot;

            // Inner wall length (excluding corners)
            int innerLen;
            int doorOffset;

            switch (doorSide)
            {
                case "south":
                    innerLen = width - 2;
                    doorOffset = ParseDoorOffset(args?["door_offset"], innerLen);
                    doorCell = new IntVec3(minX + 1 + doorOffset, 0, minZ);
                    doorRot = Rot4.North;
                    break;
                case "north":
                    innerLen = width - 2;
                    doorOffset = ParseDoorOffset(args?["door_offset"], innerLen);
                    doorCell = new IntVec3(minX + 1 + doorOffset, 0, maxZ);
                    doorRot = Rot4.North;
                    break;
                case "west":
                    innerLen = height - 2;
                    doorOffset = ParseDoorOffset(args?["door_offset"], innerLen);
                    doorCell = new IntVec3(minX, 0, minZ + 1 + doorOffset);
                    doorRot = Rot4.East;
                    break;
                case "east":
                    innerLen = height - 2;
                    doorOffset = ParseDoorOffset(args?["door_offset"], innerLen);
                    doorCell = new IntVec3(maxX, 0, minZ + 1 + doorOffset);
                    doorRot = Rot4.East;
                    break;
                default:
                    return ToolExecutor.JsonError("Invalid door_side: " + doorSide + ". Valid: north, south, east, west.");
            }

            // Remove door cell from wall cells
            wallCells.RemoveAll(c => c.x == doorCell.x && c.z == doorCell.z);

            // Scan existing buildings before placement
            int bbX1 = System.Math.Max(0, minX - 1);
            int bbZ1 = System.Math.Max(0, minZ - 1);
            int bbX2 = System.Math.Min(map.Size.x - 1, maxX + 1);
            int bbZ2 = System.Math.Min(map.Size.z - 1, maxZ + 1);
            JSONArray existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);

            // Place wall blueprints
            var proposalIds = new JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new JSONArray();

            foreach (var cell in wallCells)
            {
                if (HasExistingWallOrBlueprint(cell, map))
                {
                    sharedCount++;
                    continue;
                }
                var pr = PlaceOneBlueprint(map, faction, wallDef, cell, wallStuff, Rot4.North, autoApprove);
                if (pr.success)
                {
                    placedCount++;
                    if (!autoApprove && pr.proposalId != null)
                        proposalIds.Add(pr.proposalId);
                }
                else
                {
                    failedCount++;
                    var entry = new JSONObject();
                    entry["defName"] = wallDef.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    failuresList.Add(entry);
                }
            }

            // Place door blueprint (no auto-rotate — door rotation must match wall orientation)
            var doorResult = PlaceOneBlueprint(map, faction, doorDef, doorCell, doorStuff, doorRot, autoApprove, allowAutoRotate: false);
            if (doorResult.success)
            {
                placedCount++;
                if (!autoApprove && doorResult.proposalId != null)
                    proposalIds.Add(doorResult.proposalId);
            }
            else
            {
                failedCount++;
                var entry = new JSONObject();
                entry["defName"] = doorDef.defName;
                entry["x"] = doorCell.x;
                entry["z"] = doorCell.z;
                entry["error"] = doorResult.error;
                failuresList.Add(entry);
            }

            var result = new JSONObject();
            result["shape"] = "room";
            result["bounds"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["interior"] = (width - 2) + "x" + (height - 2);
            result["door_side"] = doorSide;
            result["door_position"] = doorCell.x + "," + doorCell.z;
            result["placed"] = placedCount;
            result["failed"] = failedCount;
            if (sharedCount > 0)
                result["shared"] = sharedCount;
            if (!autoApprove && proposalIds.Count > 0)
                result["proposal_ids"] = proposalIds;
            if (failuresList.Count > 0)
                result["failures"] = failuresList;

            // Render existing buildings and after area grid so the AI can see what changed
            if (existingInArea != null)
                result["existing_in_area"] = existingInArea;
            int gridX1 = System.Math.Max(0, minX - 1);
            int gridZ1 = System.Math.Max(0, minZ - 1);
            int gridX2 = System.Math.Min(map.Size.x - 1, maxX + 1);
            int gridZ2 = System.Math.Min(map.Size.z - 1, maxZ + 1);
            result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
            result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

            var adjacentHints = DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
            if (adjacentHints != null)
                result["adjacent_walls"] = adjacentHints;

            return result.ToString();
        }

        private static string PlaceWallLine(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int x1, int z1, int x2, int z2, bool autoApprove)
        {
            var cells = GetLine(x1, z1, x2, z2);

            // Scan existing buildings before placement
            JSONArray existingInArea;
            {
                int wlMinX = System.Math.Min(x1, x2);
                int wlMinZ = System.Math.Min(z1, z2);
                int wlMaxX = System.Math.Max(x1, x2);
                int wlMaxZ = System.Math.Max(z1, z2);
                int bbX1 = System.Math.Max(0, wlMinX - 1);
                int bbZ1 = System.Math.Max(0, wlMinZ - 1);
                int bbX2 = System.Math.Min(map.Size.x - 1, wlMaxX + 1);
                int bbZ2 = System.Math.Min(map.Size.z - 1, wlMaxZ + 1);
                existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);
            }

            var proposalIds = new JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new JSONArray();

            foreach (var cell in cells)
            {
                if (HasExistingWallOrBlueprint(cell, map))
                {
                    sharedCount++;
                    continue;
                }
                var pr = PlaceOneBlueprint(map, faction, wallDef, cell, wallStuff, Rot4.North, autoApprove);
                if (pr.success)
                {
                    placedCount++;
                    if (!autoApprove && pr.proposalId != null)
                        proposalIds.Add(pr.proposalId);
                }
                else
                {
                    failedCount++;
                    var entry = new JSONObject();
                    entry["defName"] = wallDef.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    failuresList.Add(entry);
                }
            }

            var result = new JSONObject();
            result["shape"] = "wall_line";
            result["from"] = x1 + "," + z1;
            result["to"] = x2 + "," + z2;
            result["placed"] = placedCount;
            result["failed"] = failedCount;
            if (sharedCount > 0)
                result["shared"] = sharedCount;
            if (!autoApprove && proposalIds.Count > 0)
                result["proposal_ids"] = proposalIds;
            if (failuresList.Count > 0)
                result["failures"] = failuresList;

            // Render existing buildings and after area grid so the AI can see what changed
            if (existingInArea != null)
                result["existing_in_area"] = existingInArea;
            {
                int wlMinX = System.Math.Min(x1, x2);
                int wlMinZ = System.Math.Min(z1, z2);
                int wlMaxX = System.Math.Max(x1, x2);
                int wlMaxZ = System.Math.Max(z1, z2);
                int gridX1 = System.Math.Max(0, wlMinX - 1);
                int gridZ1 = System.Math.Max(0, wlMinZ - 1);
                int gridX2 = System.Math.Min(map.Size.x - 1, wlMaxX + 1);
                int gridZ2 = System.Math.Min(map.Size.z - 1, wlMaxZ + 1);
                result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
                result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

                var adjacentHints = DetectAdjacentWalls(map, wlMinX, wlMinZ, wlMaxX, wlMaxZ);
                if (adjacentHints != null)
                    result["adjacent_walls"] = adjacentHints;
            }

            return result.ToString();
        }

        private static string PlaceWallRect(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, bool autoApprove)
        {
            var cells = GetRectOutline(minX, minZ, maxX, maxZ);

            // Scan existing buildings before placement
            int bbX1 = System.Math.Max(0, minX - 1);
            int bbZ1 = System.Math.Max(0, minZ - 1);
            int bbX2 = System.Math.Min(map.Size.x - 1, maxX + 1);
            int bbZ2 = System.Math.Min(map.Size.z - 1, maxZ + 1);
            JSONArray existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);

            var proposalIds = new JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new JSONArray();

            foreach (var cell in cells)
            {
                if (HasExistingWallOrBlueprint(cell, map))
                {
                    sharedCount++;
                    continue;
                }
                var pr = PlaceOneBlueprint(map, faction, wallDef, cell, wallStuff, Rot4.North, autoApprove);
                if (pr.success)
                {
                    placedCount++;
                    if (!autoApprove && pr.proposalId != null)
                        proposalIds.Add(pr.proposalId);
                }
                else
                {
                    failedCount++;
                    var entry = new JSONObject();
                    entry["defName"] = wallDef.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    failuresList.Add(entry);
                }
            }

            var result = new JSONObject();
            result["shape"] = "wall_rect";
            result["bounds"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["placed"] = placedCount;
            result["failed"] = failedCount;
            if (sharedCount > 0)
                result["shared"] = sharedCount;
            if (!autoApprove && proposalIds.Count > 0)
                result["proposal_ids"] = proposalIds;
            if (failuresList.Count > 0)
                result["failures"] = failuresList;

            // Render existing buildings and after area grid so the AI can see what changed
            if (existingInArea != null)
                result["existing_in_area"] = existingInArea;
            int gridX1 = System.Math.Max(0, minX - 1);
            int gridZ1 = System.Math.Max(0, minZ - 1);
            int gridX2 = System.Math.Min(map.Size.x - 1, maxX + 1);
            int gridZ2 = System.Math.Min(map.Size.z - 1, maxZ + 1);
            result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
            result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

            var adjacentHints = DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
            if (adjacentHints != null)
                result["adjacent_walls"] = adjacentHints;

            return result.ToString();
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

        private static PlacementResult PlaceOneBlueprint(Map map, Faction faction, ThingDef def, IntVec3 pos, ThingDef stuff, Rot4 rot, bool autoApprove, bool allowAutoRotate = true)
        {
            var pr = new PlacementResult();

            var report = GenConstruct.CanPlaceBlueprintAt(def, pos, rot, map, false, null, null, stuff);
            Rot4 finalRot = rot;
            if (!report.Accepted && allowAutoRotate && !typeof(Building_Door).IsAssignableFrom(def.thingClass))
            {
                // Try other rotations before giving up
                var originalReport = report;
                Rot4[] rotations = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
                foreach (var tryRot in rotations)
                {
                    if (tryRot == rot) continue;
                    var tryReport = GenConstruct.CanPlaceBlueprintAt(def, pos, tryRot, map, false, null, null, stuff);
                    if (tryReport.Accepted)
                    {
                        report = tryReport;
                        finalRot = tryRot;
                        break;
                    }
                }
                if (!report.Accepted)
                {
                    // All rotations failed — use original error message
                    string reason = originalReport.Reason ?? "blocked";
                    pr.error = "Cannot place at (" + pos.x + "," + pos.z + "): " + reason + GetPlacementHint(reason, def, map, pos);
                    return pr;
                }
            }
            else if (!report.Accepted)
            {
                string reason = report.Reason ?? "blocked";
                pr.error = "Cannot place at (" + pos.x + "," + pos.z + "): " + reason + GetPlacementHint(reason, def, map, pos);
                return pr;
            }

            var blueprint = GenConstruct.PlaceBlueprintForBuild(def, pos, map, finalRot, faction, stuff);
            if (blueprint == null)
            {
                pr.error = "Failed to place blueprint for " + def.label;
                return pr;
            }

            if (!autoApprove)
            {
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
                pr.proposalId = proposalId;
            }

            pr.success = true;
            pr.blueprint = blueprint;
            pr.autoRotated = (finalRot != rot);
            pr.finalRotation = finalRot.AsInt;
            return pr;
        }

        // Phase 2: Material pre-check
        private static MaterialCheckResult CheckMaterials(Map map, ThingDef buildingDef, ThingDef stuff)
        {
            var result = new MaterialCheckResult();
            result.hasMaterials = true;

            var shortageList = new List<string>();
            var shortagesArray = new JSONArray();

            // Calculate total material cost
            var costList = new Dictionary<ThingDef, int>();

            // Add stuff cost if applicable
            if (buildingDef.MadeFromStuff && stuff != null)
            {
                int stuffCost = buildingDef.costStuffCount;
                if (stuffCost > 0)
                    costList[stuff] = stuffCost;
            }

            // Add other costs
            if (buildingDef.costList != null)
            {
                foreach (var cost in buildingDef.costList)
                {
                    if (costList.ContainsKey(cost.thingDef))
                        costList[cost.thingDef] += cost.count;
                    else
                        costList[cost.thingDef] = cost.count;
                }
            }

            // Check availability
            foreach (var kvp in costList)
            {
                var material = kvp.Key;
                int needed = kvp.Value;
                int available = map.resourceCounter.GetCount(material);

                if (available < needed)
                {
                    result.hasMaterials = false;
                    int shortage = needed - available;
                    string shortageMsg = material.LabelCap + ": need " + shortage + " more (have " + available + "/" + needed + ")";
                    shortageList.Add(shortageMsg);

                    var shortageObj = new JSONObject();
                    shortageObj["material"] = material.defName;
                    shortageObj["label"] = material.LabelCap.ToString();
                    shortageObj["needed"] = needed;
                    shortageObj["available"] = available;
                    shortageObj["shortage"] = shortage;
                    shortagesArray.Add(shortageObj);
                }
            }

            if (!result.hasMaterials)
            {
                result.warning = "Insufficient materials: " + string.Join(", ", shortageList);
                result.shortages = shortagesArray;
            }

            return result;
        }

        // --- Utility helpers ---

        // LLMs sometimes send JSON arrays as double-encoded strings -- unwrap them
        private static JSONNode UnwrapStringArray(JSONNode node)
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

        private static ThingDef ResolveBuildingDef(string defName)
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

        private static ThingDef ResolveStuffDef(string stuffName, ThingDef buildingDef)
        {
            var stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
            if (stuff != null && stuff.IsStuff && IsStuffValidForBuilding(stuff, buildingDef))
                return stuff;

            // Fuzzy: case-insensitive match
            foreach (var candidate in DefDatabase<ThingDef>.AllDefs)
            {
                if (!candidate.IsStuff) continue;
                if (!string.Equals(candidate.defName, stuffName, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsStuffValidForBuilding(candidate, buildingDef))
                    return candidate;
            }

            return null;
        }

        private static bool IsStuffValidForBuilding(ThingDef stuff, ThingDef buildingDef)
        {
            if (buildingDef.stuffCategories == null || stuff.stuffProps?.categories == null)
                return false;
            foreach (var cat in stuff.stuffProps.categories)
            {
                if (buildingDef.stuffCategories.Contains(cat))
                    return true;
            }
            return false;
        }

        private static string FindSimilarBuildings(string defName)
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

        private static string FindSimilarStuffs(string stuffName, ThingDef buildingDef)
        {
            if (string.IsNullOrEmpty(stuffName) || buildingDef == null) return null;

            var matches = new List<string>();
            string lower = stuffName.ToLower();

            foreach (var candidate in DefDatabase<ThingDef>.AllDefs)
            {
                if (!candidate.IsStuff) continue;
                if (!IsStuffValidForBuilding(candidate, buildingDef)) continue;

                if (candidate.defName.ToLower().Contains(lower)
                    || (candidate.label != null && candidate.label.ToLower().Contains(lower)))
                {
                    matches.Add(candidate.defName);
                    if (matches.Count >= 3) break;
                }
            }

            return matches.Count > 0 ? string.Join(", ", matches) : null;
        }

        private static string GetStuffHint(ThingDef def)
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

        private static string GetPlacementHint(string reason, ThingDef def, Map map = null, IntVec3 pos = default)
        {
            if (string.IsNullOrEmpty(reason)) return "";

            if (reason.IndexOf("Occupied", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string occupantInfo = null;
                if (map != null && pos.InBounds(map))
                {
                    var things = pos.GetThingList(map);
                    foreach (var t in things)
                    {
                        if (t.def.category == ThingCategory.Building || t is Blueprint)
                        {
                            string size = t.def.size.x + "x" + t.def.size.z;
                            occupantInfo = t.def.label + (size != "1x1" ? " (" + size + ")" : "");
                            break;
                        }
                    }
                }
                if (occupantInfo != null)
                    return " Occupied by " + occupantInfo + ". Try adjacent cells.";
                return " Try adjacent cells or remove existing building first.";
            }

            if (reason.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("afford", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string needed = def.terrainAffordanceNeeded != null ? def.terrainAffordanceNeeded.defName : "suitable terrain";
                return " This needs " + needed + ". Try a different location.";
            }

            if (reason.IndexOf("Would block", StringComparison.OrdinalIgnoreCase) >= 0)
                return " Would block an adjacent door or passage.";

            return "";
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

        private static int ParseDoorOffset(JSONNode offsetNode, int innerLen)
        {
            if (innerLen <= 0) return 0;
            if (offsetNode == null || string.IsNullOrEmpty(offsetNode.Value))
                return innerLen / 2; // default: center
            int offset = offsetNode.AsInt;
            if (offset < 0) offset = 0;
            if (offset >= innerLen) offset = innerLen - 1;
            return offset;
        }

        private static bool HasExistingWallOrBlueprint(IntVec3 pos, Map map)
        {
            var thingList = pos.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                var thing = thingList[i];
                if (thing.def.category == ThingCategory.Building && thing.def.holdsRoof)
                    return true;
                if (thing is Blueprint_Build bp && bp.def.entityDefToBuild is ThingDef td && td.holdsRoof)
                    return true;
            }
            return false;
        }

        // --- Adjacent wall detection ---

        private static JSONArray DetectAdjacentWalls(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var hints = new JSONArray();

            // Check 1 cell west of west wall (x = minX - 1)
            if (minX > 0 && HasWallLine(map, minX - 1, minZ, minX - 1, maxZ))
                hints.Add("Existing wall 1 cell west at x=" + (minX - 1) + ". Use x1=" + (minX - 1) + " to share walls.");

            // Check 1 cell east of east wall (x = maxX + 1)
            if (maxX < map.Size.x - 1 && HasWallLine(map, maxX + 1, minZ, maxX + 1, maxZ))
                hints.Add("Existing wall 1 cell east at x=" + (maxX + 1) + ". Use x2=" + (maxX + 1) + " to share walls.");

            // Check 1 cell south of south wall (z = minZ - 1)
            if (minZ > 0 && HasWallLine(map, minX, minZ - 1, maxX, minZ - 1))
                hints.Add("Existing wall 1 cell south at z=" + (minZ - 1) + ". Use z1=" + (minZ - 1) + " to share walls.");

            // Check 1 cell north of north wall (z = maxZ + 1)
            if (maxZ < map.Size.z - 1 && HasWallLine(map, minX, maxZ + 1, maxX, maxZ + 1))
                hints.Add("Existing wall 1 cell north at z=" + (maxZ + 1) + ". Use z2=" + (maxZ + 1) + " to share walls.");

            return hints.Count > 0 ? hints : null;
        }

        private static bool HasWallLine(Map map, int x1, int z1, int x2, int z2)
        {
            // Check if at least 3 cells along this line have walls or wall blueprints
            // (avoids false positives from single random walls)
            int wallCount = 0;
            int totalCells = 0;

            int lineMinX = Math.Min(x1, x2), lineMaxX = Math.Max(x1, x2);
            int lineMinZ = Math.Min(z1, z2), lineMaxZ = Math.Max(z1, z2);

            for (int z = lineMinZ; z <= lineMaxZ; z++)
            {
                for (int x = lineMinX; x <= lineMaxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;
                    totalCells++;
                    if (HasExistingWallOrBlueprint(cell, map))
                        wallCount++;
                }
            }

            // Require at least 3 walls or 50% of the line to count as a wall line
            return wallCount >= 3 || (totalCells > 0 && wallCount >= totalCells * 0.5);
        }

        // --- Area scanning helper ---

        private static JSONArray ScanBuildingsInArea(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var seen = new HashSet<Thing>();
            var wallCounts = new Dictionary<string, int>(); // stuff -> count for walls
            var entries = new JSONArray();

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;

                    foreach (var thing in cell.GetThingList(map))
                    {
                        if (seen.Contains(thing)) continue;

                        ThingDef buildDef = null;
                        bool isBlueprint = false;

                        if (thing is Blueprint_Build bpb)
                        {
                            buildDef = bpb.def.entityDefToBuild as ThingDef;
                            isBlueprint = true;
                        }
                        else if (thing.def.category == ThingCategory.Building)
                        {
                            if (typeof(Blueprint).IsAssignableFrom(thing.def.thingClass)) continue;
                            if (typeof(Frame).IsAssignableFrom(thing.def.thingClass)) continue;
                            buildDef = thing.def;
                        }

                        if (buildDef == null) continue;
                        seen.Add(thing);

                        // Summarize walls by count instead of listing individually
                        bool isWall = buildDef.passability == Traversability.Impassable && buildDef.fillPercent >= 0.9f;
                        if (isWall)
                        {
                            string stuffKey = (isBlueprint ? "blueprint:" : "") + (thing.Stuff?.defName ?? "none");
                            wallCounts[stuffKey] = (wallCounts.ContainsKey(stuffKey) ? wallCounts[stuffKey] : 0) + 1;
                            continue;
                        }

                        var entry = new JSONObject();
                        entry["def"] = buildDef.defName;
                        entry["label"] = buildDef.label;
                        if (isBlueprint) entry["blueprint"] = true;
                        entry["x"] = thing.Position.x;
                        entry["z"] = thing.Position.z;
                        string size = buildDef.size.x + "x" + buildDef.size.z;
                        if (size != "1x1") entry["size"] = size;
                        if (thing.Stuff != null) entry["stuff"] = thing.Stuff.defName;
                        entries.Add(entry);
                    }
                }
            }

            // Add wall summaries
            foreach (var kvp in wallCounts)
            {
                var entry = new JSONObject();
                bool isBp = kvp.Key.StartsWith("blueprint:");
                string stuff = isBp ? kvp.Key.Substring(10) : kvp.Key;
                entry["def"] = "Wall";
                entry["count"] = kvp.Value;
                if (isBp) entry["blueprint"] = true;
                if (stuff != "none") entry["stuff"] = stuff;
                entries.Add(entry);
            }

            return entries;
        }

        // --- Shape helpers ---

        private static List<IntVec3> GetRectOutline(int x1, int z1, int x2, int z2)
        {
            var cells = new List<IntVec3>();
            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            for (int x = minX; x <= maxX; x++)
            {
                cells.Add(new IntVec3(x, 0, minZ));
                if (minZ != maxZ) cells.Add(new IntVec3(x, 0, maxZ));
            }
            for (int z = minZ + 1; z < maxZ; z++)
            {
                cells.Add(new IntVec3(minX, 0, z));
                if (minX != maxX) cells.Add(new IntVec3(maxX, 0, z));
            }
            return cells;
        }

        private static List<IntVec3> GetLine(int x1, int z1, int x2, int z2)
        {
            var cells = new List<IntVec3>();
            int dx = Math.Abs(x2 - x1), dz = Math.Abs(z2 - z1);
            int sx = x1 < x2 ? 1 : -1, sz = z1 < z2 ? 1 : -1;
            int err = dx - dz;
            int cx = x1, cz = z1;
            while (true)
            {
                cells.Add(new IntVec3(cx, 0, cz));
                if (cx == x2 && cz == z2) break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; cx += sx; }
                if (e2 < dx) { err += dx; cz += sz; }
            }
            return cells;
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

            // Result object
            var result = new JSONObject();
            result["building"] = def.defName;
            result["position"] = new JSONArray { x, z };
            result["rotation"] = rotation.ToStringHuman().ToLower();

            // Size (accounting for rotation)
            IntVec2 rotatedSize = def.size.RotatedBy(rotation);
            result["size"] = new JSONArray { rotatedSize.x, rotatedSize.z };

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
                foreach (var warning in adjacentCheck.AsArray)
                    warnings.Add(warning);
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
            var result = new JSONObject();
            
            foreach (var cell in cells)
            {
                if (!cell.InBounds(map))
                {
                    result["ok"] = false;
                    result["detail"] = "Position out of map bounds";
                    return result;
                }

                var terrain = map.terrainGrid.TerrainAt(cell);
                
                // Check terrain affordance
                if (def.terrainAffordanceNeeded != null)
                {
                    if (!terrain.affordances.Contains(def.terrainAffordanceNeeded))
                    {
                        result["ok"] = false;
                        result["detail"] = string.Format("Cell ({0},{1}) has {2} terrain, needs {3}",
                            cell.x, cell.z, terrain.label, def.terrainAffordanceNeeded.label);
                        return result;
                    }
                }

                // Check for impassable terrain (water, lava, etc.)
                if (!terrain.passability.Equals(Traversability.Standable))
                {
                    result["ok"] = false;
                    result["detail"] = string.Format("Cell ({0},{1}) is {2} (not buildable)",
                        cell.x, cell.z, terrain.label);
                    return result;
                }
            }

            result["ok"] = true;
            result["detail"] = "All cells have suitable terrain";
            return result;
        }

        private static JSONObject CheckSpace(Map map, List<IntVec3> cells)
        {
            var result = new JSONObject();
            
            foreach (var cell in cells)
            {
                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    // Check for existing buildings
                    if (thing.def.category == ThingCategory.Building)
                    {
                        result["ok"] = false;
                        result["detail"] = string.Format("Cell ({0},{1}) occupied by {2}",
                            cell.x, cell.z, thing.def.label);
                        return result;
                    }

                    // Check for blueprints
                    if (thing is Blueprint)
                    {
                        result["ok"] = false;
                        result["detail"] = string.Format("Cell ({0},{1}) has blueprint for {2}",
                            cell.x, cell.z, thing.def.label);
                        return result;
                    }

                    // Check for frames
                    if (thing is Frame)
                    {
                        result["ok"] = false;
                        result["detail"] = string.Format("Cell ({0},{1}) has construction frame",
                            cell.x, cell.z);
                        return result;
                    }
                }
            }

            result["ok"] = true;
            result["detail"] = "All cells are clear";
            return result;
        }

        private static JSONObject CheckPower(Map map, ThingDef def, IntVec3 pos)
        {
            var result = new JSONObject();
            
            // Check if building needs power
            var powerComp = def.comps?.Find(c => c is CompProperties_Power) as CompProperties_Power;
            if (powerComp == null || powerComp.PowerConsumption <= 0)
            {
                // Doesn't need power
                return result;
            }

            // Find nearest powered conduit
            var powerNet = map.powerNetManager.AllNetsListForReading;
            float nearestDistance = float.MaxValue;
            IntVec3? nearestConduit = null;

            foreach (var net in powerNet)
            {
                foreach (var transmitter in net.transmitters)
                {
                    float dist = pos.DistanceTo(transmitter.parent.Position);
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearestConduit = transmitter.parent.Position;
                    }
                }
            }

            // Power connection range is typically 6 cells
            int maxRange = 6;
            if (nearestConduit.HasValue && nearestDistance <= maxRange)
            {
                result["ok"] = true;
                result["detail"] = string.Format("Power conduit {0} cells away at ({1},{2})",
                    (int)nearestDistance, nearestConduit.Value.x, nearestConduit.Value.z);
            }
            else if (nearestConduit.HasValue)
            {
                result["ok"] = false;
                result["detail"] = string.Format("Nearest power conduit is {0} cells away (max range: {1})",
                    (int)nearestDistance, maxRange);
            }
            else
            {
                result["ok"] = false;
                result["detail"] = "No power grid found on map";
            }

            return result;
        }

        private static JSONObject CheckRoof(Map map, ThingDef def, List<IntVec3> cells)
        {
            var result = new JSONObject();
            
            // Some buildings work better or require roof
            bool needsRoof = false;
            string reason = null;

            // Electric stoves, coolers, heaters prefer indoor
            if (def.defName.Contains("Stove") || def.defName.Contains("Cooler") || 
                def.defName.Contains("Heater") || def.building?.isEdifice == true)
            {
                needsRoof = true;
                reason = "works best indoors";
            }

            if (!needsRoof)
            {
                return result; // No roof check needed
            }

            int roofedCells = 0;
            foreach (var cell in cells)
            {
                if (cell.Roofed(map))
                    roofedCells++;
            }

            if (roofedCells == cells.Count)
            {
                result["ok"] = true;
                result["detail"] = "Fully roofed (indoor)";
            }
            else if (roofedCells > 0)
            {
                result["ok"] = false;
                result["detail"] = string.Format("Partially roofed ({0}/{1} cells) - {2}",
                    roofedCells, cells.Count, reason);
            }
            else
            {
                result["ok"] = false;
                result["detail"] = "Unroofed (outdoor) - " + reason;
                result["required"] = false; // Warning, not blocker
            }

            return result;
        }

        private static JSONObject CheckSpecialRules(Map map, ThingDef def, IntVec3 pos, Rot4 rotation, List<IntVec3> cells)
        {
            var result = new JSONObject();
            
            // Check interaction cell (for workbenches, beds, etc.)
            if (def.hasInteractionCell)
            {
                var interactionCell = ThingUtility.InteractionCellWhenAt(def, pos, rotation, map);
                
                if (!interactionCell.InBounds(map))
                {
                    result["ok"] = false;
                    result["detail"] = "Interaction cell out of bounds - rotate or move";
                    return result;
                }

                if (!interactionCell.Standable(map))
                {
                    result["ok"] = false;
                    result["detail"] = string.Format("Interaction cell ({0},{1}) blocked - pawns cannot access",
                        interactionCell.x, interactionCell.z);
                    return result;
                }

                var things = interactionCell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing.def.passability == Traversability.Impassable)
                    {
                        result["ok"] = false;
                        result["detail"] = string.Format("Interaction cell ({0},{1}) blocked by {2}",
                            interactionCell.x, interactionCell.z, thing.def.label);
                        return result;
                    }
                }
            }

            // Check for vents (need adjacent wall)
            if (def.defName.Contains("Vent"))
            {
                bool hasAdjacentWall = false;
                foreach (var cell in cells)
                {
                    foreach (var adj in GenAdj.CardinalDirections)
                    {
                        var adjCell = cell + adj;
                        if (!adjCell.InBounds(map)) continue;
                        
                        var edifice = adjCell.GetEdifice(map);
                        if (edifice != null && edifice.def.holdsRoof)
                        {
                            hasAdjacentWall = true;
                            break;
                        }
                    }
                    if (hasAdjacentWall) break;
                }

                if (!hasAdjacentWall)
                {
                    result["ok"] = false;
                    result["detail"] = "Vents must be placed adjacent to a wall";
                    return result;
                }
            }

            result["ok"] = true;
            result["detail"] = "No special placement issues";
            return result;
        }

        private static JSONArray CheckAdjacent(Map map, List<IntVec3> cells)
        {
            var warnings = new JSONArray();
            
            // Check for outdoor adjacency (temperature concerns)
            bool hasOutdoorAdjacent = false;
            foreach (var cell in cells)
            {
                foreach (var adj in GenAdj.CardinalDirections)
                {
                    var adjCell = cell + adj;
                    if (!adjCell.InBounds(map)) continue;
                    
                    if (!adjCell.Roofed(map))
                    {
                        hasOutdoorAdjacent = true;
                        break;
                    }
                }
                if (hasOutdoorAdjacent) break;
            }

            if (hasOutdoorAdjacent)
            {
                warnings.Add("Adjacent to outdoor area - may affect temperature");
            }

            return warnings;
        }

        private static string SuggestAlternative(Map map, ThingDef def, IntVec3 pos, Rot4 rotation)
        {
            // Try nearby cells (simple search within 5 cells)
            for (int radius = 1; radius <= 5; radius++)
            {
                foreach (var offset in GenRadial.RadialCellsAround(pos, radius, true))
                {
                    var testPos = pos + offset;
                    if (!testPos.InBounds(map)) continue;

                    var report = GenConstruct.CanPlaceBlueprintAt(def, testPos, rotation, map, false, null, null, null);
                    if (report.Accepted)
                    {
                        return string.Format("Try position ({0},{1}) instead", testPos.x, testPos.z);
                    }
                }
            }

            return null;
        }
    }
}
