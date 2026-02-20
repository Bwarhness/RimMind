using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class EventTools
    {
        public static string GetActiveEvents()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            try
            {
                // Check for active game conditions
                var gameConditions = map.gameConditionManager?.ActiveConditions;
                if (gameConditions != null)
                {
                    foreach (var condition in gameConditions)
                    {
                        var obj = new JSONObject();
                        
                        // Basic info
                        obj["type"] = condition.def.defName;
                        obj["label"] = condition.LabelCap;
                        
                        // Duration tracking
                        int ticksRemaining = condition.TicksLeft;
                        if (ticksRemaining > 0)
                        {
                            float daysRemaining = ticksRemaining / 60000f;
                            obj["duration_remaining_days"] = daysRemaining.ToString("F1");
                            obj["duration_remaining_hours"] = (daysRemaining * 24f).ToString("F1");
                        }
                        else
                        {
                            obj["duration_remaining_days"] = "unknown";
                        }
                        
                        int ticksSoFar = Find.TickManager.TicksGame - condition.startTick;
                        obj["active_for_days"] = (ticksSoFar / 60000f).ToString("F1");
                        
                        // Event-specific intelligence
                        string eventType = condition.def.defName;
                        
                        if (eventType == "ColdSnap")
                        {
                            obj["severity"] = "severe";
                            float tempDrop = condition.def.temperatureOffset;
                            obj["temperature_impact"] = tempDrop.ToString("F1") + "°C";
                            
                            float currentTemp = map.mapTemperature.OutdoorTemp;
                            float projectedLow = currentTemp + tempDrop;
                            obj["current_outdoor_temp"] = currentTemp.ToString("F1") + "°C";
                            obj["projected_low"] = projectedLow.ToString("F1") + "°C";
                            
                            var risks = new JSONArray();
                            if (projectedLow < -10f)
                                risks.Add("Colonists below comfortable range");
                            if (projectedLow < 0f)
                                risks.Add("Crops may die");
                            if (projectedLow < -20f)
                                risks.Add("Hypothermia risk if outdoors");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Ensure all rooms have heaters");
                            if (projectedLow < 0f)
                                recommendations.Add("Harvest temperature-sensitive crops immediately");
                            recommendations.Add("Restrict outdoor work during coldest hours");
                            obj["recommendations"] = recommendations;
                        }
                        else if (eventType == "HeatWave")
                        {
                            obj["severity"] = "severe";
                            float tempRise = condition.def.temperatureOffset;
                            obj["temperature_impact"] = "+" + tempRise.ToString("F1") + "°C";
                            
                            float currentTemp = map.mapTemperature.OutdoorTemp;
                            float projectedHigh = currentTemp + tempRise;
                            obj["current_outdoor_temp"] = currentTemp.ToString("F1") + "°C";
                            obj["projected_high"] = projectedHigh.ToString("F1") + "°C";
                            
                            var risks = new JSONArray();
                            if (projectedHigh > 35f)
                                risks.Add("Colonists above comfortable range");
                            if (projectedHigh > 45f)
                                risks.Add("Heatstroke risk");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Ensure all rooms have coolers");
                            recommendations.Add("Avoid outdoor work during peak heat");
                            obj["recommendations"] = recommendations;
                        }
                        else if (eventType == "ToxicFallout")
                        {
                            obj["severity"] = "critical";
                            
                            // Toxic buildup rate (vanilla is ~0.08/day)
                            obj["toxicity_rate"] = "0.08/day";
                            obj["safe_zones"] = "roofed areas only";
                            
                            var risks = new JSONArray();
                            risks.Add("Toxic buildup causes health damage");
                            risks.Add("Outdoor animals will get sick");
                            risks.Add("Unroofed colonists exposed");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Keep all colonists indoors");
                            recommendations.Add("Move animals to roofed pens");
                            recommendations.Add("Use sun lamps for indoor farming");
                            recommendations.Add("Stock up on medicine for toxic buildup");
                            obj["recommendations"] = recommendations;
                        }
                        else if (eventType == "VolcanicWinter" || eventType == "NuclearFalloutWeather")
                        {
                            obj["severity"] = "critical";
                            
                            var risks = new JSONArray();
                            risks.Add("All outdoor crops will die");
                            risks.Add("Severe temperature drop");
                            risks.Add("Reduced sunlight affects solar panels");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Switch to hydroponics immediately");
                            recommendations.Add("Stockpile food reserves");
                            recommendations.Add("Prepare alternative power sources");
                            obj["recommendations"] = recommendations;
                        }
                        else if (eventType == "SolarFlare")
                        {
                            obj["severity"] = "high";
                            obj["power_impact"] = "all electronics disabled";
                            
                            var risks = new JSONArray();
                            risks.Add("All electrical devices offline");
                            risks.Add("Turrets non-functional");
                            risks.Add("Temperature control disabled");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Battery reserves won't help");
                            recommendations.Add("Monitor temperature in critical rooms");
                            recommendations.Add("Prepare for manual defense");
                            obj["recommendations"] = recommendations;
                        }
                        else if (eventType == "Eclipse")
                        {
                            obj["severity"] = "medium";
                            obj["solar_impact"] = "solar panels offline";
                            
                            var risks = new JSONArray();
                            risks.Add("Solar panels produce no power");
                            risks.Add("May drain battery reserves");
                            obj["risks"] = risks;
                            
                            var recommendations = new JSONArray();
                            recommendations.Add("Monitor battery levels");
                            recommendations.Add("Reduce non-essential power consumption");
                            obj["recommendations"] = recommendations;
                        }
                        else
                        {
                            // Generic event
                            obj["severity"] = "unknown";
                            obj["description"] = condition.Description;
                        }
                        
                        arr.Add(obj);
                    }
                }
                
                // Note: Most incidents (raids, infestations) are one-shot events
                // Infestations persist as Hive things on the map, raids as hostile pawns
                // These are already visible via get_hostile_threats tool
            }
            catch (Exception ex)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Could not read active events: " + ex.Message;
                arr.Add(errObj);
            }

            var result = new JSONObject();
            result["active_events"] = arr;
            result["count"] = arr.Count;
            
            if (arr.Count == 0)
            {
                result["status"] = "No active weather or disaster events";
            }
            
            return result.ToString();
        }

        public static string GetDisasterRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            
            try
            {
                // 1. Infestation Risk
                var infestationObj = new JSONObject();
                
                // Count overhead mountain tiles
                int overheadMountainCount = 0;
                int totalRoofedCount = 0;
                var infestationSpots = new List<IntVec3>();
                
                foreach (var cell in map.AllCells)
                {
                    if (cell.Roofed(map))
                    {
                        totalRoofedCount++;
                        
                        // Check for overhead mountain
                        RoofDef roof = cell.GetRoof(map);
                        if (roof != null && roof.isThickRoof)
                        {
                            overheadMountainCount++;
                            
                            // Check if this could be an infestation spawn point
                            // (near player structures, inside base)
                            if (cell.GetFirstBuilding(map) != null || cell.GetZone(map) != null)
                            {
                                if (infestationSpots.Count < 5) // Limit to top 5
                                    infestationSpots.Add(cell);
                            }
                        }
                    }
                }
                
                float infestationPercent = totalRoofedCount > 0 
                    ? (overheadMountainCount / (float)totalRoofedCount) * 100f 
                    : 0f;
                
                infestationObj["overhead_mountain_tiles"] = overheadMountainCount;
                infestationObj["total_roofed_tiles"] = totalRoofedCount;
                infestationObj["overhead_mountain_percent"] = infestationPercent.ToString("F1") + "%";
                
                // Risk assessment
                string infestRisk;
                if (overheadMountainCount == 0)
                    infestRisk = "none";
                else if (infestationPercent > 50f)
                    infestRisk = "high";
                else if (infestationPercent > 25f)
                    infestRisk = "medium";
                else
                    infestRisk = "low";
                    
                infestationObj["probability"] = infestRisk;
                
                if (overheadMountainCount > 0)
                {
                    infestationObj["reason"] = infestationPercent.ToString("F0") + "% of roofed base area is overhead mountain";
                    
                    var spawnLocations = new JSONArray();
                    foreach (var spot in infestationSpots)
                    {
                        spawnLocations.Add("(" + spot.x + "," + spot.z + ")");
                    }
                    if (spawnLocations.Count > 0)
                        infestationObj["high_risk_spawn_locations"] = spawnLocations;
                    
                    infestationObj["mitigation"] = "Remove overhead mountain with roof removal. Build in open areas. Use killboxes away from mountains.";
                }
                else
                {
                    infestationObj["reason"] = "No overhead mountain in base";
                    infestationObj["mitigation"] = "None needed - continue building in open areas";
                }
                
                result["infestation_risk"] = infestationObj;
                
                // 2. Zzzt Risk (battery explosions)
                var zzztObj = new JSONObject();
                
                float totalStoredPower = 0f;
                int batteryCount = 0;
                var batteries = map.listerBuildings.AllBuildingsColonistOfClass<Building_Battery>();
                
                foreach (var battery in batteries)
                {
                    var powerComp = battery.GetComp<CompPowerBattery>();
                    if (powerComp != null)
                    {
                        totalStoredPower += powerComp.StoredEnergy;
                        batteryCount++;
                    }
                }
                
                zzztObj["battery_count"] = batteryCount;
                zzztObj["stored_power_wd"] = totalStoredPower.ToString("F0") + " Wd";
                
                // Risk assessment (vanilla uses stored power as factor)
                string zzztRisk;
                if (totalStoredPower == 0)
                    zzztRisk = "none";
                else if (totalStoredPower > 15000f)
                    zzztRisk = "high";
                else if (totalStoredPower > 8000f)
                    zzztRisk = "medium";
                else
                    zzztRisk = "low";
                    
                zzztObj["probability"] = zzztRisk;
                
                if (totalStoredPower > 0)
                {
                    // Estimate damage
                    string damageEstimate;
                    if (totalStoredPower > 15000f)
                        damageEstimate = "4-6 battery explosions, severe fire risk";
                    else if (totalStoredPower > 8000f)
                        damageEstimate = "2-3 battery explosions, moderate fire risk";
                    else
                        damageEstimate = "1-2 battery explosions, minor fire risk";
                        
                    zzztObj["expected_damage"] = damageEstimate;
                    zzztObj["mitigation"] = "Use circuit breakers (mod). Separate power grids. Keep batteries away from flammables. Use stone walls around battery rooms.";
                }
                else
                {
                    zzztObj["mitigation"] = "None needed - no batteries";
                }
                
                result["zzzt_risk"] = zzztObj;
                
                // 3. Fire Risk Assessment
                var fireObj = new JSONObject();
                int flammableWalls = 0;
                int totalWalls = 0;
                
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building.def.building != null && building.def.building.isNaturalRock == false)
                    {
                        // Check if it's a wall
                        if (building.def.graphicData != null && building.def.passability == Traversability.Impassable)
                        {
                            totalWalls++;
                            if (building.def.stuffCategories != null && building.Stuff != null)
                            {
                                if (building.Stuff.GetStatValueAbstract(StatDefOf.Flammability) > 0.5f)
                                    flammableWalls++;
                            }
                        }
                    }
                }
                
                float flammablePercent = totalWalls > 0 ? (flammableWalls / (float)totalWalls) * 100f : 0f;
                fireObj["flammable_walls"] = flammableWalls;
                fireObj["total_walls"] = totalWalls;
                fireObj["flammable_percent"] = flammablePercent.ToString("F1") + "%";
                
                string fireRisk;
                if (flammablePercent > 50f)
                    fireRisk = "high";
                else if (flammablePercent > 25f)
                    fireRisk = "medium";
                else
                    fireRisk = "low";
                    
                fireObj["risk"] = fireRisk;
                
                if (flammablePercent > 25f)
                {
                    fireObj["mitigation"] = "Replace wooden structures with stone. Build firebreaks. Keep firefoam poppers in key areas.";
                }
                else
                {
                    fireObj["mitigation"] = "Fire risk is low - continue using stone construction";
                }
                
                result["fire_risk"] = fireObj;
                
            }
            catch (Exception ex)
            {
                result["error"] = "Could not assess disaster risks: " + ex.Message;
            }
            
            return result.ToString();
        }

        public static string GetRecentEvents(int count)
        {
            if (count <= 0) count = 5;
            if (count > 20) count = 20;

            var arr = new JSONArray();

            var archive = Find.Archive;
            if (archive != null)
            {
                var entries = archive.ArchivablesListForReading
                    .OrderByDescending(a => a.CreatedTicksGame)
                    .Take(count);

                foreach (var entry in entries)
                {
                    var obj = new JSONObject();

                    if (entry is Letter letter)
                    {
                        obj["type"] = letter.def?.LabelCap.ToString() ?? "Letter";
                        obj["title"] = letter.Label.ToString() ?? "Unknown";
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }
                    else if (entry is ArchivedDialog dialog)
                    {
                        obj["type"] = "Message";
                        obj["title"] = entry.GetType().GetProperty("text")?.GetValue(entry)?.ToString() ?? "Message";
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }
                    else
                    {
                        obj["type"] = entry.GetType().Name;
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }

                    arr.Add(obj);
                }
            }

            var result = new JSONObject();
            result["recentEvents"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetActiveAlerts()
        {
            var arr = new JSONArray();

            try
            {
                var alertsReadout = ((UIRoot_Play)Find.UIRoot).alerts;
                if (alertsReadout != null)
                {
                    // AllAlerts may be private - try reflection
                    var allAlertsField = typeof(AlertsReadout).GetField("AllAlerts", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? typeof(AlertsReadout).GetField("allAlerts", BindingFlags.NonPublic | BindingFlags.Instance);

                    System.Collections.Generic.List<Alert> allAlerts = null;
                    if (allAlertsField != null)
                        allAlerts = allAlertsField.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;

                    // Fallback: try public property
                    if (allAlerts == null)
                    {
                        var prop = typeof(AlertsReadout).GetProperty("AllAlerts", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                            allAlerts = prop.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;
                    }

                    if (allAlerts != null)
                    {
                        // Sort by priority (Critical first)
                        var sortedAlerts = allAlerts
                            .Where(a => a.Active)
                            .OrderByDescending(a => a.Priority);

                        foreach (var alert in sortedAlerts)
                        {
                            var obj = new JSONObject();
                            obj["label"] = alert.GetLabel().ToString();
                            obj["priority"] = alert.Priority.ToString(); // Critical, High, Medium, Low
                            
                            // Severity number for easy filtering (4=Critical, 3=High, 2=Medium, 1=Low)
                            obj["severity"] = (int)alert.Priority;

                            TaggedString explanation = alert.GetExplanation();
                            string explanationStr = explanation.ToString();
                            if (!string.IsNullOrEmpty(explanationStr) && explanationStr.Length > 300)
                                explanationStr = explanationStr.Substring(0, 300) + "...";
                            obj["explanation"] = explanationStr;

                            // Try to extract colonist names from alert text
                            var colonistNames = ExtractColonistNames(alert.GetLabel().ToString(), explanationStr);
                            if (colonistNames.Count > 0)
                            {
                                var namesArray = new JSONArray();
                                foreach (var name in colonistNames)
                                    namesArray.Add(name);
                                obj["affected_colonists"] = namesArray;
                            }

                            // Add alert-specific metadata
                            obj["alert_type"] = alert.GetType().Name;

                            // Add countdown timers for specific alert types
                            var timerInfo = GetAlertTimerInfo(alert, explanationStr);
                            if (!string.IsNullOrEmpty(timerInfo))
                            {
                                obj["countdown"] = timerInfo;
                            }

                            arr.Add(obj);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Could not read alerts: " + ex.Message;
                arr.Add(errObj);
            }

            var result = new JSONObject();
            result["activeAlerts"] = arr;
            result["count"] = arr.Count;
            
            // Add summary counts by severity
            var summary = new JSONObject();
            summary["critical"] = arr.Children.Count(a => a.AsObject["priority"]?.Value == "Critical");
            summary["high"] = arr.Children.Count(a => a.AsObject["priority"]?.Value == "High");
            summary["medium"] = arr.Children.Count(a => a.AsObject["priority"]?.Value == "Medium");
            summary["low"] = arr.Children.Count(a => a.AsObject["priority"]?.Value == "Low");
            result["summary"] = summary;

            return result.ToString();
        }

        private static System.Collections.Generic.List<string> ExtractColonistNames(string label, string explanation)
        {
            var names = new System.Collections.Generic.List<string>();
            var map = Find.CurrentMap;
            if (map == null) return names;

            var colonists = map.mapPawns.FreeColonists;
            foreach (var pawn in colonists)
            {
                string shortName = pawn.Name?.ToStringShort;
                if (!string.IsNullOrEmpty(shortName))
                {
                    if (label.Contains(shortName) || explanation.Contains(shortName))
                    {
                        if (!names.Contains(shortName))
                            names.Add(shortName);
                    }
                }
            }

            return names;
        }

        private static string GetAlertTimerInfo(Alert alert, string explanation)
        {
            var map = Find.CurrentMap;
            if (map == null) return null;

            string alertType = alert.GetType().Name;
            string label = alert.GetLabel().ToString();

            // Check for rescueable pawns (death timer)
            if (label.Contains("needs rescue"))
            {
                // Find pawns needing rescue
                var rescuees = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Downed && p.InBed())
                    .ToList();

                if (rescuees.Any())
                {
                    // Get the most critical one (lowest health)
                    var worst = rescuees.OrderBy(p => p.health?.summaryHealth?.SummaryHealthPercent ?? 1f).FirstOrDefault();
                    if (worst != null)
                    {
                        // Estimate time until death from bleeding
                        float bleedRate = worst.health?.hediffSet?.BleedRate ?? 0;
                        if (bleedRate > 0)
                        {
                            float blood = worst.health?.hediffSet?.BloodLoss ?? 0;
                            float hoursLeft = (1f - blood) / (bleedRate * 24f); // Convert to hours
                            return $"~{hoursLeft.ToString("F1")} hours until death from bleeding";
                        }
                        return "Critical - needs immediate medical attention";
                    }
                }
            }

            // Check for starving colonists
            if (label.Contains("starving") || label.Contains("needs food"))
            {
                var starving = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.needs?.food?.CurLevelPercentage < 0.1f)
                    .FirstOrDefault();

                if (starving != null)
                {
                    return "Critical - will die from starvation soon";
                }
            }

            // Check for draft animals that need tending
            if (label.Contains("injured animal") || label.Contains("wounded animal"))
            {
                var injuredAnimals = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Animal && p.Downed)
                    .ToList();

                if (injuredAnimals.Any())
                {
                    return $"{injuredAnimals.Count} animal(s) need rescue/tending";
                }
            }

            // Check for colonists with infections
            if (label.Contains("infection") || explanation?.Contains("infection") == true)
            {
                return "Monitor immunity - seek medical treatment";
            }

            // Check for idle colonist
            if (label.Contains("idle"))
            {
                return "Assign a job to this colonist";
            }

            // Check for prisoners needing attention
            if (label.Contains("prisoner"))
            {
                if (label.Contains("recruitment"))
                    return "Colonist ready for recruitment check";
                if (label.Contains("execution"))
                    return "Warning - execution pending";
            }

            return null;
        }
    }
}
