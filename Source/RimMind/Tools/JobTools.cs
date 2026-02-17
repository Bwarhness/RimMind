using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class JobTools
    {
        public static string PrioritizeRescue(string colonistName, string targetName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(targetName)) return ToolExecutor.JsonError("target parameter required.");

            var colonist = ColonistTools.FindPawnByName(colonistName);
            if (colonist == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var target = FindPawnByNameInMap(targetName, map);
            if (target == null) return ToolExecutor.JsonError("Target pawn '" + targetName + "' not found.");

            if (!target.Downed) return ToolExecutor.JsonError("Target is not downed.");

            var bed = RestUtility.FindBedFor(target, colonist, false, false);
            if (bed == null) return ToolExecutor.JsonError("No available bed found for rescue target.");
            var job = JobMaker.MakeJob(JobDefOf.Rescue, target, bed);
            if (colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
                result["target"] = target.Name?.ToStringShort ?? "Unknown";
                result["action"] = "rescue";
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign rescue job.");
        }

        public static string PrioritizeTend(string doctorName, string patientName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(doctorName)) return ToolExecutor.JsonError("doctor parameter required.");
            if (string.IsNullOrEmpty(patientName)) return ToolExecutor.JsonError("patient parameter required.");

            var doctor = ColonistTools.FindPawnByName(doctorName);
            if (doctor == null) return ToolExecutor.JsonError("Doctor '" + doctorName + "' not found.");

            var patient = FindPawnByNameInMap(patientName, map);
            if (patient == null) return ToolExecutor.JsonError("Patient '" + patientName + "' not found.");

            if (!patient.health.HasHediffsNeedingTend()) 
                return ToolExecutor.JsonError("Patient does not need tending.");

            var job = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            if (doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["doctor"] = doctor.Name?.ToStringShort ?? "Unknown";
                result["patient"] = patient.Name?.ToStringShort ?? "Unknown";
                result["action"] = "tend";
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign tend job.");
        }

        public static string PrioritizeHaul(string colonistName, int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var colonist = ColonistTools.FindPawnByName(colonistName);
            if (colonist == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var things = cell.GetThingList(map);
            var haulable = things.FirstOrDefault(t => t.def.EverHaulable && !t.IsForbidden(Faction.OfPlayer));

            if (haulable == null) return ToolExecutor.JsonError("No haulable item found at " + x + "," + z);

            var job = HaulAIUtility.HaulToStorageJob(colonist, haulable, false);
            if (job != null && colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
                result["item"] = haulable.LabelCap.ToString();
                result["action"] = "haul";
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign haul job.");
        }

        public static string PrioritizeRepair(string colonistName, int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var colonist = ColonistTools.FindPawnByName(colonistName);
            if (colonist == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var building = cell.GetEdifice(map);
            if (building == null) return ToolExecutor.JsonError("No building found at " + x + "," + z);

            if (building.HitPoints >= building.MaxHitPoints)
                return ToolExecutor.JsonError("Building is not damaged.");

            var job = JobMaker.MakeJob(JobDefOf.Repair, building);
            if (colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
                result["building"] = building.LabelCap.ToString();
                result["action"] = "repair";
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign repair job.");
        }

        public static string PrioritizeClean(string colonistName, int x, int z, int radius)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (radius < 1 || radius > 20) return ToolExecutor.JsonError("radius must be 1-20.");

            var colonist = ColonistTools.FindPawnByName(colonistName);
            if (colonist == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var center = new IntVec3(x, 0, z);
            if (!center.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            // Find filth in area
            var filthList = map.listerFilthInHomeArea.FilthInHomeArea
                .Where(f => f.Position.InHorDistOf(center, radius))
                .OrderBy(f => f.Position.DistanceTo(center))
                .ToList();

            if (!filthList.Any())
                return ToolExecutor.JsonError("No filth found within radius " + radius + " of " + x + "," + z);

            var nearestFilth = filthList.First();
            var job = JobMaker.MakeJob(JobDefOf.Clean, nearestFilth);
            if (colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
                result["filth"] = nearestFilth.LabelCap.ToString();
                result["location"] = nearestFilth.Position.x + "," + nearestFilth.Position.z;
                result["action"] = "clean";
                result["filthInArea"] = filthList.Count;
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign clean job.");
        }

        private static Pawn FindPawnByNameInMap(string name, Map map)
        {
            string lower = name.ToLower();
            return map.mapPawns.AllPawns
                .FirstOrDefault(p =>
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.Name?.ToStringFull?.ToLower().Contains(lower) == true ||
                    p.LabelShort?.ToLower() == lower);
        }
    }
}
