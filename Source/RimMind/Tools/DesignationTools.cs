using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class DesignationTools
    {
        public static string DesignateHunt(int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var animal = cell.GetThingList(map).OfType<Pawn>().FirstOrDefault(p => p.RaceProps.Animal && (p.Faction == null || !p.Faction.IsPlayer));
            if (animal == null) return ToolExecutor.JsonError("No wild animal found at " + x + "," + z);

            if (map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt) != null)
                return ToolExecutor.JsonError("Animal already designated for hunting.");

            map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = animal.LabelCap.ToString();
            result["location"] = x + "," + z;
            result["action"] = "hunt";
            return result.ToString();
        }

        public static string DesignateTame(int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var animal = cell.GetThingList(map).OfType<Pawn>().FirstOrDefault(p => p.RaceProps.Animal && (p.Faction == null || !p.Faction.IsPlayer));
            if (animal == null) return ToolExecutor.JsonError("No wild animal found at " + x + "," + z);

            float wildness = animal.GetStatValue(StatDefOf.Wildness);
            if (wildness > 0.98f)
                return ToolExecutor.JsonError("Animal is too wild to tame (wildness: " + wildness.ToString("P0") + ").");

            if (map.designationManager.DesignationOn(animal, DesignationDefOf.Tame) != null)
                return ToolExecutor.JsonError("Animal already designated for taming.");

            map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Tame));

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = animal.LabelCap.ToString();
            result["location"] = x + "," + z;
            result["wildness"] = wildness.ToString("P0");
            result["action"] = "tame";
            return result.ToString();
        }

        public static string CancelAnimalDesignation(int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var animal = cell.GetThingList(map).OfType<Pawn>().FirstOrDefault(p => p.RaceProps.Animal);
            if (animal == null) return ToolExecutor.JsonError("No animal found at " + x + "," + z);

            var huntDes = map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt);
            var tameDes = map.designationManager.DesignationOn(animal, DesignationDefOf.Tame);

            if (huntDes == null && tameDes == null)
                return ToolExecutor.JsonError("Animal has no hunt or tame designation.");

            string action = "none";
            if (huntDes != null) { map.designationManager.RemoveDesignation(huntDes); action = "hunt"; }
            if (tameDes != null) { map.designationManager.RemoveDesignation(tameDes); action = "tame"; }

            var result = new JSONObject();
            result["success"] = true;
            result["animal"] = animal.LabelCap.ToString();
            result["cancelledAction"] = action;
            return result.ToString();
        }

        public static string DesignateMine(int x1, int z1, int x2, int z2)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var rect = new CellRect(x1, z1, x2 - x1 + 1, z2 - z1 + 1);
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

            var rect = new CellRect(x1, z1, x2 - x1 + 1, z2 - z1 + 1);
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

            var rect = new CellRect(x1, z1, x2 - x1 + 1, z2 - z1 + 1);
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
    }
}
