using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using RimMind.API;
using RimMind.Core;

namespace RimMind.Tools
{
    /// <summary>
    /// Building deconstruction tools — operations for removing and deconstructing buildings.
    /// Includes AI-proposed blueprint removal (RemoveBuilding, ApproveBuildings) and
    /// native RimWorld deconstruction designations (DeconstructBuilding).
    /// </summary>
    public static class BuildingDeconstructTools
    {
        public static string RemoveBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool removeAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            idsNode = BuildingHelpers.UnwrapStringArray(idsNode);
            bool hasArea = !string.IsNullOrEmpty(args?["x"]?.Value);

            if (!removeAll && (idsNode == null || !idsNode.IsArray) && !hasArea)
                return ToolExecutor.JsonError("Provide 'proposal_ids' array, area (x/z/x2/z2), or 'all: true'.");

            ProposalTracker.CleanupDestroyed(map);

            var toRemove = new List<KeyValuePair<string, Thing>>();

            if (removeAll)
            {
                toRemove = ProposalTracker.GetAll(map);
            }
            else if (idsNode != null && idsNode.IsArray)
            {
                foreach (JSONNode idNode in idsNode.AsArray)
                {
                    string id = idNode.Value;
                    Thing t = ProposalTracker.FindThing(id, map);
                    if (t != null && !t.Destroyed)
                        toRemove.Add(new KeyValuePair<string, Thing>(id, t));
                }
            }
            else if (hasArea)
            {
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                int x2 = args["x2"]?.AsInt ?? x;
                int z2 = args["z2"]?.AsInt ?? z;
                int minX = System.Math.Min(x, x2), maxX = System.Math.Max(x, x2);
                int minZ = System.Math.Min(z, z2), maxZ = System.Math.Max(z, z2);
                var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                toRemove = ProposalTracker.GetInRect(rect, map);
            }

            int removed = 0;
            foreach (var kvp in toRemove)
            {
                if (!kvp.Value.Destroyed)
                    kvp.Value.Destroy(DestroyMode.Cancel);
                ProposalTracker.Untrack(kvp.Key);
                removed++;
            }

            var result = new JSONObject();
            result["removed"] = removed;
            return result.ToString();
        }

        /// <summary>
        /// Mark already-built structures for deconstruction using RimWorld's native designation system.
        /// Parameters: x/z (cell), x2/z2 (area), def_name (all of type). At least one required.
        /// </summary>
        public static string DeconstructBuilding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool hasCell = args?["x"] != null && args?["z"] != null;
            bool hasArea = hasCell && args?["x2"] != null && args?["z2"] != null;
            string defName = args?["def_name"]?.Value;

            if (!hasCell && string.IsNullOrEmpty(defName))
                return ToolExecutor.JsonError("At least one parameter required: x/z (cell), x2/z2 (area), or def_name.");

            var targets = new List<Thing>();

