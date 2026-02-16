using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class WorkTools
    {
        public static string GetWorkPriorities()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            var arr = new JSONArray();

            foreach (var pawn in colonists)
            {
                if (pawn.workSettings == null) continue;

                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                var priorities = new JSONObject();
                foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading.OrderByDescending(w => w.naturalPriority))
                {
                    if (pawn.WorkTypeIsDisabled(workType))
                        priorities[workType.labelShort ?? workType.defName] = "disabled";
                    else
                        priorities[workType.labelShort ?? workType.defName] = pawn.workSettings.GetPriority(workType);
                }
                obj["priorities"] = priorities;
                arr.Add(obj);
            }

            var result = new JSONObject();
            result["colonistPriorities"] = arr;
            return result.ToString();
        }

        public static string GetBills(string workbenchFilter)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();
            string filterLower = workbenchFilter?.ToLower();

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var workTable = building as Building_WorkTable;
                if (workTable == null) continue;
                if (filterLower != null && !workTable.LabelCap.ToString().ToLower().Contains(filterLower)) continue;

                int index = 0;
                foreach (var bill in workTable.BillStack.Bills)
                {
                    var obj = new JSONObject();
                    obj["billIndex"] = index++;
                    obj["workbench"] = workTable.LabelCap.ToString();
                    obj["recipe"] = bill.recipe?.LabelCap.ToString() ?? "Unknown";
                    obj["suspended"] = bill.suspended;

                    if (bill is Bill_Production prod)
                    {
                        obj["targetCount"] = prod.repeatCount;
                        obj["repeatMode"] = prod.repeatMode?.LabelCap.ToString() ?? "Unknown";
                    }

                    if (bill.PawnRestriction != null)
                        obj["assignedWorker"] = bill.PawnRestriction.Name?.ToStringShort;

                    arr.Add(obj);
                }
            }

            var result = new JSONObject();
            result["bills"] = arr;
            result["totalBills"] = arr.Count;
            return result.ToString();
        }

        public static string GetSchedules()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.timetable == null) continue;

                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                var schedule = new JSONArray();
                for (int hour = 0; hour < 24; hour++)
                {
                    var assignment = pawn.timetable.GetAssignment(hour);
                    schedule.Add(assignment?.LabelCap.ToString() ?? "Anything");
                }
                obj["hourlySchedule"] = schedule;
                arr.Add(obj);
            }

            var result = new JSONObject();
            result["schedules"] = arr;
            return result.ToString();
        }

        public static string ListRecipes(string workbenchName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(workbenchName)) return ToolExecutor.JsonError("workbench parameter required.");

            string benchLower = workbenchName.ToLower();
            var workbench = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.LabelCap.ToString().ToLower().Contains(benchLower));

            if (workbench == null)
                return ToolExecutor.JsonError("Workbench '" + workbenchName + "' not found.");

            var recipes = workbench.def.AllRecipes ?? new System.Collections.Generic.List<RecipeDef>();
            var arr = new JSONArray();

            foreach (var recipe in recipes.Where(r => r.AvailableNow))
            {
                var obj = new JSONObject();
                obj["name"] = recipe.LabelCap.ToString();
                obj["defName"] = recipe.defName;
                obj["description"] = recipe.description ?? "";
                
                if (recipe.products != null && recipe.products.Any())
                {
                    var products = new JSONArray();
                    foreach (var prod in recipe.products)
                        products.Add(prod.thingDef.LabelCap.ToString() + " x" + prod.count);
                    obj["products"] = products;
                }

                if (recipe.ingredients != null && recipe.ingredients.Any())
                {
                    var ingredients = new JSONArray();
                    foreach (var ing in recipe.ingredients)
                        ingredients.Add(ing.Summary);
                    obj["ingredients"] = ingredients;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["workbench"] = workbench.LabelCap.ToString();
            result["recipes"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string CreateBill(string workbenchName, string recipeName, int count)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(workbenchName)) return ToolExecutor.JsonError("workbench parameter required.");
            if (string.IsNullOrEmpty(recipeName)) return ToolExecutor.JsonError("recipe parameter required.");
            if (count < 1) return ToolExecutor.JsonError("count must be at least 1.");

            string benchLower = workbenchName.ToLower();
            var workbench = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.LabelCap.ToString().ToLower().Contains(benchLower));

            if (workbench == null)
                return ToolExecutor.JsonError("Workbench '" + workbenchName + "' not found.");

            string recipeLower = recipeName.ToLower();
            var recipe = (workbench.def.AllRecipes ?? new System.Collections.Generic.List<RecipeDef>())
                .FirstOrDefault(r => 
                    r.LabelCap.ToString().ToLower().Contains(recipeLower) ||
                    r.defName.ToLower() == recipeLower);

            if (recipe == null)
                return ToolExecutor.JsonError("Recipe '" + recipeName + "' not found for this workbench.");

            var bill = recipe.MakeNewBill();
            if (bill is Bill_Production prodBill)
            {
                prodBill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                prodBill.repeatCount = count;
            }

            workbench.BillStack.AddBill(bill);

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["recipe"] = recipe.LabelCap.ToString();
            result["count"] = count;
            result["billIndex"] = workbench.BillStack.Bills.Count - 1;
            return result.ToString();
        }

        public static string SuspendBill(string workbenchName, int billIndex)
        {
            return SetBillSuspended(workbenchName, billIndex, true);
        }

        public static string ResumeBill(string workbenchName, int billIndex)
        {
            return SetBillSuspended(workbenchName, billIndex, false);
        }

        public static string DeleteBill(string workbenchName, int billIndex)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(workbenchName)) return ToolExecutor.JsonError("workbench parameter required.");

            string benchLower = workbenchName.ToLower();
            var workbench = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.LabelCap.ToString().ToLower().Contains(benchLower));

            if (workbench == null)
                return ToolExecutor.JsonError("Workbench '" + workbenchName + "' not found.");

            if (billIndex < 0 || billIndex >= workbench.BillStack.Bills.Count)
                return ToolExecutor.JsonError("Bill index out of range (0-" + (workbench.BillStack.Bills.Count - 1) + ").");

            var bill = workbench.BillStack.Bills[billIndex];
            string recipeName = bill.recipe?.LabelCap.ToString() ?? "Unknown";

            workbench.BillStack.Delete(bill);

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["deletedRecipe"] = recipeName;
            result["remainingBills"] = workbench.BillStack.Bills.Count;
            return result.ToString();
        }

        private static string SetBillSuspended(string workbenchName, int billIndex, bool suspended)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(workbenchName)) return ToolExecutor.JsonError("workbench parameter required.");

            string benchLower = workbenchName.ToLower();
            var workbench = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.LabelCap.ToString().ToLower().Contains(benchLower));

            if (workbench == null)
                return ToolExecutor.JsonError("Workbench '" + workbenchName + "' not found.");

            if (billIndex < 0 || billIndex >= workbench.BillStack.Bills.Count)
                return ToolExecutor.JsonError("Bill index out of range (0-" + (workbench.BillStack.Bills.Count - 1) + ").");

            var bill = workbench.BillStack.Bills[billIndex];
            bill.suspended = suspended;

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["recipe"] = bill.recipe?.LabelCap.ToString() ?? "Unknown";
            result["suspended"] = suspended;
            return result.ToString();
        }
    }
}
