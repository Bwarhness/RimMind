using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
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
    }
}
