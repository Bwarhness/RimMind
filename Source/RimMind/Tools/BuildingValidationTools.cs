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
    /// Validation and placement-checking tools extracted from BuildingTools.
    /// CheckPlacement, CheckTerrain, CheckSpace, CheckPower, CheckRoof, CheckSpecialRules, CheckAdjacent, SuggestAlternative.
    /// </summary>
    public static class BuildingValidationTools
    {
        internal struct MaterialCheckResult
        {
            public bool hasMaterials;
            public string warning;
            public JSONArray shortages;
        }

        // --- Public entry point ---

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
            var def = BuildingHelpers.ResolveBuildingDef(buildingDefName);
            if (def == null)
            {
                string suggestions = BuildingHelpers.FindSimilarBuildings(buildingDefName);
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

        // --- Validation helpers ---

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

        // --- Material pre-check (used by PlaceBuilding) ---

        internal static MaterialCheckResult CheckMaterials(Map map, ThingDef buildingDef, ThingDef stuff)
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
    }
}
