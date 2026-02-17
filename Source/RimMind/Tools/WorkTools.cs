using System.Collections.Generic;
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

        public static string SetWorkPriority(string colonistName, string workType, int priority)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(workType)) return ToolExecutor.JsonError("workType parameter required.");
            if (priority < 0 || priority > 4) return ToolExecutor.JsonError("priority must be 0-4 (0=disabled, 1=highest, 4=lowest).");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.workSettings == null) return ToolExecutor.JsonError("Colonist cannot perform work.");

            // Find work type by matching defName or label
            string workTypeLower = workType.ToLower();
            var workTypeDef = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .FirstOrDefault(w =>
                    w.defName.ToLower() == workTypeLower ||
                    (w.labelShort?.ToLower() == workTypeLower) ||
                    (w.label?.ToLower() == workTypeLower) ||
                    (w.gerundLabel?.ToLower() == workTypeLower));

            if (workTypeDef == null)
            {
                return ToolExecutor.JsonError("Work type '" + workType + "' not found. Available types: " +
                    string.Join(", ", DefDatabase<WorkTypeDef>.AllDefsListForReading.Select(w => w.labelShort ?? w.defName).Take(10)));
            }

            if (pawn.WorkTypeIsDisabled(workTypeDef))
                return priority > 0
                    ? ToolExecutor.JsonError(colonistName + " cannot do " + workTypeDef.labelShort + " (disabled by traits/backstory).")
                    : ToolExecutor.JsonError(workTypeDef.labelShort + " is already disabled for " + colonistName + " by traits/backstory.");

            if (!Current.Game.playSettings.useWorkPriorities)
                Current.Game.playSettings.useWorkPriorities = true;

            pawn.workSettings.SetPriority(workTypeDef, priority);

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["workType"] = workTypeDef.labelShort ?? workTypeDef.defName;
            result["priority"] = priority;
            return result.ToString();
        }

        public static string SetSchedule(string colonistName, int hour, string assignment)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (hour < 0 || hour > 23) return ToolExecutor.JsonError("hour must be 0-23.");
            if (string.IsNullOrEmpty(assignment)) return ToolExecutor.JsonError("assignment parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.timetable == null) return ToolExecutor.JsonError("Colonist has no schedule.");

            // Find assignment by matching defName or label
            string assignmentLower = assignment.ToLower();
            var assignmentDef = DefDatabase<TimeAssignmentDef>.AllDefsListForReading
                .FirstOrDefault(a =>
                    a.defName.ToLower() == assignmentLower ||
                    (a.label?.ToLower() == assignmentLower));

            if (assignmentDef == null)
            {
                return ToolExecutor.JsonError("Assignment '" + assignment + "' not found. Available: " +
                    string.Join(", ", DefDatabase<TimeAssignmentDef>.AllDefsListForReading.Select(a => a.label ?? a.defName)));
            }

            pawn.timetable.SetAssignment(hour, assignmentDef);

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["hour"] = hour;
            result["assignment"] = assignmentDef.label ?? assignmentDef.defName;
            return result.ToString();
        }

        public static string CopySchedule(string fromName, string toName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(fromName)) return ToolExecutor.JsonError("from parameter required.");
            if (string.IsNullOrEmpty(toName)) return ToolExecutor.JsonError("to parameter required.");

            var fromPawn = ColonistTools.FindPawnByName(fromName);
            if (fromPawn == null) return ToolExecutor.JsonError("Source colonist '" + fromName + "' not found.");

            var toPawn = ColonistTools.FindPawnByName(toName);
            if (toPawn == null) return ToolExecutor.JsonError("Target colonist '" + toName + "' not found.");

            if (fromPawn.timetable == null) return ToolExecutor.JsonError("Source colonist has no schedule.");
            if (toPawn.timetable == null) return ToolExecutor.JsonError("Target colonist has no schedule.");

            for (int hour = 0; hour < 24; hour++)
            {
                var assignment = fromPawn.timetable.GetAssignment(hour);
                toPawn.timetable.SetAssignment(hour, assignment);
            }

            var result = new JSONObject();
            result["success"] = true;
            result["from"] = fromPawn.Name?.ToStringShort ?? "Unknown";
            result["to"] = toPawn.Name?.ToStringShort ?? "Unknown";
            result["message"] = "Schedule copied successfully";
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

        public static string CreateBill(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string recipeName = args?["recipe"]?.Value;
            if (string.IsNullOrEmpty(recipeName))
                return ToolExecutor.JsonError("'recipe' parameter required.");

            string workbenchName = args?["workbench"]?.Value;
            if (string.IsNullOrEmpty(workbenchName))
                return ToolExecutor.JsonError("'workbench' parameter required.");

            // Find the workbench
            var workbench = FindWorkbench(workbenchName);
            if (workbench == null)
            {
                string suggestions = FindSimilarWorkbenches(workbenchName);
                string msg = "Workbench '" + workbenchName + "' not found.";
                if (suggestions != null)
                    msg += " Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            // Find the recipe
            var recipe = ResolveRecipe(recipeName, workbench);
            if (recipe == null)
            {
                string suggestions = FindSimilarRecipes(recipeName, workbench);
                string msg = "Recipe '" + recipeName + "' not found or not available at " + workbench.LabelCap + ".";
                if (suggestions != null)
                    msg += " Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            // Create the bill
            Bill bill;
            if (recipe.products != null && recipe.products.Count > 0 && recipe.products[0].count > 0)
            {
                // Bill_Production for countable products
                var prodBill = new Bill_Production(recipe);

                // Set target count
                int targetCount = args?["count"]?.AsInt ?? 1;
                bool forever = args?["forever"]?.AsBool ?? false;

                if (forever)
                {
                    prodBill.repeatMode = BillRepeatModeDefOf.Forever;
                }
                else
                {
                    if (targetCount < 1)
                        return ToolExecutor.JsonError("'count' must be at least 1 (or set 'forever' to true).");
                    prodBill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    prodBill.repeatCount = targetCount;
                }

                bill = prodBill;
            }
            else
            {
                // Simple bill for uncountable recipes (butchering, smelting, etc.)
                bill = new Bill_Production(recipe);
            }

            // Set ingredient search radius
            int ingredientRadius = args?["ingredientRadius"]?.AsInt ?? 999;
            bill.ingredientSearchRadius = ingredientRadius;

            // Set skill requirement
            int minSkill = args?["minSkill"]?.AsInt ?? 0;
            if (minSkill > 0 && minSkill <= 20)
            {
                bill.allowedSkillRange = new IntRange(minSkill, 20);
            }

            // Paused state
            bool paused = args?["paused"]?.AsBool ?? false;
            bill.suspended = paused;

            // Add the bill to the workbench
            workbench.BillStack.AddBill(bill);

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["recipe"] = recipe.LabelCap.ToString();
            result["billIndex"] = workbench.BillStack.Bills.IndexOf(bill);

            if (bill is Bill_Production prod2)
            {
                result["targetCount"] = prod2.repeatCount;
                result["repeatMode"] = prod2.repeatMode?.LabelCap.ToString() ?? "Unknown";
            }

            result["suspended"] = bill.suspended;
            result["ingredientRadius"] = (int)bill.ingredientSearchRadius;

            return result.ToString();
        }

        public static string ModifyBill(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string workbenchName = args?["workbench"]?.Value;
            if (string.IsNullOrEmpty(workbenchName))
                return ToolExecutor.JsonError("'workbench' parameter required.");

            string recipeName = args?["recipe"]?.Value;
            int billIndex = args?["index"]?.AsInt ?? -1;

            if (string.IsNullOrEmpty(recipeName) && billIndex < 0)
                return ToolExecutor.JsonError("Either 'recipe' or 'index' parameter required to identify the bill.");

            // Find the workbench
            var workbench = FindWorkbench(workbenchName);
            if (workbench == null)
            {
                string suggestions = FindSimilarWorkbenches(workbenchName);
                string msg = "Workbench '" + workbenchName + "' not found.";
                if (suggestions != null)
                    msg += " Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            // Find the bill
            Bill targetBill = null;
            if (billIndex >= 0)
            {
                if (billIndex >= workbench.BillStack.Bills.Count)
                    return ToolExecutor.JsonError("Bill index " + billIndex + " out of range. Workbench has " + workbench.BillStack.Bills.Count + " bills.");
                targetBill = workbench.BillStack.Bills[billIndex];
            }
            else
            {
                // Find by recipe name
                string recipeLower = recipeName.ToLower();
                foreach (var bill in workbench.BillStack.Bills)
                {
                    if (bill.recipe == null) continue;
                    if (bill.recipe.defName.ToLower() == recipeLower ||
                        bill.recipe.label?.ToLower() == recipeLower)
                    {
                        targetBill = bill;
                        break;
                    }
                }

                if (targetBill == null)
                    return ToolExecutor.JsonError("Bill for recipe '" + recipeName + "' not found at " + workbench.LabelCap + ".");
            }

            // Apply modifications
            bool modified = false;

            // Pause/resume
            if (args["paused"] != null)
            {
                targetBill.suspended = args["paused"].AsBool;
                modified = true;
            }

            // Change target count
            if (args["count"] != null && targetBill is Bill_Production prodBill)
            {
                int newCount = args["count"].AsInt;
                if (newCount < 1)
                    return ToolExecutor.JsonError("'count' must be at least 1.");
                prodBill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                prodBill.repeatCount = newCount;
                modified = true;
            }

            // Set to forever
            if (args["forever"]?.AsBool == true && targetBill is Bill_Production prodBill2)
            {
                prodBill2.repeatMode = BillRepeatModeDefOf.Forever;
                modified = true;
            }

            // Ingredient radius
            if (args["ingredientRadius"] != null)
            {
                int radius = args["ingredientRadius"].AsInt;
                targetBill.ingredientSearchRadius = radius;
                modified = true;
            }

            // Skill requirement
            if (args["minSkill"] != null)
            {
                int minSkill = args["minSkill"].AsInt;
                if (minSkill >= 0 && minSkill <= 20)
                {
                    targetBill.allowedSkillRange = new IntRange(minSkill, 20);
                    modified = true;
                }
            }

            if (!modified)
                return ToolExecutor.JsonError("No valid modifications specified. Use 'paused', 'count', 'forever', 'ingredientRadius', or 'minSkill'.");

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["recipe"] = targetBill.recipe?.LabelCap.ToString() ?? "Unknown";
            result["billIndex"] = workbench.BillStack.Bills.IndexOf(targetBill);
            result["suspended"] = targetBill.suspended;

            if (targetBill is Bill_Production prod)
            {
                result["targetCount"] = prod.repeatCount;
                result["repeatMode"] = prod.repeatMode?.LabelCap.ToString() ?? "Unknown";
            }

            result["ingredientRadius"] = (int)targetBill.ingredientSearchRadius;

            return result.ToString();
        }

        public static string DeleteBill(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string workbenchName = args?["workbench"]?.Value;
            if (string.IsNullOrEmpty(workbenchName))
                return ToolExecutor.JsonError("'workbench' parameter required.");

            string recipeName = args?["recipe"]?.Value;
            int billIndex = args?["index"]?.AsInt ?? -1;

            if (string.IsNullOrEmpty(recipeName) && billIndex < 0)
                return ToolExecutor.JsonError("Either 'recipe' or 'index' parameter required to identify the bill.");

            // Find the workbench
            var workbench = FindWorkbench(workbenchName);
            if (workbench == null)
            {
                string suggestions = FindSimilarWorkbenches(workbenchName);
                string msg = "Workbench '" + workbenchName + "' not found.";
                if (suggestions != null)
                    msg += " Did you mean: " + suggestions + "?";
                return ToolExecutor.JsonError(msg);
            }

            // Find the bill
            Bill targetBill = null;
            if (billIndex >= 0)
            {
                if (billIndex >= workbench.BillStack.Bills.Count)
                    return ToolExecutor.JsonError("Bill index " + billIndex + " out of range. Workbench has " + workbench.BillStack.Bills.Count + " bills.");
                targetBill = workbench.BillStack.Bills[billIndex];
            }
            else
            {
                // Find by recipe name
                string recipeLower = recipeName.ToLower();
                foreach (var bill in workbench.BillStack.Bills)
                {
                    if (bill.recipe == null) continue;
                    if (bill.recipe.defName.ToLower() == recipeLower ||
                        bill.recipe.label?.ToLower() == recipeLower)
                    {
                        targetBill = bill;
                        break;
                    }
                }

                if (targetBill == null)
                    return ToolExecutor.JsonError("Bill for recipe '" + recipeName + "' not found at " + workbench.LabelCap + ".");
            }

            string deletedRecipe = targetBill.recipe?.LabelCap.ToString() ?? "Unknown";
            int deletedIndex = workbench.BillStack.Bills.IndexOf(targetBill);

            // Delete the bill
            workbench.BillStack.Delete(targetBill);

            var result = new JSONObject();
            result["success"] = true;
            result["workbench"] = workbench.LabelCap.ToString();
            result["deletedRecipe"] = deletedRecipe;
            result["deletedIndex"] = deletedIndex;
            result["remainingBills"] = workbench.BillStack.Bills.Count;

            return result.ToString();
        }

        // Helper methods for workbench and recipe resolution

        private static Building_WorkTable FindWorkbench(string name)
        {
            var map = Find.CurrentMap;
            if (map == null) return null;

            string nameLower = name.ToLower();

            // Try exact match first
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var workTable = building as Building_WorkTable;
                if (workTable == null) continue;

                if (workTable.def.defName.ToLower() == nameLower ||
                    workTable.LabelCap.ToString().ToLower() == nameLower)
                    return workTable;
            }

            // Try fuzzy match
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var workTable = building as Building_WorkTable;
                if (workTable == null) continue;

                if (workTable.def.defName.ToLower().Contains(nameLower) ||
                    workTable.LabelCap.ToString().ToLower().Contains(nameLower))
                    return workTable;
            }

            return null;
        }

        private static string FindSimilarWorkbenches(string name)
        {
            var map = Find.CurrentMap;
            if (map == null || string.IsNullOrEmpty(name)) return null;

            var matches = new List<string>();
            string nameLower = name.ToLower();

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var workTable = building as Building_WorkTable;
                if (workTable == null) continue;

                if (workTable.def.defName.ToLower().Contains(nameLower) ||
                    workTable.LabelCap.ToString().ToLower().Contains(nameLower))
                {
                    matches.Add(workTable.LabelCap.ToString());
                    if (matches.Count >= 3) break;
                }
            }

            return matches.Count > 0 ? string.Join(", ", matches) : null;
        }

        private static RecipeDef ResolveRecipe(string name, Building_WorkTable workbench)
        {
            if (string.IsNullOrEmpty(name) || workbench == null) return null;

            string nameLower = name.ToLower();

            // Get available recipes for this workbench
            var availableRecipes = workbench.def.AllRecipes;
            if (availableRecipes == null) return null;

            // Try exact defName match
            foreach (var recipe in availableRecipes)
            {
                if (recipe.defName.ToLower() == nameLower)
                    return recipe;
            }

            // Try exact label match
            foreach (var recipe in availableRecipes)
            {
                if (recipe.label?.ToLower() == nameLower)
                    return recipe;
            }

            // Try fuzzy match
            foreach (var recipe in availableRecipes)
            {
                if (recipe.defName.ToLower().Contains(nameLower) ||
                    (recipe.label?.ToLower().Contains(nameLower) ?? false))
                    return recipe;
            }

            return null;
        }

        private static string FindSimilarRecipes(string name, Building_WorkTable workbench)
        {
            if (string.IsNullOrEmpty(name) || workbench == null) return null;

            var matches = new List<string>();
            string nameLower = name.ToLower();

            var availableRecipes = workbench.def.AllRecipes;
            if (availableRecipes == null) return null;

            foreach (var recipe in availableRecipes)
            {
                if (recipe.defName.ToLower().Contains(nameLower) ||
                    (recipe.label?.ToLower().Contains(nameLower) ?? false))
                {
                    matches.Add(recipe.label ?? recipe.defName);
                    if (matches.Count >= 3) break;
                }
            }

            return matches.Count > 0 ? string.Join(", ", matches) : null;
        }
    }
}
