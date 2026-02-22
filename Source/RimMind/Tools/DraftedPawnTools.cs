using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class DraftedPawnTools
    {
        /// <summary>
        /// Move a drafted pawn to specific map coordinates.
        /// </summary>
        public static string MovePawn(string pawnName, int x, int z)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("pawnName parameter required.");

            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            var pawn = ColonistTools.FindPawnByName(pawnName);
            if (pawn == null)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' not found.");

            if (pawn.drafter == null || !pawn.drafter.Drafted)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is not drafted. Use draft_colonist first.");

            if (pawn.Downed)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is downed and cannot move.");

            var dest = new IntVec3(x, 0, z);
            if (!dest.InBounds(map))
                return ToolExecutor.JsonError("Coordinates (" + x + ", " + z + ") are out of map bounds.");

            if (!dest.Standable(map))
                return ToolExecutor.JsonError("Destination (" + x + ", " + z + ") is not standable.");

            var job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(dest));
            job.playerForced = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, null, false);

            var result = new JSONObject();
            result["success"] = true;
            result["pawn"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
            result["destination"] = "(" + x + ", " + z + ")";
            result["action"] = "moving";
            return result.ToString();
        }

        /// <summary>
        /// Order a drafted pawn to attack a specific target pawn.
        /// </summary>
        public static string OrderAttack(string pawnName, string targetName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("pawnName parameter required.");
            if (string.IsNullOrEmpty(targetName))
                return ToolExecutor.JsonError("targetName parameter required.");

            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            var pawn = ColonistTools.FindPawnByName(pawnName);
            if (pawn == null)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' not found.");

            if (pawn.drafter == null || !pawn.drafter.Drafted)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is not drafted. Use draft_colonist first.");

            if (pawn.Downed)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is downed and cannot attack.");

            // Find target in all spawned pawns on the map
            string targetLower = targetName.ToLower();
            var target = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p =>
                p != pawn &&
                (p.Name?.ToStringShort?.ToLower() == targetLower ||
                 p.Name?.ToStringFull?.ToLower().Contains(targetLower) == true ||
                 p.LabelShort?.ToLower() == targetLower ||
                 p.LabelShort?.ToLower().Contains(targetLower) == true));

            if (target == null)
                return ToolExecutor.JsonError("Target '" + targetName + "' not found on the map.");

            if (target.Dead)
                return ToolExecutor.JsonError("Target '" + targetName + "' is already dead.");

            // Determine job type based on pawn's equipped weapon
            var weapon = pawn.equipment?.Primary;
            bool isRanged = weapon != null && weapon.def.IsRangedWeapon;

            Job job;
            string attackType;
            if (isRanged)
            {
                job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                attackType = "ranged";
            }
            else
            {
                job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                attackType = "melee";
            }

            job.playerForced = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, null, false);

            var result = new JSONObject();
            result["success"] = true;
            result["pawn"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
            result["target"] = target.Name?.ToStringShort ?? target.LabelShort;
            result["attackType"] = attackType;
            result["weapon"] = weapon?.LabelCap.ToString() ?? "Unarmed";
            return result.ToString();
        }

        /// <summary>
        /// Order a drafted pawn to hold their current position in combat stance.
        /// </summary>
        public static string HoldPosition(string pawnName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("pawnName parameter required.");

            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            var pawn = ColonistTools.FindPawnByName(pawnName);
            if (pawn == null)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' not found.");

            if (pawn.drafter == null || !pawn.drafter.Drafted)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is not drafted. Use draft_colonist first.");

            if (pawn.Downed)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' is downed.");

            var job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            job.playerForced = true;
            job.expiryInterval = -1; // Hold indefinitely
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, null, false);

            var result = new JSONObject();
            result["success"] = true;
            result["pawn"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
            result["position"] = "(" + pawn.Position.x + ", " + pawn.Position.z + ")";
            result["action"] = "holding position";
            return result.ToString();
        }

        /// <summary>
        /// Toggle fire mode for a pawn's weapon. Not available in vanilla RimWorld.
        /// </summary>
        public static string SetFireMode(string pawnName, string mode)
        {
            // Fire mode control requires Combat Extended or similar mods.
            // In vanilla RimWorld, there is no fire mode system.
            var result = new JSONObject();
            result["success"] = false;
            result["message"] = "Fire mode control is not available in vanilla RimWorld. Install Combat Extended mod for fire mode support.";
            return result.ToString();
        }

        /// <summary>
        /// Order multiple drafted pawns to attack a single target.
        /// </summary>
        public static string OrderGroupAttack(string[] pawnNames, string targetName)
        {
            if (pawnNames == null || pawnNames.Length == 0)
                return ToolExecutor.JsonError("pawnNames array required and must not be empty.");
            if (string.IsNullOrEmpty(targetName))
                return ToolExecutor.JsonError("targetName parameter required.");

            var results = new JSONArray();
            int successCount = 0;
            int failCount = 0;

            foreach (var name in pawnNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                var attackResult = OrderAttack(name, targetName);
                var parsed = JSONNode.Parse(attackResult);

                var entry = new JSONObject();
                entry["pawn"] = name;

                if (parsed["error"] != null)
                {
                    entry["success"] = false;
                    entry["error"] = parsed["error"].Value;
                    failCount++;
                }
                else
                {
                    entry["success"] = parsed["success"].AsBool;
                    entry["attackType"] = parsed["attackType"]?.Value ?? "unknown";
                    if (parsed["success"].AsBool) successCount++;
                    else failCount++;
                }

                results.Add(entry);
            }

            var result = new JSONObject();
            result["target"] = targetName;
            result["totalOrdered"] = pawnNames.Length;
            result["successCount"] = successCount;
            result["failCount"] = failCount;
            result["details"] = results;
            return result.ToString();
        }

        /// <summary>
        /// Switch a pawn's equipped weapon to another weapon from their inventory.
        /// Accepts both defName and label (partial match) for the weapon.
        /// </summary>
        public static string SwitchWeapon(string pawnName, string weaponDefName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("pawnName parameter required.");
            if (string.IsNullOrEmpty(weaponDefName))
                return ToolExecutor.JsonError("weaponDefName parameter required.");

            var map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map.");

            var pawn = ColonistTools.FindPawnByName(pawnName);
            if (pawn == null)
                return ToolExecutor.JsonError("Pawn '" + pawnName + "' not found.");

            if (pawn.inventory == null)
                return ToolExecutor.JsonError("Pawn has no inventory.");

            if (pawn.equipment == null)
                return ToolExecutor.JsonError("Pawn cannot equip weapons.");

            // Find weapon in inventory by defName or label (partial match, case-insensitive)
            string searchLower = weaponDefName.ToLower();
            Thing weaponThing = pawn.inventory.innerContainer.FirstOrDefault(t =>
                t.def.IsWeapon &&
                (t.def.defName.ToLower() == searchLower ||
                 t.def.defName.ToLower().Contains(searchLower) ||
                 t.Label?.ToLower().Contains(searchLower) == true ||
                 t.LabelCap?.ToString().ToLower().Contains(searchLower) == true));

            if (weaponThing == null)
            {
                // List available weapons in inventory for better error message
                var inventoryWeapons = pawn.inventory.innerContainer
                    .Where(t => t.def.IsWeapon)
                    .Select(t => t.LabelCap.ToString())
                    .ToList();

                if (inventoryWeapons.Count == 0)
                    return ToolExecutor.JsonError("Pawn '" + pawnName + "' has no weapons in inventory.");

                return ToolExecutor.JsonError(
                    "Weapon '" + weaponDefName + "' not found in '" + pawnName + "' inventory. " +
                    "Available: " + string.Join(", ", inventoryWeapons));
            }

            var newWeapon = weaponThing as ThingWithComps;
            if (newWeapon == null)
                return ToolExecutor.JsonError("Item '" + weaponThing.LabelCap + "' cannot be equipped as a weapon.");

            // Record current weapon info for the response
            string oldWeaponName = pawn.equipment.Primary?.LabelCap.ToString() ?? "None";

            // Transfer current equipped weapon to inventory (if any)
            if (pawn.equipment.Primary != null)
            {
                ThingWithComps currentWeapon = pawn.equipment.Primary;
                pawn.equipment.TryTransferEquipmentToContainer(currentWeapon, pawn.inventory.innerContainer);
            }

            // Remove the new weapon from inventory and equip it
            pawn.inventory.innerContainer.Remove(newWeapon);
            pawn.equipment.AddEquipment(newWeapon);

            var result = new JSONObject();
            result["success"] = true;
            result["pawn"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
            result["previousWeapon"] = oldWeaponName;
            result["newWeapon"] = newWeapon.LabelCap.ToString();
            return result.ToString();
        }

        /// <summary>
        /// Helper to extract pawn names from a JSONNode array (for use in ToolExecutor).
        /// </summary>
        public static string[] ExtractPawnNames(JSONNode node)
        {
            if (node == null) return new string[0];
            var names = new List<string>();
            for (int i = 0; i < node.Count; i++)
            {
                var val = node[i]?.Value;
                if (!string.IsNullOrEmpty(val))
                    names.Add(val);
            }
            return names.ToArray();
        }
    }
}
