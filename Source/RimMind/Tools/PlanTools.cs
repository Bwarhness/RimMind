using System;
using System.Collections.Generic;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class PlanTools
    {
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
                if (map.designationManager.DesignationAt(cell, DesignationDefOf.Plan) != null)
                {
                    skipped++;
                    continue;
                }
                map.designationManager.AddDesignation(new Designation(cell, DesignationDefOf.Plan));
                placed++;
            }

            var result = new JSONObject();
            result["placed"] = placed;
            result["skipped_existing"] = skipped;
            result["out_of_bounds"] = outOfBounds;
            result["shape"] = shape;
            return result.ToString();
        }

        public static string RemovePlans(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool removeAll = args != null && args["all"]?.AsBool == true;

            if (removeAll)
            {
                var planDesignations = new List<Designation>();
                foreach (var des in map.designationManager.allDesignations)
                {
                    if (des.def == DesignationDefOf.Plan)
                        planDesignations.Add(des);
                }
                foreach (var des in planDesignations)
                    map.designationManager.RemoveDesignation(des);

                var result = new JSONObject();
                result["removed"] = planDesignations.Count;
                result["scope"] = "all";
                return result.ToString();
            }

            if (args == null || string.IsNullOrEmpty(args["x"]?.Value) || string.IsNullOrEmpty(args["z"]?.Value))
                return ToolExecutor.JsonError("Either 'all: true' or x/z coordinates are required.");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;

            bool hasRange = !string.IsNullOrEmpty(args["x2"]?.Value) && !string.IsNullOrEmpty(args["z2"]?.Value);

            if (!hasRange)
            {
                var cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError("Coordinates out of bounds. Map size: " + map.Size.x + "x" + map.Size.z);

                var des = map.designationManager.DesignationAt(cell, DesignationDefOf.Plan);
                bool removed = false;
                if (des != null)
                {
                    map.designationManager.RemoveDesignation(des);
                    removed = true;
                }

                var result = new JSONObject();
                result["removed"] = removed ? 1 : 0;
                result["scope"] = "single";
                return result.ToString();
            }

            int x2 = args["x2"].AsInt;
            int z2 = args["z2"].AsInt;
            int minX = Math.Min(x, x2);
            int maxX = Math.Max(x, x2);
            int minZ = Math.Min(z, z2);
            int maxZ = Math.Max(z, z2);

            int removedCount = 0;
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var cell = new IntVec3(cx, 0, cz);
                    if (!cell.InBounds(map)) continue;
                    var des = map.designationManager.DesignationAt(cell, DesignationDefOf.Plan);
                    if (des != null)
                    {
                        map.designationManager.RemoveDesignation(des);
                        removedCount++;
                    }
                }
            }

            var result2 = new JSONObject();
            result2["removed"] = removedCount;
            result2["scope"] = "area";
            result2["area"] = (maxX - minX + 1) + "x" + (maxZ - minZ + 1);
            return result2.ToString();
        }

        private static void AddRectOutline(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2);
            int maxZ = Math.Max(z1, z2);

            // Top and bottom edges
            for (int x = minX; x <= maxX; x++)
            {
                cells.Add(new IntVec3(x, 0, minZ));
                if (minZ != maxZ)
                    cells.Add(new IntVec3(x, 0, maxZ));
            }
            // Left and right edges (excluding corners already added)
            for (int z = minZ + 1; z < maxZ; z++)
            {
                cells.Add(new IntVec3(minX, 0, z));
                if (minX != maxX)
                    cells.Add(new IntVec3(maxX, 0, z));
            }
        }

        private static void AddFilledRect(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            int minZ = Math.Min(z1, z2);
            int maxZ = Math.Max(z1, z2);

            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    cells.Add(new IntVec3(x, 0, z));
        }

        private static void AddLine(List<IntVec3> cells, int x1, int z1, int x2, int z2)
        {
            // Bresenham's line algorithm
            int dx = Math.Abs(x2 - x1);
            int dz = Math.Abs(z2 - z1);
            int sx = x1 < x2 ? 1 : -1;
            int sz = z1 < z2 ? 1 : -1;
            int err = dx - dz;

            int cx = x1, cz = z1;
            while (true)
            {
                cells.Add(new IntVec3(cx, 0, cz));
                if (cx == x2 && cz == z2) break;
                int e2 = 2 * err;
                if (e2 > -dz)
                {
                    err -= dz;
                    cx += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    cz += sz;
                }
            }
        }
    }
}
