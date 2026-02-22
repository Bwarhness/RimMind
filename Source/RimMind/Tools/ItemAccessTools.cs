using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ItemAccessTools
    {
        /// <summary>
        /// Set allowed/forbidden status on items matching filter criteria.
        /// </summary>
        public static string SetItemAllowed(JSONNode args)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            // Parse required 'allowed' parameter
            if (args?["allowed"] == null)
                return ToolExecutor.JsonError("'allowed' parameter is required (true = allow, false = forbid).");
            bool allowed = args["allowed"].AsBool;

            // Parse optional filters
            string locationFilter = args?["location_filter"]?.Value ?? "all";
            int? x = args?["x"] != null ? args["x"].AsInt : (int?)null;
            int? z = args?["z"] != null ? args["z"].AsInt : (int?)null;
            string defName = args?["def_name"]?.Value;
            string category = args?["category"]?.Value;

            // Validate at least one targeting parameter
            bool hasCoords = x.HasValue && z.HasValue;
            bool hasDefName = !string.IsNullOrEmpty(defName);
            bool hasCategory = !string.IsNullOrEmpty(category);
            if (!hasCoords && !hasDefName && !hasCategory)
                return ToolExecutor.JsonError("At least one of x/z, def_name, or category is required.");

            // Collect items based on filters
            List<Thing> items = CollectItems(map, x, z, defName, category);

            // Apply location filter
            items = ApplyLocationFilter(items, map, locationFilter);

            int changed = 0;
            int skipped = 0;
            var changedItems = new JSONArray();

            foreach (Thing thing in items)
            {
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp == null)
                {
                    skipped++;
                    continue;
                }

                // ForbidUtility.SetForbidden sets Forbidden property (true = forbidden, false = allowed)
                // Our 'allowed' param: true = allow (not forbidden), false = forbid
                bool shouldForbid = !allowed;
                if (comp.Forbidden != shouldForbid)
                {
                    ForbidUtility.SetForbidden(thing, shouldForbid, false);
                    changed++;
                    changedItems.Add(thing.LabelCapNoCount + (thing.stackCount > 1 ? " x" + thing.stackCount : ""));
                }
                else
                {
                    skipped++;
                }
            }

            var result = new JSONObject();
            result["changed"] = changed;
            result["skipped"] = skipped;
            result["items"] = changedItems;
            return result.ToString();
        }

        /// <summary>
        /// Get all forbidden items matching filter criteria.
        /// </summary>
        public static string GetForbiddenItems(JSONNode args)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            string category = args?["category"]?.Value ?? "all";
            string locationFilter = args?["location_filter"]?.Value ?? "all";

            // Collect all haulable items on the map
            List<Thing> items = CollectItems(map, null, null, null, category);

            // Apply location filter
            items = ApplyLocationFilter(items, map, locationFilter);

            // Filter to only forbidden items
            var forbiddenItems = new List<Thing>();
            foreach (Thing thing in items)
            {
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp != null && comp.Forbidden)
                {
                    forbiddenItems.Add(thing);
                }
            }

            var itemsArray = new JSONArray();
            foreach (Thing thing in forbiddenItems)
            {
                var itemObj = new JSONObject();
                itemObj["name"] = thing.LabelCapNoCount + (thing.stackCount > 1 ? " x" + thing.stackCount : "");
                itemObj["x"] = thing.Position.x;
                itemObj["z"] = thing.Position.z;
                itemObj["def"] = thing.def.defName;
                
                Zone_Stockpile stockpile = map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
                itemObj["in_stockpile"] = stockpile != null;
                
                itemsArray.Add(itemObj);
            }

            var result = new JSONObject();
            result["count"] = forbiddenItems.Count;
            result["items"] = itemsArray;
            return result.ToString();
        }

        private static List<Thing> CollectItems(Map map, int? x, int? z, string defName, string category)
        {
            var items = new List<Thing>();

            // If specific cell is given
            if (x.HasValue && z.HasValue)
            {
                IntVec3 cell = new IntVec3(x.Value, 0, z.Value);
                if (cell.InBounds(map))
                {
                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        if (thing.def.category == ThingCategory.Item)
                        {
                            items.Add(thing);
                        }
                    }
                }
                return items;
            }

            // If defName is given, find all items of that def
            if (!string.IsNullOrEmpty(defName))
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    items.AddRange(map.listerThings.ThingsOfDef(def).Where(t => t.def.category == ThingCategory.Item));
                }
                return items;
            }

            // If category is given (or "all"), collect by category
            if (!string.IsNullOrEmpty(category))
            {
                items = GetItemsByCategory(map, category);
            }

            return items;
        }

        private static List<Thing> GetItemsByCategory(Map map, string category)
        {
            var items = new List<Thing>();
            string cat = category.ToLowerInvariant();

            switch (cat)
            {
                case "medicine":
                    items.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine));
                    break;

                case "corpses":
                    items.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse));
                    break;

                case "weapons":
                    items.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                        .Where(t => !t.def.IsApparel && t.def.category == ThingCategory.Item));
                    break;

                case "apparel":
                    items.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                        .Where(t => t.def.category == ThingCategory.Item));
                    break;

                case "food":
                    foreach (Thing t in map.listerThings.AllThings)
                    {
                        if (t.def.category == ThingCategory.Item && t.def.IsNutritionGivingIngestible)
                        {
                            items.Add(t);
                        }
                    }
                    break;

                case "resources":
                    foreach (Thing t in map.listerThings.AllThings)
                    {
                        if (t.def.category == ThingCategory.Item && t.def.IsStuff)
                        {
                            items.Add(t);
                        }
                    }
                    break;

                case "all":
                default:
                    foreach (Thing t in map.listerThings.AllThings)
                    {
                        if (t.def.category == ThingCategory.Item && t.def.EverHaulable && t.Spawned)
                        {
                            items.Add(t);
                        }
                    }
                    break;
            }

            return items;
        }

        private static List<Thing> ApplyLocationFilter(List<Thing> items, Map map, string locationFilter)
        {
            if (string.IsNullOrEmpty(locationFilter) || locationFilter.ToLowerInvariant() == "all")
                return items;

            string filter = locationFilter.ToLowerInvariant();
            var filtered = new List<Thing>();

            foreach (Thing thing in items)
            {
                Zone_Stockpile stockpile = map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
                bool inStockpile = stockpile != null;

                if (filter == "stockpile" && inStockpile)
                {
                    filtered.Add(thing);
                }
                else if (filter == "ground" && !inStockpile)
                {
                    filtered.Add(thing);
                }
            }

            return filtered;
        }
    }
}
