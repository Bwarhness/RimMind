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
            obj["growingSeasonActive"] = map.mapTemperature.OutdoorTemp > 0f;

            // Active events summary (Phase 7)
            try
            {
                var activeEventsArr = new JSONArray();
                var gameConditions = map.gameConditionManager?.ActiveConditions;
                
                if (gameConditions != null && gameConditions.Count > 0)
                {
                    foreach (var condition in gameConditions)
                    {
                        var eventObj = new JSONObject();
                        eventObj["type"] = condition.def.defName;
                        eventObj["label"] = condition.LabelCap;
                        
                        int ticksRemaining = condition.TicksLeft;
                        if (ticksRemaining > 0)
                        {
                            float daysRemaining = ticksRemaining / 60000f;
                            eventObj["duration_remaining_days"] = daysRemaining.ToString("F1");
                        }
                        else
                        {
                            eventObj["duration_remaining_days"] = "unknown";
                        }
                        
                        activeEventsArr.Add(eventObj);
                    }
                    
                    obj["active_events"] = activeEventsArr;
                    obj["active_event_count"] = activeEventsArr.Count;
                    obj["note"] = "Use get_active_events for detailed event information, risks, and recommendations";
                }
                else
                {
                    obj["active_events"] = activeEventsArr;
                    obj["active_event_count"] = 0;
                }
            }
            catch (Exception ex)
            {
                obj["events_error"] = "Could not read active events: " + ex.Message;
            }

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
            
            float surplus = totalGeneration - totalConsumption;
            obj["surplus"] = surplus.ToString("F0") + " W";
            obj["batteryCount"] = batteryCount;
            obj["storedEnergy"] = totalStored.ToString("F0") + " Wd";
            obj["storageCapacity"] = totalStorageCapacity.ToString("F0") + " Wd";
            obj["powerNets"] = nets.Count;

            if (totalStorageCapacity > 0)
                obj["batteryPercentage"] = (totalStored / totalStorageCapacity * 100f).ToString("F1") + "%";

            // Phase 4: Power Failure Prediction
            // Calculate battery drain rate and time until blackout
            if (batteryCount > 0)
            {
                // Net power flow: negative = draining, positive = charging
                float netPowerFlow = surplus;
                
                // Convert W to Wd/hour (1 hour = 2500 ticks, power is in watts)
                // Power consumption is continuous, so Wd/hour = W * (2500 ticks/hour / 60000 ticks/day)
                // Simplified: Wd/hour ≈ W / 24
                float drainRatePerHour = -netPowerFlow / 24f;
                
                if (netPowerFlow < 0)
                {
                    // Batteries are draining
                    obj["batteryDrainRate"] = drainRatePerHour.ToString("F1") + " Wd/hour";
                    
                    // Calculate hours until blackout
                    if (drainRatePerHour > 0)
                    {
                        float hoursRemaining = totalStored / drainRatePerHour;
                        obj["hoursUntilBlackout"] = hoursRemaining.ToString("F1");
                        
                        // Critical warning if < 2 hours
                        if (hoursRemaining < 2f)
                        {
                            obj["status"] = "critical";
                            obj["warning"] = string.Format("⚠️ CRITICAL: Batteries will die in {0:F1} hours without power generation!", hoursRemaining);
                        }
                        else if (hoursRemaining < 6f)
                        {
                            obj["status"] = "low";
                            obj["warning"] = string.Format("Low power: {0:F1} hours of battery remaining", hoursRemaining);
                        }
                        else
                        {
                            obj["status"] = "draining";
                        }
                    }
                    else
                    {
                        obj["hoursUntilBlackout"] = "unknown";
                        obj["status"] = "draining";
                    }
                }
                else if (netPowerFlow > 0)
                {
                    // Batteries are charging
                    float chargeRate = netPowerFlow / 24f;
                    obj["batteryChargeRate"] = chargeRate.ToString("F1") + " Wd/hour";
                    obj["status"] = "charging";
                    
                    // Calculate time to full charge
                    float energyNeeded = totalStorageCapacity - totalStored;
                    if (chargeRate > 0 && energyNeeded > 0)
                    {
                        float hoursToFull = energyNeeded / chargeRate;
                        obj["hoursToFullCharge"] = hoursToFull.ToString("F1");
                    }
                }
                else
                {
                    // Perfectly balanced
                    obj["status"] = "balanced";
                }
            }
            else
            {
                // No batteries
                if (surplus >= 0)
                    obj["status"] = "stable";
                else
                    obj["status"] = "insufficient_generation";
            }

            return obj.ToString();
        }

        public static string GetTemperatureRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var atRisk = new JSONArray();
            var safe = new JSONArray();

            var colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0)
            {
                result["message"] = "No colonists on the map";
                return result.ToString();
            }

            // Get ambient temperature
            float ambientTemp = map.mapTemperature.OutdoorTemp;

            foreach (var colonist in colonists)
            {
                var obj = new JSONObject();
                obj["name"] = colonist.Name.ToStringShort;

                // Get position temperature at colonist's location
                var position = colonist.Position;
                var room = position.GetRoom(map);
                float localTemp = room != null ? room.Temperature : ambientTemp;

                obj["current_temp"] = localTemp.ToString("F1") + "°C";
                obj["ambient_temp"] = ambientTemp.ToString("F1") + "°C";

                // Get comfortable temperature range
                float minComfort = colonist.GetStatValue(StatDefOf.ComfyTemperatureMin, false);
                float maxComfort = colonist.GetStatValue(StatDefOf.ComfyTemperatureMax, false);

                obj["comfortable_range"] = minComfort.ToString("F0") + "°C to " + maxComfort.ToString("F0") + "°C";

                // Check for risk
                if (localTemp < minComfort - 5f)
                {
                    obj["risk"] = "freezing";
                    obj["severity"] = (minComfort - localTemp > 15f) ? "critical" : "warning";
                    obj["action"] = "Move to heated area";
                    atRisk.Add(obj);
                }
                else if (localTemp > maxComfort + 5f)
                {
                    obj["risk"] = "overheating";
                    obj["severity"] = (localTemp - maxComfort > 15f) ? "critical" : "warning";
                    obj["action"] = "Move to cooled area";
                    atRisk.Add(obj);
                }
                else
                {
                    obj["risk"] = "safe";
                    obj["status"] = "OK";
                    safe.Add(obj);
                }
            }

            result["at_risk"] = atRisk;
            result["safe"] = safe;
            result["total_checked"] = colonists.Count;

            // Set overall status
            if (atRisk.Count > 0)
            {
                result["overall_status"] = "warning";
                result["message"] = atRisk.Count + " colonists at risk from temperature";
            }
            else
            {
                result["overall_status"] = "safe";
                result["message"] = "All colonists in safe temperature range";
            }

            return result.ToString();
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
            var codeDefNames = new Dictionary<char, HashSet<string>>();
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

                    // Track actual defNames for enriched legend
                    if (char.IsLetter(code))
                    {
                        if (!codeDefNames.ContainsKey(code))
                            codeDefNames[code] = new HashSet<string>();

                        foreach (var thing in cell.GetThingList(map))
                        {
                            if (thing is Blueprint_Build bpb)
                            {
                                var builtDef = bpb.def.entityDefToBuild as ThingDef;
                                if (builtDef != null)
                                {
                                    codeDefNames[code].Add(builtDef.defName);
                                    break;
                                }
                            }
                            else if (thing is Blueprint_Install bpi)
                            {
                                var builtDef = bpi.def.entityDefToBuild as ThingDef;
                                if (builtDef != null)
                                {
                                    codeDefNames[code].Add(builtDef.defName);
                                    break;
                                }
                            }
                            else if (thing.def.category == ThingCategory.Building)
                            {
                                codeDefNames[code].Add(thing.def.defName);
                                break;
                            }
                        }
                    }
                }
                grid.Add(new string(rowChars));
            }

            result["grid"] = grid;

            // Include pawns in region so AI doesn't need to hunt for '@' in the grid
            var pawnsInRegion = new JSONArray();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                var pos = pawn.Position;
                if (pos.x >= startX && pos.x < startX + w && pos.z >= startZ && pos.z < startZ + h)
                {
                    var po = new JSONObject();
                    po["name"] = pawn.LabelShort;
                    po["x"] = pos.x;
                    po["z"] = pos.z;
                    po["type"] = pawn.Faction != null && pawn.Faction.IsPlayer
                        ? (pawn.RaceProps.Animal ? "colony_animal" : "colonist")
                        : (pawn.RaceProps.Animal ? "wild_animal" : "other");
                    pawnsInRegion.Add(po);
                }
            }
            if (pawnsInRegion.Count > 0)
                result["pawns"] = pawnsInRegion;

            var legend = new JSONObject();
            foreach (var code in usedCodes)
            {
                string desc = GetLegendDescription(code);
                if (desc != null)
                {
                    if (codeDefNames.ContainsKey(code) && codeDefNames[code].Count > 0 && codeDefNames[code].Count <= 5)
                    {
                        var sorted = codeDefNames[code].ToList();
                        sorted.Sort();
                        desc += " (" + string.Join(", ", sorted) + ")";
                    }
                    legend[code.ToString()] = desc;
                }
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

        /// <summary>
        /// Render a small character grid of a map area. Used by other tools to show build results.
        /// </summary>
        public static string RenderArea(Map map, int x1, int z1, int x2, int z2)
        {
            int minX = System.Math.Min(x1, x2);
            int maxX = System.Math.Max(x1, x2);
            int minZ = System.Math.Min(z1, z2);
            int maxZ = System.Math.Max(z1, z2);

            var sb = new System.Text.StringBuilder();
            // Render top-to-bottom (high Z first)
            for (int z = maxZ; z >= minZ; z--)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map))
                        sb.Append(' ');
                    else
                        sb.Append(ClassifyCell(cell, map));
                }
                if (z > minZ) sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static char ClassifyCell(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return ' ';

            var things = cell.GetThingList(map);
            Building building = null;
            Thing blueprint = null;
            ThingDef blueprintDef = null;
            Pawn pawn = null;
            bool hasItem = false;

            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                // Check for blueprints (Blueprint doesn't extend Building!)
                if (thing is Blueprint bp)
                {
                    ThingDef builtDef = null;
                    if (bp is Blueprint_Build bpb)
                        builtDef = bpb.def.entityDefToBuild as ThingDef;
                    else if (bp is Blueprint_Install bpi)
                        builtDef = bpi.def.entityDefToBuild as ThingDef;

                    if (builtDef != null && blueprint == null)
                    {
                        blueprint = bp;
                        blueprintDef = builtDef;
                    }
                }
                else if (thing is Building b && building == null)
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

            // Priority 1b: Blueprints (show as lowercase of what they'll become)
            if (blueprint != null && blueprintDef != null)
            {
                char code = GetBuildingCodeForDef(blueprintDef);
                return char.ToLower(code);
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

        internal static char GetBuildingCodeForDef(ThingDef def)
        {
            if (def == null) return 'Q';

            // Natural rock
            if (def.IsNonResourceNaturalRock) return '^';
            // Door
            if (typeof(Building_Door).IsAssignableFrom(def.thingClass)) return 'D';
            // Trap
            if (typeof(Building_Trap).IsAssignableFrom(def.thingClass)) return 'X';
            // Turret
            if (typeof(Building_Turret).IsAssignableFrom(def.thingClass)) return 'U';
            // Smoothed rock
            if (def.IsSmoothed) return '#';
            // Wall (impassable, high fill)
            if (def.passability == Traversability.Impassable && def.fillPercent >= 0.9f) return 'W';
            // Beds
            if (typeof(Building_Bed).IsAssignableFrom(def.thingClass))
            {
                if (def.defName.Contains("Medical")) return 'M';
                return 'B';
            }
            // Power conduit
            if (def.defName.Contains("Conduit")) return 'P';
            // Barricade/Sandbag
            if (def.defName.Contains("Sandbag") || def.defName.Contains("Barricade")) return 'F';
            // Nutrient paste
            if (def.defName.Contains("NutrientPaste")) return 'N';
            // Research bench
            if (def.defName.Contains("ResearchBench")) return 'R';
            // Work tables
            if (typeof(Building_WorkTable).IsAssignableFrom(def.thingClass)) return 'H';
            // Climate control
            if (def.defName.Contains("Heater") || def.defName.Contains("Cooler") || def.defName.Contains("Vent")) return 'K';
            // Lamp/Light
            if (def.defName.Contains("Lamp") || def.defName.Contains("Light") || def.defName.Contains("Torch")) return 'L';
            // Battery
            if (def.defName.Contains("Battery")) return 'A';
            // Power generators
            if (def.defName.Contains("Generator") || def.defName.Contains("Solar") || def.defName.Contains("Wind") ||
                def.defName.Contains("Geothermal") || def.defName.Contains("WoodFired") || def.defName.Contains("Chemfuel")) return 'G';
            // Table (eat surface)
            if (def.surfaceType == SurfaceType.Eat) return 'T';
            // Chair
            if (def.building != null && def.building.isSittable) return 'C';
            // Shelf/storage
            if (def.thingClass != null && def.thingClass.Name.Contains("Storage")) return 'S';
            // Fallback
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

        internal static string GetLegendDescription(char code)
        {
            // Blueprint variants (lowercase building codes, excluding letters already used: g=growing, s=stockpile, f=floor, p=plan)
            if (char.IsLower(code) && "wdbmtchraulknxq".IndexOf(code) >= 0)
            {
                string baseDesc = GetLegendDescription(char.ToUpper(code));
                return baseDesc != null ? baseDesc + " (blueprint)" : "blueprint";
            }

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
                var blueprintsArr = new JSONArray();

                foreach (var thing in things)
                {
                    // Check if this is a blueprint
                    if (thing is Blueprint bp)
                    {
                        var bpObj = new JSONObject();
                        ThingDef builtDef = null;
                        if (bp is Blueprint_Build bpb)
                            builtDef = bpb.def.entityDefToBuild as ThingDef;
                        else if (bp is Blueprint_Install bpi)
                            builtDef = bpi.def.entityDefToBuild as ThingDef;

                        if (builtDef != null)
                        {
                            bpObj["defName"] = builtDef.defName;
                            bpObj["label"] = builtDef.LabelCap.ToString();
                            bpObj["status"] = "unbuilt";
                            
                            // Show stuff if applicable
                            if (bp.Stuff != null)
                                bpObj["material"] = bp.Stuff.LabelCap.ToString();
                            
                            blueprintsArr.Add(bpObj);
                        }
                        continue; // Don't add to things array
                    }

                    var thingObj = new JSONObject();
                    thingObj["name"] = thing.LabelCap.ToString();
                    thingObj["type"] = thing.def.category.ToString();

                    if (thing is Building bld)
                    {
                        thingObj["hitPoints"] = bld.HitPoints + "/" + bld.MaxHitPoints;
                        thingObj["status"] = "built";
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

                if (blueprintsArr.Count > 0)
                    obj["blueprints"] = blueprintsArr;
                if (thingsArr.Count > 0)
                    obj["things"] = thingsArr;
            }

            return obj;
        }

        public static string GetBlueprints(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Optional filter by defName or label
            string filterRaw = args?["filter"]?.Value;
            string filter = (string.IsNullOrEmpty(filterRaw) || filterRaw == "null") ? null : filterRaw.ToLowerInvariant();

            // Optional bounds
            bool hasBounds = args?["x1"] != null && args?["z1"] != null && args?["x2"] != null && args?["z2"] != null;
            int bx1 = hasBounds ? args["x1"].AsInt : 0;
            int bz1 = hasBounds ? args["z1"].AsInt : 0;
            int bx2 = hasBounds ? args["x2"].AsInt : map.Size.x - 1;
            int bz2 = hasBounds ? args["z2"].AsInt : map.Size.z - 1;

            bool InBounds(IntVec3 pos) => !hasBounds || (pos.x >= bx1 && pos.x <= bx2 && pos.z >= bz1 && pos.z <= bz2);

            var result = new JSONObject();
            var blueprints = new JSONArray();
            int totalFound = 0;
            const int MaxResults = 200;

            // Find all blueprints
            foreach (var thing in map.listerThings.AllThings)
            {
                if (!(thing is Blueprint bp)) continue;
                if (!thing.Spawned) continue;
                if (!InBounds(thing.Position)) continue;

                ThingDef builtDef = null;
                if (bp is Blueprint_Build bpb)
                    builtDef = bpb.def.entityDefToBuild as ThingDef;
                else if (bp is Blueprint_Install bpi)
                    builtDef = bpi.def.entityDefToBuild as ThingDef;

                if (builtDef == null) continue;

                // Apply filter
                if (filter != null)
                {
                    bool matches = builtDef.defName.ToLowerInvariant().Contains(filter)
                        || builtDef.LabelCap.ToString().ToLowerInvariant().Contains(filter);
                    if (!matches) continue;
                }

                totalFound++;
                if (blueprints.Count < MaxResults)
                {
                    var bpObj = new JSONObject();
                    bpObj["defName"] = builtDef.defName;
                    bpObj["label"] = builtDef.LabelCap.ToString();
                    bpObj["x"] = thing.Position.x;
                    bpObj["z"] = thing.Position.z;

                    if (bp.Stuff != null)
                        bpObj["material"] = bp.Stuff.LabelCap.ToString();

                    // Add size info for multi-cell buildings
                    if (builtDef.size.x > 1 || builtDef.size.z > 1)
                        bpObj["size"] = builtDef.size.x + "x" + builtDef.size.z;

                    // Show rotation for doors, coolers, beds, etc.
                    bpObj["rotation"] = bp.Rotation.AsInt;

                    blueprints.Add(bpObj);
                }
            }

            result["total"] = totalFound;
            result["returned"] = blueprints.Count;
            if (totalFound > blueprints.Count)
                result["note"] = "Results capped at " + MaxResults + ". Use bounds (x1,z1,x2,z2) or filter to narrow results.";
            result["blueprints"] = blueprints;

            if (totalFound == 0)
                result["message"] = "No blueprints found on the map" + (filter != null ? " matching filter '" + filter + "'" : "") + ".";

            return result.ToString();
        }

        public static string SearchMap(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string type = args?["type"]?.Value?.ToLowerInvariant();
            if (string.IsNullOrEmpty(type))
                return ToolExecutor.JsonError("'type' is required. Options: colonists, hostiles, animals, items, buildings, minerals, plants");

            string filterRaw = args?["filter"]?.Value;
            string filter = (string.IsNullOrEmpty(filterRaw) || filterRaw == "null") ? null : filterRaw.ToLowerInvariant();

            bool hasBounds = args?["x1"] != null && args?["z1"] != null && args?["x2"] != null && args?["z2"] != null;
            int bx1 = hasBounds ? args["x1"].AsInt : 0;
            int bz1 = hasBounds ? args["z1"].AsInt : 0;
            int bx2 = hasBounds ? args["x2"].AsInt : map.Size.x - 1;
            int bz2 = hasBounds ? args["z2"].AsInt : map.Size.z - 1;

            var result = new JSONObject();
            var matches = new JSONArray();
            int totalFound = 0;
            const int MaxResults = 100;

            bool InBounds(IntVec3 pos) => !hasBounds || (pos.x >= bx1 && pos.x <= bx2 && pos.z >= bz1 && pos.z <= bz2);
            bool MatchesFilter(string defName, string label) => filter == null
                || defName.ToLowerInvariant().Contains(filter)
                || label.ToLowerInvariant().Contains(filter);
            bool MatchesFilterDef(ThingDef def) => MatchesFilter(def.defName, def.LabelCap.ToString())
                || (def.thingCategories != null && def.thingCategories.Any(c =>
                    c.defName.ToLowerInvariant().Contains(filter)
                    || c.LabelCap.ToString().ToLowerInvariant().Contains(filter)));

            if (type == "colonists" || type == "hostiles" || type == "animals")
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    bool typeMatch = false;
                    if (type == "colonists") typeMatch = pawn.Faction?.IsPlayer == true && !pawn.RaceProps.Animal;
                    else if (type == "animals") typeMatch = pawn.RaceProps.Animal;
                    else if (type == "hostiles") { try { typeMatch = pawn.HostileTo(Faction.OfPlayer); } catch { } }

                    if (!typeMatch) continue;
                    if (!InBounds(pawn.Position)) continue;
                    if (!MatchesFilter(pawn.def.defName, pawn.def.LabelCap.ToString())
                        && (filter == null || !pawn.LabelShort.ToLowerInvariant().Contains(filter))) continue;

                    totalFound++;
                    if (matches.Count < MaxResults)
                    {
                        var m = new JSONObject();
                        m["name"] = pawn.LabelShort;
                        m["x"] = pawn.Position.x;
                        m["z"] = pawn.Position.z;
                        if (type == "animals") m["faction"] = pawn.Faction?.IsPlayer == true ? "colony" : "wild";
                        if (pawn.CurJobDef != null) m["job"] = pawn.CurJobDef.reportString;
                        matches.Add(m);
                    }
                }
            }
            else if (type == "items")
            {
                // Group by defName: list up to 50 distinct items, each with up to 10 locations
                var grouped = new Dictionary<string, JSONObject>();
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                {
                    if (!thing.Spawned) continue;
                    if (!InBounds(thing.Position)) continue;
                    if (filter != null && !MatchesFilterDef(thing.def)) continue;

                    string defName = thing.def.defName;
                    totalFound += thing.stackCount;

                    if (!grouped.ContainsKey(defName) && grouped.Count < 50)
                    {
                        var g = new JSONObject();
                        g["defName"] = defName;
                        g["label"] = thing.def.LabelCap.ToString();
                        g["totalCount"] = 0;
                        g["locations"] = new JSONArray();
                        grouped[defName] = g;
                    }

                    if (grouped.ContainsKey(defName))
                    {
                        grouped[defName]["totalCount"] = grouped[defName]["totalCount"].AsInt + thing.stackCount;
                        var locs = grouped[defName]["locations"].AsArray;
                        if (locs.Count < 10)
                        {
                            var loc = new JSONObject();
                            loc["x"] = thing.Position.x;
                            loc["z"] = thing.Position.z;
                            if (thing.stackCount > 1) loc["count"] = thing.stackCount;
                            locs.Add(loc);
                        }
                    }
                }
                foreach (var g in grouped.Values) matches.Add(g);
                totalFound = matches.Count > 0 ? totalFound : 0;
            }
            else if (type == "buildings")
            {
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (!building.Spawned) continue;
                    if (!InBounds(building.Position)) continue;
                    if (filter != null && !MatchesFilterDef(building.def)) continue;

                    totalFound++;
                    if (matches.Count < MaxResults)
                    {
                        var m = new JSONObject();
                        m["defName"] = building.def.defName;
                        m["label"] = building.def.LabelCap.ToString();
                        m["x"] = building.Position.x;
                        m["z"] = building.Position.z;
                        if (building.def.size.x > 1 || building.def.size.z > 1)
                            m["size"] = building.def.size.x + "x" + building.def.size.z;
                        matches.Add(m);
                    }
                }
            }
            else if (type == "minerals")
            {
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (!(thing is Building)) continue;
                    if (!thing.Spawned) continue;
                    var bDef = ((Building)thing).def.building;
                    if (bDef == null || bDef.mineableThing == null) continue;
                    if (!InBounds(thing.Position)) continue;

                    string yieldLabel = bDef.mineableThing.LabelCap.ToString();
                    if (filter != null && !MatchesFilterDef(thing.def)
                        && !yieldLabel.ToLowerInvariant().Contains(filter)) continue;

                    totalFound++;
                    if (matches.Count < MaxResults)
                    {
                        var m = new JSONObject();
                        m["defName"] = thing.def.defName;
                        m["label"] = thing.def.LabelCap.ToString();
                        m["yields"] = yieldLabel;
                        m["x"] = thing.Position.x;
                        m["z"] = thing.Position.z;
                        matches.Add(m);
                    }
                }
            }
            else if (type == "plants")
            {
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (!(thing is Plant plant)) continue;
                    if (!thing.Spawned) continue;
                    if (!InBounds(thing.Position)) continue;
                    if (filter != null && !MatchesFilterDef(thing.def)) continue;

                    totalFound++;
                    if (matches.Count < MaxResults)
                    {
                        var m = new JSONObject();
                        m["defName"] = thing.def.defName;
                        m["label"] = thing.def.LabelCap.ToString();
                        m["x"] = thing.Position.x;
                        m["z"] = thing.Position.z;
                        m["growth"] = (plant.Growth * 100f).ToString("F0") + "%";
                        matches.Add(m);
                    }
                }
            }
            else
            {
                return ToolExecutor.JsonError("Unknown type '" + type + "'. Options: colonists, hostiles, animals, items, buildings, minerals, plants");
            }

            result["type"] = type;
            if (filter != null) result["filter"] = filter;
            result["total"] = totalFound;
            result["returned"] = matches.Count;
            if (totalFound > matches.Count)
                result["note"] = "Results capped at " + MaxResults + ". Use bounds (x1,z1,x2,z2) or a more specific filter to narrow results.";
            result["matches"] = matches;
            return result.ToString();
        }

        // ===== Environmental Visibility Tools =====

        public static string GetLightLevels(JSONNode args)
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

                float glow = map.glowGrid.GroundGlowAt(cell, false);
                var obj = new JSONObject();
                obj["cell"] = x + "," + z;
                obj["glowLevel"] = glow.ToString("F2");
                obj["description"] = GetLightDescription(glow);
                obj["isDark"] = glow < 0.3f;
                return obj.ToString();
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
                return ToolExecutor.JsonError("Range too large (" + rangeW + "x" + rangeH + " = " + (rangeW * rangeH) + " cells). Max 225 cells (15x15).");

            var result = new JSONObject();
            result["range"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["size"] = rangeW + "x" + rangeH;

            var cells = new JSONArray();
            float totalGlow = 0;
            float minGlow = 1f;
            float maxGlow = 0f;
            int darkCells = 0;

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new IntVec3(cx, 0, cz);
                    if (!cell.InBounds(map)) continue;

                    float glow = map.glowGrid.GroundGlowAt(cell, false);
                    totalGlow += glow;
                    if (glow < minGlow) minGlow = glow;
                    if (glow > maxGlow) maxGlow = glow;
                    if (glow < 0.3f) darkCells++;

                    var cellObj = new JSONObject();
                    cellObj["x"] = cx;
                    cellObj["z"] = cz;
                    cellObj["glowLevel"] = glow.ToString("F2");
                    cellObj["description"] = GetLightDescription(glow);
                    cells.Add(cellObj);
                }
            }

            int totalCells = rangeW * rangeH;
            result["cells"] = cells;

            var stats = new JSONObject();
            stats["averageGlow"] = (totalGlow / totalCells).ToString("F2");
            stats["minGlow"] = minGlow.ToString("F2");
            stats["maxGlow"] = maxGlow.ToString("F2");
            stats["darkCells"] = darkCells;
            stats["darkPercentage"] = ((darkCells * 100f) / totalCells).ToString("F1") + "%";
            result["stats"] = stats;

            return result.ToString();
        }

        public static string GetLightSources(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool hasBounds = args?["x1"] != null && args?["z1"] != null && args?["x2"] != null && args?["z2"] != null;
            int bx1 = hasBounds ? args["x1"].AsInt : 0;
            int bz1 = hasBounds ? args["z1"].AsInt : 0;
            int bx2 = hasBounds ? args["x2"].AsInt : map.Size.x - 1;
            int bz2 = hasBounds ? args["z2"].AsInt : map.Size.z - 1;

            string filterRaw = args?["filter"]?.Value;
            string filter = (string.IsNullOrEmpty(filterRaw) || filterRaw == "null") ? null : filterRaw.ToLowerInvariant();

            var result = new JSONObject();
            var lightSources = new JSONArray();
            int totalCount = 0;

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (!building.Spawned) continue;

                var glower = building.GetComp<CompGlower>();
                if (glower == null) continue;

                var pos = building.Position;
                if (hasBounds && (pos.x < bx1 || pos.x > bx2 || pos.z < bz1 || pos.z > bz2))
                    continue;

                if (filter != null && !building.def.defName.ToLowerInvariant().Contains(filter)
                    && !building.LabelCap.ToString().ToLowerInvariant().Contains(filter))
                    continue;

                totalCount++;

                var obj = new JSONObject();
                obj["defName"] = building.def.defName;
                obj["label"] = building.LabelCap.ToString();
                obj["x"] = pos.x;
                obj["z"] = pos.z;
                obj["glowRadius"] = glower.Props.glowRadius.ToString("F1");
                
                if (glower.Props.glowColor != null)
                    obj["glowColor"] = "(" + glower.Props.glowColor.r.ToString("F2") + ", " + 
                                       glower.Props.glowColor.g.ToString("F2") + ", " + 
                                       glower.Props.glowColor.b.ToString("F2") + ")";

                // Check if powered
                var powerComp = building.GetComp<CompPowerTrader>();
                if (powerComp != null)
                    obj["powered"] = powerComp.PowerOn;

                lightSources.Add(obj);
            }

            result["count"] = totalCount;
            result["lightSources"] = lightSources;
            
            if (hasBounds)
                result["bounds"] = bx1 + "," + bz1 + " to " + bx2 + "," + bz2;
            if (filter != null)
                result["filter"] = filter;

            return result.ToString();
        }

        public static string GetCellBeauty(JSONNode args)
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

                float beauty = BeautyUtility.AverageBeautyPerceptible(cell, map);
                var obj = new JSONObject();
                obj["cell"] = x + "," + z;
                obj["beauty"] = beauty.ToString("F1");
                obj["description"] = GetBeautyDescription(beauty);
                obj["beautyCategory"] = GetBeautyCategory(beauty);
                return obj.ToString();
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
                return ToolExecutor.JsonError("Range too large (" + rangeW + "x" + rangeH + " = " + (rangeW * rangeH) + " cells). Max 225 cells (15x15).");

            var result = new JSONObject();
            result["range"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["size"] = rangeW + "x" + rangeH;

            var cells = new JSONArray();
            float totalBeauty = 0;
            float minBeauty = float.MaxValue;
            float maxBeauty = float.MinValue;
            int uglyCells = 0;

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new IntVec3(cx, 0, cz);
                    if (!cell.InBounds(map)) continue;

                    float beauty = BeautyUtility.AverageBeautyPerceptible(cell, map);
                    totalBeauty += beauty;
                    if (beauty < minBeauty) minBeauty = beauty;
                    if (beauty > maxBeauty) maxBeauty = beauty;
                    if (beauty < -5f) uglyCells++;

                    var cellObj = new JSONObject();
                    cellObj["x"] = cx;
                    cellObj["z"] = cz;
                    cellObj["beauty"] = beauty.ToString("F1");
                    cellObj["description"] = GetBeautyDescription(beauty);
                    cells.Add(cellObj);
                }
            }

            int totalCells = rangeW * rangeH;
            result["cells"] = cells;

            var stats = new JSONObject();
            stats["averageBeauty"] = (totalBeauty / totalCells).ToString("F1");
            stats["minBeauty"] = minBeauty.ToString("F1");
            stats["maxBeauty"] = maxBeauty.ToString("F1");
            stats["uglyCells"] = uglyCells;
            stats["uglyPercentage"] = ((uglyCells * 100f) / totalCells).ToString("F1") + "%";
            result["stats"] = stats;

            return result.ToString();
        }

        public static string GetPollution(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (!ModsConfig.BiotechActive)
                return ToolExecutor.JsonError("Biotech DLC not active - pollution system not available.");

            if (args == null || string.IsNullOrEmpty(args["x"]?.Value) || string.IsNullOrEmpty(args["z"]?.Value))
                return ToolExecutor.JsonError("x and z coordinates are required.");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;

            string x2Val = args["x2"]?.Value;
            string z2Val = args["z2"]?.Value;
            bool hasRange = !string.IsNullOrEmpty(x2Val) && !string.IsNullOrEmpty(z2Val);

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map))
                return ToolExecutor.JsonError("Coordinates out of bounds. Map size: " + map.Size.x + "x" + map.Size.z);

            if (!hasRange)
            {
                bool isPolluted = map.pollutionGrid.IsPolluted(cell);
                
                // Calculate nearby pollution manually (RimWorld 1.6 API change)
                int pollutedCount = 0;
                int totalCount = 0;
                foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, 10f, true))
                {
                    if (!c.InBounds(map)) continue;
                    totalCount++;
                    if (map.pollutionGrid.IsPolluted(c)) pollutedCount++;
                }
                float nearbyPollution = totalCount > 0 ? (pollutedCount * 100f / totalCount) : 0f;

                var obj = new JSONObject();
                obj["biotechActive"] = true;
                obj["cell"] = x + "," + z;
                obj["isPolluted"] = isPolluted;
                obj["nearbyPollutionRadius10"] = nearbyPollution.ToString("F1") + "%";
                return obj.ToString();
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
                return ToolExecutor.JsonError("Range too large (" + rangeW + "x" + rangeH + " = " + (rangeW * rangeH) + " cells). Max 225 cells (15x15).");

            var result = new JSONObject();
            result["biotechActive"] = true;
            result["range"] = minX + "," + minZ + " to " + maxX + "," + maxZ;
            result["size"] = rangeW + "x" + rangeH;

            var cells = new JSONArray();
            int pollutedCells = 0;

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var c = new IntVec3(cx, 0, cz);
                    if (!c.InBounds(map)) continue;

                    bool isPolluted = map.pollutionGrid.IsPolluted(c);
                    if (isPolluted) pollutedCells++;

                    var cellObj = new JSONObject();
                    cellObj["x"] = cx;
                    cellObj["z"] = cz;
                    cellObj["isPolluted"] = isPolluted;
                    cells.Add(cellObj);
                }
            }

            int totalCells = rangeW * rangeH;
            result["cells"] = cells;
            result["pollutedCells"] = pollutedCells;
            result["pollutedPercentage"] = ((pollutedCells * 100f) / totalCells).ToString("F1") + "%";

            return result.ToString();
        }

        public static string GetRoofStatus(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null || string.IsNullOrEmpty(args["x1"]?.Value) || string.IsNullOrEmpty(args["z1"]?.Value)
                || string.IsNullOrEmpty(args["x2"]?.Value) || string.IsNullOrEmpty(args["z2"]?.Value))
                return ToolExecutor.JsonError("x1, z1, x2, z2 coordinates are required for region bounds.");

            int x1 = args["x1"].AsInt;
            int z1 = args["z1"].AsInt;
            int x2 = args["x2"].AsInt;
            int z2 = args["z2"].AsInt;

            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2);
            int maxZ = Math.Max(z1, z2);

            string roofTypeFilter = args?["roofType"]?.Value?.ToLowerInvariant();
            bool detectBreaches = args?["detectBreaches"]?.AsBool ?? false;

            var result = new JSONObject();
            var region = new JSONObject();
            region["x1"] = minX;
            region["z1"] = minZ;
            region["x2"] = maxX;
            region["z2"] = maxZ;
            result["region"] = region;

            int totalCells = 0;
            int roofed = 0;
            int unroofed = 0;
            int constructed = 0;
            int naturalThin = 0;
            int naturalThick = 0;
            var breaches = new JSONArray();

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;

                    totalCells++;
                    var roof = map.roofGrid.RoofAt(cell);

                    if (roof == null)
                    {
                        unroofed++;

                        // Breach detection: check if this unroofed cell has thick roof neighbors
                        if (detectBreaches)
                        {
                            bool hasThickRoofNeighbor = false;
                            foreach (var neighbor in GenAdj.CellsAdjacent8Way(cell, Rot4.North, IntVec2.One))
                            {
                                if (!neighbor.InBounds(map)) continue;
                                var neighborRoof = map.roofGrid.RoofAt(neighbor);
                                if (neighborRoof != null && neighborRoof.isThickRoof)
                                {
                                    hasThickRoofNeighbor = true;
                                    break;
                                }
                            }

                            if (hasThickRoofNeighbor)
                            {
                                var breach = new JSONObject();
                                breach["x"] = x;
                                breach["z"] = z;
                                breach["reason"] = "Unroofed cell under thick mountain roof (potential breach)";
                                breaches.Add(breach);
                            }
                        }
                    }
                    else
                    {
                        roofed++;

                        if (roof.defName == "RoofConstructed")
                            constructed++;
                        else if (roof.isThickRoof)
                            naturalThick++;
                        else
                            naturalThin++;
                    }
                }
            }

            result["totalCells"] = totalCells;
            result["roofed"] = roofed;
            result["unroofed"] = unroofed;

            var breakdown = new JSONObject();
            breakdown["constructed"] = constructed;
            breakdown["naturalThin"] = naturalThin;
            breakdown["naturalThick"] = naturalThick;
            breakdown["none"] = unroofed;
            result["breakdown"] = breakdown;

            if (detectBreaches && breaches.Count > 0)
                result["breaches"] = breaches;

            return result.ToString();
        }

        // Helper methods for environmental tools
        private static string GetLightDescription(float glow)
        {
            if (glow < 0.15f) return "Pitch black";
            if (glow < 0.3f) return "Very dark";
            if (glow < 0.5f) return "Dark";
            if (glow < 0.7f) return "Dim";
            if (glow < 0.85f) return "Well lit";
            return "Bright";
        }

        private static string GetBeautyDescription(float beauty)
        {
            if (beauty > 50f) return "Very beautiful";
            if (beauty > 15f) return "Beautiful";
            if (beauty > 5f) return "Pretty";
            if (beauty > -5f) return "Neutral";
            if (beauty > -20f) return "Ugly";
            if (beauty > -40f) return "Very ugly";
            return "Hideous";
        }

        private static string GetBeautyCategory(float beauty)
        {
            if (beauty > 50f) return "VeryBeautiful";
            if (beauty > 15f) return "Beautiful";
            if (beauty > 5f) return "Pretty";
            if (beauty > -5f) return "Neutral";
            if (beauty > -20f) return "Ugly";
            if (beauty > -40f) return "VeryUgly";
            return "Hideous";
        }
    }
}
