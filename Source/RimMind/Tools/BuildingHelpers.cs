// Extracted from BuildingTools.cs as part of refactoring — these are shared utilities
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
using RimMind.API;
using RimWorld.Planet;
using RimMind.Core;

namespace RimMind.Tools
{
    public static class BuildingHelpers
    {
        // --- JSON utility ---

        /// <summary>
        /// LLMs sometimes send JSON arrays as double-encoded strings -- unwrap them.
        /// </summary>
        internal static JSONNode UnwrapStringArray(JSONNode node)
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

        // --- Fuzzy / resolution helpers ---

        internal static ThingDef ResolveBuildingDef(string defName)
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

        internal static ThingDef ResolveStuffDef(string stuffName, ThingDef buildingDef)
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

        internal static bool IsStuffValidForBuilding(ThingDef stuff, ThingDef buildingDef)
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

        internal static string FindSimilarBuildings(string defName)
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

        internal static string FindSimilarStuffs(string stuffName, ThingDef buildingDef)
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

        internal static string GetStuffHint(ThingDef def)
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

        internal static string GetPlacementHint(string reason, ThingDef def, Map map = null, IntVec3 pos = default)
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

        // --- Parsing helpers ---

        internal static Rot4 ParseRotation(JSONNode rotNode)
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

        internal static int ParseDoorOffset(JSONNode offsetNode, int innerLen)
        {
            if (innerLen <= 0) return 0;
            if (offsetNode == null || string.IsNullOrEmpty(offsetNode.Value))
                return innerLen / 2; // default: center
            int offset = offsetNode.AsInt;
            if (offset < 0) offset = 0;
            if (offset >= innerLen) offset = innerLen - 1;
            return offset;
        }

        // --- Grid / terrain helpers ---

        /// <summary>
        /// Get the terrain def at a cell.
        /// </summary>
        internal static TerrainDef GetTerrainDef(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return null;
            return map.terrainGrid.TerrainAt(cell);
        }

        /// <summary>
        /// Check if a cell is walkable.
        /// </summary>
        internal static bool IsWalkable(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return false;
            return cell.Walkable(map);
        }

        /// <summary>
        /// Get the roof def at a cell.
        /// </summary>
        internal static RoofDef GetRoofDef(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return null;
            return map.roofGrid.RoofAt(cell);
        }

        // --- Map / cell helpers ---

        /// <summary>
        /// Get walkable adjacent cells to a given cell.
        /// </summary>
        internal static List<IntVec3> GetAdjacentCells(Map map, IntVec3 cell)
        {
            var result = new List<IntVec3>();
            foreach (var adj in GenAdj.CardinalDirections)
            {
                var adjCell = cell + adj;
                if (adjCell.InBounds(map) && adjCell.Walkable(map))
                    result.Add(adjCell);
            }
            return result;
        }

        /// <summary>
        /// Get the zone (if any) at a given cell.
        /// </summary>
        internal static Zone GetZoneFor(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return null;
            return map.zoneManager.ZoneAt(cell);
        }

        /// <summary>
        /// Flood-fill from a starting cell up to maxRadius, returning all reached cells.
        /// </summary>
        internal static List<IntVec3> GetFloodFillArea(Map map, IntVec3 start, int maxRadius, Predicate<IntVec3> canEnter = null)
        {
            var visited = new HashSet<IntVec3>();
            var frontier = new Queue<IntVec3>();
            frontier.Enqueue(start);
            visited.Add(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var adj in GenAdj.CardinalDirections)
                {
                    var next = current + adj;
                    if (!next.InBounds(map)) continue;
                    if (visited.Contains(next)) continue;
                    if (canEnter != null && !canEnter(next)) continue;
                    if (!next.Walkable(map)) continue;

                    // Check distance from start
                    int dist = Math.Abs(next.x - start.x) + Math.Abs(next.z - start.z);
                    if (dist > maxRadius) continue;

                    visited.Add(next);
                    frontier.Enqueue(next);
                }
            }

            return new List<IntVec3>(visited);
        }

        /// <summary>
        /// Find an area/zone by name.
        /// </summary>
        internal static Area GetAreaByName(Map map, string name)
        {
            if (string.IsNullOrEmpty(name) || map.areaManager == null) return null;

            // Check built-in areas
            foreach (var area in map.areaManager.AllAreas)
            {
                if (string.Equals(area.Label, name, StringComparison.OrdinalIgnoreCase))
                    return area;
            }
            return null;
        }

