using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class PowerTools
    {
        public static string AnalyzePowerGrid()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var networks = new JSONArray();
            var unpowered = new JSONArray();

            var nets = map.powerNetManager.AllNetsListForReading;
            int networkId = 1;

            foreach (var net in nets)
            {
                var netObj = new JSONObject();
                netObj["id"] = networkId++;

                // Categorize buildings by type
                var buildings = new JSONArray();
                var generators = new JSONArray();
                var batteries = new JSONArray();

                float totalGen = 0;
                float totalCons = 0;

                foreach (var comp in net.powerComps)
                {
                    var building = comp.parent as Building;
                    if (building == null) continue;

                    string label = building.LabelCap;
                    var pos = building.Position;
                    string locStr = $"{label} ({pos.x},{pos.z})";

                    // Track generators
                    if (comp.PowerOutput > 0)
                    {
                        generators.Add(locStr + $" [{comp.PowerOutput:F0}W]");
                        totalGen += comp.PowerOutput;
                    }
                    // Track consumers
                    else if (comp is CompPowerTrader trader && trader.PowerOutput < 0)
                    {
                        buildings.Add(locStr + $" [{-trader.PowerOutput:F0}W]");
                        totalCons += -trader.PowerOutput;
                    }
                    else
                    {
                        buildings.Add(locStr);
                    }
                }

                // Track batteries
                foreach (var battery in net.batteryComps)
                {
                    var building = battery.parent as Building;
                    if (building == null) continue;
                    
                    string label = building.LabelCap;
                    var pos = building.Position;
                    batteries.Add($"{label} ({pos.x},{pos.z}) [{battery.StoredEnergy:F0}/{battery.Props.storedEnergyMax:F0} Wd]");
                }

                netObj["buildings"] = buildings;
                netObj["generators"] = generators;
                netObj["batteries"] = batteries;
                netObj["totalGeneration"] = $"{totalGen:F0} W";
                netObj["totalConsumption"] = $"{totalCons:F0} W";
                netObj["netPower"] = $"{(totalGen - totalCons):F0} W";

                if (totalGen >= totalCons)
                    netObj["status"] = "surplus";
                else if (totalGen > 0)
                    netObj["status"] = "deficit";
                else
                    netObj["status"] = "no_power";

                networks.Add(netObj);
            }

            // Find unpowered buildings that need power
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var powerComp = building.TryGetComp<CompPower>();
                if (powerComp == null) continue;

                // Skip if doesn't need power or is already powered
                if (powerComp.PowerOutput > 0) continue;

                var obj = new JSONObject();
                obj["defName"] = building.def.defName;
                obj["label"] = building.LabelCap;
                obj["position"] = $"({building.Position.x},{building.Position.z})";

                // Check if near a conduit
                var nearestConduit = FindNearestConduit(building.Position, map);
                if (nearestConduit != null)
                {
                    int dist = (nearestConduit.Value - building.Position).LengthManhattan;
                    obj["reason"] = $"near conduit but not connected (distance: {dist})";
                    obj["nearestConduit"] = $"({nearestConduit.Value.x},{nearestConduit.Value.z})";
                }
                else
                {
                    obj["reason"] = "no conduit connection";
                }

                unpowered.Add(obj);
            }

            result["networks"] = networks;
            result["networkCount"] = networks.Count;
            result["unpowered"] = unpowered;
            result["unpoweredCount"] = unpowered.Count;

            return result.ToString();
        }

        public static string CheckPowerConnection(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null)
                return ToolExecutor.JsonError("Missing arguments. Provide 'x' and 'z' or area with 'x1', 'z1', 'x2', 'z2'.");

            // Single cell check
            if (args["x"] != null && args["z"] != null)
            {
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                var cell = new IntVec3(x, 0, z);

                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError($"Coordinates ({x},{z}) out of bounds.");

                return CheckSingleCell(cell, map).ToString();
            }

            // Area check
            if (args["x1"] != null && args["z1"] != null && args["x2"] != null && args["z2"] != null)
            {
                int x1 = args["x1"].AsInt;
                int z1 = args["z1"].AsInt;
                int x2 = args["x2"].AsInt;
                int z2 = args["z2"].AsInt;

                var result = new JSONObject();
                var buildings = new JSONArray();

                for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                {
                    for (int z = Math.Min(z1, z2); z <= Math.Max(z1, z2); z++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) continue;

                        var building = cell.GetFirstBuilding(map);
                        if (building == null) continue;

                        var powerComp = building.TryGetComp<CompPower>();
                        if (powerComp == null) continue;

                        buildings.Add(CheckSingleCell(cell, map));
                    }
                }

                result["buildings"] = buildings;
                result["count"] = buildings.Count;
                return result.ToString();
            }

            return ToolExecutor.JsonError("Invalid arguments. Provide either (x, z) for single check or (x1, z1, x2, z2) for area.");
        }

        private static JSONObject CheckSingleCell(IntVec3 cell, Map map)
        {
            var obj = new JSONObject();
            obj["position"] = $"({cell.x},{cell.z})";

            var building = cell.GetFirstBuilding(map);
            if (building == null)
            {
                obj["hasBuilding"] = false;
                return obj;
            }

            obj["building"] = building.LabelCap;
            obj["defName"] = building.def.defName;

            var powerComp = building.TryGetComp<CompPower>();
            if (powerComp == null)
            {
                obj["needsPower"] = false;
                return obj;
            }

            obj["needsPower"] = true;
            obj["isPowered"] = powerComp.PowerOutput > 0;

            // Find which network this belongs to
            if (powerComp.PowerNet != null)
            {
                var nets = map.powerNetManager.AllNetsListForReading;
                int networkId = nets.IndexOf(powerComp.PowerNet) + 1;
                obj["connectedToNetwork"] = networkId;
            }
            else
            {
                obj["connectedToNetwork"] = 0;
            }

            // Find nearest conduit
            var nearestConduit = FindNearestConduit(cell, map);
            if (nearestConduit != null)
            {
                var nearestObj = new JSONObject();
                nearestObj["x"] = nearestConduit.Value.x;
                nearestObj["z"] = nearestConduit.Value.z;
                nearestObj["distance"] = (nearestConduit.Value - cell).LengthManhattan;
                obj["nearestConduit"] = nearestObj;
            }

            return obj;
        }

        private static IntVec3? FindNearestConduit(IntVec3 pos, Map map)
        {
            IntVec3? nearest = null;
            int nearestDist = int.MaxValue;

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building.def.defName != "PowerConduit") continue;

                int dist = (building.Position - pos).LengthManhattan;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = building.Position;
                }
            }

            return nearest;
        }

        public static string SuggestPowerRoute(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null || args["x1"] == null || args["z1"] == null || args["x2"] == null || args["z2"] == null)
                return ToolExecutor.JsonError("Missing arguments. Provide 'x1', 'z1', 'x2', 'z2'.");

            int x1 = args["x1"].AsInt;
            int z1 = args["z1"].AsInt;
            int x2 = args["x2"].AsInt;
            int z2 = args["z2"].AsInt;

            var start = new IntVec3(x1, 0, z1);
            var end = new IntVec3(x2, 0, z2);

            if (!start.InBounds(map) || !end.InBounds(map))
                return ToolExecutor.JsonError("Coordinates out of bounds.");

            bool avoidWalls = args["avoidWalls"]?.AsBool ?? true;
            bool minimizeCost = args["minimizeCost"]?.AsBool ?? true;

            var path = FindConduitPath(start, end, map, avoidWalls, minimizeCost);

            if (path == null || path.Count == 0)
                return ToolExecutor.JsonError("No valid path found between the two points.");

            var result = new JSONObject();
            var pathArray = new JSONArray();

            foreach (var cell in path)
            {
                var cellObj = new JSONObject();
                cellObj["x"] = cell.x;
                cellObj["z"] = cell.z;
                pathArray.Add(cellObj);
            }

            result["path"] = pathArray;
            result["conduitCount"] = path.Count;
            result["costEstimate"] = $"{path.Count * 2} steel"; // PowerConduit costs 2 steel

            // Check for obstacles
            var notes = new List<string>();
            foreach (var cell in path)
            {
                var edifice = cell.GetEdifice(map);
                if (edifice != null && edifice.def.passability == Traversability.Impassable)
                {
                    notes.Add($"Path goes through wall/obstacle at ({cell.x},{cell.z})");
                }
            }

            if (notes.Count > 0)
                result["notes"] = string.Join(". ", notes);
            else
                result["notes"] = "Clear path";

            return result.ToString();
        }

        private static List<IntVec3> FindConduitPath(IntVec3 start, IntVec3 end, Map map, bool avoidWalls, bool minimizeCost)
        {
            // Simple A* pathfinding for conduit placement
            var openSet = new HashSet<IntVec3> { start };
            var cameFrom = new Dictionary<IntVec3, IntVec3>();
            var gScore = new Dictionary<IntVec3, float> { { start, 0 } };
            var fScore = new Dictionary<IntVec3, float> { { start, Heuristic(start, end) } };

            while (openSet.Count > 0)
            {
                // Find cell with lowest fScore
                var current = openSet.OrderBy(c => fScore.ContainsKey(c) ? fScore[c] : float.MaxValue).First();

                if (current == end)
                {
                    // Reconstruct path
                    var path = new List<IntVec3>();
                    while (cameFrom.ContainsKey(current))
                    {
                        path.Add(current);
                        current = cameFrom[current];
                    }
                    path.Reverse();
                    return path;
                }

                openSet.Remove(current);

                // Check all neighbors (4-directional)
                foreach (var offset in new[] { new IntVec3(1, 0, 0), new IntVec3(-1, 0, 0), new IntVec3(0, 0, 1), new IntVec3(0, 0, -1) })
                {
                    var neighbor = current + offset;

                    if (!neighbor.InBounds(map))
                        continue;

                    // Calculate cost
                    float moveCost = 1f;

                    if (avoidWalls)
                    {
                        var edifice = neighbor.GetEdifice(map);
                        if (edifice != null && edifice.def.passability == Traversability.Impassable)
                            moveCost = 100f; // High penalty for walls
                    }

                    if (minimizeCost)
                    {
                        var terrain = neighbor.GetTerrain(map);
                        if (terrain.affordances != null && terrain.affordances.Contains(TerrainAffordanceDefOf.Heavy))
                            moveCost += 0.5f; // Slight preference for buildable terrain
                    }

                    float tentativeGScore = gScore[current] + moveCost;

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, end);

                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }

            // No path found
            return null;
        }

        private static float Heuristic(IntVec3 a, IntVec3 b)
        {
            // Manhattan distance
            return Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z);
        }

        public static string AutoRoutePower(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null || args["targetX"] == null || args["targetZ"] == null)
                return ToolExecutor.JsonError("Missing arguments. Provide 'targetX' and 'targetZ'.");

            int targetX = args["targetX"].AsInt;
            int targetZ = args["targetZ"].AsInt;
            var targetCell = new IntVec3(targetX, 0, targetZ);

            if (!targetCell.InBounds(map))
                return ToolExecutor.JsonError($"Coordinates ({targetX},{targetZ}) out of bounds.");

            bool autoApprove = args["autoApprove"]?.AsBool ?? false;

            // Find the building at target
            var building = targetCell.GetFirstBuilding(map);
            if (building == null)
                return ToolExecutor.JsonError($"No building found at ({targetX},{targetZ}).");

            var powerComp = building.TryGetComp<CompPower>();
            if (powerComp == null)
                return ToolExecutor.JsonError($"Building '{building.LabelCap}' does not require power.");

            if (powerComp.PowerOutput > 0)
                return ToolExecutor.JsonError($"Building '{building.LabelCap}' is already powered.");

            // Find nearest powered conduit
            var nearestPoweredConduit = FindNearestPoweredConduit(targetCell, map);
            if (nearestPoweredConduit == null)
                return ToolExecutor.JsonError("No powered conduit found on the map. Build a power generator and conduit first.");

            // Find path from nearest conduit to target
            var path = FindConduitPath(nearestPoweredConduit.Value, targetCell, map, true, true);
            if (path == null || path.Count == 0)
                return ToolExecutor.JsonError($"Could not find a valid path from power grid to target building.");

            // Place conduit blueprints
            var conduitDef = ThingDefOf.PowerConduit;
            var placedBlueprints = new List<Thing>();
            var failedCells = new List<IntVec3>();

            foreach (var cell in path)
            {
                // Skip if there's already a conduit here
                if (cell.GetFirstBuilding(map)?.def == conduitDef)
                    continue;

                // Check if we can place here
                if (!GenConstruct.CanPlaceBlueprintAt(conduitDef, cell, Rot4.North, map).Accepted)
                {
                    failedCells.Add(cell);
                    continue;
                }

                // Place blueprint
                var blueprint = GenConstruct.PlaceBlueprintForBuild(conduitDef, cell, map, Rot4.North, Faction.OfPlayer, null);
                if (blueprint != null)
                {
                    // Forbid it unless auto-approve
                    if (!autoApprove && blueprint is Building_Blueprint bp)
                    {
                        bp.SetForbidden(true);
                        ProposalTracker.RegisterProposal(map, bp, "auto_route_power");
                    }
                    placedBlueprints.Add(blueprint);
                }
            }

            var result = new JSONObject();
            result["targetBuilding"] = building.LabelCap;
            result["targetPosition"] = $"({targetX},{targetZ})";
            result["startPosition"] = $"({nearestPoweredConduit.Value.x},{nearestPoweredConduit.Value.z})";
            result["conduitsPlaced"] = placedBlueprints.Count;
            result["costEstimate"] = $"{placedBlueprints.Count * 2} steel";
            result["autoApproved"] = autoApprove;

            if (failedCells.Count > 0)
            {
                var failedArray = new JSONArray();
                foreach (var cell in failedCells)
                    failedArray.Add($"({cell.x},{cell.z})");
                result["failedPlacements"] = failedArray;
                result["note"] = $"{failedCells.Count} cells could not be used (blocked or invalid). Path may be incomplete.";
            }

            if (!autoApprove)
                result["approval"] = "Conduits placed as forbidden blueprints. Use 'approve_buildings' to allow construction.";

            return result.ToString();
        }

        private static IntVec3? FindNearestPoweredConduit(IntVec3 pos, Map map)
        {
            IntVec3? nearest = null;
            int nearestDist = int.MaxValue;

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building.def != ThingDefOf.PowerConduit) continue;

                var powerComp = building.TryGetComp<CompPower>();
                if (powerComp == null || powerComp.PowerNet == null || !powerComp.PowerNet.HasActivePowerSource)
                    continue;

                int dist = (building.Position - pos).LengthManhattan;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = building.Position;
                }
            }

            return nearest;
        }
    }
}
