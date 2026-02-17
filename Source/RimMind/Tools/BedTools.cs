using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class BedTools
    {
        public static string ListBeds()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                var obj = new JSONObject();
                obj["label"] = bed.LabelCap.ToString();
                obj["x"] = bed.Position.x;
                obj["z"] = bed.Position.z;
                obj["forPrisoners"] = bed.ForPrisoners;
                obj["medical"] = bed.Medical;

                if (bed.def.building != null)
                {
                    obj["sleepingSlotsCount"] = bed.def.building.bed_maxBodySize > 1.0f ? 2 : 1;
                }

                var owners = bed.OwnersForReading;
                if (owners != null && owners.Count > 0)
                {
                    var ownerNames = new JSONArray();
                    foreach (var owner in owners)
                        ownerNames.Add(owner.Name?.ToStringShort ?? "Unknown");
                    obj["owners"] = ownerNames;
                    obj["assigned"] = true;
                }
                else
                {
                    obj["assigned"] = false;
                }

                // Room quality
                var room = bed.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    obj["impressiveness"] = room.GetStat(RoomStatDefOf.Impressiveness).ToString("F0");
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["beds"] = arr;
            result["totalBeds"] = arr.Count;
            return result.ToString();
        }

        public static string AssignBed(string colonistName, int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var bed = cell.GetThingList(map).OfType<Building_Bed>().FirstOrDefault();
            if (bed == null) return ToolExecutor.JsonError("No bed found at " + x + "," + z);

            if (bed.ForPrisoners)
                return ToolExecutor.JsonError("Cannot assign colonist to prisoner bed.");

            if (bed.Medical)
                return ToolExecutor.JsonError("Cannot assign ownership of medical bed.");

            // Check if bed is full
            var currentOwners = bed.OwnersForReading;
            int maxOwners = bed.def.building.bed_maxBodySize > 1.0f ? 2 : 1;
            
            if (currentOwners != null && currentOwners.Count >= maxOwners && !currentOwners.Contains(pawn))
                return ToolExecutor.JsonError("Bed is full (max " + maxOwners + " owners).");

            // Unassign from current bed if any
            if (pawn.ownership != null && pawn.ownership.OwnedBed != null)
            {
                var oldBedComp = pawn.ownership.OwnedBed.CompAssignableToPawn;
                oldBedComp?.TryUnassignPawn(pawn);
            }

            // Assign new bed
            pawn.ownership.ClaimBedIfNonMedical(bed);

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["bed"] = bed.LabelCap.ToString();
            result["location"] = x + "," + z;

            var newOwners = bed.OwnersForReading;
            if (newOwners != null && newOwners.Count > 0)
            {
                var ownerNames = new JSONArray();
                foreach (var owner in newOwners)
                    ownerNames.Add(owner.Name?.ToStringShort ?? "Unknown");
                result["allOwners"] = ownerNames;
            }

            return result.ToString();
        }

        public static string UnassignBed(string colonistName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.ownership == null || pawn.ownership.OwnedBed == null)
                return ToolExecutor.JsonError("Colonist does not own a bed.");

            var bed = pawn.ownership.OwnedBed;
            string bedLabel = bed.LabelCap.ToString();

            var bedComp = bed.CompAssignableToPawn;
            if (bedComp != null)
                bedComp.TryUnassignPawn(pawn);
            else
                return ToolExecutor.JsonError("Cannot unassign from this bed type.");

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["previousBed"] = bedLabel;
            return result.ToString();
        }

        public static string GetBedAssignments()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                var obj = new JSONObject();
                obj["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";

                if (pawn.ownership != null && pawn.ownership.OwnedBed != null)
                {
                    var bed = pawn.ownership.OwnedBed;
                    obj["bed"] = bed.LabelCap.ToString();
                    obj["x"] = bed.Position.x;
                    obj["z"] = bed.Position.z;
                    obj["assigned"] = true;

                    // Check if sharing
                    var owners = bed.OwnersForReading;
                    if (owners != null && owners.Count > 1)
                    {
                        var sharing = new JSONArray();
                        foreach (var owner in owners)
                        {
                            if (owner != pawn)
                                sharing.Add(owner.Name?.ToStringShort ?? "Unknown");
                        }
                        if (sharing.Count > 0)
                            obj["sharingWith"] = sharing;
                    }
                }
                else
                {
                    obj["assigned"] = false;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["assignments"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }
    }
}
