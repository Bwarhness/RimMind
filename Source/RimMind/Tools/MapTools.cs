using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class MapTools
    {
        public static string GetWeatherAndSeason()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();
            obj["weather"] = map.weatherManager.curWeather?.LabelCap.ToString() ?? "Unknown";
            obj["outdoorTemperature"] = map.mapTemperature.OutdoorTemp.ToString("F1") + "°C";
            obj["season"] = GenLocalDate.Season(map).LabelCap().ToString();
            obj["biome"] = map.Biome.LabelCap.ToString();
            obj["dayOfYear"] = GenLocalDate.DayOfYear(map);
            obj["year"] = GenLocalDate.Year(map);
            obj["hour"] = GenLocalDate.HourInteger(map);

            // Growing season info
            obj["growingSeasonActive"] = map.mapTemperature.SeasonalTemp > 0;

            return obj.ToString();
        }

        public static string GetGrowingZones()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var zone in map.zoneManager.AllZones)
            {
                var growing = zone as Zone_Growing;
                if (growing == null) continue;

                var obj = new JSONObject();
                obj["label"] = growing.label;
                obj["cellCount"] = growing.CellCount;

                var plantDef = growing.GetPlantDefToGrow();
                obj["crop"] = plantDef?.LabelCap.ToString() ?? "None";

                // Calculate average growth
                float totalGrowth = 0;
                int plantCount = 0;
                foreach (var cell in growing.Cells)
                {
                    var plant = cell.GetPlant(map);
                    if (plant != null)
                    {
                        totalGrowth += plant.Growth;
                        plantCount++;
                    }
                }

                if (plantCount > 0)
                    obj["averageGrowth"] = (totalGrowth / plantCount * 100f).ToString("F1") + "%";
                else
                    obj["averageGrowth"] = "0% (no plants)";

                obj["plantedCells"] = plantCount;

                // Average fertility
                float totalFertility = 0;
                foreach (var cell in growing.Cells)
                    totalFertility += map.fertilityGrid.FertilityAt(cell);
                obj["averageFertility"] = (totalFertility / growing.CellCount).ToString("F2");

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["growingZones"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetPowerStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();
            var nets = map.powerNetManager.AllNetsListForReading;

            float totalGeneration = 0;
            float totalConsumption = 0;
            float totalStored = 0;
            float totalStorageCapacity = 0;
            int batteryCount = 0;

            foreach (var net in nets)
            {
                foreach (var comp in net.powerComps)
                {
                    if (comp.PowerOn)
                    {
                        if (comp.PowerOutput > 0)
                            totalGeneration += comp.PowerOutput;
                        else
                            totalConsumption += -comp.PowerOutput;
                    }
                }

                foreach (var battery in net.batteryComps)
                {
                    totalStored += battery.StoredEnergy;
                    totalStorageCapacity += battery.Props.storedEnergyMax;
                    batteryCount++;
                }
            }

            obj["totalGeneration"] = totalGeneration.ToString("F0") + " W";
            obj["totalConsumption"] = totalConsumption.ToString("F0") + " W";
            obj["surplus"] = (totalGeneration - totalConsumption).ToString("F0") + " W";
            obj["batteryCount"] = batteryCount;
            obj["storedEnergy"] = totalStored.ToString("F0") + " Wd";
            obj["storageCapacity"] = totalStorageCapacity.ToString("F0") + " Wd";
            obj["powerNets"] = nets.Count;

            if (totalStorageCapacity > 0)
                obj["batteryPercentage"] = (totalStored / totalStorageCapacity * 100f).ToString("F1") + "%";

            return obj.ToString();
        }

        public static string GetMapRegion(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            int startX = 0, startZ = 0;
            int w = map.Size.x, h = map.Size.z;

            if (args != null)
            {
                int tmp;
                if (int.TryParse(args["x"]?.Value, out tmp)) startX = tmp;
                if (int.TryParse(args["z"]?.Value, out tmp)) startZ = tmp;
                if (int.TryParse(args["width"]?.Value, out tmp)) w = tmp;
                if (int.TryParse(args["height"]?.Value, out tmp)) h = tmp;
            }

            // Clamp to map bounds
            startX = Math.Max(0, Math.Min(startX, map.Size.x - 1));
            startZ = Math.Max(0, Math.Min(startZ, map.Size.z - 1));
            w = Math.Min(w, map.Size.x - startX);
            h = Math.Min(h, map.Size.z - startZ);

            var result = new JSONObject();
            result["mapSize"] = map.Size.x + "x" + map.Size.z;

            var region = new JSONObject();
            region["x"] = startX;
            region["z"] = startZ;
            region["width"] = w;
            region["height"] = h;
            result["region"] = region;

            var usedCodes = new HashSet<char>();
            var grid = new JSONArray();

            // Iterate high Z to low Z so grid reads north-to-south (top-to-bottom)
            for (int row = startZ + h - 1; row >= startZ; row--)
            {
                var rowChars = new char[w];
                for (int col = startX; col < startX + w; col++)
                {
                    var cell = new IntVec3(col, 0, row);
                    char code = ClassifyCell(cell, map);
                    rowChars[col - startX] = code;
                    usedCodes.Add(code);
                }
                grid.Add(new string(rowChars));
            }

            result["grid"] = grid;

            var legend = new JSONObject();
            foreach (var code in usedCodes)
            {
                string desc = GetLegendDescription(code);
                if (desc != null)
                    legend[code.ToString()] = desc;
            }
            result["legend"] = legend;

            return result.ToString();
        }

        public static string GetCellDetails(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null || string.IsNullOrEmpty(args["x"]?.Value) || string.IsNullOrEmpty(args["z"]?.Value))
                return ToolExecutor.JsonError("x and z coordinates are required.");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;

            string x2Val = args["x2"]?.Value;
            string z2Val = args["z2"]?.Value;
            bool hasRange = !string.IsNullOrEmpty(x2Val) && !string.IsNullOrEmpty(z2Val);

            if (!hasRange)
            {
                var cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError("Coordinates out of bounds. Map size: " + map.Size.x + "x" + map.Size.z);
                return GetSingleCellDetail(cell, map).ToString();
            }

            int endX = args["x2"].AsInt;
            int endZ = args["z2"].AsInt;
            int minX = Math.Min(x, endX);
            int maxX = Math.Max(x, endX);
            int minZ = Math.Min(z, endZ);
            int maxZ = Math.Max(z, endZ);

            int rangeW = maxX - minX + 1;
            int rangeH = maxZ - minZ + 1;

            if (rangeW * rangeH > 225)
                return ToolExecutor.JsonError("Range too large (" + rangeW + "x" + rangeH + " = " + (rangeW * rangeH) + " cells). Max 225 cells (15x15). Use get_map_region for overview, get_cell_details for smaller sections.");

            var result = new JSONObject();
            result["range"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["size"] = rangeW + "x" + rangeH;

            var cells = new JSONArray();
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new IntVec3(cx, 0, cz);
                    if (cell.InBounds(map))
                        cells.Add(GetSingleCellDetail(cell, map));
                }
            }
            result["cells"] = cells;
            return result.ToString();
        }

        private static char ClassifyCell(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return ' ';

            var things = cell.GetThingList(map);
            Building building = null;
            Pawn pawn = null;
            bool hasItem = false;

            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing is Building b && building == null)
                    building = b;
                else if (thing is Pawn p && pawn == null)
                    pawn = p;
                else if (thing.def.category == ThingCategory.Item)
                    hasItem = true;
            }

            // Priority 1: Buildings
            if (building != null)
            {
                char bc = GetBuildingCode(building);
                if (bc != '\0') return bc;
            }

            // Priority 2: Pawns
            if (pawn != null)
                return GetPawnCode(pawn);

            // Priority 3: Items
            if (hasItem) return 'i';

            // Priority 4: Plans
            if (map.planManager.PlanAt(cell) != null) return 'p';

            // Priority 5: Zones
            var zone = map.zoneManager.ZoneAt(cell);
            if (zone is Zone_Growing) return 'g';
            if (zone is Zone_Stockpile) return 's';

            // Priority 5b: Custom labeled zones
            var tracker = RimMind.Core.ZoneTracker.Instance;
            if (tracker != null && tracker.GetZoneAt(cell.x, cell.z) != null) return 'z';

            // Priority 5c: Constructed floor
            var terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain.layerable) return 'f';

            // Priority 6: Terrain
            return GetTerrainCode(terrain);
        }

        private static char GetBuildingCode(Building b)
        {
            var def = b.def;
            string defName = def.defName;

            // Natural rock = mountain
            if (def.building != null && def.building.isNaturalRock)
                return '^';

            // Doors
            if (b is Building_Door)
                return 'D';

            // Traps
            if (b is Building_Trap)
                return 'X';

            // Turrets
            if (b is Building_Turret)
                return 'U';

            // Smoothed rock
            if (def.IsSmoothed)
                return '#';

            // Walls (impassable, high fill)
            if (def.passability == Traversability.Impassable && def.fillPercent >= 0.99f)
                return 'W';

            // Beds
            if (def.IsBed)
            {
                if (b is Building_Bed bed && bed.Medical)
                    return 'M';
                return 'B';
            }

            // Power conduits
            if (defName.Contains("Conduit"))
                return 'P';

            // Barricades/sandbags
            if (defName.Contains("Sandbag") || defName.Contains("Barricade"))
                return 'F';

            // Nutrient paste
            if (b is Building_NutrientPasteDispenser)
                return 'N';

            // Research benches (before workbench check)
            if (defName.Contains("ResearchBench"))
                return 'R';

            // Workbenches
            if (b is Building_WorkTable)
                return 'H';

            // Climate control
            if (defName == "Heater" || defName == "Cooler" || defName.Contains("Vent") || defName == "PassiveCooler")
                return 'K';

            // Lamps/lights
            if (defName.Contains("Lamp") || defName.Contains("Light"))
                return 'L';

            // Batteries
            if (defName.Contains("Battery"))
                return 'A';

            // Power generators
            if (defName.Contains("Generator") || defName.Contains("Solar") || defName.Contains("Wind") ||
                defName.Contains("Geothermal") || defName.Contains("WoodFired") || defName.Contains("Chemfuel"))
                return 'G';

            // Tables
            if (def.surfaceType == SurfaceType.Eat)
                return 'T';

            // Chairs
            if (def.building != null && def.building.isSittable)
                return 'C';

            // Storage buildings
            if (defName.Contains("Shelf"))
                return 'S';

            return 'Q';
        }

        private static char GetPawnCode(Pawn p)
        {
            if (p.Faction != null && p.Faction.IsPlayer)
                return p.RaceProps.Animal ? '&' : '@';

            try
            {
                if (p.HostileTo(Faction.OfPlayer))
                    return '!';
            }
            catch { }

            return p.RaceProps.Animal ? '%' : '?';
        }

        private static char GetTerrainCode(TerrainDef terrain)
        {
            string defName = terrain.defName;

            if (defName.Contains("Marsh"))
                return '*';
            if (defName.Contains("Water") || defName.Contains("Ocean") || defName.Contains("Lake") || defName.Contains("River"))
                return '~';
            if (defName.Contains("Ice"))
                return '-';
            if (defName.Contains("Sand"))
                return '_';
            if (defName.Contains("Gravel"))
                return ',';
            if (defName.Contains("Soil") || defName.Contains("Dirt") || defName.Contains("Moss") || defName.Contains("Rich"))
                return '.';
            if (defName.Contains("Smooth"))
                return '#';

            return 'o';
        }

        private static string GetLegendDescription(char code)
        {
            switch (code)
            {
                case 'W': return "wall";
                case 'D': return "door";
                case 'B': return "bed";
                case 'M': return "medical bed";
                case 'T': return "table";
                case 'C': return "chair";
                case 'H': return "workbench";
                case 'R': return "research bench";
                case 'G': return "generator";
                case 'A': return "battery";
                case 'U': return "turret";
                case 'L': return "lamp";
                case 'P': return "power conduit";
                case 'K': return "climate control";
                case 'S': return "storage building";
                case 'N': return "nutrient paste";
                case 'X': return "trap";
                case 'F': return "barricade/sandbag";
                case 'Q': return "other building";
                case '@': return "colonist";
                case '!': return "hostile pawn";
                case '?': return "neutral pawn";
                case '&': return "colony animal";
                case '%': return "wild animal";
                case 'i': return "item";
                case 'p': return "plan designation";
                case 'g': return "growing zone";
                case 's': return "stockpile zone";
                case 'z': return "planning zone (AI-created)";
                case 'f': return "constructed floor";
                case '.': return "soil";
                case ',': return "gravel";
                case '~': return "water";
                case '^': return "mountain/rock";
                case '#': return "smoothed rock";
                case '_': return "sand";
                case '*': return "marsh";
                case '-': return "ice";
                case 'o': return "other terrain";
                default: return null;
            }
        }

        private static JSONObject GetSingleCellDetail(IntVec3 cell, Map map)
        {
            var obj = new JSONObject();
            obj["x"] = cell.x;
            obj["z"] = cell.z;

            var terrain = map.terrainGrid.TerrainAt(cell);
            obj["terrain"] = terrain.LabelCap.ToString();

            var roof = map.roofGrid.RoofAt(cell);
            obj["roof"] = roof != null ? roof.LabelCap.ToString() : "None";

            float temp;
            if (GenTemperature.TryGetTemperatureForCell(cell, map, out temp))
                obj["temperature"] = temp.ToString("F1") + "\u00B0C";

            obj["fertility"] = map.fertilityGrid.FertilityAt(cell).ToString("F2");

            try
            {
                var room = cell.GetRoom(map);
                if (room != null && !room.TouchesMapEdge)
                {
                    var roomObj = new JSONObject();
                    roomObj["role"] = room.Role != null ? room.Role.LabelCap.ToString() : "None";
                    try
                    {
                        roomObj["impressiveness"] = room.GetStat(RoomStatDefOf.Impressiveness).ToString("F1");
                        roomObj["beauty"] = room.GetStat(RoomStatDefOf.Beauty).ToString("F1");
                        roomObj["cleanliness"] = room.GetStat(RoomStatDefOf.Cleanliness).ToString("F1");
                        roomObj["space"] = room.GetStat(RoomStatDefOf.Space).ToString("F1");
                    }
                    catch { }
                    obj["room"] = roomObj;
                }
            }
            catch { }

            var zone = map.zoneManager.ZoneAt(cell);
            if (zone != null)
                obj["zone"] = zone.label;

            // Designations
            var designations = map.designationManager.AllDesignationsAt(cell);
            if (designations != null && designations.Count > 0)
            {
                var desArr = new JSONArray();
                foreach (var des in designations)
                    desArr.Add(des.def.defName);
                obj["designations"] = desArr;
            }

            var things = cell.GetThingList(map);
            if (things.Count > 0)
            {
                var thingsArr = new JSONArray();
                foreach (var thing in things)
                {
                    var thingObj = new JSONObject();
                    thingObj["name"] = thing.LabelCap.ToString();
                    thingObj["type"] = thing.def.category.ToString();

                    if (thing is Building bld)
                    {
                        thingObj["hitPoints"] = bld.HitPoints + "/" + bld.MaxHitPoints;
                    }
                    else if (thing is Pawn pawn)
                    {
                        if (pawn.Faction != null)
                            thingObj["faction"] = pawn.Faction.Name;
                        if (pawn.CurJobDef != null)
                            thingObj["currentJob"] = pawn.CurJobDef.reportString;
                    }

                    if (thing.stackCount > 1)
                        thingObj["count"] = thing.stackCount;

                    thingsArr.Add(thingObj);
                }
                obj["things"] = thingsArr;
            }

            return obj;
        }
    }
}
