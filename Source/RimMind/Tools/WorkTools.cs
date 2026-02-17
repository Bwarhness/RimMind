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

                foreach (var bill in workTable.BillStack.Bills)
                {
                    var obj = new JSONObject();
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
    }
}
