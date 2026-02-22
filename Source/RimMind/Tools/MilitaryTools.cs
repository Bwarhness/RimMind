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

            // Hostile pawns with detailed analysis
            var hostiles = new JSONArray();
            var meleeUnits = new JSONArray();
            var rangedUnits = new JSONArray();
            var specialUnits = new JSONArray();
            var grenadiers = new JSONArray();
            var dangerousUnits = new JSONArray();
            
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer) && !pawn.Dead && !pawn.Downed)
                {
                    var h = new JSONObject();
                    h["name"] = pawn.LabelShort;
                    h["faction"] = pawn.Faction.Name;
                    h["kind"] = pawn.kindDef?.label ?? "Unknown";
                    h["position"] = $"({pawn.Position.x}, {pawn.Position.z})";
                    
                    // Weapon analysis
                    var weapon = pawn.equipment?.Primary;
                    if (weapon != null)
                    {
                        h["weapon"] = weapon.def.label;
                        h["weaponQuality"] = weapon.TryGetQuality(out QualityCategory qc) ? qc.GetLabel() : "normal";
                        
                        if (weapon.def.IsRangedWeapon)
                        {
                            h["combatRole"] = "Ranged";
                            
                            // Check for grenades/launchers
                            if (weapon.def.defName.ToLower().Contains("grenade") || 
                                weapon.def.defName.ToLower().Contains("launcher"))
                            {
                                h["combatRole"] = "Grenadier";
                                grenadiers.Add(h);
                            }
                            else
                            {
                                rangedUnits.Add(h);
                            }
                        }
                        else
                        {
                            h["combatRole"] = "Melee";
                            meleeUnits.Add(h);
                        }
                    }
                    else
                    {
                        h["combatRole"] = "Melee (Unarmed)";
                        meleeUnits.Add(h);
                    }
                    
                    // Armor analysis
                    if (pawn.apparel != null && pawn.apparel.WornApparel.Any())
                    {
                        var armorPieces = new JSONArray();
                        var hasSignificantArmor = false;
                        
                        foreach (var ap in pawn.apparel.WornApparel)
                        {
                            var sharpArmor = ap.GetStatValue(StatDefOf.ArmorRating_Sharp);
                            if (sharpArmor > 0.1f)
                            {
                                armorPieces.Add(ap.def.label);
                                hasSignificantArmor = true;
                            }
                        }
                        
                        if (hasSignificantArmor)
                        {
                            h["armor"] = armorPieces;
                            h["armored"] = true;
                        }
                        else
                        {
                            h["armored"] = false;
                        }
                    }
                    else
                    {
                        h["armored"] = false;
                    }
                    
                    // Identify dangerous units
                    var kindDefName = pawn.kindDef?.defName?.ToLower() ?? "";
                    if (kindDefName.Contains("centipede") || kindDefName.Contains("scyther") || 
                        kindDefName.Contains("lancer") || kindDefName.Contains("pikeman") ||
                        kindDefName.Contains("termite"))
                    {
                        h["dangerous"] = true;
                        h["reason"] = "Mechanoid unit - high threat";
                        dangerousUnits.Add(pawn.LabelShort);
                        specialUnits.Add(h);
                    }
                    else if (kindDefName.Contains("sapper"))
                    {
                        h["dangerous"] = true;
                        h["reason"] = "Sapper - will breach walls";
                        dangerousUnits.Add(pawn.LabelShort);
                        specialUnits.Add(h);
                    }
                    else if (kindDefName.Contains("breach"))
                    {
                        h["dangerous"] = true;
                        h["reason"] = "Breacher - specialized wall destruction";
                        dangerousUnits.Add(pawn.LabelShort);
                        specialUnits.Add(h);
                    }
                    
                    hostiles.Add(h);
                }
            }
            
            obj["hostilePawns"] = hostiles;
            obj["hostileCount"] = hostiles.Count;
            
            // Combat role breakdown
            var composition = new JSONObject();
            composition["melee"] = meleeUnits.Count;
            composition["ranged"] = rangedUnits.Count;
            composition["grenadiers"] = grenadiers.Count;
            composition["specialUnits"] = specialUnits.Count;
            obj["raidComposition"] = composition;
            
            if (meleeUnits.Count > 0) obj["meleeUnits"] = meleeUnits;
            if (rangedUnits.Count > 0) obj["rangedUnits"] = rangedUnits;
            if (grenadiers.Count > 0) obj["grenadiers"] = grenadiers;
            if (specialUnits.Count > 0) obj["specialUnits"] = specialUnits;
            if (dangerousUnits.Count > 0) obj["dangerousUnits"] = dangerousUnits;
            
            // Raid strategy detection
            var strategy = DetectRaidStrategy(map, hostiles);
            obj["raidStrategy"] = strategy;

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
        
        private static JSONObject DetectRaidStrategy(Map map, JSONArray hostiles)
        {
            var strategy = new JSONObject();
            
            if (hostiles.Count == 0)
            {
                strategy["type"] = "None";
                return strategy;
            }
            
            // Check for sappers
            var hasSappers = false;
            var hasBreachers = false;
            
            foreach (JSONNode hostile in hostiles)
            {
                var kind = hostile["kind"]?.Value?.ToLower() ?? "";
                var defName = hostile["name"]?.Value?.ToLower() ?? "";
                
                if (kind.Contains("sapper") || defName.Contains("sapper"))
                    hasSappers = true;
                    
                if (kind.Contains("breach") || defName.Contains("breach"))
                    hasBreachers = true;
            }
            
            // Check for siege equipment
            var siegeThings = map.listerThings.AllThings
                .Where(t => t.Faction != null && t.Faction.HostileTo(Faction.OfPlayer) && 
                            (t.def.defName.Contains("Artillery") || t.def.building?.turretGunDef != null))
                .ToList();
            
            // Determine raid type
            if (siegeThings.Any())
            {
                strategy["type"] = "Siege";
                strategy["behavior"] = "Enemies are setting up artillery. They will bombard from a distance.";
                strategy["counterTactics"] = "Rush the siege position before they finish setup, or use mortars to destroy their artillery.";
            }
            else if (hasSappers)
            {
                strategy["type"] = "Sapper";
                strategy["behavior"] = "Sappers will mine through walls to bypass defenses.";
                strategy["counterTactics"] = "Reinforce interior defenses. Don't rely solely on outer walls. Intercept sappers in tunnels.";
            }
            else if (hasBreachers)
            {
                strategy["type"] = "Breach";
                strategy["behavior"] = "Breach units will rapidly destroy walls with specialized equipment.";
                strategy["counterTactics"] = "Prepare fallback positions. Walls won't hold. Set up layered defenses.";
            }
            else
            {
                // Check if enemies are near map edge (likely just spawned)
                var nearEdge = 0;
                foreach (JSONNode hostile in hostiles)
                {
                    var posStr = hostile["position"]?.Value ?? "";
                    if (posStr.Contains("("))
                    {
                        var coords = posStr.Trim('(', ')').Split(',');
                        if (coords.Length == 2)
                        {
                            if (int.TryParse(coords[0].Trim(), out int x) && 
                                int.TryParse(coords[1].Trim(), out int z))
                            {
                                if (x < 10 || z < 10 || x > map.Size.x - 10 || z > map.Size.z - 10)
                                    nearEdge++;
                            }
                        }
                    }
                }
                
                if (nearEdge > hostiles.Count * 0.8)
                {
                    strategy["type"] = "Direct Assault";
                    strategy["behavior"] = "Standard raid - enemies will assault from map edge through main defenses.";
                    strategy["counterTactics"] = "Use killbox and defensive positions. Funnel enemies through chokepoints.";
                }
                else
                {
                    strategy["type"] = "Drop Pod / Tunneler";
                    strategy["behavior"] = "Enemies spawned inside or near the base - likely drop pods or tunnelers.";
                    strategy["counterTactics"] = "No time for preparation. Engage immediately. Use interior defensive positions.";
                }
            }
            
            return strategy;
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

        public static string GetFireSupport()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var fireSupport = new JSONArray();
            var turrets = new JSONArray();

            // Get drafted colonists who can provide fire support
            foreach (var colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist.Drafted)
                {
                    var obj = new JSONObject();
                    obj["name"] = colonist.Name.ToStringShort;
                    obj["position"] = $"({colonist.Position.x}, {colonist.Position.z})";

                    var weapon = colonist.equipment?.Primary;
                    if (weapon != null)
                    {
                        obj["weapon"] = weapon.def.label;
                        obj["weaponType"] = weapon.def.IsRangedWeapon ? "ranged" : "melee";
                    }

                    obj["shootingSkill"] = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                    fireSupport.Add(obj);
                }
            }

            // Get turrets
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_TurretGun turret && turret.Spawned)
                {
                    var obj = new JSONObject();
                    obj["name"] = building.LabelCap;
                    obj["position"] = $"({building.Position.x}, {building.Position.z})";
                    obj["type"] = "turret";

                    var gunDef = building.def.building?.turretGunDef;
                    if (gunDef != null)
                    {
                        obj["weapon"] = gunDef.label;
                    }

                    turrets.Add(obj);
                }
            }

            result["colonistFireSupport"] = fireSupport;
            result["turrets"] = turrets;
            result["totalFireSupport"] = fireSupport.Count + turrets.Count;

            // Analyze threats that could be engaged
            var hostiles = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                .ToList();

            result["hostileCount"] = hostiles.Count;
            result["message"] = $"Found {fireSupport.Count} colonists and {turrets.Count} turrets providing fire support against {hostiles.Count} hostiles";

            return result.ToString();
        }

        public static string GetCasualties()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var downed = new JSONArray();
            var dead = new JSONArray();
            var injured = new JSONArray();

            foreach (var colonist in map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (colonist.Dead)
                {
                    var obj = new JSONObject();
                    obj["name"] = colonist.Name.ToStringShort;
                    obj["position"] = $"({colonist.Position.x}, {colonist.Position.z})";
                    obj["status"] = "dead";
                    dead.Add(obj);
                }
                else if (colonist.Downed)
                {
                    var obj = new JSONObject();
                    obj["name"] = colonist.Name.ToStringShort;
                    obj["position"] = $"({colonist.Position.x}, {colonist.Position.z})";
                    obj["status"] = "downed";

                    // Get injuries
                    if (colonist.health?.hediffSet != null)
                    {
                        var injuriesArr = new JSONArray();
                        foreach (var hediff in colonist.health.hediffSet.hediffs)
                        {
                            if (hediff.Severity > 0)
                                injuriesArr.Add(hediff.def.label);
                        }
                        if (injuriesArr.Count > 0)
                            obj["injuries"] = injuriesArr;
                    }

                    // Find nearest bed
                    var beds = map.listerBuildings.allBuildingsColonist
                        .Where(b => b is Building_Bed bed && bed.Medical && !bed.ForPrisoners)
                        .OrderBy(b => b.Position.DistanceTo(colonist.Position))
                        .FirstOrDefault();

                    if (beds != null)
                    {
                        obj["nearestMedical"] = $"({beds.Position.x}, {beds.Position.z})";
                        obj["distance"] = beds.Position.DistanceTo(colonist.Position);
                    }

                    downed.Add(obj);
                }
                else if (colonist.health?.hediffSet?.HasTendableHediff() == true)
                {
                    var obj = new JSONObject();
                    obj["name"] = colonist.Name.ToStringShort;
                    obj["position"] = $"({colonist.Position.x}, {colonist.Position.z})";
                    obj["status"] = "injured";

                    var injuriesArr = new JSONArray();
                    foreach (var hediff in colonist.health.hediffSet.hediffs.Where(h => h.TendableNow()))
                    {
                        injuriesArr.Add(hediff.def.label);
                    }
                    obj["injuriesNeedingTreatment"] = injuriesArr;

                    injured.Add(obj);
                }
            }

            result["downed"] = downed;
            result["dead"] = dead;
            result["injured"] = injured;
            result["totalCasualties"] = downed.Count + dead.Count;
            result["totalInjured"] = injured.Count;

            if (downed.Count > 0)
                result["message"] = $"{downed.Count} colonists downed, {injured.Count} injured, {dead.Count} dead";
            else if (injured.Count > 0)
                result["message"] = $"{injured.Count} colonists injured";
            else
                result["message"] = "No casualties";

            return result.ToString();
        }
    }
}
