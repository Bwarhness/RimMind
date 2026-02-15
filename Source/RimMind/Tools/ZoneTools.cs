using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ZoneTools
    {
        public static string ListZones()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // --- Native RimWorld Zones ---
            var zonesArr = new JSONArray();
            foreach (var zone in map.zoneManager.AllZones)
            {
                var obj = new JSONObject();
                obj["label"] = zone.label;
                obj["cellCount"] = zone.CellCount;

                // Bounding box
                int minX = int.MaxValue, minZ = int.MaxValue;
                int maxX = int.MinValue, maxZ = int.MinValue;
                foreach (var cell in zone.Cells)
                {
                    if (cell.x < minX) minX = cell.x;
                    if (cell.x > maxX) maxX = cell.x;
                    if (cell.z < minZ) minZ = cell.z;
                    if (cell.z > maxZ) maxZ = cell.z;
                }
                obj["x1"] = minX;
                obj["z1"] = minZ;
                obj["x2"] = maxX;
                obj["z2"] = maxZ;

                if (zone is Zone_Growing growing)
                {
                    obj["type"] = "growing";
                    var plantDef = growing.GetPlantDefToGrow();
                    obj["crop"] = plantDef?.LabelCap.ToString() ?? "None";

                    float totalGrowth = 0;
                    int plantCount = 0;
                    foreach (var cell in growing.Cells)
                    {
                        var plant = cell.GetPlant(map);
                        if (plant != null) { totalGrowth += plant.Growth; plantCount++; }
                    }
                    if (plantCount > 0)
                        obj["averageGrowth"] = (totalGrowth / plantCount * 100f).ToString("F0") + "%";
                    obj["plantedCells"] = plantCount;
                }
                else if (zone is Zone_Stockpile stockpile)
                {
                    obj["type"] = "stockpile";
                    obj["priority"] = stockpile.settings.Priority.ToString();

                    // Count items in stockpile
                    int itemCount = 0;
                    foreach (var cell in stockpile.Cells)
                    {
                        foreach (var thing in cell.GetThingList(map))
                        {
                            if (thing.def.category == ThingCategory.Item)
                                itemCount += thing.stackCount;
                        }
                    }
                    obj["itemCount"] = itemCount;
                }
                else
                {
                    obj["type"] = zone.GetType().Name;
                }

                zonesArr.Add(obj);
            }
            result["zones"] = zonesArr;
            result["zoneCount"] = zonesArr.Count;

            // --- Areas ---
            var areasArr = new JSONArray();
            try
            {
                foreach (var area in map.areaManager.AllAreas)
                {
                    var obj = new JSONObject();
                    obj["label"] = area.Label;
                    obj["type"] = area.GetType().Name.Replace("Area_", "");
                    int areaMinX = int.MaxValue, areaMinZ = int.MaxValue;
                    int areaMaxX = int.MinValue, areaMaxZ = int.MinValue;
                    int areaCellCount = 0;
                    foreach (var cell in area.ActiveCells)
                    {
                        areaCellCount++;
                        if (cell.x < areaMinX) areaMinX = cell.x;
                        if (cell.x > areaMaxX) areaMaxX = cell.x;
                        if (cell.z < areaMinZ) areaMinZ = cell.z;
                        if (cell.z > areaMaxZ) areaMaxZ = cell.z;
                    }
                    obj["cellCount"] = areaCellCount;
                    if (areaCellCount > 0)
                    {
                        obj["x1"] = areaMinX;
                        obj["z1"] = areaMinZ;
                        obj["x2"] = areaMaxX;
                        obj["z2"] = areaMaxZ;
                    }
                    obj["mutable"] = area.Mutable;
                    areasArr.Add(obj);
                }
            }
            catch { }
            result["areas"] = areasArr;
            result["areaCount"] = areasArr.Count;

            // --- Custom Planning Zones ---
            var tracker = ZoneTracker.Instance;
            if (tracker != null && tracker.Zones.Count > 0)
            {
                var planningArr = new JSONArray();
                foreach (var pz in tracker.Zones)
                {
                    var obj = new JSONObject();
                    obj["id"] = pz.id;
                    obj["label"] = pz.label;
                    obj["purpose"] = pz.purpose;
                    obj["x1"] = pz.x1;
                    obj["z1"] = pz.z1;
                    obj["x2"] = pz.x2;
                    obj["z2"] = pz.z2;
                    obj["size"] = pz.Width + "x" + pz.Height;
                    planningArr.Add(obj);
                }
                result["planningZones"] = planningArr;
                result["planningCount"] = planningArr.Count;
            }

            return result.ToString();
        }

        public static string CreateZone(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");
            if (args == null) return ToolExecutor.JsonError("Arguments required.");

            string type = (args["type"]?.Value ?? "planning").ToLower();

            if (string.IsNullOrEmpty(args["x1"]?.Value) || string.IsNullOrEmpty(args["z1"]?.Value) ||
                string.IsNullOrEmpty(args["x2"]?.Value) || string.IsNullOrEmpty(args["z2"]?.Value))
                return ToolExecutor.JsonError("x1, z1, x2, z2 coordinates are all required.");

            int x1 = args["x1"].AsInt, z1 = args["z1"].AsInt;
            int x2 = args["x2"].AsInt, z2 = args["z2"].AsInt;
            int minX = Math.Min(x1, x2), minZ = Math.Min(z1, z2);
            int maxX = Math.Max(x1, x2), maxZ = Math.Max(z1, z2);
            int area = (maxX - minX + 1) * (maxZ - minZ + 1);

            if (area > 10000)
                return ToolExecutor.JsonError("Zone too large (" + area + " cells). Maximum 10000 cells.");

            if (!new IntVec3(minX, 0, minZ).InBounds(map) || !new IntVec3(maxX, 0, maxZ).InBounds(map))
                return ToolExecutor.JsonError("Coordinates out of bounds. Map size: " + map.Size.x + "x" + map.Size.z);

            switch (type)
            {
                case "stockpile":
                    return CreateStockpileZone(map, args, minX, minZ, maxX, maxZ);
                case "growing":
                    return CreateGrowingZone(map, args, minX, minZ, maxX, maxZ);
                case "planning":
                    return CreatePlanningZone(map, args, minX, minZ, maxX, maxZ);
                default:
                    return ToolExecutor.JsonError("Unknown zone type: '" + type + "'. Valid types: stockpile, growing, planning.");
            }
        }

        public static string DeleteZone(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");
            if (args == null) return ToolExecutor.JsonError("Arguments required.");

            string label = args["label"]?.Value;
            if (string.IsNullOrEmpty(label))
                return ToolExecutor.JsonError("'label' is required — the name of the zone to delete.");

            // Try native RimWorld zones first
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (zone.label.Equals(label, StringComparison.OrdinalIgnoreCase))
                {
                    string zoneType = zone is Zone_Growing ? "growing" : zone is Zone_Stockpile ? "stockpile" : zone.GetType().Name;
                    int cells = zone.CellCount;
                    zone.Delete();

                    var result = new JSONObject();
                    result["deleted"] = true;
                    result["label"] = label;
                    result["type"] = zoneType;
                    result["cellsFreed"] = cells;
                    return result.ToString();
                }
            }

            // Try custom planning zones
            var tracker = ZoneTracker.Instance;
            if (tracker != null)
            {
                var pz = tracker.GetZoneByLabel(label);
                if (pz != null)
                {
                    bool removePlans = args["remove_plans"]?.AsBool ?? false;
                    int plansRemoved = 0;

                    if (removePlans)
                    {
                        for (int z = pz.z1; z <= pz.z2; z++)
                            for (int x = pz.x1; x <= pz.x2; x++)
                            {
                                var cell = new IntVec3(x, 0, z);
                                if (!cell.InBounds(map)) continue;
                                var des = map.designationManager.DesignationAt(cell, DesignationDefOf.Plan);
                                if (des != null) { map.designationManager.RemoveDesignation(des); plansRemoved++; }
                            }
                    }

                    tracker.RemoveZone(pz.id);
                    var result = new JSONObject();
                    result["deleted"] = true;
                    result["label"] = label;
                    result["type"] = "planning";
                    if (removePlans) result["plansRemoved"] = plansRemoved;
                    return result.ToString();
                }
            }

            return ToolExecutor.JsonError("No zone found with label '" + label + "'. Use list_zones to see all zones.");
        }

        private static string CreateStockpileZone(Map map, JSONNode args, int minX, int minZ, int maxX, int maxZ)
        {
            try
            {
                var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
                map.zoneManager.RegisterZone(zone);

                string label = args["name"]?.Value;
                if (!string.IsNullOrEmpty(label))
                    zone.label = label;

                // Set priority if specified
                string priority = args["priority"]?.Value;
                if (!string.IsNullOrEmpty(priority))
                {
                    StoragePriority p;
                    if (Enum.TryParse(priority, true, out p))
                        zone.settings.Priority = p;
                }

                int added = 0, skipped = 0;
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map) || !cell.Standable(map)) { skipped++; continue; }
                        if (map.zoneManager.ZoneAt(cell) != null) { skipped++; continue; }
                        zone.AddCell(cell);
                        added++;
                    }
                }

                if (added == 0)
                {
                    zone.Delete();
                    return ToolExecutor.JsonError("Could not add any cells — area may be occupied by other zones or impassable terrain.");
                }

                var result = new JSONObject();
                result["type"] = "stockpile";
                result["label"] = zone.label;
                result["cellsAdded"] = added;
                result["cellsSkipped"] = skipped;
                result["priority"] = zone.settings.Priority.ToString();
                return result.ToString();
            }
            catch (Exception ex)
            {
                return ToolExecutor.JsonError("Failed to create stockpile zone: " + ex.Message);
            }
        }

        private static string CreateGrowingZone(Map map, JSONNode args, int minX, int minZ, int maxX, int maxZ)
        {
            try
            {
                var zone = new Zone_Growing(map.zoneManager);
                map.zoneManager.RegisterZone(zone);

                string label = args["name"]?.Value;
                if (!string.IsNullOrEmpty(label))
                    zone.label = label;

                int added = 0, skipped = 0;
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) { skipped++; continue; }
                        if (map.zoneManager.ZoneAt(cell) != null) { skipped++; continue; }
                        // Check if the cell has soil/fertile ground
                        if (map.fertilityGrid.FertilityAt(cell) <= 0) { skipped++; continue; }
                        zone.AddCell(cell);
                        added++;
                    }
                }

                if (added == 0)
                {
                    zone.Delete();
                    return ToolExecutor.JsonError("Could not add any cells — area may be occupied, impassable, or infertile.");
                }

                // Set crop if specified
                string crop = args["crop"]?.Value;
                if (!string.IsNullOrEmpty(crop))
                {
                    var plantDef = DefDatabase<ThingDef>.AllDefsListForReading
                        .FirstOrDefault(d => d.plant != null && d.plant.sowTags.Contains("Ground")
                            && (d.label.IndexOf(crop, StringComparison.OrdinalIgnoreCase) >= 0
                                || d.defName.IndexOf(crop, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (plantDef != null)
                        zone.SetPlantDefToGrow(plantDef);
                }

                var result = new JSONObject();
                result["type"] = "growing";
                result["label"] = zone.label;
                result["cellsAdded"] = added;
                result["cellsSkipped"] = skipped;
                result["crop"] = zone.GetPlantDefToGrow()?.LabelCap.ToString() ?? "Default";
                return result.ToString();
            }
            catch (Exception ex)
            {
                return ToolExecutor.JsonError("Failed to create growing zone: " + ex.Message);
            }
        }

        private static string CreatePlanningZone(Map map, JSONNode args, int minX, int minZ, int maxX, int maxZ)
        {
            var tracker = ZoneTracker.Instance;
            if (tracker == null) return ToolExecutor.JsonError("ZoneTracker not available. Is a game loaded?");

            string label = args["name"]?.Value ?? args["label"]?.Value;
            string purpose = args["purpose"]?.Value;

            if (string.IsNullOrEmpty(label)) return ToolExecutor.JsonError("'name' is required for planning zones.");
            if (string.IsNullOrEmpty(purpose)) return ToolExecutor.JsonError("'purpose' is required for planning zones (e.g. 'housing', 'defense', 'prison').");

            if (tracker.GetZoneByLabel(label) != null)
                return ToolExecutor.JsonError("A planning zone with name '" + label + "' already exists.");

            var zone = tracker.AddZone(label, purpose, minX, minZ, maxX, maxZ);

            // Place plan designation outline
            bool markOnMap = args["mark_on_map"]?.AsBool ?? true;
            int plansPlaced = 0;
            if (markOnMap)
            {
                var plan = PlanTools.PlacePlanRect(map, zone.x1, zone.z1, zone.x2, zone.z2, zone.label);
                if (plan != null)
                    plansPlaced = plan.CellCount;
            }

            var result = new JSONObject();
            result["type"] = "planning";
            result["label"] = zone.label;
            result["purpose"] = zone.purpose;
            result["size"] = zone.Width + "x" + zone.Height;
            result["cellCount"] = zone.CellCount;
            if (markOnMap) result["plansPlaced"] = plansPlaced;
            return result.ToString();
        }

    }
}