            // Collect target buildings
            if (!string.IsNullOrEmpty(defName))
            {
                // Target all buildings of this defName on the map
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (string.Equals(building.def.defName, defName, StringComparison.OrdinalIgnoreCase))
                        targets.Add(building);
                }
                // Also check non-colonist buildings (ancient ruins, ship chunks, etc.)
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                    {
                        if (string.Equals(thing.def.defName, defName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!targets.Contains(thing))
                                targets.Add(thing);
                        }
                    }
                }
            }
            else if (hasArea)
            {
                // Area selection
                int x1 = args["x"].AsInt;
                int z1 = args["z"].AsInt;
                int x2 = args["x2"].AsInt;
                int z2 = args["z2"].AsInt;
                int minX = System.Math.Min(x1, x2), maxX = System.Math.Max(x1, x2);
                int minZ = System.Math.Min(z1, z2), maxZ = System.Math.Max(z1, z2);

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) continue;

                        foreach (var thing in cell.GetThingList(map))
                        {
                            if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                            {
                                if (!targets.Contains(thing))
                                    targets.Add(thing);
                            }
                        }
                    }
                }
            }
            else
            {
                // Single cell
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                var cell = new IntVec3(x, 0, z);

                if (!cell.InBounds(map))
                    return ToolExecutor.JsonError($"Position ({x}, {z}) is outside map bounds.");

                foreach (var thing in cell.GetThingList(map))
                {
                    if (thing is Building && !(thing is Blueprint) && !(thing is Frame))
                        targets.Add(thing);
                }
            }

            int designated = 0;
            int alreadyDesignated = 0;
            int skipped = 0;
            var structuresList = new JSONArray();

            foreach (var thing in targets)
            {
                // Check if already designated for deconstruction
                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) != null)
                {
                    alreadyDesignated++;
                    continue;
                }

                // Check if it can be deconstructed
                bool canDeconstruct = thing.def.building?.IsDeconstructible ?? false;

                // Also allow ship chunks and mineable things
                if (!canDeconstruct && thing.def.mineable)
                    canDeconstruct = true;

                // Check if it's a real built thing
                bool isBuilt = thing is Building && !(thing is Blueprint) && !(thing is Frame);

                if (!canDeconstruct || !isBuilt)
                {
                    skipped++;
                    continue;
                }

                // Apply the deconstruction designation
                map.designationManager.AddDesignation(new Designation(thing, DesignationDefOf.Deconstruct));
                designated++;

                string label = thing.def.label ?? thing.def.defName;
                structuresList.Add($"{label} at {thing.Position.x},{thing.Position.z}");
            }

            var result = new JSONObject();
            result["designated"] = designated;
            result["already_designated"] = alreadyDesignated;
            result["skipped"] = skipped;
            if (structuresList.Count > 0 && structuresList.Count <= 50)
                result["structures"] = structuresList;
            else if (structuresList.Count > 50)
                result["structures_note"] = $"{structuresList.Count} structures designated (list truncated)";

            return result.ToString();
        }

        public static string ApproveBuildings(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            bool approveAll = args?["all"]?.AsBool == true;
            var idsNode = args?["proposal_ids"];
            idsNode = BuildingHelpers.UnwrapStringArray(idsNode);
            bool hasArea = !string.IsNullOrEmpty(args?["x"]?.Value);

            if (!approveAll && (idsNode == null || !idsNode.IsArray) && !hasArea)
                return ToolExecutor.JsonError("Provide 'proposal_ids' array, area (x/z/x2/z2), or 'all: true'.");

            ProposalTracker.CleanupDestroyed(map);

            var toApprove = new List<KeyValuePair<string, Thing>>();

            if (approveAll)
            {
                toApprove = ProposalTracker.GetAll(map);
            }
            else if (idsNode != null && idsNode.IsArray)
            {
                foreach (JSONNode idNode in idsNode.AsArray)
                {
                    string id = idNode.Value;
                    Thing t = ProposalTracker.FindThing(id, map);
                    if (t != null && !t.Destroyed)
                        toApprove.Add(new KeyValuePair<string, Thing>(id, t));
                }
            }
            else if (hasArea)
            {
                int x = args["x"].AsInt;
                int z = args["z"].AsInt;
                int x2 = args["x2"]?.AsInt ?? x;
                int z2 = args["z2"]?.AsInt ?? z;
                int minX = System.Math.Min(x, x2), maxX = System.Math.Max(x, x2);
                int minZ = System.Math.Min(z, z2), maxZ = System.Math.Max(z, z2);
                var rect = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                toApprove = ProposalTracker.GetInRect(rect, map);
            }

            int approved = 0;
            foreach (var kvp in toApprove)
            {
                if (!kvp.Value.Destroyed)
                {
                    kvp.Value.SetForbidden(false, false);
                    approved++;
                }
                ProposalTracker.Untrack(kvp.Key);
            }

            var result = new JSONObject();
            result["approved"] = approved;
            return result.ToString();
        }
    }
}
