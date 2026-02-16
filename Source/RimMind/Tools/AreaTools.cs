using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class AreaTools
    {
        public static string RestrictToArea(string colonistName, string areaName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(areaName)) return ToolExecutor.JsonError("areaName parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.playerSettings == null)
                return ToolExecutor.JsonError("Colonist has no player settings.");

            // Find area
            string areaLower = areaName.ToLower();
            var area = map.areaManager.AllAreas
                .Where(a => a.AssignableAsAllowed())
                .FirstOrDefault(a => a.Label.ToLower().Contains(areaLower));

            if (area == null)
            {
                var availableAreas = map.areaManager.AllAreas
                    .Where(a => a.AssignableAsAllowed())
                    .Select(a => a.Label)
                    .Take(10);
                return ToolExecutor.JsonError("Area '" + areaName + "' not found. Available: " + string.Join(", ", availableAreas));
            }

            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["area"] = area.Label;
            result["cellCount"] = area.TrueCount;
            return result.ToString();
        }

        public static string Unrestrict(string colonistName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.playerSettings == null)
                return ToolExecutor.JsonError("Colonist has no player settings.");

            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["restriction"] = "removed";
            return result.ToString();
        }

        public static string GetAreaRestrictions()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.playerSettings == null) continue;

                var obj = new JSONObject();
                obj["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
                
                var restriction = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
                if (restriction != null)
                {
                    obj["area"] = restriction.Label;
                    obj["restricted"] = true;
                }
                else
                {
                    obj["restricted"] = false;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["restrictions"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string ListAreas()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var area in map.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()))
            {
                var obj = new JSONObject();
                obj["label"] = area.Label;
                obj["cellCount"] = area.TrueCount;
                obj["type"] = area.GetType().Name.Replace("Area_", "");

                // Get bounds
                if (area.TrueCount > 0)
                {
                    int minX = int.MaxValue, minZ = int.MaxValue;
                    int maxX = int.MinValue, maxZ = int.MinValue;
                    foreach (var cell in area.ActiveCells)
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

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["areas"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }
    }
}
