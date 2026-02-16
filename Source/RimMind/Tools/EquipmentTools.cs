using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class EquipmentTools
    {
        public static string EquipWeapon(string colonistName, int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var weapon = cell.GetThingList(map).FirstOrDefault(t => t.def.IsWeapon);
            if (weapon == null) return ToolExecutor.JsonError("No weapon found at " + x + "," + z);

            // Create job to equip
            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
                result["weapon"] = weapon.LabelCap.ToString();
                result["location"] = x + "," + z;
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign equip job.");
        }

        public static string WearApparel(string colonistName, int x, int z)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map)) return ToolExecutor.JsonError("Coordinates out of bounds.");

            var apparel = cell.GetThingList(map).FirstOrDefault(t => t.def.IsApparel);
            if (apparel == null) return ToolExecutor.JsonError("No apparel found at " + x + "," + z);

            // Create job to wear
            var job = JobMaker.MakeJob(JobDefOf.Wear, apparel);
            if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                var result = new JSONObject();
                result["success"] = true;
                result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
                result["apparel"] = apparel.LabelCap.ToString();
                result["location"] = x + "," + z;
                return result.ToString();
            }

            return ToolExecutor.JsonError("Failed to assign wear job.");
        }

        public static string DropEquipment(string colonistName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.equipment == null || pawn.equipment.Primary == null)
                return ToolExecutor.JsonError("Colonist has no equipped weapon.");

            var weapon = pawn.equipment.Primary;
            string weaponName = weapon.LabelCap.ToString();

            ThingWithComps droppedWeapon;
            pawn.equipment.TryDropEquipment(weapon, out droppedWeapon, pawn.Position);

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["droppedWeapon"] = weaponName;
            return result.ToString();
        }

        public static string ListEquipment()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                var obj = new JSONObject();
                obj["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";

                // Weapon
                if (pawn.equipment != null && pawn.equipment.Primary != null)
                {
                    obj["weapon"] = pawn.equipment.Primary.LabelCap.ToString();
                    obj["weaponDamage"] = pawn.equipment.Primary.def.Verbs?.FirstOrDefault()?.defaultProjectile?.projectile?.GetDamageAmount(1f).ToString() ?? "0";
                }
                else
                {
                    obj["weapon"] = "Unarmed";
                }

                // Apparel
                if (pawn.apparel != null && pawn.apparel.WornApparel.Any())
                {
                    var apparelList = new JSONArray();
                    foreach (var apparel in pawn.apparel.WornApparel)
                    {
                        var apparelObj = new JSONObject();
                        apparelObj["name"] = apparel.LabelCap.ToString();
                        apparelObj["layer"] = apparel.def.apparel?.LastLayer.ToString() ?? "Unknown";
                        
                        // Armor stats
                        var statModifiers = apparel.def.statBases;
                        if (statModifiers != null)
                        {
                            var armorSharp = statModifiers.FirstOrDefault(s => s.stat == StatDefOf.ArmorRating_Sharp);
                            var armorBlunt = statModifiers.FirstOrDefault(s => s.stat == StatDefOf.ArmorRating_Blunt);
                            if (armorSharp != null) apparelObj["armorSharp"] = armorSharp.value.ToString("F2");
                            if (armorBlunt != null) apparelObj["armorBlunt"] = armorBlunt.value.ToString("F2");
                        }
                        
                        apparelList.Add(apparelObj);
                    }
                    obj["apparel"] = apparelList;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["equipment"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string AssignOutfit(string colonistName, string outfitName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(outfitName)) return ToolExecutor.JsonError("outfitName parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.outfits == null)
                return ToolExecutor.JsonError("Colonist has no outfit settings.");

            // Find outfit
            string outfitLower = outfitName.ToLower();
            var outfit = Current.Game.outfitDatabase.AllOutfits
                .FirstOrDefault(o => o.label.ToLower().Contains(outfitLower));

            if (outfit == null)
            {
                var available = Current.Game.outfitDatabase.AllOutfits.Select(o => o.label).Take(10);
                return ToolExecutor.JsonError("Outfit '" + outfitName + "' not found. Available: " + string.Join(", ", available));
            }

            pawn.outfits.CurrentOutfit = outfit;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["outfit"] = outfit.label;
            return result.ToString();
        }

        public static string AssignDrugPolicy(string colonistName, string policyName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(policyName)) return ToolExecutor.JsonError("policyName parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.drugs == null)
                return ToolExecutor.JsonError("Colonist has no drug policy settings.");

            // Find policy
            string policyLower = policyName.ToLower();
            var policy = Current.Game.drugPolicyDatabase.AllPolicies
                .FirstOrDefault(p => p.label.ToLower().Contains(policyLower));

            if (policy == null)
            {
                var available = Current.Game.drugPolicyDatabase.AllPolicies.Select(p => p.label).Take(10);
                return ToolExecutor.JsonError("Drug policy '" + policyName + "' not found. Available: " + string.Join(", ", available));
            }

            pawn.drugs.CurrentPolicy = policy;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["drugPolicy"] = policy.label;
            return result.ToString();
        }

        public static string AssignFoodRestriction(string colonistName, string restrictionName)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            if (string.IsNullOrEmpty(colonistName)) return ToolExecutor.JsonError("colonist parameter required.");
            if (string.IsNullOrEmpty(restrictionName)) return ToolExecutor.JsonError("restrictionName parameter required.");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            if (pawn.foodRestriction == null)
                return ToolExecutor.JsonError("Colonist has no food restriction settings.");

            // Find restriction
            string restrictionLower = restrictionName.ToLower();
            var restriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions
                .FirstOrDefault(r => r.label.ToLower().Contains(restrictionLower));

            if (restriction == null)
            {
                var available = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(r => r.label).Take(10);
                return ToolExecutor.JsonError("Food restriction '" + restrictionName + "' not found. Available: " + string.Join(", ", available));
            }

            pawn.foodRestriction.CurrentFoodRestriction = restriction;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["foodRestriction"] = restriction.label;
            return result.ToString();
        }
    }
}
