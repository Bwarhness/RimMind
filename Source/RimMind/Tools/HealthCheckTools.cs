using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class HealthCheckTools
    {
        public static string ColonyHealthCheck()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            result["timestamp"] = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile));
            
            var systems = new JSONArray();
            var criticalAlerts = new JSONArray();
            var topRecommendations = new List<string>();

            // Track overall status (healthy/stable/warning/critical)
            string worstStatus = "healthy";

            // 1. FOOD SECURITY
            var foodSystem = CheckFoodSecurity(map, criticalAlerts, topRecommendations);
            systems.Add(foodSystem);
            worstStatus = GetWorstStatus(worstStatus, foodSystem["status"].Value);

            // 2. POWER GRID
            var powerSystem = CheckPowerGrid(map, criticalAlerts, topRecommendations);
            systems.Add(powerSystem);
            worstStatus = GetWorstStatus(worstStatus, powerSystem["status"].Value);

            // 3. DEFENSE READINESS
            var defenseSystem = CheckDefenseReadiness(map, criticalAlerts, topRecommendations);
            systems.Add(defenseSystem);
            worstStatus = GetWorstStatus(worstStatus, defenseSystem["status"].Value);

            // 4. COLONIST WELLBEING
            var wellbeingSystem = CheckColonistWellbeing(map, criticalAlerts, topRecommendations);
            systems.Add(wellbeingSystem);
            worstStatus = GetWorstStatus(worstStatus, wellbeingSystem["status"].Value);

            // 5. RESOURCE BOTTLENECKS
            var resourcesSystem = CheckResourceBottlenecks(map, criticalAlerts, topRecommendations);
            systems.Add(resourcesSystem);
            worstStatus = GetWorstStatus(worstStatus, resourcesSystem["status"].Value);

            // 6. RESEARCH PROGRESS
            var researchSystem = CheckResearchProgress(criticalAlerts, topRecommendations);
            systems.Add(researchSystem);
            worstStatus = GetWorstStatus(worstStatus, researchSystem["status"].Value);

            // 7. HOUSING QUALITY
            var housingSystem = CheckHousingQuality(map, criticalAlerts, topRecommendations);
            systems.Add(housingSystem);
            worstStatus = GetWorstStatus(worstStatus, housingSystem["status"].Value);

            // 8. PRODUCTION ISSUES
            var productionSystem = CheckProductionIssues(map, criticalAlerts, topRecommendations);
            systems.Add(productionSystem);
            worstStatus = GetWorstStatus(worstStatus, productionSystem["status"].Value);

            result["overall_status"] = worstStatus;
            result["summary"] = GenerateSummary(worstStatus, criticalAlerts.Count);
            result["systems"] = systems;
            result["critical_alerts"] = criticalAlerts;
            
            // Take top 5 recommendations
            var topRecsArray = new JSONArray();
            for (int i = 0; i < Math.Min(5, topRecommendations.Count); i++)
                topRecsArray.Add(topRecommendations[i]);
            result["top_recommendations"] = topRecsArray;

            return result.ToString();
        }

        private static JSONObject CheckFoodSecurity(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Food Security";
            var issues = new JSONArray();
            var recs = new JSONArray();

            // Calculate food availability
            float totalNutrition = map.resourceCounter.TotalHumanEdibleNutrition;
            int colonistCount = map.mapPawns.FreeColonistsCount;
            float daysOfFood = colonistCount > 0 ? totalNutrition / (colonistCount * 2f) : 999f; // ~2 nutrition per colonist per day

            // Growing zones
            int growingZones = map.zoneManager.AllZones.OfType<Zone_Growing>().Count();
            int activeGrowingZones = map.zoneManager.AllZones.OfType<Zone_Growing>()
                .Count(z => z.GetPlantDefToGrow() != null && map.mapTemperature.SeasonalTemp > 0);

            // Hunting capability (wild animals on map)
            int huntableAnimals = map.mapPawns.AllPawnsSpawned
                .Count(p => p.RaceProps.Animal && p.Faction == null && p.RaceProps.baseBodySize >= 0.5f);

            // Determine status
            string status = "healthy";
            if (daysOfFood < 3)
            {
                status = "critical";
                criticalAlerts.Add("Only " + daysOfFood.ToString("F1") + " days of food remaining!");
                recommendations.Insert(0, "URGENT: Hunt animals, harvest crops, or buy food immediately");
            }
            else if (daysOfFood < 7)
            {
                status = "warning";
                issues.Add("Low food reserves (" + daysOfFood.ToString("F1") + " days)");
                recs.Add("Increase food production or stockpiles");
            }

            if (activeGrowingZones == 0 && growingZones > 0)
            {
                issues.Add("Growing zones exist but none are active (wrong season or no plants assigned)");
                recs.Add("Check growing zone crop assignments");
            }

            if (growingZones == 0 && colonistCount > 3)
            {
                issues.Add("No growing zones established");
                recs.Add("Create growing zones to ensure long-term food security");
                if (status == "healthy") status = "stable";
            }

            string details = daysOfFood.ToString("F1") + " days of food, " + 
                            activeGrowingZones + "/" + growingZones + " growing zones active";
            if (huntableAnimals > 0)
                details += ", " + huntableAnimals + " huntable animals nearby";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckPowerGrid(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Power Grid";
            var issues = new JSONArray();
            var recs = new JSONArray();

            float totalGeneration = 0;
            float totalConsumption = 0;
            float totalStored = 0;
            float totalStorageCapacity = 0;
            int batteryCount = 0;

            var nets = map.powerNetManager.AllNetsListForReading;
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

            float surplus = totalGeneration - totalConsumption;
            float batteryPercent = totalStorageCapacity > 0 ? (totalStored / totalStorageCapacity * 100f) : 0;

            // Determine status
            string status = "healthy";
            
            if (surplus < 0)
            {
                status = "critical";
                criticalAlerts.Add("Power deficit: " + (-surplus).ToString("F0") + "W shortfall!");
                recommendations.Insert(0, "URGENT: Build more generators or disable non-essential power consumers");
            }
            else if (surplus < 500 && totalConsumption > 1000)
            {
                status = "warning";
                issues.Add("Low power margin (" + surplus.ToString("F0") + "W surplus)");
                recs.Add("Add backup power generation");
            }

            if (batteryCount == 0 && totalGeneration > 0)
            {
                issues.Add("No batteries - vulnerable to night/eclipse brownouts");
                recs.Add("Build batteries for power storage");
                if (status == "healthy") status = "stable";
            }
            else if (batteryPercent < 20 && batteryCount > 0)
            {
                issues.Add("Battery reserves low (" + batteryPercent.ToString("F0") + "%)");
                if (status == "healthy") status = "warning";
            }

            if (totalGeneration == 0 && totalConsumption > 0)
            {
                status = "critical";
                criticalAlerts.Add("No power generation!");
            }

            string details = totalGeneration.ToString("F0") + "W generation, " + 
                           totalConsumption.ToString("F0") + "W consumption, " +
                           surplus.ToString("F0") + "W surplus";
            if (batteryCount > 0)
                details += ", " + batteryCount + " batteries at " + batteryPercent.ToString("F0") + "%";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckDefenseReadiness(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Defense Readiness";
            var issues = new JSONArray();
            var recs = new JSONArray();

            // Turrets
            int turrets = map.listerBuildings.allBuildingsColonist.OfType<Building_TurretGun>().Count();

            // Traps
            int traps = map.listerBuildings.allBuildingsColonist
                .Count(b => b.def.building != null && b.def.building.isTrap);

            // Combat-capable colonists (armed or decent combat skills)
            int combatCapable = 0;
            int armed = 0;
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Downed || pawn.Dead) continue;
                
                bool hasWeapon = pawn.equipment?.Primary != null;
                if (hasWeapon) armed++;

                int shootSkill = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                
                if (hasWeapon || shootSkill >= 5 || meleeSkill >= 5)
                    combatCapable++;
            }

            // Weapon stockpile
            int weaponStock = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Count(t => t.Faction == Faction.OfPlayer || !t.Position.Fogged(map));

            // Determine status
            string status = "healthy";
            int colonistCount = map.mapPawns.FreeColonistsCount;

            if (combatCapable == 0 && colonistCount > 0)
            {
                status = "critical";
                criticalAlerts.Add("No combat-capable colonists!");
                recommendations.Insert(0, "URGENT: Arm colonists and assign weapon training");
            }
            else if (combatCapable < colonistCount / 2)
            {
                status = "warning";
                issues.Add("Less than half of colonists are combat-ready");
                recs.Add("Arm more colonists or train combat skills");
            }

            if (turrets == 0 && colonistCount > 3)
            {
                issues.Add("No defensive turrets");
                recs.Add("Build turrets for base defense");
                if (status == "healthy") status = "stable";
            }

            if (armed < colonistCount / 2)
            {
                issues.Add("Many colonists are unarmed");
                recs.Add("Craft or purchase weapons for colonists");
            }

            string details = combatCapable + "/" + colonistCount + " combat-capable colonists, " +
                           turrets + " turrets, " + traps + " traps, " +
                           weaponStock + " weapons in stockpile";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckColonistWellbeing(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Colonist Wellbeing";
            var issues = new JSONArray();
            var recs = new JSONArray();

            int totalColonists = map.mapPawns.FreeColonistsCount;
            int injured = 0;
            int diseased = 0;
            int moodRisk = 0;
            int starving = 0;
            int exhausted = 0;
            int mentalBreak = 0;

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                // Health issues
                if (pawn.health.HasHediffsNeedingTend())
                    injured++;

                var diseases = pawn.health.hediffSet.hediffs
                    .Where(h => h.def.makesSickThought || h.def.lethalSeverity > 0);
                if (diseases.Any())
                    diseased++;

                // Mood
                float moodLevel = pawn.needs?.mood?.CurLevelPercentage ?? 1f;
                if (moodLevel < 0.3f)
                    moodRisk++;

                // Needs
                float foodLevel = pawn.needs?.food?.CurLevelPercentage ?? 1f;
                if (foodLevel < 0.2f)
                    starving++;

                float restLevel = pawn.needs?.rest?.CurLevelPercentage ?? 1f;
                if (restLevel < 0.2f)
                    exhausted++;

                // Mental state
                if (pawn.InMentalState)
                    mentalBreak++;
            }

            // Determine status
            string status = "healthy";

            if (mentalBreak > 0)
            {
                status = "warning";
                issues.Add(mentalBreak + " colonist(s) in mental break");
                recs.Add("Address mood issues and give colonists breaks");
            }

            if (starving > 0)
            {
                status = "critical";
                criticalAlerts.Add(starving + " colonist(s) starving!");
                recommendations.Insert(0, "URGENT: Feed colonists immediately");
            }

            if (moodRisk > 0)
            {
                if (status == "healthy") status = "warning";
                issues.Add(moodRisk + " colonist(s) at risk of mental break (mood < 30%)");
                recs.Add("Improve mood through better food, recreation, or room quality");
            }

            if (diseased > 0)
            {
                issues.Add(diseased + " colonist(s) with disease");
                recs.Add("Ensure adequate medical care and medicine supply");
                if (status == "healthy") status = "stable";
            }

            if (injured > totalColonists / 3)
            {
                if (status == "healthy") status = "warning";
                issues.Add("Many colonists need medical treatment");
                recs.Add("Prioritize medical care");
            }

            if (exhausted > 0)
            {
                issues.Add(exhausted + " colonist(s) exhausted");
                recs.Add("Check work schedules - ensure colonists get adequate sleep");
            }

            string details = "Healthy: " + (totalColonists - injured - diseased) + "/" + totalColonists;
            if (injured > 0) details += ", " + injured + " injured";
            if (diseased > 0) details += ", " + diseased + " diseased";
            if (moodRisk > 0) details += ", " + moodRisk + " mood risk";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckResourceBottlenecks(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Resource Bottlenecks";
            var issues = new JSONArray();
            var recs = new JSONArray();

            // Check critical resources
            var shortages = new Dictionary<string, int>();
            var thresholds = new Dictionary<string, int>
            {
                { "Steel", 200 },
                { "WoodLog", 300 },
                { "ComponentIndustrial", 20 },
                { "MedicineIndustrial", 10 },
                { "Silver", 500 },
                { "Plasteel", 50 }
            };

            foreach (var item in thresholds)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(item.Key);
                if (def != null)
                {
                    int count = map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);
                    if (count < item.Value)
                        shortages[item.Key] = count;
                }
            }

            // Determine status
            string status = "healthy";
            
            if (shortages.ContainsKey("Steel") && shortages["Steel"] < 50)
            {
                status = "critical";
                criticalAlerts.Add("Critically low on steel (" + shortages["Steel"] + ")!");
                recommendations.Insert(0, "URGENT: Mine steel or trade for materials");
            }
            else if (shortages.ContainsKey("Steel"))
            {
                status = "warning";
                issues.Add("Low steel reserves (" + shortages["Steel"] + ")");
                recs.Add("Mine more steel deposits");
            }

            if (shortages.ContainsKey("MedicineIndustrial") && shortages["MedicineIndustrial"] < 5)
            {
                if (status == "healthy") status = "warning";
                issues.Add("Low medicine (" + shortages["MedicineIndustrial"] + ")");
                recs.Add("Craft or purchase medicine");
            }

            if (shortages.ContainsKey("ComponentIndustrial") && shortages["ComponentIndustrial"] < 10)
            {
                if (status == "healthy") status = "warning";
                issues.Add("Low components (" + shortages["ComponentIndustrial"] + ")");
                recs.Add("Deconstruct mechanoids or trade for components");
            }

            if (shortages.Count > 3)
            {
                if (status == "healthy") status = "stable";
                issues.Add("Multiple resource shortages detected");
                recs.Add("Diversify resource gathering and trading");
            }

            string details = shortages.Count > 0 
                ? "Shortages: " + string.Join(", ", shortages.Select(s => s.Key + "(" + s.Value + ")"))
                : "All critical resources adequately stocked";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckResearchProgress(JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Research Progress";
            var issues = new JSONArray();
            var recs = new JSONArray();

            var manager = Find.ResearchManager;
            var currentProject = manager.GetProject();

            string status = "healthy";
            string details = "";

            if (currentProject != null)
            {
                float progress = manager.GetProgress(currentProject) / currentProject.baseCost * 100f;
                details = "Researching: " + currentProject.LabelCap + " (" + progress.ToString("F0") + "%)";
                
                // Check if we have research benches
                var map = Find.CurrentMap;
                if (map != null)
                {
                    int benches = map.listerBuildings.allBuildingsColonist
                        .Count(b => b.def.defName.Contains("ResearchBench"));
                    
                    if (benches == 0)
                    {
                        status = "warning";
                        issues.Add("Research project active but no research benches found");
                        recs.Add("Build a research bench to enable research");
                    }
                }
            }
            else
            {
                status = "stable";
                issues.Add("No active research project");
                recs.Add("Select a research project to improve colony technology");
                details = "No active research";
            }

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckHousingQuality(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Housing Quality";
            var issues = new JSONArray();
            var recs = new JSONArray();

            int colonistCount = map.mapPawns.FreeColonistsCount;
            int beds = 0;
            int privateBeds = 0;
            int barracksStyleBeds = 0;
            int badBedroomCount = 0;

            // Track colonists with beds
            var colonistsWithBeds = new HashSet<Pawn>();

            // Check all beds
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_Bed bed && !bed.Medical)
                {
                    beds++;
                    
                    foreach (var owner in bed.OwnersForReading)
                        colonistsWithBeds.Add(owner);

                    var room = bed.GetRoom();
                    if (room != null && !room.OutdoorNow)
                    {
                        int bedsInRoom = room.ContainedBeds.Count();
                        if (bedsInRoom == 1)
                            privateBeds++;
                        else
                            barracksStyleBeds++;

                        float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
                        if (impressiveness < 20)
                            badBedroomCount++;
                    }
                }
            }

            int colonistsWithoutBeds = colonistCount - colonistsWithBeds.Count;

            // Determine status
            string status = "healthy";

            if (colonistsWithoutBeds > 0)
            {
                status = "warning";
                issues.Add(colonistsWithoutBeds + " colonist(s) without assigned beds");
                recs.Add("Build more beds and assign to colonists");
            }

            if (badBedroomCount > colonistCount / 2)
            {
                if (status == "healthy") status = "stable";
                issues.Add("Many bedrooms have low impressiveness (< 20)");
                recs.Add("Improve bedroom quality with better floors, decorations, or larger rooms");
            }

            if (barracksStyleBeds > privateBeds && colonistCount > 3)
            {
                if (status == "healthy") status = "stable";
                issues.Add("Most colonists in shared bedrooms (barracks)");
                recs.Add("Build private bedrooms to improve colonist mood");
            }

            string details = beds + " beds total, " + privateBeds + " private, " + 
                           barracksStyleBeds + " in barracks";
            if (colonistsWithoutBeds > 0)
                details += ", " + colonistsWithoutBeds + " colonists unassigned";

            system["status"] = status;
            system["details"] = details;
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static JSONObject CheckProductionIssues(Map map, JSONArray criticalAlerts, List<string> recommendations)
        {
            var system = new JSONObject();
            system["name"] = "Production Issues";
            var issues = new JSONArray();
            var recs = new JSONArray();

            int totalBills = 0;
            int suspendedBills = 0;
            int billsWithoutWorkers = 0;

            // Check all workbenches
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_WorkTable workTable && workTable.BillStack != null)
                {
                    foreach (var bill in workTable.BillStack.Bills)
                    {
                        totalBills++;

                        if (bill.suspended)
                            suspendedBills++;

                        // Check if anyone can do this work
                        if (bill.recipe?.workSkill != null)
                        {
                            var workType = bill.recipe.workSkill.workTags.ToString();
                            bool hasWorker = map.mapPawns.FreeColonists
                                .Any(p => !p.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.AllDefs
                                    .FirstOrDefault(wt => wt.relevantSkills.Contains(bill.recipe.workSkill))));
                            
                            if (!hasWorker)
                                billsWithoutWorkers++;
                        }
                    }
                }
            }

            // Determine status
            string status = "healthy";

            if (billsWithoutWorkers > 0)
            {
                status = "warning";
                issues.Add(billsWithoutWorkers + " bill(s) have no assigned workers");
                recs.Add("Enable work types for colonists or assign workers to production bills");
            }

            if (suspendedBills > totalBills / 2 && totalBills > 0)
            {
                if (status == "healthy") status = "stable";
                issues.Add("Many production bills are suspended");
                recs.Add("Review and resume important production bills");
            }

            if (totalBills == 0)
            {
                if (status == "healthy") status = "stable";
                details = "No active production bills";
            }
            else
            {
                string details = totalBills + " total bills";
                if (suspendedBills > 0)
                    details += ", " + suspendedBills + " suspended";
                if (billsWithoutWorkers > 0)
                    details += ", " + billsWithoutWorkers + " without workers";
                system["details"] = details;
            }

            system["status"] = status;
            system["details"] = totalBills > 0 
                ? totalBills + " active bills, " + suspendedBills + " suspended"
                : "No production bills configured";
            system["issues"] = issues;
            system["recommendations"] = recs;
            
            foreach (var rec in recs.AsArray) recommendations.Add(rec.Value);

            return system;
        }

        private static string GetWorstStatus(string current, string newStatus)
        {
            // critical > warning > stable > healthy
            if (current == "critical" || newStatus == "critical") return "critical";
            if (current == "warning" || newStatus == "warning") return "warning";
            if (current == "stable" || newStatus == "stable") return "stable";
            return "healthy";
        }

        private static string GenerateSummary(string status, int criticalCount)
        {
            switch (status)
            {
                case "critical":
                    return "Colony in CRITICAL condition - " + criticalCount + " urgent issue(s) require immediate attention!";
                case "warning":
                    return "Colony stable but facing challenges - address warnings before they become critical";
                case "stable":
                    return "Colony functioning adequately with room for improvement";
                case "healthy":
                default:
                    return "Colony in good health - all systems functioning well";
            }
        }
    }
}
