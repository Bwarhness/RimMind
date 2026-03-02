using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class JobTools
    {
        /// <summary>
        /// Assigns a haul job, queueing it if the colonist already has haul work in progress.
        /// Mirrors the TakeOrQueueJob pattern from EquipmentTools.
        /// </summary>
        private static bool TakeOrQueueHaulJob(Pawn colonist, Job job)
        {
            bool shouldQueue = colonist.jobs.jobQueue.Count > 0 ||
                colonist.CurJobDef == JobDefOf.HaulToCell ||
                colonist.CurJobDef == JobDefOf.HaulToContainer;
            return colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: shouldQueue);
        }

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
            if (job != null && TakeOrQueueHaulJob(colonist, job))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
                result["item"] = haulable.LabelCap.ToString();
                result["queued"] = colonist.jobs.jobQueue.Count > 0;
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

        /// <summary>
        /// Find all items of a given type on the map and queue haul jobs for all of them.
        /// Use this when the player says "haul all [resource]" rather than pointing at a specific location.
        /// </summary>
        public static string HaulAllOfType(string colonistName, string itemType)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(itemType)) return ToolExecutor.JsonError("itemType parameter required.");

            var colonist = ColonistTools.FindPawnByName(colonistName);
            if (colonist == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            string typeLower = itemType.ToLower();

            // Find all haulable Things matching the item type (by label or defName)
            var matches = map.listerThings.AllThings
                .Where(t =>
                    t.def.EverHaulable &&
                    !t.IsForbidden(Faction.OfPlayer) &&
                    t.Spawned &&
                    (t.def.defName.ToLower().Contains(typeLower) ||
                     t.def.label?.ToLower().Contains(typeLower) == true ||
                     t.LabelShort?.ToLower().Contains(typeLower) == true))
                .OrderBy(t => t.Position.DistanceTo(colonist.Position))
                .Take(30) // cap to avoid absurd queues
                .ToList();

            if (matches.Count == 0)
                return ToolExecutor.JsonError("No haulable items matching '" + itemType + "' found on the map.");

            var queued = new JSONArray();
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var thing = matches[i];
                var job = HaulAIUtility.HaulToStorageJob(colonist, thing, false);
                if (job == null) { failCount++; continue; }

                // First job: smart queue (interrupts if idle, queues if already hauling)
                // Subsequent jobs: always queue
                bool ok = i == 0
                    ? TakeOrQueueHaulJob(colonist, job)
                    : colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: true);

                if (ok)
                {
                    var entry = new JSONObject();
                    entry["item"] = thing.LabelCap.ToString();
                    entry["count"] = thing.stackCount;
                    entry["position"] = "(" + thing.Position.x + ", " + thing.Position.z + ")";
                    queued.Add(entry);
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            if (successCount == 0)
                return ToolExecutor.JsonError("Failed to queue any haul jobs for '" + itemType + "'.");

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
            result["itemType"] = itemType;
            result["jobsQueued"] = successCount;
            result["failed"] = failCount;
            result["hauls"] = queued;
            return result.ToString();
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
