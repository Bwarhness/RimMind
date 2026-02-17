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

        private static int CountItems(Map map, ThingDef def)
        {
            return map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);
        }
    }
}
