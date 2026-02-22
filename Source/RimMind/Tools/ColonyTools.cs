using System;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ColonyTools
    {
        public static string GetColonyOverview()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();
            obj["colonistCount"] = map.mapPawns.FreeColonistsCount;
            obj["totalWealth"] = map.wealthWatcher.WealthTotal.ToString("F0");
            obj["daysSurvived"] = GenDate.DaysPassed;
            obj["currentHour"] = GenLocalDate.HourInteger(map);

            var storyteller = Find.Storyteller;
            if (storyteller != null)
                obj["storyteller"] = storyteller.def.LabelCap.ToString();

            obj["difficulty"] = Find.Storyteller?.difficultyDef?.label ?? "Unknown";

            var biome = map.Biome;
            if (biome != null)
                obj["biome"] = biome.LabelCap.ToString();

            obj["mapSize"] = map.Size.x + "x" + map.Size.z;
            obj["prisonersCount"] = map.mapPawns.PrisonersOfColonyCount;
            obj["guestsCount"] = map.mapPawns.AllPawnsSpawned
                .Count(p => p.RaceProps.Humanlike && !p.IsColonist && !p.IsPrisoner
                    && p.HostFaction == Faction.OfPlayer);

            // Recreation analysis (Phase 3 enhancement - Issue #56)
            var recreation = new JSONObject();
            var joyBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.joyKind != null)
                .ToList();

            recreation["joySourceCount"] = joyBuildings.Count;

            // Count distinct joy types for variety analysis
            var joyKinds = new System.Collections.Generic.HashSet<string>();
            int socialJoy = 0;
            int soloJoy = 0;

            foreach (var building in joyBuildings)
            {
                var joyKind = building.def.building.joyKind;
                if (joyKind != null)
                {
                    joyKinds.Add(joyKind.defName);

                    // Check if it's social recreation
                    // Social joy kinds: Social, Gaming_Dexterity (poker/billiards), Gaming_Cerebral (chess)
                    bool isSocial = joyKind.defName == "Social" || 
                                   joyKind.defName.StartsWith("Gaming") ||
                                   (building.def.building != null && building.def.building.isSittable);
                    
                    if (isSocial)
                        socialJoy++;
                    else
                        soloJoy++;
                }
            }

            recreation["joyVariety"] = joyKinds.Count;
            recreation["socialJoy"] = socialJoy;
            recreation["soloJoy"] = soloJoy;

            // Assess adequacy based on colonist count
            int colonists = map.mapPawns.FreeColonistsCount;
            string adequacy = "sufficient";
            if (joyBuildings.Count < colonists / 2)
                adequacy = "insufficient";
            else if (joyBuildings.Count < colonists)
                adequacy = "low";

            recreation["adequacy"] = adequacy;

            if (joyKinds.Count < 3)
                recreation["varietyWarning"] = "Low joy variety - colonists may get bored with repetitive recreation";

            obj["recreation"] = recreation;

            return obj.ToString();
        }

        public static string GetResources(string category)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string cat = (category ?? "all").ToLower();
            var obj = new JSONObject();

            var counter = map.resourceCounter;

            if (cat == "all" || cat == "food")
            {
                var food = new JSONObject();
                food["totalNutrition"] = counter.TotalHumanEdibleNutrition.ToString("F1");
                food["mealsFine"] = CountItems(map, ThingDefOf.MealFine);
                food["mealsSimple"] = CountItems(map, ThingDefOf.MealSimple);
                var mealLavishDef = DefDatabase<ThingDef>.GetNamedSilentFail("MealLavish");
                if (mealLavishDef != null)
                    food["mealsLavish"] = CountItems(map, mealLavishDef);
                food["rawFood"] = counter.TotalHumanEdibleNutrition.ToString("F0");
                obj["food"] = food;
            }

            if (cat == "all" || cat == "materials")
            {
                var mats = new JSONObject();
                mats["steel"] = CountItems(map, ThingDefOf.Steel);
                mats["wood"] = CountItems(map, ThingDefOf.WoodLog);
                mats["plasteel"] = CountItems(map, ThingDefOf.Plasteel);
                mats["components"] = CountItems(map, ThingDefOf.ComponentIndustrial);
                mats["advancedComponents"] = CountItems(map, ThingDefOf.ComponentSpacer);
                mats["gold"] = CountItems(map, ThingDefOf.Gold);
                mats["silver"] = CountItems(map, ThingDefOf.Silver);
                mats["uranium"] = CountItems(map, ThingDefOf.Uranium);
                mats["jade"] = CountItems(map, ThingDefOf.Jade);
                mats["cloth"] = CountItems(map, ThingDefOf.Cloth);
                obj["materials"] = mats;
            }

            if (cat == "all" || cat == "medicine")
            {
                var med = new JSONObject();
                med["medicineTotalCount"] = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Sum(t => t.stackCount);
                med["herbalMedicine"] = CountItems(map, ThingDefOf.MedicineHerbal);
                med["industrialMedicine"] = CountItems(map, ThingDefOf.MedicineIndustrial);
                med["glitterworldMedicine"] = CountItems(map, ThingDefOf.MedicineUltratech);
                obj["medicine"] = med;
            }

            if (cat == "weapons" || cat == "all")
            {
                int weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                    .Count(t => t.Faction == Faction.OfPlayer || (t.Faction == null && !t.Position.Fogged(map)));
                obj["weaponsCount"] = weapons;
            }

            if (cat == "apparel" || cat == "all")
            {
                int apparel = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                    .Count(t => !t.Position.Fogged(map));
                obj["apparelCount"] = apparel;
            }

            return obj.ToString();
        }

        public static string GetRooms()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            // Collect unique rooms from all regions
            var seenRooms = new System.Collections.Generic.HashSet<int>();
            foreach (var region in map.regionGrid.AllRegions)
            {
                var room = region.Room;
                if (room == null) continue;
                if (!seenRooms.Add(room.ID)) continue;
                if (room.TouchesMapEdge || room.IsDoorway) continue;

                var obj = new JSONObject();
                obj["role"] = room.Role?.LabelCap.ToString() ?? "None";
                obj["cellCount"] = room.CellCount;

                var stats = room.GetStat(RoomStatDefOf.Impressiveness);
                obj["impressiveness"] = stats.ToString("F1");
                obj["beauty"] = room.GetStat(RoomStatDefOf.Beauty).ToString("F1");
                obj["cleanliness"] = room.GetStat(RoomStatDefOf.Cleanliness).ToString("F2");
                obj["space"] = room.GetStat(RoomStatDefOf.Space).ToString("F1");

                // Check for owners (beds)
                var beds = room.ContainedBeds;
                if (beds != null)
                {
                    var owners = new JSONArray();
                    foreach (var bed in beds)
                    {
                        foreach (var owner in bed.OwnersForReading)
                            owners.Add(owner.Name?.ToStringShort ?? "Unknown");
                    }
                    if (owners.Count > 0)
                        obj["owners"] = owners;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["rooms"] = arr;
            result["totalRooms"] = arr.Count;
            return result.ToString();
        }

        public static string GetStockpiles()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var zone in map.zoneManager.AllZones)
            {
                var stockpile = zone as Zone_Stockpile;
                if (stockpile == null) continue;

                var obj = new JSONObject();
                obj["name"] = stockpile.label;
                obj["priority"] = stockpile.settings.Priority.ToString();
                obj["cellCount"] = stockpile.CellCount;

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["stockpiles"] = arr;
            return result.ToString();
        }

        public static string GetResourceTrends()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var resourceTracker = Core.ResourceTracker.Instance;

            try
            {
                // Get current resource counts
                var currentFood = GetFoodNutrition(map);
                var currentMedicine = CountItems(map, ThingDefOf.MedicineIndustrial) + CountItems(map, ThingDefOf.MedicineHerbal) + CountItems(map, ThingDefOf.MedicineUltratech);
                var currentWood = CountItems(map, ThingDefOf.WoodLog);
                var currentSteel = CountItems(map, ThingDefOf.Steel);

                // Calculate burn rates using snapshots from ResourceTracker
                var foodBurnRate = CalculateBurnRate(resourceTracker, "food");
                var medicineBurnRate = CalculateBurnRate(resourceTracker, "medicine");
                var woodBurnRate = CalculateBurnRate(resourceTracker, "wood");
                var steelBurnRate = CalculateBurnRate(resourceTracker, "steel");

                // Food analysis
                var food = new JSONObject();
                food["current"] = currentFood;
                food["burn_rate_per_day"] = foodBurnRate;
                if (foodBurnRate > 0 && currentFood > 0)
                {
                    float daysRemaining = currentFood / foodBurnRate;
                    food["days_remaining"] = daysRemaining.ToString("F1");
                    food["status"] = GetResourceStatus(daysRemaining);
                }
                else if (currentFood > 0)
                {
                    food["days_remaining"] = "unknown";
                    food["status"] = "unknown";
                }
                else
                {
                    food["days_remaining"] = "0";
                    food["status"] = "critical";
                }
                result["food"] = food;

                // Medicine analysis
                var medicine = new JSONObject();
                medicine["current"] = currentMedicine;
                medicine["burn_rate_per_day"] = medicineBurnRate;
                if (medicineBurnRate > 0 && currentMedicine > 0)
                {
                    float daysRemaining = currentMedicine / medicineBurnRate;
                    medicine["days_remaining"] = daysRemaining.ToString("F1");
                    medicine["status"] = GetResourceStatus(daysRemaining);
                }
                else if (currentMedicine > 0)
                {
                    medicine["days_remaining"] = "unknown";
                    medicine["status"] = "unknown";
                }
                else
                {
                    medicine["days_remaining"] = "0";
                    medicine["status"] = "critical";
                }
                result["medicine"] = medicine;

                // Wood analysis
                var wood = new JSONObject();
                wood["current"] = currentWood;
                wood["burn_rate_per_day"] = woodBurnRate;
                if (woodBurnRate > 0 && currentWood > 0)
                {
                    float daysRemaining = currentWood / woodBurnRate;
                    wood["days_remaining"] = daysRemaining.ToString("F1");
                    wood["status"] = GetResourceStatus(daysRemaining);
                }
                else if (currentWood > 0)
                {
                    wood["days_remaining"] = "unknown";
                    wood["status"] = "unknown";
                }
                else
                {
                    wood["days_remaining"] = "0";
                    wood["status"] = "critical";
                }
                result["wood"] = wood;

                // Steel analysis
                var steel = new JSONObject();
                steel["current"] = currentSteel;
                steel["burn_rate_per_day"] = steelBurnRate;
                if (steelBurnRate > 0 && currentSteel > 0)
                {
                    float daysRemaining = currentSteel / steelBurnRate;
                    steel["days_remaining"] = daysRemaining.ToString("F1");
                    steel["status"] = GetResourceStatus(daysRemaining);
                }
                else if (currentSteel > 0)
                {
                    steel["days_remaining"] = "unknown";
                    steel["status"] = "unknown";
                }
                else
                {
                    steel["days_remaining"] = "0";
                    steel["status"] = "critical";
                }
                result["steel"] = steel;

                // Summary with overall assessment
                var summary = new JSONObject();
                var criticalResources = new JSONArray();
                var lowResources = new JSONArray();
                var warningResources = new JSONArray();

                CheckResourceStatus(food, "food", criticalResources, lowResources, warningResources);
                CheckResourceStatus(medicine, "medicine", criticalResources, lowResources, warningResources);
                CheckResourceStatus(wood, "wood", criticalResources, lowResources, warningResources);
                CheckResourceStatus(steel, "steel", criticalResources, lowResources, warningResources);

                summary["critical"] = criticalResources;
                summary["low"] = lowResources;
                summary["warning"] = warningResources;

                if (criticalResources.Count > 0)
                    summary["overall_status"] = "critical";
                else if (lowResources.Count > 0)
                    summary["overall_status"] = "low";
                else if (warningResources.Count > 0)
                    summary["overall_status"] = "warning";
                else
                    summary["overall_status"] = "stable";

                result["summary"] = summary;

                // Historical snapshot data if available
                var history = new JSONObject();
                if (resourceTracker != null)
                {
                    int day0 = resourceTracker.GetSnapshot("food", 0);
                    int day1 = resourceTracker.GetSnapshot("food", 1);
                    int day2 = resourceTracker.GetSnapshot("food", 2);
                    int day3 = resourceTracker.GetSnapshot("food", 3);

                    if (day0 >= 0) history["today"] = day0;
                    if (day1 >= 0) history["1_day_ago"] = day1;
                    if (day2 >= 0) history["2_days_ago"] = day2;
                    if (day3 >= 0) history["3_days_ago"] = day3;
                }
                result["food_history"] = history;

            }
            catch (Exception ex)
            {
                result["error"] = "Could not analyze resource trends: " + ex.Message;
            }

            return result.ToString();
        }

        private static int GetFoodNutrition(Map map)
        {
            int total = 0;
            foreach (var thing in map.listerThings.AllThings)
            {
                if (thing.def.IsNutritionGivingIngestible)
                {
                    total += thing.stackCount;
                }
            }
            return total;
        }

        private static float CalculateBurnRate(Core.ResourceTracker tracker, string resource)
        {
            if (tracker == null)
            {
                // Fallback: estimate from current consumption if no tracker
                return EstimateCurrentBurnRate(resource);
            }

            int day0 = tracker.GetSnapshot(resource, 0);
            int day1 = tracker.GetSnapshot(resource, 1);

            if (day0 < 0 || day1 < 0)
            {
                return EstimateCurrentBurnRate(resource);
            }

            float diff = day0 - day1;
            if (diff < 0) diff = 0;
            return diff;
        }

        private static float EstimateCurrentBurnRate(string resource)
        {
            // Fallback estimate based on typical consumption
            // These are rough estimates that get refined by actual data
            switch (resource)
            {
                case "food": return 10f;    // ~10 nutrition/day average
                case "medicine": return 0.5f; // ~0.5 medicine/day average
                case "wood": return 5f;    // ~5 wood/day average
                case "steel": return 3f;   // ~3 steel/day average
                default: return 1f;
            }
        }

        private static string GetResourceStatus(float daysRemaining)
        {
            if (daysRemaining <= 3)
                return "critical";
            else if (daysRemaining <= 7)
                return "low";
            else if (daysRemaining <= 14)
                return "warning";
            else
                return "stable";
        }

        private static void CheckResourceStatus(JSONObject resourceObj, string name, JSONArray critical, JSONArray low, JSONArray warning)
        {
            var status = resourceObj["status"]?.Value;
            if (status == "critical")
                critical.Add(name);
            else if (status == "low")
                low.Add(name);
            else if (status == "warning")
                warning.Add(name);
        }

        private static int CountItems(Map map, ThingDef def)
        {
            return map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);
        }
    }
}