        /// <summary>
        /// Find or suggest a storage spot near a position.
        /// </summary>
        internal static IntVec3? GetOrSuggestStorage(Map map, IntVec3 near)
        {
            // Look for existing stockpiles
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Stockpile stockpile)) continue;
                foreach (var cell in stockpile.Cells)
                {
                    if (cell.Walkable(map))
                        return cell;
                }
            }

            // Fallback: return adjacent walkable cell
            foreach (var adj in GenAdj.CardinalDirections)
            {
                var adjCell = near + adj;
                if (adjCell.InBounds(map) && adjCell.Walkable(map))
                    return adjCell;
            }

            return null;
        }

        // --- Shape helpers ---

        /// <summary>
        /// Compute all cells to fill for a given shape type, def, and size.
        /// </summary>
        internal static List<IntVec3> ComputeShapeCells(string shape, ThingDef def, IntVec3 basePos, IntVec3 size, Rot4 rotation)
        {
            switch (shape?.ToLower())
            {
                case "rect":
                case "room":
                case "wall_rect":
                    return ComputeRectangleCells(basePos, size, rotation);
                case "line":
                case "wall_line":
                    // For wall_line, size.x is the length
                    return ComputeLineCells(basePos, new IntVec3(basePos.x + size.x - 1, 0, basePos.z + size.z - 1));
                default:
                    // Default: single cell or def size
                    var cells = new List<IntVec3>();
                    var occupied = GenAdj.CellsOccupiedBy(basePos, rotation, def.size);
                    cells.AddRange(occupied);
                    return cells;
            }
        }

        /// <summary>
        /// Compute cells for a rectangle shape.
        /// </summary>
        internal static List<IntVec3> ComputeRectangleCells(IntVec3 basePos, IntVec3 size, Rot4 rotation)
        {
            // Apply rotation to size
            var rotatedSize = size;
            if (rotation == Rot4.East || rotation == Rot4.West)
            {
                // Swap width/depth for E/W rotations
                rotatedSize = new IntVec3(size.z, size.y, size.x);
            }
            
            var cells = new List<IntVec3>();
            int minX = basePos.x;
            int maxX = basePos.x + rotatedSize.x - 1;
            int minZ = basePos.z;
            int maxZ = basePos.z + rotatedSize.z - 1;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                    cells.Add(new IntVec3(x, 0, z));
            }
            return cells;
        }

        /// <summary>
        /// Compute cells along a line between two points (Bresenham-style).
        /// </summary>
        internal static List<IntVec3> ComputeLineCells(IntVec3 from, IntVec3 to)
        {
            var cells = new List<IntVec3>();
            int x1 = from.x, z1 = from.z, x2 = to.x, z2 = to.z;
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

        /// <summary>
        /// Get wall buildings touching a cell.
        /// </summary>
        internal static List<Building> GetWallsTouchingCell(Map map, IntVec3 cell)
        {
            var walls = new List<Building>();
            foreach (var adj in GenAdj.CardinalDirections)
            {
                var adjCell = cell + adj;
                if (!adjCell.InBounds(map)) continue;
                var edifice = adjCell.GetEdifice(map);
                if (edifice != null && edifice.def.holdsRoof)
                    walls.Add(edifice);
            }
            return walls;
        }

        // --- Wall / shape helpers used by PlaceRoom / PlaceWallLine / PlaceWallRect ---

        internal static List<IntVec3> GetRectOutline(int x1, int z1, int x2, int z2)
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

        internal static List<IntVec3> GetLine(int x1, int z1, int x2, int z2)
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

        internal static bool HasExistingWallOrBlueprint(IntVec3 pos, Map map)
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

        internal static RimMind.API.JSONArray DetectAdjacentWalls(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var hints = new RimMind.API.JSONArray();

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

        internal static bool HasWallLine(Map map, int x1, int z1, int x2, int z2)
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

        internal static RimMind.API.JSONArray ScanBuildingsInArea(Map map, int minX, int minZ, int maxX, int maxZ)
        {
            var seen = new HashSet<Thing>();
            var wallCounts = new Dictionary<string, int>(); // stuff -> count for walls
            var entries = new RimMind.API.JSONArray();

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

                        var entry = new RimMind.API.JSONObject();
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
                var entry = new RimMind.API.JSONObject();
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

        // --- Core placement helper ---

        internal struct PlacementResult
        {
            public bool success;
            public string proposalId;
            public string error;
            public Thing blueprint;
            public bool autoRotated;
            public int finalRotation;
        }

        internal static PlacementResult PlaceOneBlueprint(Map map, Faction faction, ThingDef def, IntVec3 pos, ThingDef stuff, Rot4 rot, bool autoApprove, bool allowAutoRotate = true)
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

        // --- Material pre-check ---

        internal struct MaterialCheckResult
        {
            public bool hasMaterials;
            public string warning;
            public RimMind.API.JSONArray shortages;
        }

        internal static MaterialCheckResult CheckMaterials(Map map, ThingDef buildingDef, ThingDef stuff)
        {
            var result = new MaterialCheckResult();
            result.hasMaterials = true;

            var shortageList = new List<string>();
            var shortagesArray = new RimMind.API.JSONArray();

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

                    var shortageObj = new RimMind.API.JSONObject();
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

        // --- Validation helpers ---

        /// <summary>
        /// Check if a building can be placed at a position (terrain, zone, occupied).
        /// </summary>
        internal static bool CanPlaceAt(ThingDef def, IntVec3 pos, Rot4 rot, Map map, ThingDef stuff = null)
        {
            var report = GenConstruct.CanPlaceBlueprintAt(def, pos, rot, map, false, null, null, stuff);
            return report.Accepted;
        }

        /// <summary>
        /// Compute resources needed to place a building.
        /// </summary>
        internal static Dictionary<ThingDef, int> GetPlacementCost(ThingDef def, ThingDef stuff = null)
        {
            var cost = new Dictionary<ThingDef, int>();

            if (def.MadeFromStuff && stuff != null)
            {
                cost[stuff] = def.costStuffCount;
            }

            if (def.costList != null)
            {
                foreach (var c in def.costList)
                {
                    if (cost.ContainsKey(c.thingDef))
                        cost[c.thingDef] += c.count;
                    else
                        cost[c.thingDef] = c.count;
                }
            }

            return cost;
        }

        /// <summary>
        /// Check if any colonist has the construction skill required for a building.
        /// </summary>
        internal static bool HasSkillFor(ThingDef def, Map map)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return false;

            // Get required construction skill (from WorkToBuild stat or default 0)
            float workToBuild = 0;
            if (def.statBases != null)
            {
                var workStat = def.statBases.FirstOrDefault(s => s.stat.defName == "WorkToBuild");
                if (workStat != null) workToBuild = workStat.value;
            }

            // Rough skill requirement: WorkToBuild / 500 = skill level needed
            int requiredSkill = Math.Min(20, Math.Max(1, (int)(workToBuild / 500)));

            foreach (var colonist in colonists)
            {
                var skill = colonist.skills?.GetSkill(SkillDefOf.Construction);
                if (skill != null && skill.Level >= requiredSkill)
                    return true;
            }

            return false;
        }

        // --- Terrain/space validation (used by CheckPlacement) ---

        internal static RimMind.API.JSONObject CheckTerrain(Map map, ThingDef def, List<IntVec3> cells)
        {
            var result = new RimMind.API.JSONObject();

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

        internal static RimMind.API.JSONObject CheckSpace(Map map, List<IntVec3> cells)
        {
            var result = new RimMind.API.JSONObject();

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

        internal static RimMind.API.JSONObject CheckPower(Map map, ThingDef def, IntVec3 pos)
        {
            var result = new RimMind.API.JSONObject();

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

        internal static RimMind.API.JSONObject CheckRoof(Map map, ThingDef def, List<IntVec3> cells)
        {
            var result = new RimMind.API.JSONObject();

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

        internal static RimMind.API.JSONObject CheckSpecialRules(Map map, ThingDef def, IntVec3 pos, Rot4 rotation, List<IntVec3> cells)
        {
            var result = new RimMind.API.JSONObject();

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

        internal static RimMind.API.JSONArray CheckAdjacent(Map map, List<IntVec3> cells)
        {
            var warnings = new RimMind.API.JSONArray();

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

        internal static string SuggestAlternative(Map map, ThingDef def, IntVec3 pos, Rot4 rotation)
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

        // --- PlaceRoom, PlaceWallLine, PlaceWallRect implementations ---

        internal static string PlaceRoom(Map map, Faction faction, ThingDef wallDef, ThingDef doorDef,
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
            int bbX1 = Math.Max(0, minX - 1);
            int bbZ1 = Math.Max(0, minZ - 1);
            int bbX2 = Math.Min(map.Size.x - 1, maxX + 1);
            int bbZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            RimMind.API.JSONArray existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);

            // Place wall blueprints
            var proposalIds = new RimMind.API.JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new RimMind.API.JSONArray();

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
                    var entry = new RimMind.API.JSONObject();
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
                var entry = new RimMind.API.JSONObject();
                entry["defName"] = doorDef.defName;
                entry["x"] = doorCell.x;
                entry["z"] = doorCell.z;
                entry["error"] = doorResult.error;
                failuresList.Add(entry);
            }

            var result = new RimMind.API.JSONObject();
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
            int gridX1 = Math.Max(0, minX - 1);
            int gridZ1 = Math.Max(0, minZ - 1);
            int gridX2 = Math.Min(map.Size.x - 1, maxX + 1);
            int gridZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
            result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

            var adjacentHints = DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
            if (adjacentHints != null)
                result["adjacent_walls"] = adjacentHints;

            return result.ToString();
        }

        internal static string PlaceWallLine(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int x1, int z1, int x2, int z2, bool autoApprove)
        {
            var cells = GetLine(x1, z1, x2, z2);

            // Scan existing buildings before placement
            RimMind.API.JSONArray existingInArea;
            {
                int wlMinX = Math.Min(x1, x2);
                int wlMinZ = Math.Min(z1, z2);
                int wlMaxX = Math.Max(x1, x2);
                int wlMaxZ = Math.Max(z1, z2);
                int bbX1 = Math.Max(0, wlMinX - 1);
                int bbZ1 = Math.Max(0, wlMinZ - 1);
                int bbX2 = Math.Min(map.Size.x - 1, wlMaxX + 1);
                int bbZ2 = Math.Min(map.Size.z - 1, wlMaxZ + 1);
                existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);
            }

            var proposalIds = new RimMind.API.JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new RimMind.API.JSONArray();

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
                    var entry = new RimMind.API.JSONObject();
                    entry["defName"] = wallDef.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    failuresList.Add(entry);
                }
            }

            var result = new RimMind.API.JSONObject();
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
                int wlMinX = Math.Min(x1, x2);
                int wlMinZ = Math.Min(z1, z2);
                int wlMaxX = Math.Max(x1, x2);
                int wlMaxZ = Math.Max(z1, z2);
                int gridX1 = Math.Max(0, wlMinX - 1);
                int gridZ1 = Math.Max(0, wlMinZ - 1);
                int gridX2 = Math.Min(map.Size.x - 1, wlMaxX + 1);
                int gridZ2 = Math.Min(map.Size.z - 1, wlMaxZ + 1);
                result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
                result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

                var adjacentHints = DetectAdjacentWalls(map, wlMinX, wlMinZ, wlMaxX, wlMaxZ);
                if (adjacentHints != null)
                    result["adjacent_walls"] = adjacentHints;
            }

            return result.ToString();
        }

        internal static string PlaceWallRect(Map map, Faction faction, ThingDef wallDef,
            ThingDef wallStuff, int minX, int minZ, int maxX, int maxZ, bool autoApprove)
        {
            var cells = GetRectOutline(minX, minZ, maxX, maxZ);

            // Scan existing buildings before placement
            int bbX1 = Math.Max(0, minX - 1);
            int bbZ1 = Math.Max(0, minZ - 1);
            int bbX2 = Math.Min(map.Size.x - 1, maxX + 1);
            int bbZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            RimMind.API.JSONArray existingInArea = ScanBuildingsInArea(map, bbX1, bbZ1, bbX2, bbZ2);

            var proposalIds = new RimMind.API.JSONArray();
            int placedCount = 0;
            int failedCount = 0;
            int sharedCount = 0;
            var failuresList = new RimMind.API.JSONArray();

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
                    var entry = new RimMind.API.JSONObject();
                    entry["defName"] = wallDef.defName;
                    entry["x"] = cell.x;
                    entry["z"] = cell.z;
                    entry["error"] = pr.error;
                    failuresList.Add(entry);
                }
            }

            var result = new RimMind.API.JSONObject();
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
            int gridX1 = Math.Max(0, minX - 1);
            int gridZ1 = Math.Max(0, minZ - 1);
            int gridX2 = Math.Min(map.Size.x - 1, maxX + 1);
            int gridZ2 = Math.Min(map.Size.z - 1, maxZ + 1);
            result["area_after"] = MapTools.RenderArea(map, gridX1, gridZ1, gridX2, gridZ2);
            result["buildings_in_area"] = ScanBuildingsInArea(map, gridX1, gridZ1, gridX2, gridZ2);

            var adjacentHints = DetectAdjacentWalls(map, minX, minZ, maxX, maxZ);
            if (adjacentHints != null)
                result["adjacent_walls"] = adjacentHints;

            return result.ToString();
        }
    }
}
