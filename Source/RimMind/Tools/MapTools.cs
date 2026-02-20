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

        }
    }
