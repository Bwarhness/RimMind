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
        /// Set allowed/forbidden status on items.
        /// Targeting modes (checked in priority order):
        ///   1. ids       — exact items by thingIDNumber
        ///   2. type      — fuzzy match on defName or label
        ///   3. category  — predefined groups (weapons, medicine, etc.)
        ///   4. x/z       — single cell or rect area (x1/z1/x2/z2)
        ///   5. (none)    — ALL items on the map
        /// </summary>
        public static string SetItemAllowed(JSONNode args)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            if (args?["allowed"] == null)
                return ToolExecutor.JsonError("'allowed' parameter is required (true/false).");
            bool allowed = args["allowed"].AsBool;

            string targetMode;
            List<Thing> items = CollectTargetedItems(map, args, out targetMode);

            Log.Message("[RimMind] set_item_allowed: mode=" + targetMode + " matched=" + items.Count + " allowed=" + allowed);

            int changed = 0;
            int skipped = 0;
            int noComp = 0;
            var changedNames = new JSONArray();

            foreach (Thing thing in items)
            {
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp == null)
                {
                    noComp++;
                    continue;
                }

                bool shouldForbid = !allowed;
                if (comp.Forbidden != shouldForbid)
                {
                    ForbidUtility.SetForbidden(thing, shouldForbid, false);
                    changed++;
                    if (changedNames.Count < 20)
                        changedNames.Add(thing.LabelCapNoCount + (thing.stackCount > 1 ? " x" + thing.stackCount : ""));
                }
                else
                {
                    skipped++;
                }
            }

            var result = new JSONObject();
            result["targeting_mode"] = targetMode;
            result["changed"] = changed;
            result["already_correct"] = skipped;
            result["total_matched"] = items.Count;
            if (noComp > 0)
                result["not_forbiddable"] = noComp;
            if (changed > 0)
                result["examples"] = changedNames;
            return result.ToString();
        }

        /// <summary>
        /// Get all currently forbidden items on the map.
        /// </summary>
        public static string GetForbiddenItems(JSONNode args)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            string category = args?["category"]?.Value;

            // Collect all spawned items
            List<Thing> items;
            if (!string.IsNullOrEmpty(category) && category != "all")
                items = GetItemsByCategory(map, category);
            else
                items = GetAllItems(map);

            // Filter to only forbidden
            var forbidden = new List<Thing>();
            foreach (Thing thing in items)
            {
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp != null && comp.Forbidden)
                    forbidden.Add(thing);
            }

            var itemsArray = new JSONArray();
            foreach (Thing thing in forbidden)
            {
                var obj = new JSONObject();
                obj["id"] = thing.thingIDNumber;
                obj["name"] = thing.LabelCapNoCount + (thing.stackCount > 1 ? " x" + thing.stackCount : "");
                obj["def"] = thing.def.defName;
                obj["x"] = thing.Position.x;
                obj["z"] = thing.Position.z;

                Zone_Stockpile stockpile = map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
                if (stockpile != null)
                    obj["stockpile"] = stockpile.label;

                itemsArray.Add(obj);
            }

            var result = new JSONObject();
            result["count"] = forbidden.Count;
            result["items"] = itemsArray;
            return result.ToString();
        }

        /// <summary>
        /// Check if a JSON key actually exists (not just a LazyCreator).
        /// SimpleJSON's operator== overload makes args["key"] != null return true
        /// for non-existent keys because JSONLazyCreator is not JSONNull.
        /// </summary>
        private static bool HasKey(JSONNode obj, string key)
        {
            if (obj == null) return false;
            return obj.HasKey(key);
        }

        /// <summary>
        /// Collect items based on targeting mode in priority order:
        /// ids > type > category > coords > all
        /// </summary>
        private static List<Thing> CollectTargetedItems(Map map, JSONNode args, out string mode)
        {
            // 1. By IDs array
            if (HasKey(args, "ids") && args["ids"].Count > 0)
            {
                mode = "ids";
                JSONNode idsNode = args["ids"];
                var idSet = new HashSet<int>();
                for (int i = 0; i < idsNode.Count; i++)
                    idSet.Add(idsNode[i].AsInt);

                var items = new List<Thing>();
                foreach (Thing t in map.listerThings.AllThings)
                {
                    if (t.Spawned && idSet.Contains(t.thingIDNumber))
                        items.Add(t);
                }
                return items;
            }

            // 2. By type (fuzzy match on defName or label)
            string type = HasKey(args, "type") ? args["type"].Value : null;
            if (!string.IsNullOrEmpty(type))
            {
                mode = "type:" + type;

                // Try exact defName first
                ThingDef exactDef = DefDatabase<ThingDef>.GetNamedSilentFail(type);
                if (exactDef != null)
                {
                    return map.listerThings.ThingsOfDef(exactDef)
                        .Where(t => t.Spawned).ToList();
                }

                // Fuzzy fallback
                string lower = type.ToLowerInvariant();
                var items = new List<Thing>();
                foreach (Thing t in map.listerThings.AllThings)
                {
                    if (!t.Spawned || !t.def.EverHaulable) continue;
                    if (t.def.defName.ToLowerInvariant().Contains(lower)
                        || t.LabelCapNoCount.ToLowerInvariant().Contains(lower))
                        items.Add(t);
                }
                return items;
            }

            // 3. By category
            string category = HasKey(args, "category") ? args["category"].Value : null;
            if (!string.IsNullOrEmpty(category))
            {
                mode = "category:" + category;
                return GetItemsByCategory(map, category);
            }

            // 4. By coordinates (single cell or rect) — use HasKey to avoid LazyCreator false positives
            bool hasRect = HasKey(args, "x1") && HasKey(args, "z1") && HasKey(args, "x2") && HasKey(args, "z2");
            bool hasCell = HasKey(args, "x") && HasKey(args, "z");
            if (hasRect || hasCell)
            {
                int ax = hasRect ? args["x1"].AsInt : args["x"].AsInt;
                int az = hasRect ? args["z1"].AsInt : args["z"].AsInt;
                int bx = hasRect ? args["x2"].AsInt : args["x"].AsInt;
                int bz = hasRect ? args["z2"].AsInt : args["z"].AsInt;

                int minX = System.Math.Min(ax, bx), maxX = System.Math.Max(ax, bx);
                int minZ = System.Math.Min(az, bz), maxZ = System.Math.Max(az, bz);

                mode = "coords:" + minX + "," + minZ + "-" + maxX + "," + maxZ;

                var items = new List<Thing>();
                for (int ix = minX; ix <= maxX; ix++)
                {
                    for (int iz = minZ; iz <= maxZ; iz++)
                    {
                        IntVec3 cell = new IntVec3(ix, 0, iz);
                        if (!cell.InBounds(map)) continue;
                        foreach (Thing t in cell.GetThingList(map))
                        {
                            if (t.Spawned && t.def.EverHaulable)
                                items.Add(t);
                        }
                    }
                }
                return items;
            }

            // 5. No filter = ALL items on the map
            mode = "all";
            return GetAllItems(map);
        }

        private static List<Thing> GetAllItems(Map map)
        {
            var items = new List<Thing>();
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t.Spawned && t.def.EverHaulable)
                    items.Add(t);
            }
            return items;
        }

        private static List<Thing> GetItemsByCategory(Map map, string category)
        {
            var items = new List<Thing>();
            string cat = (category ?? "all").ToLowerInvariant();

            // Iterate all spawned haulable items and filter by category property.
            // We avoid ThingRequestGroup because it can miss items in certain states.
            List<Thing> allThings = map.listerThings.AllThings.ToList();

            switch (cat)
            {
                case "medicine":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t.def.EverHaulable && t.def.IsMedicine)
                            items.Add(t);
                    }
                    break;

                case "corpses":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t is Corpse)
                            items.Add(t);
                    }
                    break;

                case "weapons":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t.def.EverHaulable && t.def.IsWeapon
                            && !t.def.IsApparel && t.def.category == ThingCategory.Item)
                            items.Add(t);
                    }
                    break;

                case "apparel":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t.def.EverHaulable && t.def.IsApparel
                            && t.def.category == ThingCategory.Item)
                            items.Add(t);
                    }
                    break;

                case "food":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t.def.EverHaulable
                            && t.def.IsNutritionGivingIngestible)
                            items.Add(t);
                    }
                    break;

                case "resources":
                    foreach (Thing t in allThings)
                    {
                        if (t.Spawned && t.def.EverHaulable && t.def.IsStuff)
                            items.Add(t);
                    }
                    break;

                case "all":
                default:
                    return GetAllItems(map);
            }

            Log.Message("[RimMind] GetItemsByCategory('" + cat + "'): scanned " + allThings.Count + " things, matched " + items.Count);
            return items;
        }
    }
}
