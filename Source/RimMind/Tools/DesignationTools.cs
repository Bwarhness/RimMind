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
        /// <summary>
        /// Designate wild animals for hunting.
        /// Use id for precise targeting (from get_wild_animals), or animal+count for species-based.
        /// count: 1 (default) = one animal, N = exactly N, -1 = all matching.
        /// </summary>
        public static string DesignateHunt(string animal, int count = 1, int id = -1)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // ID-based targeting: find exact animal by thingIDNumber
            if (id >= 0)
            {
                var pawn = FindAnimalById(map, id);
                if (pawn == null) return ToolExecutor.JsonError("No wild animal with id " + id + " found. Use get_wild_animals to see available animals with IDs.");
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null)
                    return ToolExecutor.JsonError("Animal '" + pawn.LabelCap + "' (id " + id + ") is already designated for hunting.");

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Hunt));
                var result = new JSONObject();
                result["success"] = true;
                result["species"] = pawn.kindDef?.label ?? "Unknown";
                result["id"] = id;
                result["gender"] = pawn.gender.ToString().ToLower();
                result["location"] = pawn.Position.x + "," + pawn.Position.z;
                result["designated_count"] = 1;
                result["action"] = "hunt";
                return result.ToString();
            }

            // Species-based targeting
            if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' or 'id' parameter required.");

            var matches = FindWildAnimals(map, animal);
            if (matches.Count == 0)
                return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use get_wild_animals to see available animals.");

            int limit = count == -1 ? matches.Count : count;
            int designated = 0;
            int alreadyDesignated = 0;
            string species = null;
            var designatedAnimals = new JSONArray();

            foreach (var pawn in matches)
            {
                if (designated >= limit) break;
                species = species ?? (pawn.kindDef?.label ?? "Unknown");
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null) { alreadyDesignated++; continue; }

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Hunt));
                var entry = new JSONObject();
                entry["id"] = pawn.thingIDNumber;
                entry["gender"] = pawn.gender.ToString().ToLower();
                entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                designatedAnimals.Add(entry);
                designated++;
            }

            if (designated == 0 && alreadyDesignated > 0)
                return ToolExecutor.JsonError("All " + alreadyDesignated + " matching animals already designated for hunting.");
            if (designated == 0)
                return ToolExecutor.JsonError("Could not designate any matching animals for hunting.");

            var result2 = new JSONObject();
            result2["success"] = true;
            result2["species"] = species;
            result2["designated_count"] = designated;
            result2["designated"] = designatedAnimals;
            result2["total_matching"] = matches.Count;
            if (alreadyDesignated > 0) result2["already_designated"] = alreadyDesignated;
            result2["action"] = "hunt";
            return result2.ToString();
        }

        /// <summary>
        /// Designate wild animals for taming.
        /// Use id for precise targeting (from get_wild_animals), or animal+count for species-based.
        /// count: 1 (default) = one animal, N = exactly N, -1 = all matching.
        /// </summary>
        public static string DesignateTame(string animal, int count = 1, int id = -1)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // ID-based targeting: find exact animal by thingIDNumber
            if (id >= 0)
            {
                var pawn = FindAnimalById(map, id);
                if (pawn == null) return ToolExecutor.JsonError("No wild animal with id " + id + " found. Use get_wild_animals to see available animals with IDs.");

                float wildness = pawn.GetStatValue(StatDefOf.Wildness);
                if (wildness > 0.98f)
                    return ToolExecutor.JsonError("Animal '" + pawn.LabelCap + "' (id " + id + ") is too wild to tame (wildness: " + (wildness * 100f).ToString("F0") + "%).");
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null)
                    return ToolExecutor.JsonError("Animal '" + pawn.LabelCap + "' (id " + id + ") is already designated for taming.");

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Tame));
                var result = new JSONObject();
                result["success"] = true;
                result["species"] = pawn.kindDef?.label ?? "Unknown";
                result["id"] = id;
                result["gender"] = pawn.gender.ToString().ToLower();
                result["location"] = pawn.Position.x + "," + pawn.Position.z;
                result["designated_count"] = 1;
                result["action"] = "tame";
                return result.ToString();
            }

            // Species-based targeting
            if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' or 'id' parameter required.");

            var matches = FindWildAnimals(map, animal);
            if (matches.Count == 0)
                return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use get_wild_animals to see available animals.");

            int limit = count == -1 ? matches.Count : count;
            int designated = 0;
            int alreadyDesignated = 0;
            int tooWild = 0;
            string species = null;
            var designatedAnimals = new JSONArray();

            foreach (var pawn in matches)
            {
                if (designated >= limit) break;
                species = species ?? (pawn.kindDef?.label ?? "Unknown");
                float wildness = pawn.GetStatValue(StatDefOf.Wildness);
                if (wildness > 0.98f) { tooWild++; continue; }
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null) { alreadyDesignated++; continue; }

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Tame));
                var entry = new JSONObject();
                entry["id"] = pawn.thingIDNumber;
                entry["gender"] = pawn.gender.ToString().ToLower();
                entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                designatedAnimals.Add(entry);
                designated++;
            }

            if (designated == 0 && alreadyDesignated > 0)
                return ToolExecutor.JsonError("All " + alreadyDesignated + " matching animals already designated for taming.");
            if (designated == 0 && tooWild > 0)
                return ToolExecutor.JsonError("All matching animals are too wild to tame.");
            if (designated == 0)
                return ToolExecutor.JsonError("Could not designate any matching animals for taming.");

            var result2 = new JSONObject();
            result2["success"] = true;
            result2["species"] = species;
            result2["designated_count"] = designated;
            result2["designated"] = designatedAnimals;
            result2["total_matching"] = matches.Count;
            if (alreadyDesignated > 0) result2["already_designated"] = alreadyDesignated;
            if (tooWild > 0) result2["too_wild"] = tooWild;
            result2["action"] = "tame";
            return result2.ToString();
        }

        public static string CancelAnimalDesignation(string animal, int id = -1)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            Pawn pawn = null;

            // ID-based lookup
            if (id >= 0)
            {
                pawn = map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.RaceProps.Animal && p.thingIDNumber == id &&
                        (map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) != null ||
                         map.designationManager.DesignationOn(p, DesignationDefOf.Tame) != null));
                if (pawn == null) return ToolExecutor.JsonError("No animal with id " + id + " with an active hunt/tame designation found.");
            }
            else
            {
                if (string.IsNullOrEmpty(animal)) return ToolExecutor.JsonError("'animal' or 'id' parameter required.");
                string lower = animal.ToLower();
                pawn = map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.RaceProps.Animal &&
                        (p.Name?.ToStringShort?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                         p.LabelShort?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                         p.kindDef?.label?.Equals(lower, StringComparison.OrdinalIgnoreCase) == true ||
                         p.LabelCap.ToString().Equals(animal, StringComparison.OrdinalIgnoreCase)) &&
                        (map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) != null ||
                         map.designationManager.DesignationOn(p, DesignationDefOf.Tame) != null));
                if (pawn == null) return ToolExecutor.JsonError("No animal matching '" + animal + "' with an active hunt/tame designation found.");
            }

            var huntDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt);
            var tameDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame);

            string action = "none";
            if (huntDes != null) { map.designationManager.RemoveDesignation(huntDes); action = "hunt"; }
            if (tameDes != null) { map.designationManager.RemoveDesignation(tameDes); action = "tame"; }

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = pawn.LabelCap.ToString();
            result["id"] = pawn.thingIDNumber;
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

        /// <summary>
        /// Find a wild animal by its unique thingIDNumber (from get_wild_animals output).
        /// </summary>
        private static Pawn FindAnimalById(Map map, int id)
        {
            return map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p.RaceProps.Animal &&
                    (p.Faction == null || !p.Faction.IsPlayer) &&
                    p.thingIDNumber == id);
        }

        /// <summary>
        /// Find ALL wild animals matching the search string.
        /// If search matches a specific named animal, returns just that one.
        /// If search matches a species/kind, returns ALL animals of that species.
        /// </summary>
        private static List<Pawn> FindWildAnimals(Map map, string search)
        {
            var wildAnimals = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal && (p.Faction == null || !p.Faction.IsPlayer))
                .ToList();

            // Exact name match first — return single animal
            var namedMatch = wildAnimals.FirstOrDefault(p =>
                p.Name?.ToStringShort?.Equals(search, StringComparison.OrdinalIgnoreCase) == true);
            if (namedMatch != null) return new List<Pawn> { namedMatch };

            // Species/kind exact match — return ALL of that species
            var speciesMatches = wildAnimals.Where(p =>
                p.kindDef?.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true ||
                p.def.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true ||
                p.LabelCap.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)).ToList();
            if (speciesMatches.Count > 0) return speciesMatches;

            // Contains match as last resort — return ALL matching
            string lower = search.ToLower();
            var containsMatches = wildAnimals.Where(p =>
                p.LabelCap.ToString().ToLower().Contains(lower) ||
                (p.kindDef?.label?.ToLower().Contains(lower) == true)).ToList();

            return containsMatches;
        }
    }
}
