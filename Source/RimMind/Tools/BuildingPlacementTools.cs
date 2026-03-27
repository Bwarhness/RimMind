using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimMind.API;
using RimMind.Core;
using static RimMind.Tools.BuildingHelpers;

namespace RimMind.Tools
    {
    /// <summary>
    /// Building placement tools — place blueprints, structures, and validate placements.
    /// Extracted from BuildingTools.cs.
    /// </summary>
    public static class BuildingPlacementTools
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


        // --- Placement tools (remain here) ---

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


        // ========================================================================
        // Shared shape placement result
        // ========================================================================

        private class ShapePlacementResult
        {
            public int placedCount;
            public int failedCount;
            public int sharedCount;
            public JSONArray proposalIds = new JSONArray();
            public JSONArray failuresList = new JSONArray();
            public JSONArray existingInArea;
            public int gridX1, gridZ1, gridX2, gridZ2;
        }

        // ========================================================================
        // PlaceShapeCore — shared placement logic for wall_line and wall_rect
        // ========================================================================

        private static ShapePlacementResult PlaceShapeCore(
            Map map, Faction faction, ThingDef def, ThingDef stuff,
            List<IntVec3> cells, int bbX1, int bbZ1, int bbX2, int bbZ2,
            int gridX1, int gridZ1, int gridX2, int gridZ2,
            bool autoApprove, HashSet<IntVec3> excludeCells = null)
        {
            var result = new ShapePlacementResult
            {
                existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2),
                gridX1 = gridX1, gridZ1 = gridZ1, gridX2 = gridX2, gridZ2 = gridZ2
            };

            foreach (var cell in cells)
            {
                if (excludeCells != null && excludeCells.Contains(cell))
                    continue;

                if (HasExistingWallOrBlueprint(cell, map))
                {
                    result.sharedCount++;
                    continue;
                }

                var pr = PlaceOneBlueprint(map, faction, def, cell, stuff, Rot4.North, autoApprove);
                if (pr.success)
                {
                    result.placedCount++;
                    if (!autoApprove && pr.proposalId != null)
                        result.proposalIds.Add(pr.proposalId);
                }
                else
                {
                    result.failedCount++;
                    var entry = new JSONObject();
                    entry["defName"] = def.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    result.failuresList.Add(entry);
                }
            }

            return result;
        }

        private static JSONObject BuildShapeResult(string shape, ShapePlacementResult sr,
            int gridX1, int gridZ1, int gridX2, int gridZ2,
            Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var result = new JSONObject();
            result["shape"] = shape;
            result["bounds"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["placed"] = sr.placedCount;
            result["failed"] = sr.failedCount;
            if (sr.sharedCount > 0)
                result["shared"] = sr.sharedCount;
            if (sr.proposalIds.Count > 0)
                result["proposal_ids"] = sr.proposalIds;
            if (sr.failuresList.Count > 0)
                result["failures"] = sr.failuresList;
            if (sr.existingInArea != null)
                result["existing_in_area"] = sr.existingInArea;
            result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
            result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);
            var adjacentHints = DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
            if (adjacentHints != null)
                result["adjacent_walls"] = adjacentHints;
            return result;
        }

        // ========================================================================
        // PlaceRoom — uses PlaceShapeCore for walls, places door separately
        // ========================================================================

        private static string PlaceRoom(Map map, Faction faction, ThingDef wallDef, ThingDef doorDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, JSONNode args, bool autoApprove)
        {
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            if (width > 25 || height > 25)
                return ToolExecutor.JsonError("Maximum room size is 25x25. Got " + width + "x" + height + ".");
            if (width < 3 || height < 3)
                return ToolExecutor.JsonError("Minimum room size is 3x3 (1x1 interior + walls). Got " + width + "x" + height + ".");

            // Door stuff resolution
            string doorStuffName = args?["door_stuff"]?.Value;
            if (string.IsNullOrEmpty(doorStuffName) || doorStuffName == "null")
                doorStuffName = args?["stuff"]?.Value;
            if (doorStuffName == "null") doorStuffName = null;
            ThingDef doorStuff = wallStuff;
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
                var checkDoorStuff = ResolveStuffDef(doorStuff.defName, doorDef);
                if (checkDoorStuff == null)
                {
                    doorStuff = ResolveStuffDef("WoodLog", doorDef);
                    if (doorStuff == null)
                        return ToolExecutor.JsonError("Wall stuff '" + wallStuff.defName + "' is not valid for doors. Specify 'door_stuff'.");
                }
            }

            // Door position
            string doorSide = (args?["door_side"]?.Value ?? "south").ToLower();
            int innerLen = doorSide == "west" || doorSide == "east" ? height - 2 : width - 2;
            int doorOffset = ParseDoorOffset(args?["door_offset"], innerLen);

            IntVec3 doorCell;
            Rot4 doorRot;
            switch (doorSide)
            {
                case "south":
                    doorCell = new IntVec3(minX + 1 + doorOffset, 0, minZ);
                    doorRot = Rot4.North;
                    break;
                case "north":
                    doorCell = new IntVec3(minX + 1 + doorOffset, 0, maxZ);
                    doorRot = Rot4.North;
                    break;
                case "west":
                    doorCell = new IntVec3(minX, 0, minZ + 1 + doorOffset);
                    doorRot = Rot4.East;
                    break;
                case "east":
                    doorCell = new IntVec3(maxX, 0, minZ + 1 + doorOffset);
                    doorRot = Rot4.East;
                    break;
                default:
                    return ToolExecutor.JsonError("Invalid door_side: " + doorSide + ". Valid: north, south, east, west.");
            }

            var excludeCells = new HashSet<IntVec3> { doorCell };
            var wallCells = GetRectOutline(minX, minZ, maxX, maxZ);

            int bbX1 = Math.Max(0, minX - 1), bbZ1 = Math.Max(0, minZ - 1);
            int bbX2 = Math.Min(map.Size.x - 1, maxX + 1), bbZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            int gridX1 = bbX1, gridZ1 = bbZ1, gridX2 = bbX2, gridZ2 = bbZ2;

            var sr = PlaceShapeCore(map, faction, wallDef, wallStuff, wallCells,
                bbX1, bbZ1, bbX2, bbZ2, gridX1, gridZ1, gridX2, gridZ2, autoApprove, excludeCells);

            // Place door blueprint
            var doorResult = PlaceOneBlueprint(map, faction, doorDef, doorCell, doorStuff, doorRot, autoApprove, allowAutoRotate: false);
            if (doorResult.success)
            {
                sr.placedCount++;
                if (!autoApprove && doorResult.proposalId != null)
                    sr.proposalIds.Add(doorResult.proposalId);
            }
            else
            {
                sr.failedCount++;
                var entry = new JSONObject();
                entry["defName"] = doorDef.defName;
                entry["x"] = doorCell.x;
                entry["z"] = doorCell.z;
                entry["error"] = doorResult.error;
                sr.failuresList.Add(entry);
            }

            var result = BuildShapeResult("room", sr, gridX1, gridZ1, gridX2, gridZ2, map, minX, minZ, maxX, maxZ);
            result["interior"] = (width - 2) + "x" + (height - 2);
            result["door_side"] = doorSide;
            result["door_position"] = doorCell.x + "," + doorCell.z;
            return result.ToString();
        }

        // ========================================================================
        // PlaceWallLine — delegates entirely to PlaceShapeCore
        // ========================================================================

        private static string PlaceWallLine(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int x1, int z1, int x2, int z2, bool autoApprove)
        {
            var cells = GetLine(x1, z1, x2, z2);

            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            int bbX1 = Math.Max(0, minX - 1), bbZ1 = Math.Max(0, minZ - 1);
            int bbX2 = Math.Min(map.Size.x - 1, maxX + 1), bbZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            int gridX1 = bbX1, gridZ1 = bbZ1, gridX2 = bbX2, gridZ2 = bbZ2;

            var sr = PlaceShapeCore(map, faction, wallDef, wallStuff, cells,
                bbX1, bbZ1, bbX2, bbZ2, gridX1, gridZ1, gridX2, gridZ2, autoApprove);

            var result = BuildShapeResult("wall_line", sr, gridX1, gridZ1, gridX2, gridZ2, map, minX, minZ, maxX, maxZ);
            result["from"] = x1 + "," + z1;
            result["to"] = x2 + "," + z2;
            return result.ToString();
        }

        // ========================================================================
        // PlaceWallRect — delegates entirely to PlaceShapeCore
        // ========================================================================

        private static string PlaceWallRect(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, bool autoApprove)
        {
            var cells = GetRectOutline(minX, minZ, maxX, maxZ);

            int bbX1 = Math.Max(0, minX - 1), bbZ1 = Math.Max(0, minZ - 1);
            int bbX2 = Math.Min(map.Size.x - 1, maxX + 1), bbZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            int gridX1 = bbX1, gridZ1 = bbZ1, gridX2 = bbX2, gridZ2 = bbZ2;

            var sr = PlaceShapeCore(map, faction, wallDef, wallStuff, cells,
                bbX1, bbZ1, bbX2, bbZ2, gridX1, gridZ1, gridX2, gridZ2, autoApprove);

            var result = BuildShapeResult("wall_rect", sr, gridX1, gridZ1, gridX2, gridZ2, map, minX, minZ, maxX, maxZ);
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

        // --- Private helpers ---

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
            return BuildingHelpers.ResolveBuildingDef(defName);
        }

        private static string FindSimilarBuildings(string defName)
        {
            return BuildingHelpers.FindSimilarBuildings(defName);
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
            return BuildingHelpers.GetStuffHint(def);
        }

        private static string GetPlacementHint(string reason, ThingDef def, Map map, IntVec3 pos)
        {
            string hint = "";

            if (reason != null && def != null && map != null && pos.IsValid)
            {
                if (reason.ToLower().Contains("blocked"))
                {
                    var things = pos.GetThingList(map);
                    if (things.Count > 0)
                    {
                        var blocking = things.FirstOrDefault(t => t.def.category == ThingCategory.Building || t is Blueprint);
                        if (blocking != null)
                            hint = ". Blocked by: " + blocking.def.label;
                    }
                }

                if (def.placeWorkers != null)
                {
                    foreach (var pwType in def.placeWorkers)
                    {
                        string n = pwType.Name;
                        if (n.Contains("OnSteamGeyser"))
                            hint += ". Must be on a steam geyser.";
                        else if (n.Contains("NotUnderRoof"))
                            hint += ". Must be placed outdoors.";
                    }
                }
            }

            return hint;
        }

        private static Rot4 ParseRotation(object rotationNode)
        {
            if (rotationNode == null) return Rot4.North;
            int rot = 0;
            if (rotationNode is int ri) rot = ri;
            else if (rotationNode is string rs && int.TryParse(rs, out int parsed)) rot = parsed;
            else if (rotationNode is JSONNode jn) rot = jn.AsInt;

            rot = ((rot % 4) + 4) % 4;
            return rot switch
            {
                1 => Rot4.East,
                2 => Rot4.South,
                3 => Rot4.West,
                _ => Rot4.North
            };
        }

        private static int ParseDoorOffset(object offsetNode, int wallLen)
        {
            if (offsetNode == null) return wallLen / 2;
            int offset = 0;
            if (offsetNode is int oi) offset = oi;
            else if (offsetNode is string os && int.TryParse(os, out int parsed)) offset = parsed;
            else if (offsetNode is JSONNode jn) offset = jn.AsInt;
            return ((offset % wallLen) + wallLen) % wallLen;
        }

        private static bool HasExistingWallOrBlueprint(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return true;

            var edifice = cell.GetEdifice(map);
            if (edifice != null)
            {
                if (edifice.def.passability == Traversability.Impassable) return true;
                if (typeof(Blueprint).IsAssignableFrom(edifice.def.thingClass)) return true;
            }

            // Also check blueprints that haven't been placed yet
            var things = cell.GetThingList(map);
            foreach (var t in things)
            {
                if (t is Blueprint bp && bp.def.entityDefToBuild is ThingDef td)
                {
                    if (td.passability == Traversability.Impassable) return true;
                }
            }

            return false;
        }

        private static JSONArray DetectAdjacentWalls(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var hints = new JSONArray();

            for (int x = minX; x <= maxX; x++)
            {
                if (HasWallLine(map, x, minZ - 1)) hints.Add("wall adjacent to north at x=" + x);
                if (HasWallLine(map, x, maxZ + 1)) hints.Add("wall adjacent to south at x=" + x);
            }
            for (int z = minZ; z <= maxZ; z++)
            {
                if (HasWallLine(map, minX - 1, z)) hints.Add("wall adjacent to west at z=" + z);
                if (HasWallLine(map, maxX + 1, z)) hints.Add("wall adjacent to east at z=" + z);
            }

            return hints;
        }

        private static bool HasWallLine(Map map, int x, int z)
        {
            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return false;
            var edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def.passability == Traversability.Impassable;
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
            var result = new JSONObject();
            
            if (map == null || def == null || cells == null || cells.Count == 0)
            {
                result["ok"] = false;
                result["detail"] = "Invalid parameters for terrain check";
                return result;
            }
            
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
                if (terrain.passability != Traversability.Standable)
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
            
            if (map == null || cells == null || cells.Count == 0)
            {
                result["ok"] = false;
                result["detail"] = "Invalid parameters for space check";
                return result;
            }
            
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
            
            if (map == null || def == null)
                return result;
            
            // Check if building needs power
            var powerComp = def.comps?.Find(c => c is CompProperties_Power) as CompProperties_Power;
            if (powerComp == null || powerComp.PowerConsumption <= 0)
            {
                // Doesn't need power
                return result;
            }

            // Find nearest powered conduit
            var powerNet = map.powerNetManager?.AllNetsListForReading;
            if (powerNet == null || powerNet.Count == 0)
            {
                result["ok"] = false;
                result["detail"] = "No power grid found on map";
                return result;
            }
            
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
                result["detail"] = "No powered conduits found on map";
            }

            return result;
        }

        private static JSONObject CheckRoof(Map map, ThingDef def, List<IntVec3> cells)
        {
            var result = new JSONObject();
            
            if (map == null || def == null || cells == null || cells.Count == 0)
                return result; // No roof check if invalid params
            
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
            
            if (map == null || def == null || cells == null)
            {
                result["ok"] = false;
                result["detail"] = "Invalid parameters for special rules check";
                return result;
            }
            
            // Check interaction cell (for workbenches, beds, etc.)
            if (def.hasInteractionCell)
            {
                var interactionCell = ThingUtility.InteractionCellWhenAt(def, pos, rotation, map);
                
                if (!interactionCell.IsValid || !interactionCell.InBounds(map))
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
            
            if (map == null || cells == null || cells.Count == 0)
                return warnings;
            
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