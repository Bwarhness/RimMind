using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    /// <summary>
    /// Semantic and Query Architecture for Building (Issue #94)
    /// 
    /// Implements:
    /// - Week 1: Semantic Generator (GetSemanticOverview)
    /// - Week 2: Query Tools (find_buildable_area, check_placement, get_requirements)
    /// - Week 3: Integration (semantic overview injected into prompt context)
    /// - Week 4: Polish
    /// </summary>
    public static class SemanticTools
    {
        /// <summary>
        /// Get a compact semantic overview of the colony layout.
        /// Generated fresh on every LLM request to ensure accuracy.
        /// </summary>
        public static string GetSemanticOverview()
        {
            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            return GenerateSemanticOverview(map);
        }

        /// <summary>
        /// Generate the semantic overview from scratch.
        /// Target: ~150-200 tokens describing colony layout, rooms, power, terrain, and issues.
        /// </summary>
        private static string GenerateSemanticOverview(Map map)
        {
            var sb = new StringBuilder();

            // Colony header
            var colonists = map.mapPawns.FreeColonistsSpawned;
            int colonistCount = colonists.Count();
            int dayOfYear = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);

            sb.AppendLine($"Colony: {Find.World.info.name ?? "Unknown"} - {colonistCount} colonists, Year {year} Day {dayOfYear}");
            sb.AppendLine();

            // Rooms section
            sb.AppendLine("Rooms:");
            var rooms = map.regionGrid.AllRooms
                .Where(r => !r.PsychologicallyOutdoors && !r.TouchesMapEdge)
                .OrderByDescending(r => r.CellCount)
                .Take(15) // Limit to top 15 rooms to keep token count down
                .ToList();

            if (rooms.Count == 0)
            {
                sb.AppendLine("- No enclosed rooms yet");
            }
            else
            {
                foreach (var room in rooms)
                {
                    string roomDesc = DescribeRoom(room, map);
                    sb.AppendLine($"- {roomDesc}");
                }
            }
            sb.AppendLine();

            // Power section
            sb.Append("Power: ");
            string powerDesc = DescribePower(map);
            sb.AppendLine(powerDesc);
            sb.AppendLine();

            // Terrain section
            sb.Append("Terrain: ");
            string terrainDesc = DescribeTerrain(map);
            sb.AppendLine(terrainDesc);
            sb.AppendLine();

            // Issues section
            sb.Append("Issues: ");
            string issuesDesc = IdentifyIssues(map, rooms);
            sb.AppendLine(issuesDesc);

            return sb.ToString();
        }

        /// <summary>
        /// Describe a single room: type, location, size, contents, doors
        /// </summary>
        private static string DescribeRoom(Room room, Map map)
        {
            if (room == null)
                return "Unknown room";

            var sb = new StringBuilder();

            // Room type
            string roomType = room.Role?.label ?? "Room";

            // Calculate bounds
            var cells = room.Cells.ToList();
            if (cells.Count == 0)
                return roomType + " (empty)";

            int minX = cells.Min(c => c.x);
            int maxX = cells.Max(c => c.x);
            int minZ = cells.Min(c => c.z);
            int maxZ = cells.Max(c => c.z);
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            int centerX = (minX + maxX) / 2;
            int centerZ = (minZ + maxZ) / 2;

            sb.Append($"{roomType} ({centerX},{centerZ}) {width}x{height}");

            // Contents (key furniture)
            var contents = DescribeRoomContents(room, map);
            if (!string.IsNullOrEmpty(contents))
            {
                sb.Append($": {contents}");
            }

            // Door count - find doors on room boundary (RimWorld 1.6 compatible)
            var doorSet = new HashSet<Building_Door>();
            foreach (var region in room.Regions)
            {
                foreach (var cell in region.Cells)
                {
                    foreach (var adj in GenAdj.CardinalDirections)
                    {
                        var adjCell = cell + adj;
                        if (!adjCell.InBounds(map)) continue;
                        var door = adjCell.GetDoor(map);
                        if (door != null)
                            doorSet.Add(door);
                    }
                }
            }
            int doorCount = doorSet.Count;
            if (doorCount > 0)
            {
                sb.Append($", {doorCount} door{(doorCount > 1 ? "s" : "")}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Describe key furniture/equipment in a room
        /// </summary>
        private static string DescribeRoomContents(Room room, Map map)
        {
            var contents = new List<string>();

            // Count key furniture types
            int beds = 0;
            int medicalBeds = 0;
            int workbenches = 0;
            int stoves = 0;
            int tables = 0;
            int researches = 0;

            foreach (var cell in room.Cells)
            {
                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing is Building_Bed bed)
                    {
                        if (bed.Medical)
                            medicalBeds++;
                        else
                            beds++;
                    }
                    else if (thing is Building_WorkTable workTable)
                    {
                        if (thing.def.defName.Contains("Research"))
                            researches++;
                        else if (thing.def.defName.Contains("Stove") || thing.def.defName.Contains("Butcher"))
                            stoves++;
                        else
                            workbenches++;
                    }
                    else if (thing.def.surfaceType == SurfaceType.Eat)
                    {
                        tables++;
                    }
                }
            }

            // Build contents description
            if (beds > 0) contents.Add($"{beds} bed{(beds > 1 ? "s" : "")}");
            if (medicalBeds > 0) contents.Add($"{medicalBeds} medical");
            if (stoves > 0) contents.Add($"{stoves} cook station{(stoves > 1 ? "s" : "")}");
            if (workbenches > 0) contents.Add($"{workbenches} workbench{(workbenches > 1 ? "es" : "")}");
            if (researches > 0) contents.Add($"{researches} research");
            if (tables > 0) contents.Add($"{tables} table{(tables > 1 ? "s" : "")}");

            return string.Join(", ", contents);
        }

        /// <summary>
        /// Describe power generation and conduit coverage
        /// </summary>
        private static string DescribePower(Map map)
        {
            var generators = new List<string>();
            float totalGeneration = 0f;
            float totalConsumption = 0f;
            int batteryCount = 0;

            var nets = map.powerNetManager.AllNetsListForReading;
            foreach (var net in nets)
            {
                foreach (var comp in net.powerComps)
                {
                    if (comp.PowerOn)
                    {
                        if (comp.PowerOutput > 0)
                        {
                            totalGeneration += comp.PowerOutput;
                            // Count generator types
                            var parent = comp.parent;
                            if (parent != null)
                            {
                                string defName = parent.def.defName;
                                if (defName.Contains("Solar"))
                                    generators.Add("solar");
                                else if (defName.Contains("Wind"))
                                    generators.Add("wind");
                                else if (defName.Contains("Geothermal"))
                                    generators.Add("geo");
                                else if (defName.Contains("Generator"))
                                    generators.Add("fuel");
                            }
                        }
                        else
                        {
                            totalConsumption += -comp.PowerOutput;
                        }
                    }
                }

                batteryCount += net.batteryComps.Count;
            }

            var genCounts = generators.GroupBy(g => g)
                .Select(g => $"{g.Count()} {g.Key}")
                .ToList();

            float surplus = totalGeneration - totalConsumption;
            string powerStatus = surplus >= 0 ? "stable" : "deficit";

            var parts = new List<string>();
            if (genCounts.Any())
                parts.Add(string.Join(", ", genCounts));
            else
                parts.Add("no generators");

            parts.Add($"{(int)totalGeneration}W gen");
            parts.Add($"{(int)totalConsumption}W use");

            if (batteryCount > 0)
                parts.Add($"{batteryCount} batteries");

            parts.Add(powerStatus);

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Describe buildable terrain areas and obstacles
        /// </summary>
        private static string DescribeTerrain(Map map)
        {
            var parts = new List<string>();

            // Sample terrain to find buildable outdoor areas
            // We'll do a quick grid sample to estimate buildable space
            int totalCells = map.Size.x * map.Size.z;
            int sampleInterval = Math.Max(5, (int)Math.Sqrt(totalCells) / 10);
            int buildableCells = 0;
            int mountainCells = 0;
            int waterCells = 0;

            for (int x = 0; x < map.Size.x; x += sampleInterval)
            {
                for (int z = 0; z < map.Size.z; z += sampleInterval)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;

                    var terrain = map.terrainGrid.TerrainAt(cell);
                    
                    // Check if buildable
                    if (cell.Standable(map) && !cell.Roofed(map))
                    {
                        buildableCells++;
                    }
                    
                    // Check for mountain
                    var naturalRock = cell.GetFirstBuilding(map);
                    if (naturalRock != null && naturalRock.def.building != null && 
                        naturalRock.def.building.isNaturalRock)
                    {
                        mountainCells++;
                    }
                    
                    // Check for water
                    if (!terrain.passability.Equals(Traversability.Standable))
                    {
                        waterCells++;
                    }
                }
            }

            // Estimate percentages
            int totalSamples = (map.Size.x / sampleInterval) * (map.Size.z / sampleInterval);
            int buildablePercent = (buildableCells * 100) / Math.Max(1, totalSamples);
            int mountainPercent = (mountainCells * 100) / Math.Max(1, totalSamples);

            if (buildablePercent > 30)
                parts.Add($"{buildablePercent}% buildable outdoor");
            if (mountainPercent > 20)
                parts.Add($"{mountainPercent}% mountain");
            if (waterCells > totalSamples / 10)
                parts.Add("water present");

            return parts.Any() ? string.Join(", ", parts) : "mostly open terrain";
        }

        /// <summary>
        /// Identify colony issues like missing rooms, cramped spaces, etc.
        /// </summary>
        private static string IdentifyIssues(Map map, List<Room> rooms)
        {
            var issues = new List<string>();

            // Check for essential room types
            bool hasKitchen = rooms.Any(r => r.Role?.defName == "Kitchen" || 
                r.Cells.Any(c => c.GetThingList(map).Any(t => 
                    t.def.defName.Contains("Stove") || t.def.defName.Contains("Butcher"))));
            
            bool hasBedroom = rooms.Any(r => r.Role?.defName == "Bedroom" || 
                r.Cells.Any(c => c.GetThingList(map).Any(t => t is Building_Bed bed && !bed.Medical)));
            
            bool hasWorkshop = rooms.Any(r => r.Role?.defName == "Workshop" || 
                r.Cells.Any(c => c.GetThingList(map).Any(t => t is Building_WorkTable)));

            if (!hasKitchen && map.mapPawns.FreeColonistsSpawned.Any())
                issues.Add("no kitchen");
            if (!hasBedroom && map.mapPawns.FreeColonistsSpawned.Any())
                issues.Add("no bedrooms");
            if (!hasWorkshop)
                issues.Add("no workshop");

            // Check for cramped rooms
            var crampedRooms = rooms.Where(r => r.CellCount < 12 && 
                r.Cells.Any(c => c.GetThingList(map).Any(t => t is Building_Bed)));
            if (crampedRooms.Any())
                issues.Add($"{crampedRooms.Count()} cramped bedroom{(crampedRooms.Count() > 1 ? "s" : "")}");

            // Check power generation vs consumption
            float totalGen = 0f;
            float totalCons = 0f;
            foreach (var net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (var comp in net.powerComps)
                {
                    if (comp.PowerOn)
                    {
                        if (comp.PowerOutput > 0)
                            totalGen += comp.PowerOutput;
                        else
                            totalCons += -comp.PowerOutput;
                    }
                }
            }
            if (totalGen < totalCons)
                issues.Add("power deficit");

            return issues.Any() ? string.Join(", ", issues) : "none detected";
        }

        /// <summary>
        /// Find buildable area candidates matching size and constraints.
        /// Returns scored candidates with exact positions.
        /// </summary>
        public static string FindBuildableArea(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            // Parse required parameters
            int minWidth = args?["minWidth"]?.AsInt ?? 0;
            int minHeight = args?["minHeight"]?.AsInt ?? 0;
            
            if (minWidth <= 0 || minHeight <= 0)
                return ToolExecutor.JsonError("minWidth and minHeight are required and must be positive.");

            // Parse optional parameters
            string nearRef = args?["near"]?.Value;
            int maxDistance = args?["maxDistance"]?.AsInt ?? 999;
            bool indoor = args?["indoor"]?.AsBool ?? false;
            bool requirePower = args?["requirePower"]?.AsBool ?? false;

            // Find target position if "near" is specified
            IntVec3 targetPos = IntVec3.Invalid;
            if (!string.IsNullOrEmpty(nearRef))
            {
                targetPos = FindTargetPosition(nearRef, map);
                if (!targetPos.IsValid)
                    return ToolExecutor.JsonError($"Could not find target '{nearRef}'");
            }

            // Scan map for buildable areas
            var candidates = new List<BuildableAreaCandidate>();
            
            // Grid-based sampling to find contiguous buildable areas
            var checkedCells = new HashSet<IntVec3>();
            
            for (int x = 0; x < map.Size.x - minWidth + 1; x++)
            {
                for (int z = 0; z < map.Size.z - minHeight + 1; z++)
                {
                    var topLeft = new IntVec3(x, 0, z);
                    
                    // Skip if we've already checked this area as part of another candidate
                    if (checkedCells.Contains(topLeft))
                        continue;
                    
                    // Try to find the largest contiguous buildable rectangle starting here
                    var area = FindLargestBuildableRect(topLeft, minWidth, minHeight, map, indoor, requirePower);
                    
                    if (area != null)
                    {
                        // Mark cells as checked
                        for (int cx = area.x; cx < area.x + area.width; cx++)
                        {
                            for (int cz = area.z; cz < area.z + area.height; cz++)
                            {
                                checkedCells.Add(new IntVec3(cx, 0, cz));
                            }
                        }
                        
                        // Calculate score
                        float score = ScoreBuildableArea(area, targetPos, maxDistance, map, requirePower);
                        
                        if (score > 0)
                        {
                            candidates.Add(new BuildableAreaCandidate
                            {
                                area = area,
                                score = score,
                                distanceToTarget = targetPos.IsValid ? (int)area.Center.DistanceTo(targetPos) : -1
                            });
                        }
                    }
                }
            }

            // Sort by score and take top 5
            candidates = candidates.OrderByDescending(c => c.score).Take(5).ToList();

            // Build response
            var result = new JSONObject();
            
            if (candidates.Count == 0)
            {
                result["totalFound"] = 0;
                result["candidates"] = new JSONArray();
                result["message"] = "No buildable areas found matching criteria. Try reducing minWidth/minHeight or increasing maxDistance.";
                if (targetPos.IsValid)
                    result["targetPosition"] = $"({targetPos.x}, {targetPos.z})";
                return result.ToString();
            }

            var candidatesArray = new JSONArray();

            foreach (var candidate in candidates)
            {
                var candObj = new JSONObject();
                
                var posArray = new JSONArray();
                posArray.Add(candidate.area.x);
                posArray.Add(candidate.area.z);
                candObj["position"] = posArray;
                
                var sizeArray = new JSONArray();
                sizeArray.Add(candidate.area.width);
                sizeArray.Add(candidate.area.height);
                candObj["size"] = sizeArray;
                
                candObj["score"] = candidate.score.ToString("F2");
                
                if (candidate.distanceToTarget >= 0)
                    candObj["distanceToTarget"] = candidate.distanceToTarget;
                
                candObj["powered"] = candidate.area.hasPower;
                candObj["roofed"] = candidate.area.isRoofed;
                candObj["notes"] = candidate.area.notes;
                
                candidatesArray.Add(candObj);
            }

            result["candidates"] = candidatesArray;
            result["totalFound"] = candidates.Count;

            if (targetPos.IsValid)
            {
                result["targetPosition"] = $"({targetPos.x}, {targetPos.z})";
            }

            return result.ToString();
        }

        private class BuildableAreaInfo
        {
            public int x;
            public int z;
            public int width;
            public int height;
            public bool hasPower;
            public bool isRoofed;
            public string notes;
            
            public IntVec3 Center => new IntVec3(x + width / 2, 0, z + height / 2);
        }

        private class BuildableAreaCandidate
        {
            public BuildableAreaInfo area;
            public float score;
            public int distanceToTarget;
        }

        /// <summary>
        /// Find target position from a reference string (coordinates or thing name)
        /// </summary>
        private static IntVec3 FindTargetPosition(string nearRef, Map map)
        {
            if (map == null || string.IsNullOrEmpty(nearRef))
                return IntVec3.Invalid;

            // Try parsing as coordinates: "x,z" or "(x,z)"
            var coordPattern = nearRef.Replace("(", "").Replace(")", "").Trim();
            var parts = coordPattern.Split(',');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int z))
                {
                    var cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map))
                        return cell;
                    // Coordinates out of bounds - return invalid
                    return IntVec3.Invalid;
                }
            }

            // Try finding by thing name (stockpile, building, etc.)
            // Search stockpiles
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (zone is Zone_Stockpile stockpile)
                {
                    if (stockpile.label.ToLower().Contains(nearRef.ToLower()) || 
                        nearRef.ToLower().Contains("stockpile"))
                    {
                        return stockpile.Cells.FirstOrDefault();
                    }
                }
            }

            // Search buildings
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building.Label.ToLower().Contains(nearRef.ToLower()))
                {
                    return building.Position;
                }
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// Find the largest buildable rectangle starting from a top-left position
        /// </summary>
        private static BuildableAreaInfo FindLargestBuildableRect(IntVec3 topLeft, int minWidth, int minHeight, 
            Map map, bool mustBeIndoor, bool requirePower)
        {
            // Find maximum width at this row
            int maxWidth = 0;
            for (int w = 0; w < map.Size.x - topLeft.x; w++)
            {
                var cell = new IntVec3(topLeft.x + w, 0, topLeft.z);
                if (IsCellBuildable(cell, map, mustBeIndoor, requirePower))
                    maxWidth = w + 1;
                else
                    break;
            }

            if (maxWidth < minWidth)
                return null;

            // Find maximum height with this width
            int maxHeight = 0;
            for (int h = 0; h < map.Size.z - topLeft.z; h++)
            {
                bool rowValid = true;
                for (int w = 0; w < maxWidth; w++)
                {
                    var cell = new IntVec3(topLeft.x + w, 0, topLeft.z + h);
                    if (!IsCellBuildable(cell, map, mustBeIndoor, requirePower))
                    {
                        rowValid = false;
                        break;
                    }
                }
                
                if (rowValid)
                    maxHeight = h + 1;
                else
                    break;
            }

            if (maxHeight < minHeight)
                return null;

            // Check properties of the area
            bool hasPower = CheckAreaHasPower(topLeft, maxWidth, maxHeight, map);
            bool isRoofed = CheckAreaIsRoofed(topLeft, maxWidth, maxHeight, map);
            string notes = GenerateAreaNotes(topLeft, maxWidth, maxHeight, map);

            return new BuildableAreaInfo
            {
                x = topLeft.x,
                z = topLeft.z,
                width = maxWidth,
                height = maxHeight,
                hasPower = hasPower,
                isRoofed = isRoofed,
                notes = notes
            };
        }

        /// <summary>
        /// Check if a cell is buildable according to criteria
        /// </summary>
        private static bool IsCellBuildable(IntVec3 cell, Map map, bool mustBeIndoor, bool requirePower)
        {
            if (!cell.InBounds(map))
                return false;

            // Check overhead mountain (thick roof)
            var roof = map.roofGrid.RoofAt(cell);
            if (roof != null && roof.isThickRoof)
                return false;

            // Check if indoor requirement is met
            if (mustBeIndoor && (roof == null || !cell.Roofed(map)))
                return false;

            var terrain = map.terrainGrid.TerrainAt(cell);
            
            // Check terrain passability
            if (terrain.passability == Traversability.Impassable)
                return false;

            // Check for existing buildings/blueprints
            var things = cell.GetThingList(map);
            foreach (var thing in things)
            {
                if (thing is Building || thing is Blueprint)
                    return false;
            }

            // Check if blueprint can be placed (RimWorld's own check)
            if (!GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map).Accepted)
                return false;

            // Check power requirement
            if (requirePower && !IsPowerNearby(cell, map))
                return false;

            return true;
        }

        /// <summary>
        /// Check if power conduit is nearby
        /// </summary>
        private static bool IsPowerNearby(IntVec3 cell, Map map)
        {
            // Check within 6 tiles for power conduit
            foreach (var c in GenRadial.RadialCellsAround(cell, 6f, true))
            {
                if (!c.InBounds(map))
                    continue;

                var things = c.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing.def.defName.Contains("Conduit") || 
                        (thing.TryGetComp<CompPowerTransmitter>() != null))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check if an area has power nearby
        /// </summary>
        private static bool CheckAreaHasPower(IntVec3 topLeft, int width, int height, Map map)
        {
            var center = new IntVec3(topLeft.x + width / 2, 0, topLeft.z + height / 2);
            return IsPowerNearby(center, map);
        }

        /// <summary>
        /// Check if an area is roofed
        /// </summary>
        private static bool CheckAreaIsRoofed(IntVec3 topLeft, int width, int height, Map map)
        {
            int roofedCells = 0;
            int totalCells = width * height;
            
            for (int x = topLeft.x; x < topLeft.x + width; x++)
            {
                for (int z = topLeft.z; z < topLeft.z + height; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (cell.Roofed(map))
                        roofedCells++;
                }
            }
            
            // Consider roofed if >80% of cells are roofed
            return roofedCells > (totalCells * 0.8);
        }

        /// <summary>
        /// Generate descriptive notes about the area
        /// </summary>
        private static string GenerateAreaNotes(IntVec3 topLeft, int width, int height, Map map)
        {
            var notes = new List<string>();
            
            // Check terrain quality
            var terrainTypes = new Dictionary<string, int>();
            for (int x = topLeft.x; x < topLeft.x + width; x++)
            {
                for (int z = topLeft.z; z < topLeft.z + height; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    var terrain = map.terrainGrid.TerrainAt(cell);
                    var terrainLabel = terrain.label ?? "unknown";
                    
                    if (!terrainTypes.ContainsKey(terrainLabel))
                        terrainTypes[terrainLabel] = 0;
                    terrainTypes[terrainLabel]++;
                }
            }
            
            // Get dominant terrain
            var dominantTerrain = terrainTypes.OrderByDescending(kvp => kvp.Value).First();
            if (dominantTerrain.Value > (width * height * 0.7))
            {
                notes.Add($"{dominantTerrain.Key} terrain");
            }
            else
            {
                notes.Add("mixed terrain");
            }
            
            // Check if flat (no hills/elevation changes in RimWorld context)
            notes.Add("flat");
            
            return string.Join(", ", notes);
        }

        /// <summary>
        /// Score a buildable area based on criteria
        /// </summary>
        private static float ScoreBuildableArea(BuildableAreaInfo area, IntVec3 targetPos, int maxDistance, 
            Map map, bool requirePower)
        {
            float score = 1.0f;
            
            // Distance scoring (if target specified)
            if (targetPos.IsValid)
            {
                float distance = area.Center.DistanceTo(targetPos);
                
                if (distance > maxDistance)
                    return 0; // Outside max distance
                
                // Score decreases with distance (closer is better)
                float distanceScore = 1.0f - (distance / maxDistance);
                score *= (0.5f + distanceScore * 0.5f); // Weight: 0.5 to 1.0
            }
            
            // Size bonus (larger is slightly better, but not too much weight)
            float sizeBonus = Math.Min((area.width * area.height) / 100.0f, 0.2f);
            score += sizeBonus;
            
            // Power bonus
            if (area.hasPower)
                score *= 1.15f;
            
            // Roofed bonus
            if (area.isRoofed)
                score *= 1.1f;
            
            // Clamp score to 0-1 range
            return Math.Min(score, 1.0f);
        }
    }
}
