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
        /// Primary: ids array for exact targeting (from get_wild_animals).
        /// Fallback: animal + all:true for designating all of a species.
        /// </summary>
        public static string DesignateHunt(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Primary approach: ids array
            var idsNode = args?["ids"];
            if (idsNode != null && idsNode.IsArray && idsNode.Count > 0)
            {
                var results = new JSONArray();
                int successCount = 0;
                int alreadyDesignated = 0;
                int notFound = 0;

                foreach (var idNode in idsNode.Children)
                {
                    int id = idNode.AsInt;
                    var pawn = FindAnimalById(map, id);
                    
                    var entry = new JSONObject();
                    entry["id"] = id;
                    
                    if (pawn == null)
                    {
                        entry["status"] = "not_found";
                        notFound++;
                    }
                    else if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null)
                    {
                        entry["status"] = "already_designated";
                        entry["species"] = pawn.kindDef?.label ?? "Unknown";
                        alreadyDesignated++;
                    }
                    else
                    {
                        map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Hunt));
                        entry["status"] = "designated";
                        entry["species"] = pawn.kindDef?.label ?? "Unknown";
                        entry["gender"] = pawn.gender.ToString().ToLower();
                        entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                        successCount++;
                    }
                    results.Add(entry);
                }

                var result = new JSONObject();
                result["success"] = successCount > 0;
                result["designated_count"] = successCount;
                result["already_designated"] = alreadyDesignated;
                result["not_found"] = notFound;
                result["results"] = results;
                result["action"] = "hunt";
                return result.ToString();
            }

            // Fallback: animal + all:true for species-based bulk targeting
            string animal = args?["animal"]?.Value;
            bool all = args?["all"]?.AsBool ?? false;

            if (string.IsNullOrEmpty(animal))
                return ToolExecutor.JsonError("Either 'ids' array or 'animal' parameter required. Use get_wild_animals to see available animals with IDs.");

            if (!all)
                return ToolExecutor.JsonError("When using 'animal' parameter, 'all' must be true. This prevents accidentally hunting random animals. Use 'ids' array to target specific animals, or set all:true to hunt ALL " + animal + " on the map.");

            var matches = FindWildAnimals(map, animal);
            if (matches.Count == 0)
                return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use get_wild_animals to see available animals.");

            int designated = 0;
            int alreadyDesignatedCount = 0;
            string species = null;
            var designatedAnimals = new JSONArray();

            foreach (var pawn in matches)
            {
                species = species ?? (pawn.kindDef?.label ?? "Unknown");
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null) { alreadyDesignatedCount++; continue; }

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Hunt));
                var entry = new JSONObject();
                entry["id"] = pawn.thingIDNumber;
                entry["gender"] = pawn.gender.ToString().ToLower();
                entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                designatedAnimals.Add(entry);
                designated++;
            }

            if (designated == 0 && alreadyDesignatedCount > 0)
                return ToolExecutor.JsonError("All " + alreadyDesignatedCount + " matching animals already designated for hunting.");
            if (designated == 0)
                return ToolExecutor.JsonError("Could not designate any matching animals for hunting.");

            var result2 = new JSONObject();
            result2["success"] = true;
            result2["species"] = species;
            result2["designated_count"] = designated;
            result2["designated"] = designatedAnimals;
            result2["total_matching"] = matches.Count;
            if (alreadyDesignatedCount > 0) result2["already_designated"] = alreadyDesignatedCount;
            result2["action"] = "hunt";
            return result2.ToString();
        }

        /// <summary>
        /// Designate wild animals for taming.
        /// Primary: ids array for exact targeting (from get_wild_animals).
        /// Fallback: animal + all:true for designating all of a species.
        /// </summary>
        public static string DesignateTame(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Primary approach: ids array
            var idsNode = args?["ids"];
            if (idsNode != null && idsNode.IsArray && idsNode.Count > 0)
            {
                var results = new JSONArray();
                int successCount = 0;
                int alreadyDesignated = 0;
                int notFound = 0;
                int tooWild = 0;

                foreach (var idNode in idsNode.Children)
                {
                    int id = idNode.AsInt;
                    var pawn = FindAnimalById(map, id);
                    
                    var entry = new JSONObject();
                    entry["id"] = id;
                    
                    if (pawn == null)
                    {
                        entry["status"] = "not_found";
                        notFound++;
                    }
                    else
                    {
                        float wildness = pawn.GetStatValue(StatDefOf.Wildness);
                        if (wildness > 0.98f)
                        {
                            entry["status"] = "too_wild";
                            entry["species"] = pawn.kindDef?.label ?? "Unknown";
                            entry["wildness"] = (wildness * 100f).ToString("F0") + "%";
                            tooWild++;
                        }
                        else if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null)
                        {
                            entry["status"] = "already_designated";
                            entry["species"] = pawn.kindDef?.label ?? "Unknown";
                            alreadyDesignated++;
                        }
                        else
                        {
                            map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Tame));
                            entry["status"] = "designated";
                            entry["species"] = pawn.kindDef?.label ?? "Unknown";
                            entry["gender"] = pawn.gender.ToString().ToLower();
                            entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                            successCount++;
                        }
                    }
                    results.Add(entry);
                }

                var result = new JSONObject();
                result["success"] = successCount > 0;
                result["designated_count"] = successCount;
                result["already_designated"] = alreadyDesignated;
                result["not_found"] = notFound;
                result["too_wild"] = tooWild;
                result["results"] = results;
                result["action"] = "tame";
                return result.ToString();
            }

            // Fallback: animal + all:true for species-based bulk targeting
            string animal = args?["animal"]?.Value;
            bool all = args?["all"]?.AsBool ?? false;

            if (string.IsNullOrEmpty(animal))
                return ToolExecutor.JsonError("Either 'ids' array or 'animal' parameter required. Use get_wild_animals to see available animals with IDs.");

            if (!all)
                return ToolExecutor.JsonError("When using 'animal' parameter, 'all' must be true. This prevents accidentally taming random animals. Use 'ids' array to target specific animals, or set all:true to tame ALL " + animal + " on the map.");

            var matches = FindWildAnimals(map, animal);
            if (matches.Count == 0)
                return ToolExecutor.JsonError("No wild animal matching '" + animal + "' found. Use get_wild_animals to see available animals.");

            int designated = 0;
            int alreadyDesignatedCount = 0;
            int tooWildCount = 0;
            string species = null;
            var designatedAnimals = new JSONArray();

            foreach (var pawn in matches)
            {
                species = species ?? (pawn.kindDef?.label ?? "Unknown");
                float wildness = pawn.GetStatValue(StatDefOf.Wildness);
                if (wildness > 0.98f) { tooWildCount++; continue; }
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null) { alreadyDesignatedCount++; continue; }

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Tame));
                var entry = new JSONObject();
                entry["id"] = pawn.thingIDNumber;
                entry["gender"] = pawn.gender.ToString().ToLower();
                entry["location"] = pawn.Position.x + "," + pawn.Position.z;
                designatedAnimals.Add(entry);
                designated++;
            }

            if (designated == 0 && alreadyDesignatedCount > 0)
                return ToolExecutor.JsonError("All " + alreadyDesignatedCount + " matching animals already designated for taming.");
            if (designated == 0 && tooWildCount > 0)
                return ToolExecutor.JsonError("All matching animals are too wild to tame.");
            if (designated == 0)
                return ToolExecutor.JsonError("Could not designate any matching animals for taming.");

            var result2 = new JSONObject();
            result2["success"] = true;
            result2["species"] = species;
            result2["designated_count"] = designated;
            result2["designated"] = designatedAnimals;
            result2["total_matching"] = matches.Count;
            if (alreadyDesignatedCount > 0) result2["already_designated"] = alreadyDesignatedCount;
            if (tooWildCount > 0) result2["too_wild"] = tooWildCount;
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
                         map.designationManager.DesignationOn(p, DesignationDefOf.Tame) != null ||
                         map.designationManager.DesignationOn(p, DesignationDefOf.Slaughter) != null));
                if (pawn == null) return ToolExecutor.JsonError("No animal with id " + id + " with an active hunt/tame/slaughter designation found.");
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
                         map.designationManager.DesignationOn(p, DesignationDefOf.Tame) != null ||
                         map.designationManager.DesignationOn(p, DesignationDefOf.Slaughter) != null));
                if (pawn == null) return ToolExecutor.JsonError("No animal matching '" + animal + "' with an active hunt/tame/slaughter designation found.");
            }

            var huntDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt);
            var tameDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame);
            var slaughterDes = map.designationManager.DesignationOn(pawn, DesignationDefOf.Slaughter);

            string action = "none";
            if (huntDes != null) { map.designationManager.RemoveDesignation(huntDes); action = "hunt"; }
            if (tameDes != null) { map.designationManager.RemoveDesignation(tameDes); action = "tame"; }
            if (slaughterDes != null) { map.designationManager.RemoveDesignation(slaughterDes); action = "slaughter"; }

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
        /// Designate tamed animals for slaughter.
        /// Only works on colony-owned animals (Faction == Player).
        /// Primary: ids array for exact targeting (from list_animals).
        /// Fallback: animal + all:true for designating all of a species.
        /// </summary>
        public static string DesignateSlaughter(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Primary approach: ids array
            var idsNode = args?["ids"];
            if (idsNode != null && idsNode.IsArray && idsNode.Count > 0)
            {
                var results = new JSONArray();
                int successCount = 0;
                int alreadyDesignated = 0;
                int notFound = 0;
                int totalMeatYield = 0;

                foreach (var idNode in idsNode.Children)
                {
                    int id = idNode.AsInt;
                    var pawn = FindTamedAnimalById(map, id);
                    
                    var entry = new JSONObject();
                    entry["id"] = id;
                    
                    if (pawn == null)
                    {
                        entry["status"] = "not_found";
                        notFound++;
                    }
                    else if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Slaughter) != null)
                    {
                        entry["status"] = "already_designated";
                        entry["species"] = pawn.kindDef?.label ?? "Unknown";
                        entry["name"] = pawn.LabelCap.ToString();
                        alreadyDesignated++;
                    }
                    else
                    {
                        map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Slaughter));
                        int meatYield = (int)Math.Round(pawn.GetStatValue(StatDefOf.MeatAmount));
                        totalMeatYield += meatYield;
                        entry["status"] = "designated";
                        entry["species"] = pawn.kindDef?.label ?? "Unknown";
                        entry["name"] = pawn.LabelCap.ToString();
                        entry["meat_yield"] = meatYield;
                        successCount++;
                    }
                    results.Add(entry);
                }

                var result = new JSONObject();
                result["success"] = successCount > 0;
                result["designated_count"] = successCount;
                result["already_designated"] = alreadyDesignated;
                result["not_found"] = notFound;
                result["total_meat_yield"] = totalMeatYield;
                result["results"] = results;
                result["action"] = "slaughter";
                return result.ToString();
            }

            // Fallback: animal + all:true for species-based bulk targeting
            string animal = args?["animal"]?.Value;
            bool all = args?["all"]?.AsBool ?? false;

            if (string.IsNullOrEmpty(animal))
                return ToolExecutor.JsonError("Either 'ids' array or 'animal' parameter required. Use list_animals to see colony animals with IDs.");

            if (!all)
                return ToolExecutor.JsonError("When using 'animal' parameter, 'all' must be true. This prevents accidentally slaughtering random animals. Use 'ids' array to target specific animals, or set all:true to slaughter ALL " + animal + " in the colony.");

            var matches = FindTamedAnimals(map, animal);
            if (matches.Count == 0)
                return ToolExecutor.JsonError("No tamed animal matching '" + animal + "' found. Use list_animals to see colony animals. Only tamed animals can be slaughtered (wild animals should be hunted instead).");

            int designated = 0;
            int alreadyDesignatedCount = 0;
            string species = null;
            int totalMeat = 0;
            var designatedAnimals = new JSONArray();

            foreach (var pawn in matches)
            {
                species = species ?? (pawn.kindDef?.label ?? "Unknown");
                if (map.designationManager.DesignationOn(pawn, DesignationDefOf.Slaughter) != null) { alreadyDesignatedCount++; continue; }

                map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Slaughter));
                int meatYield = (int)Math.Round(pawn.GetStatValue(StatDefOf.MeatAmount));
                totalMeat += meatYield;
                var entry = new JSONObject();
                entry["id"] = pawn.thingIDNumber;
                entry["name"] = pawn.LabelCap.ToString();
                entry["meat_yield"] = meatYield;
                designatedAnimals.Add(entry);
                designated++;
            }

            if (designated == 0 && alreadyDesignatedCount > 0)
                return ToolExecutor.JsonError("All " + alreadyDesignatedCount + " matching animals already designated for slaughter.");
            if (designated == 0)
                return ToolExecutor.JsonError("Could not designate any matching animals for slaughter.");

            var result2 = new JSONObject();
            result2["success"] = true;
            result2["species"] = species;
            result2["designated_count"] = designated;
            result2["designated"] = designatedAnimals;
            result2["total_matching"] = matches.Count;
            result2["total_meat_yield"] = totalMeat;
            if (alreadyDesignatedCount > 0) result2["already_designated"] = alreadyDesignatedCount;
            result2["action"] = "slaughter";
            return result2.ToString();
        }

        /// <summary>
        /// Find a tamed animal by its unique thingIDNumber.
        /// </summary>
        private static Pawn FindTamedAnimalById(Map map, int id)
        {
            return map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p.RaceProps.Animal &&
                    p.Faction == Faction.OfPlayer &&
                    p.thingIDNumber == id);
        }

        /// <summary>
        /// Find ALL tamed animals matching the search string.
        /// If search matches a specific named animal, returns just that one.
        /// If search matches a species/kind, returns ALL animals of that species.
        /// </summary>
        private static List<Pawn> FindTamedAnimals(Map map, string search)
        {
            var tamedAnimals = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer)
                .ToList();

            // Exact name match first — return single animal
            var namedMatch = tamedAnimals.FirstOrDefault(p =>
                p.Name?.ToStringShort?.Equals(search, StringComparison.OrdinalIgnoreCase) == true);
            if (namedMatch != null) return new List<Pawn> { namedMatch };

            // Species/kind exact match — return ALL of that species
            var speciesMatches = tamedAnimals.Where(p =>
                p.kindDef?.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true ||
                p.def.label?.Equals(search, StringComparison.OrdinalIgnoreCase) == true ||
                p.LabelCap.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)).ToList();
            if (speciesMatches.Count > 0) return speciesMatches;

            // Contains match as last resort — return ALL matching
            string lower = search.ToLower();
            var containsMatches = tamedAnimals.Where(p =>
                p.LabelCap.ToString().ToLower().Contains(lower) ||
                (p.kindDef?.label?.ToLower().Contains(lower) == true)).ToList();

            return containsMatches;
        }

        /// <summary>
        /// Query all active animal designations (hunt, tame, slaughter).
        /// </summary>
        public static string GetAnimalDesignations(string type = "all")
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var results = new JSONArray();
            int count = 0;

            foreach (var des in map.designationManager.AllDesignations)
            {
                var pawn = des.target.Thing as Pawn;
                if (pawn == null || !pawn.RaceProps.Animal) continue;

                string desType = null;
                if (des.def == DesignationDefOf.Hunt) desType = "hunt";
                else if (des.def == DesignationDefOf.Tame) desType = "tame";
                else if (des.def == DesignationDefOf.Slaughter) desType = "slaughter";
                else continue;

                if (type != "all" && desType != type) continue;

                var entry = new JSONObject();
                entry["animal_name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                entry["species"] = pawn.kindDef?.label ?? "Unknown";
                entry["id"] = pawn.thingIDNumber;
                entry["gender"] = pawn.gender.ToString().ToLower();
                entry["x"] = pawn.Position.x;
                entry["z"] = pawn.Position.z;
                entry["designation"] = desType;
                entry["faction"] = pawn.Faction == Faction.OfPlayer ? "tamed" : "wild";
                results.Add(entry);
                count++;
            }

            var result = new JSONObject();
            result["total"] = count;
            result["filter"] = type;
            result["designations"] = results;
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
