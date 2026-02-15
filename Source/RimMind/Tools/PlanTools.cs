using System;
using System.Collections.Generic;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class PlanTools
    {
        public static string GetPlans()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var planManager = map.planManager;
            var allPlans = planManager.AllPlans;

            var result = new JSONObject();
            result["totalPlans"] = allPlans.Count;

            if (allPlans.Count == 0)
            {
                result["message"] = "No plans on the map.";
                return result.ToString();
            }

            var plansArr = new JSONArray();
            foreach (var plan in allPlans)
            {
                var obj = new JSONObject();
                obj["label"] = plan.RenamableLabel ?? plan.BaseLabel ?? "Unnamed";
                obj["cellCount"] = plan.CellCount;
                obj["hidden"] = plan.Hidden;

                if (plan.Color != null)
                    obj["color"] = plan.Color.defName;

                // Bounding box
                if (plan.CellCount > 0)
                {
                    int minX = int.MaxValue, minZ = int.MaxValue;
                    int maxX = int.MinValue, maxZ = int.MinValue;
                    foreach (var cell in plan.Cells)
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
                }

                // List cells if plan is small enough
                if (plan.CellCount <= 50)
                {
                    var cellsArr = new JSONArray();
                    foreach (var cell in plan.Cells)
                    {
                        var c = new JSONObject();
                        c["x"] = cell.x;
                        c["z"] = cell.z;
                        cellsArr.Add(c);
                    }
                    obj["cells"] = cellsArr;
                }

                plansArr.Add(obj);
            }

            result["plans"] = plansArr;
            return result.ToString();
        }

        public static string PlacePlans(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (args == null || string.IsNullOrEmpty(args["x"]?.Value) || string.IsNullOrEmpty(args["z"]?.Value))
                return ToolExecutor.JsonError("x and z coordinates are required.");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;
            string shape = args["shape"]?.Value ?? "single";

            int x2 = x, z2 = z;
            if (!string.IsNullOrEmpty(args["x2"]?.Value)) x2 = args["x2"].AsInt;
            if (!string.IsNullOrEmpty(args["z2"]?.Value)) z2 = args["z2"].AsInt;

            if (shape != "single" && (string.IsNullOrEmpty(args["x2"]?.Value) || string.IsNullOrEmpty(args["z2"]?.Value)))
                return ToolExecutor.JsonError("x2 and z2 are required for shape '" + shape + "'.");

            string label = args["name"]?.Value;

            var cells = new List<IntVec3>();

            switch (shape)
            {
                case "single":
                    cells.Add(new IntVec3(x, 0, z));
                    break;
                case "rect":
                    AddRectOutline(cells, x, z, x2, z2);
                    break;
                case "filled_rect":
                    AddFilledRect(cells, x, z, x2, z2);
                    break;
                case "line":
                    AddLine(cells, x, z, x2, z2);
                    break;
                default:
                    return ToolExecutor.JsonError("Unknown shape: " + shape + ". Valid shapes: single, rect, filled_rect, line.");
            }

            if (cells.Count > 2500)
                return ToolExecutor.JsonError("Too many cells (" + cells.Count + "). Maximum 2500 cells per call (50x50).");

            // Create a new Plan object using the 1.6 planning system
            var planManager = map.planManager;
            var colorDef = DefDatabase<ColorDef>.GetNamedSilentFail("PlanGray") ?? ColorDefOf.PlanGray;
            var plan = new Plan(colorDef, planManager);
            planManager.RegisterPlan(plan);

            if (!string.IsNullOrEmpty(label))
                plan.label = label;

            int placed = 0;
            int skipped = 0;
            int outOfBounds = 0;

            foreach (var cell in cells)
            {
                if (!cell.InBounds(map))
                {
                    outOfBounds++;
                    continue;
                }
                if (planManager.PlanAt(cell) != null)
                {
                    skipped++;
                    continue;
                }
                plan.AddCell(cell);
                placed++;
            }

            if (placed == 0)
            {
                plan.Delete(false);
                return ToolExecutor.JsonError("Could not place any plan cells — area may already have plans.");
            }

            var result = new JSONObject();
            result["placed"] = placed;
            result["skipped_existing"] = skipped;
            result["out_of_bounds"] = outOfBounds;
            result["shape"] = shape;
            result["planLabel"] = plan.RenamableLabel ?? plan.BaseLabel ?? "Plan";
            return result.ToString();
        }

        public static string RemovePlans(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var planManager = map.planManager;

            bool removeAll = args != null && args["all"]?.AsBool == true;

            if (removeAll)
            {
                int count = planManager.AllPlans.Count;
                // Delete all plans - iterate backwards since Delete modifies the list
                for (int i = planManager.AllPlans.Count - 1; i >= 0; i--)
                    planManager.AllPlans[i].Delete(false);

                var result = new JSONObject();
                result["removed"] = count;
                result["scope"] = "all";
                return result.ToString();
            }

            // Remove by label
            string label = args?["label"]?.Value;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = planManager.AllPlans.Count - 1; i >= 0; i--)
                {
                    var plan = planManager.AllPlans[i];
                    string planLabel = plan.RenamableLabel ?? plan.BaseLabel ?? "";
                    if (planLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
                    {
                        int cellCount = plan.CellCount;
                        plan.Delete(false);
                        var result = new JSONObject();
                        result["removed"] = 1;
                        result["cellsCleared"] = cellCount;
                        result["scope"] = "by_label";
                        result["label"] = label;
                        return result.ToString();
                    }
                }
                return ToolExecutor.JsonError("No plan found with label '" + label + "'. Use get_plans to see all plans.");
            }

            // Remove by coordinates
            if (args == null || string.IsNullOrEmpty(args["x"]?.Value) || string.IsNullOrEmpty(args["z"]?.Value))
                return ToolExecutor.JsonError("Provide 'all: true', 'label', or x/z coordinates.");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;
            bool hasRange = !string.IsNullOrEmpty(args["x2"]?.Value) && !string.IsNullOrEmpty(args["z2"]?.Value);

            if (!hasRange)
            {
                var cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError("Coordinates out of bounds. Map size: " + map.Size.x + "x" + map.Size.z);

                var plan = planManager.PlanAt(cell);
                bool removed = false;
                if (plan != null)
                {
                    plan.RemoveCell(cell);
                    if (plan.CellCount == 0)
                        plan.Delete(false);
                    removed = true;
                }

                var result2 = new JSONObject();
                result2["removed"] = removed ? 1 : 0;
                result2["scope"] = "single";
                return result2.ToString();
            }

            int rx2 = args["x2"].AsInt;
            int rz2 = args["z2"].AsInt;
            int minX = Math.Min(x, rx2);
            int maxX = Math.Max(x, rx2);
            int minZ = Math.Min(z, rz2);
            int maxZ = Math.Max(z, rz2);

            int removedCount = 0;
            var affectedPlans = new HashSet<Plan>();
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new IntVec3(cx, 0, cz);
                    if (!cell.InBounds(map)) continue;
                    var plan = planManager.PlanAt(cell);
                    if (plan != null)
                    {
                        plan.RemoveCell(cell);
                        affectedPlans.Add(plan);
                        removedCount++;
                    }
                }
            }

            // Clean up empty plans
            foreach (var plan in affectedPlans)
            {
                if (plan.CellCount == 0)
                    plan.Delete(false);
            }

            var result3 = new JSONObject();
            result3["removed"] = removedCount;
            result3["scope"] = "area";
            result3["area"] = (maxX - minX + 1) + "x" + (maxZ - minZ + 1);
            return result3.ToString();
        }

        // --- Shape helpers (used by PlacePlans) ---

        private static void AddRectOutline(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            for (int x = minX; x <= maxX; x++)
            {
                cells.Add(new IntVec3(x, 0, minZ));
                if (minZ != maxZ) cells.Add(new IntVec3(x, 0, maxZ));
            }
            for (int z = minZ + 1; z < maxZ; z++)
            {
                cells.Add(new IntVec3(minX, 0, z));
                if (minX != maxX) cells.Add(new IntVec3(maxX, 0, z));
            }
        }

        private static void AddFilledRect(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    cells.Add(new IntVec3(x, 0, z));
        }

        private static void AddLine(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            int dx = Math.Abs(x2 - x1), dz = Math.Abs(z2 - z1);
            int sx = x1 < x2 ? 1 : -1, sz = z1 < z2 ? 1 : -1;
            int err = dx - dz;
            int cx = x1, cz = z1;
            while (true)
            {
                cells.Add(new IntVec3(cx, 0, cz));
                if (cx == x2 && cz == z2) break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; cx += sx; }
                if (e2 < dx) { err += dx; cz += sz; }
            }
        }

        /// <summary>
        /// Helper for other tools (like ZoneTools) to place a plan outline.
        /// Returns the Plan object so it can be named/configured.
        /// </summary>
        public static Plan PlacePlanRect(Map map, int x1, int z1, int x2, int z2, string label = null)
        {
            var planManager = map.planManager;
            var colorDef = DefDatabase<ColorDef>.GetNamedSilentFail("PlanGray") ?? ColorDefOf.PlanGray;
            var plan = new Plan(colorDef, planManager);
            planManager.RegisterPlan(plan);

            if (!string.IsNullOrEmpty(label))
                plan.label = label;

            var cells = new List<IntVec3>();
            AddRectOutline(cells, x1, z1, x2, z2);

            foreach (var cell in cells)
            {
                if (!cell.InBounds(map)) continue;
                if (planManager.PlanAt(cell) != null) continue;
                plan.AddCell(cell);
            }

            if (plan.CellCount == 0)
            {
                plan.Delete(false);
                return null;
            }

            return plan;
        }
    }
}
