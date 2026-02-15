using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class MilitaryTools
    {
        public static string GetThreats()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();

            // Hostile pawns
            var hostiles = new JSONArray();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer) && !pawn.Dead && !pawn.Downed)
                {
                    var h = new JSONObject();
                    h["name"] = pawn.LabelShort;
                    h["faction"] = pawn.Faction.Name;
                    h["kind"] = pawn.kindDef?.label ?? "Unknown";
                    hostiles.Add(h);
                }
            }
            obj["hostilePawns"] = hostiles;
            obj["hostileCount"] = hostiles.Count;

            // Manhunter animals
            var manhunters = map.mapPawns.AllPawnsSpawned
                .Where(p => p.MentalStateDef == MentalStateDefOf.Manhunter || p.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
                .Select(p => p.LabelShort)
                .ToList();
            if (manhunters.Count > 0)
            {
                var mArr = new JSONArray();
                foreach (var m in manhunters) mArr.Add(m);
                obj["manhunterAnimals"] = mArr;
            }

            // Check for active conditions
            var conditions = new JSONArray();
            foreach (var condition in map.gameConditionManager.ActiveConditions)
            {
                conditions.Add(condition.LabelCap.ToString());
            }
            if (conditions.Count > 0)
                obj["activeConditions"] = conditions;

            return obj.ToString();
        }

        public static string GetDefenses()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var turrets = new JSONArray();
            var traps = new JSONArray();

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_TurretGun turret)
                {
                    var t = new JSONObject();
                    t["type"] = turret.def.LabelCap.ToString();
                    t["position"] = turret.Position.ToString();
                    t["hitPoints"] = turret.HitPoints + "/" + turret.MaxHitPoints;
                    turrets.Add(t);
                }
                else if (building.def.building != null && building.def.building.isTrap)
                {
                    var t = new JSONObject();
                    t["type"] = building.def.LabelCap.ToString();
                    t["position"] = building.Position.ToString();
                    traps.Add(t);
                }
            }

            var obj = new JSONObject();
            obj["turrets"] = turrets;
            obj["turretCount"] = turrets.Count;
            obj["traps"] = traps;
            obj["trapCount"] = traps.Count;
            return obj.ToString();
        }

        public static string GetCombatReadiness()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                // Weapon
                var weapon = pawn.equipment?.Primary;
                if (weapon != null)
                {
                    obj["weapon"] = weapon.LabelCap.ToString();
                    obj["weaponType"] = weapon.def.IsRangedWeapon ? "Ranged" : "Melee";
                }
                else
                {
                    obj["weapon"] = "Unarmed";
                }

                // Armor
                if (pawn.apparel != null)
                {
                    var armor = new JSONArray();
                    foreach (var ap in pawn.apparel.WornApparel)
                    {
                        if (ap.def.apparel?.bodyPartGroups != null &&
                            ap.GetStatValue(StatDefOf.ArmorRating_Sharp) > 0.1f)
                        {
                            armor.Add(ap.LabelCap.ToString());
                        }
                    }
                    if (armor.Count > 0)
                        obj["armor"] = armor;
                }

                // Skills
                obj["shootingSkill"] = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                obj["meleeSkill"] = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["combatReadiness"] = arr;
            return result.ToString();
        }
    }
}
