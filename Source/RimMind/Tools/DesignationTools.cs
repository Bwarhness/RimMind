using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class DesignationTools
    {
        public static string DesignateHunt(string animal)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");
            if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' parameter required.");

            var pawn = FindWildAnimal(map, animal);
            if (pawn == null) return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use list_animals or search_map type='animals' to see available animals.");

            if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null)
                return ToolExecutor.JsonError("Animal already designated for hunting.");

            map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Hunt));

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = pawn.LabelCap.ToString();
            result["species"] = pawn.kindDef?.label ?? "Unknown";
            result["location"] = pawn.Position.x + "," + pawn.Position.z;
            result["action"] = "hunt";
            return result.ToString();
        }

        public static string DesignateTame(string animal)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");
            if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' parameter required.");

            var pawn = FindWildAnimal(map, animal);
            if (pawn == null) return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use list_animals or search_map type='animals' to see available animals.");

            float wildness = pawn.GetStatValue(StatDefOf.Wildness);
            if (wildness > 0.98f)
                return ToolExecutor.JsonError("Animal is too wild to tame (wildness: " + wildness.ToString("P0") + ").");

            if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null)
                return ToolExecutor.JsonError("Animal already designated for taming.");

            map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Tame));

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = pawn.LabelCap.ToString();
            result["species"] = pawn.kindDef?.label ?? "Unknown";
            result["location"] = pawn.Position.x + "," + pawn.Position.z;
            result["wildness"] = wildness.ToString("P0");
            result["action"] = "tame";
            return result.ToString();
        }

        public static string CancelAnimalDesignation(string animal)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");
            if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' parameter required.");

            // Search all animals (including tamed) for designation cancellation
            string lower = animal.ToLower();
            var pawn = map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p.RaceProps.Animal &&
                    (p.Name?.ToStringShort?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                     p.LabelShort?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                     p.kindDef?.label?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                     p.LabelCap.ToString().Equals(animal, StringComparison.OrdinalIgnoreCase)) &&
                    (map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) != null ||
                     map.designationManager.DesignationOn(p, DesignationDefOf.Tame) != null));

            if (pawn == null) return ToolExecutor.JsonError("No animal matching '" + animal + "' with an active hunt/tame designation found.");

            var huntDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt);
            var tameDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame);

            string action = "none";
            if (huntDes != null) { map.designationManager.RemoveDesignation(huntDes); action = "hunt"; }
            if (tameDes != null) { map.designationManager.RemoveDesignation(tameDes); action = "tame"; }

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = pawn.LabelCap.ToString();
            result["cancelledAction"] = action;
            return result.ToString();
        }

        public static string DesignateMine(int x1, int z1, int x2, int z2)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            int minX = Math.Min(x1, x2), minZ = Math.Min(z1, z2);
            int maxX = Math.Max(x1, x2), maxZ = Math.Max(z1, z2);
            var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            if (!rect.InBounds(map)) return ToolExecutor.JsonError("Area out of bounds.");

            int designated = 0;
            foreach (var cell in rect.Cells)
            {
                var mineable = cell.GetFirstMineable(map);
                if (mineable != null && map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                {
                    map.designationManager.AddDesignation(new Designation(cell, DesignationDefOf.Mine));
                    designated++;
                }
            }

            if (designated == 0)
                return ToolExecutor.JsonError("No mineable rock found in area.");

            var result = new JSONObject();
            result["success"] = true;
            result["area"] = x1 + "," + z1 + " to " + x2 + "," + z2;
            result["cellsDesignated"] = designated;
            result["action"] = "mine";
            return result.ToString();
        }

        public static string DesignateChop(int x1, int z1, int x2, int z2)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            int minX = Math.Min(x1, x2), minZ = Math.Min(z1, z2);
            int maxX = Math.Max(x1, x2), maxZ = Math.Max(z1, z2);
            var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            if (!rect.InBounds(map)) return ToolExecutor.JsonError("Area out of bounds.");

            int designated = 0;
            foreach (var cell in rect.Cells)
            {
                var plant = cell.GetPlant(map);
                if (plant != null && plant.def.plant.IsTree && map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) == null)
                {
                    map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                    designated++;
                }
            }

            if (designated == 0)
                return ToolExecutor.JsonError("No trees found in area.");

            var result = new JSONObject();
            result["success"] = true;
            result["area"] = x1 + "," + z1 + " to " + x2 + "," + z2;
            result["treesDesignated"] = designated;
            result["action"] = "chop";
            return result.ToString();
        }

        public static string DesignateHarvest(int x1, int z1, int x2, int z2)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            int minX = Math.Min(x1, x2), minZ = Math.Min(z1, z2);
            int maxX = Math.Max(x1, x2), maxZ = Math.Max(z1, z2);
            var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            if (!rect.InBounds(map)) return ToolExecutor.JsonError("Area out of bounds.");

            int designated = 0;
            foreach (var cell in rect.Cells)
            {
                var plant = cell.GetPlant(map);
                if (plant != null && plant.HarvestableNow && map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) == null)
                {
                    map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.HarvestPlant));
                    designated++;
                }
            }

            if (designated == 0)
                return ToolExecutor.JsonError("No harvestable plants found in area.");

            var result = new JSONObject();
            result["success"] = true;
            result["area"] = x1 + "," + z1 + " to " + x2 + "," + z2;
            result["plantsDesignated"] = designated;
            result["action"] = "harvest";
            return result.ToString();
        }

        private static Pawn FindWildAnimal(Map map, string search)
        {
            // Search wild animals by name, species, or label
            var wildAnimals = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal && (p.Faction == null || !p.Faction.IsPlayer));

            // Exact name match first
            var match = wildAnimals.FirstOrDefault(p =>
                p.Name?.ToStringShort?.Equals(search, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null) return match;

            // Label match (e.g. "Hare", "Wild boar")
            match = wildAnimals.FirstOrDefault(p =>
                p.LabelCap.ToString().Equals(search, StringComparison.OrdinalIgnoreCase) ||
                p.LabelShort?.Equals(search, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null) return match;

            // Species/kind match (e.g. "hare" matches any hare)
            match = wildAnimals.FirstOrDefault(p =>
                p.kindDef?.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true ||
                p.def.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null) return match;

            // Contains match as last resort
            string lower = search.ToLower();
            match = wildAnimals.FirstOrDefault(p =>
                p.LabelCap.ToString().ToLower().Contains(lower) ||
                (p.kindDef?.label?.ToLower().Contains(lower) == true));

            return match;
        }
    }
}
