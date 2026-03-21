using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class CombatTools
    {
        public static string GetWeaponStats(string pawnName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("Pawn name is required.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Find the pawn (colonist or hostile)
            var pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => 
                p.LabelShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase) ||
                p.Name?.ToStringShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase) == true);

            if (pawn == null)
                return ToolExecutor.JsonError($"Pawn '{pawnName}' not found.");

            var obj = new JSONObject();
            obj["pawnName"] = pawn.LabelShort;
            obj["faction"] = pawn.Faction?.Name ?? "None";

            var weapon = pawn.equipment?.Primary;
            if (weapon != null)
            {
                var weaponObj = new JSONObject();
                weaponObj["name"] = weapon.LabelCap.ToString();
                weaponObj["defName"] = weapon.def.defName;
                weaponObj["quality"] = weapon.TryGetQuality(out QualityCategory qc) ? qc.GetLabel() : "normal";
                weaponObj["isRanged"] = weapon.def.IsRangedWeapon;

                if (weapon.def.IsRangedWeapon)
                {
                    var verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
                    if (verb != null)
                    {
                        weaponObj["range"] = verb.verbProps.range;
                        weaponObj["warmupTime"] = verb.verbProps.warmupTime;
                        weaponObj["cooldownTime"] = verb.verbProps.defaultCooldownTime;
                        weaponObj["burstShotCount"] = verb.verbProps.burstShotCount;
                        weaponObj["ticksBetweenBurstShots"] = verb.verbProps.ticksBetweenBurstShots;
                        
                        // Accuracy at different ranges
                        var accObj = new JSONObject();
                        accObj["touch"] = verb.verbProps.accuracyTouch;
                        accObj["short"] = verb.verbProps.accuracyShort;
                        accObj["medium"] = verb.verbProps.accuracyMedium;
                        accObj["long"] = verb.verbProps.accuracyLong;
                        weaponObj["accuracy"] = accObj;
                    }
                }

                // Damage stats
                var verb2 = weapon.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault();
                if (verb2?.verbProps?.defaultProjectile != null)
                {
                    var projectile = verb2.verbProps.defaultProjectile;
                    weaponObj["damageType"] = projectile.projectile.damageDef.label;
                    weaponObj["baseDamage"] = projectile.projectile.GetDamageAmount(weapon);
                    weaponObj["armorPenetration"] = projectile.projectile.GetArmorPenetration(weapon);
                    weaponObj["stoppingPower"] = projectile.projectile.stoppingPower;
                }
                else if (weapon.def.tools != null && weapon.def.tools.Any())
                {
                    // Melee weapon
                    var tool = weapon.def.tools.FirstOrDefault();
                    if (tool != null)
                    {
                        weaponObj["damageType"] = tool.linkedBodyPartsGroup?.defName ?? "Unknown";
                        weaponObj["baseDamage"] = tool.power;
                        weaponObj["armorPenetration"] = tool.armorPenetration;
                        weaponObj["cooldownTime"] = tool.cooldownTime;
                    }
                }

                // Calculate DPS
                if (weapon.def.IsRangedWeapon)
                {
                    var verb3 = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
                    if (verb3?.verbProps?.defaultProjectile != null)
                    {
                        var dmg = verb3.verbProps.defaultProjectile.projectile.GetDamageAmount(weapon);
                        var burstCount = verb3.verbProps.burstShotCount;
                        var cooldown = verb3.verbProps.warmupTime + verb3.verbProps.defaultCooldownTime + 
                                       (verb3.verbProps.ticksBetweenBurstShots * (burstCount - 1)) / 60f;
                        if (cooldown > 0)
                        {
                            weaponObj["dps"] = Math.Round((dmg * burstCount) / cooldown, 2);
                        }
                    }
                }

                obj["weapon"] = weaponObj;
            }
            else
            {
                obj["weapon"] = "Unarmed";
                var meleeVerb = pawn.meleeVerbs?.TryGetMeleeVerb(null);
                if (meleeVerb != null)
                {
                    var unarmedObj = new JSONObject();
                    unarmedObj["name"] = "Fists";
                    unarmedObj["baseDamage"] = meleeVerb.verbProps.AdjustedMeleeDamageAmount(meleeVerb, pawn);
                    unarmedObj["cooldownTime"] = meleeVerb.verbProps.AdjustedCooldown(meleeVerb, pawn);
                    obj["unarmedStats"] = unarmedObj;
                }
            }

            // Shooting/Melee skill affects accuracy and damage
            var shootingSkill = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            var meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            obj["shootingSkill"] = shootingSkill;
            obj["meleeSkill"] = meleeSkill;

            return obj.ToString();
        }

        public static string GetArmorStats(string pawnName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return ToolExecutor.JsonError("Pawn name is required.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => 
                p.LabelShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase) ||
                p.Name?.ToStringShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase) == true);

            if (pawn == null)
                return ToolExecutor.JsonError($"Pawn '{pawnName}' not found.");

            var obj = new JSONObject();
            obj["pawnName"] = pawn.LabelShort;
            obj["faction"] = pawn.Faction?.Name ?? "None";

            // Overall armor ratings
            var sharpArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp);
            var bluntArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Blunt);
            var heatArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Heat);

            obj["armorRating_Sharp"] = Math.Round(sharpArmor * 100, 1) + "%";
            obj["armorRating_Blunt"] = Math.Round(bluntArmor * 100, 1) + "%";
            obj["armorRating_Heat"] = Math.Round(heatArmor * 100, 1) + "%";

            // Individual armor pieces
            var apparelList = new JSONArray();
            if (pawn.apparel != null)
            {
                foreach (var ap in pawn.apparel.WornApparel)
                {
                    var apObj = new JSONObject();
                    apObj["name"] = ap.LabelCap.ToString();
                    apObj["defName"] = ap.def.defName;
                    apObj["quality"] = ap.TryGetQuality(out QualityCategory qc) ? qc.GetLabel() : "normal";
                    
                    var sharpVal = ap.GetStatValue(StatDefOf.ArmorRating_Sharp);
                    var bluntVal = ap.GetStatValue(StatDefOf.ArmorRating_Blunt);
                    var heatVal = ap.GetStatValue(StatDefOf.ArmorRating_Heat);

                    if (sharpVal > 0) apObj["sharp"] = Math.Round(sharpVal * 100, 1) + "%";
                    if (bluntVal > 0) apObj["blunt"] = Math.Round(bluntVal * 100, 1) + "%";
                    if (heatVal > 0) apObj["heat"] = Math.Round(heatVal * 100, 1) + "%";

                    // Body coverage
                    if (ap.def.apparel?.bodyPartGroups != null && ap.def.apparel.bodyPartGroups.Any())
                    {
                        var coverage = string.Join(", ", ap.def.apparel.bodyPartGroups.Select(bg => bg.defName));
                        apObj["covers"] = coverage;
                    }

                    apObj["hitPoints"] = $"{ap.HitPoints}/{ap.MaxHitPoints}";
                    apparelList.Add(apObj);
                }
            }

            if (apparelList.Count > 0)
                obj["apparelWorn"] = apparelList;
            else
                obj["apparelWorn"] = "None";

            return obj.ToString();
        }

        public static string GetEnemyMorale()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var hostileFactions = new Dictionary<Faction, List<Pawn>>();
            
            // Group hostiles by faction
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer) && !pawn.Dead)
                {
                    if (!hostileFactions.ContainsKey(pawn.Faction))
                        hostileFactions[pawn.Faction] = new List<Pawn>();
                    hostileFactions[pawn.Faction].Add(pawn);
                }
            }

            if (hostileFactions.Count == 0)
                return ToolExecutor.JsonError("No hostile forces detected.");

            var result = new JSONObject();
            var factionArray = new JSONArray();

            foreach (var kvp in hostileFactions)
            {
                var faction = kvp.Key;
                var pawns = kvp.Value;
                
                var factionObj = new JSONObject();
                factionObj["factionName"] = faction.Name;
                
                var alive = pawns.Count(p => !p.Dead && !p.Downed);
                var downed = pawns.Count(p => p.Downed && !p.Dead);
                var dead = pawns.Count(p => p.Dead);
                var total = alive + downed + dead;

                factionObj["alive"] = alive;
                factionObj["downed"] = downed;
                factionObj["dead"] = dead;
                factionObj["total"] = total;

                // Calculate morale percentage (alive / total)
                var moralePercent = total > 0 ? (alive * 100f / total) : 0f;
                factionObj["moralePercent"] = Math.Round(moralePercent, 1);

                // Morale threshold (typically ~50% for fleeing, but can vary)
                var fleeThreshold = 40f; // RimWorld uses various thresholds
                factionObj["fleeThreshold"] = fleeThreshold;
                
                if (moralePercent <= fleeThreshold)
                {
                    factionObj["status"] = "LIKELY TO FLEE SOON";
                    factionObj["prediction"] = "Enemy is near breaking point. Expect retreat.";
                }
                else if (moralePercent <= 60f)
                {
                    factionObj["status"] = "Weakened";
                    factionObj["prediction"] = "Enemy morale is declining. More casualties will trigger retreat.";
                }
                else
                {
                    factionObj["status"] = "Strong";
                    factionObj["prediction"] = "Enemy is still committed to the fight.";
                }

                factionArray.Add(factionObj);
            }

            result["enemyForces"] = factionArray;
            return result.ToString();
        }

        public static string GetFriendlyFireRisk(string shooterName, string targetName)
        {
            if (string.IsNullOrEmpty(shooterName) || string.IsNullOrEmpty(targetName))
                return ToolExecutor.JsonError("Both shooter and target names are required.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var shooter = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => 
                p.LabelShort.Equals(shooterName, StringComparison.OrdinalIgnoreCase) ||
                p.Name?.ToStringShort.Equals(shooterName, StringComparison.OrdinalIgnoreCase) == true);

            var target = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => 
                p.LabelShort.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                p.Name?.ToStringShort.Equals(targetName, StringComparison.OrdinalIgnoreCase) == true);

            if (shooter == null)
                return ToolExecutor.JsonError($"Shooter '{shooterName}' not found.");
            if (target == null)
                return ToolExecutor.JsonError($"Target '{targetName}' not found.");

            var result = new JSONObject();
            result["shooter"] = shooter.LabelShort;
            result["target"] = target.LabelShort;

            // Calculate line of fire
            var shooterPos = shooter.Position;
            var targetPos = target.Position;
            var distance = shooterPos.DistanceTo(targetPos);

            result["distance"] = Math.Round(distance, 1);

            // Find colonists in line of fire
            var colonistsInLine = new JSONArray();
            var highRiskColonists = new JSONArray();

            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (colonist == shooter || colonist == target) continue;

                var colonistPos = colonist.Position;
                
                // Calculate if colonist is near the line of fire
                var distanceToLine = DistanceToLine(shooterPos.ToVector3(), targetPos.ToVector3(), colonistPos.ToVector3());
                
                if (distanceToLine < 3f) // Within 3 cells of the line
                {
                    var colonistObj = new JSONObject();
                    colonistObj["name"] = colonist.LabelShort;
                    colonistObj["distanceFromLine"] = Math.Round(distanceToLine, 1);
                    colonistObj["position"] = $"({colonistPos.x}, {colonistPos.z})";
                    
                    // High risk if very close to line
                    if (distanceToLine < 1.5f)
                    {
                        colonistObj["risk"] = "HIGH";
                        highRiskColonists.Add(colonist.LabelShort);
                    }
                    else
                    {
                        colonistObj["risk"] = "MEDIUM";
                    }
                    
                    colonistsInLine.Add(colonistObj);
                }
            }

            result["colonistsInLineOfFire"] = colonistsInLine;
            result["highRiskCount"] = highRiskColonists.Count;

            // Calculate friendly fire probability
            var weapon = shooter.equipment?.Primary;
            if (weapon != null && weapon.def.IsRangedWeapon)
            {
                var shootingSkill = shooter.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                var baseAccuracy = 0.7f + (shootingSkill * 0.02f); // Simplified accuracy calculation
                
                // Reduce accuracy for each colonist in line
                var ffRisk = colonistsInLine.Count * 0.15f; // 15% risk per colonist in line
                
                result["baseAccuracy"] = Math.Round(baseAccuracy * 100, 1) + "%";
                result["friendlyFireProbability"] = Math.Round(ffRisk * 100, 1) + "%";
                
                if (ffRisk > 0.3f)
                {
                    result["recommendation"] = "DANGER: High friendly fire risk. Reposition shooter or hold fire.";
                }
                else if (ffRisk > 0.15f)
                {
                    result["recommendation"] = "CAUTION: Moderate friendly fire risk. Consider repositioning.";
                }
                else if (colonistsInLine.Count > 0)
                {
                    result["recommendation"] = "Low risk, but colonists are nearby. Monitor positions.";
                }
                else
                {
                    result["recommendation"] = "Clear line of fire. Safe to engage.";
                }
            }
            else
            {
                result["recommendation"] = "Melee combat - no friendly fire risk from ranged weapons.";
            }

            return result.ToString();
        }

        public static string GetCoverAnalysis(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            int x = args?["x"]?.AsInt ?? -1;
            int z = args?["z"]?.AsInt ?? -1;
            int radius = args?["radius"]?.AsInt ?? 10;

            if (x < 0 || z < 0)
                return ToolExecutor.JsonError("x and z coordinates are required.");

            var center = new IntVec3(x, 0, z);
            if (!center.InBounds(map))
                return ToolExecutor.JsonError("Coordinates out of bounds.");

            var result = new JSONObject();
            result["centerPosition"] = $"({x}, {z})";
            result["searchRadius"] = radius;

            var coverPositions = new JSONArray();
            var fullCover = new JSONArray();
            var halfCover = new JSONArray();
            var noCover = new JSONArray();

            // Scan area for cover
            foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map)) continue;

                var coverValue = cell.GetCover(map);
                if (coverValue != null)
                {
                    var coverObj = new JSONObject();
                    coverObj["position"] = $"({cell.x}, {cell.z})";
                    
                    // Get cover provider
                    var coverThing = cell.GetCover(map);
                    var coverProviderName = "Unknown";
                    var edifice = cell.GetEdifice(map);
                    if (edifice != null)
                    {
                        coverProviderName = edifice.def.label;
                    }
                    
                    coverObj["coverProvider"] = coverProviderName;
                    
                    // Check cover percentage
                    var coverPercent = coverValue.BaseBlockChance();
                    coverObj["coverPercent"] = Math.Round(coverPercent * 100, 0) + "%";
                    
                    if (coverPercent >= 0.75f)
                    {
                        coverObj["coverType"] = "Full Cover";
                        fullCover.Add(coverObj);
                    }
                    else if (coverPercent >= 0.25f)
                    {
                        coverObj["coverType"] = "Half Cover";
                        halfCover.Add(coverObj);
                    }
                    
                    coverPositions.Add(coverObj);
                }
                else
                {
                    // No cover positions
                    if (cell.Standable(map) && cell.GetFirstPawn(map) == null)
                    {
                        noCover.Add($"({cell.x}, {cell.z})");
                    }
                }
            }

            result["fullCoverPositions"] = fullCover;
            result["fullCoverCount"] = fullCover.Count;
            result["halfCoverPositions"] = halfCover;
            result["halfCoverCount"] = halfCover.Count;
            result["exposedPositionsCount"] = noCover.Count;

            // Strategic recommendations
            var recommendations = new JSONArray();
            if (fullCover.Count > 0)
            {
                recommendations.Add("Full cover available: Use for maximum protection (75% hit chance reduction).");
            }
            if (halfCover.Count > 0)
            {
                recommendations.Add("Half cover available: Provides moderate protection (25-50% hit chance reduction).");
            }
            if (fullCover.Count == 0 && halfCover.Count == 0)
            {
                recommendations.Add("WARNING: No cover in area. Colonists will be fully exposed.");
            }
            
            result["recommendations"] = recommendations;

            return result.ToString();
        }

        public static string GetTacticalPathfinding(JSONNode args)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            
            // Analyze current threats and their positions
            var hostiles = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                .ToList();

            if (hostiles.Count == 0)
            {
                result["status"] = "No active threats detected.";
                return result.ToString();
            }

            result["activeThreats"] = hostiles.Count;

            // Identify likely approach vectors
            var approachVectors = new JSONArray();
            var homeArea = map.areaManager.Home;
            
            // Find edges of home area
            var homeCells = homeArea.ActiveCells.ToList();
            if (homeCells.Any())
            {
                var minX = homeCells.Min(c => c.x);
                var maxX = homeCells.Max(c => c.x);
                var minZ = homeCells.Min(c => c.z);
                var maxZ = homeCells.Max(c => c.z);

                // Determine which direction hostiles are approaching from
                foreach (var hostile in hostiles)
                {
                    var pos = hostile.Position;
                    var direction = "";
                    
                    if (pos.x < minX) direction += "West";
                    else if (pos.x > maxX) direction += "East";
                    
                    if (pos.z < minZ) direction += "South";
                    else if (pos.z > maxZ) direction += "North";
                    
                    if (!string.IsNullOrEmpty(direction))
                    {
                        var vectorObj = new JSONObject();
                        vectorObj["hostile"] = hostile.LabelShort;
                        vectorObj["position"] = $"({pos.x}, {pos.z})";
                        vectorObj["approachDirection"] = direction;
                        approachVectors.Add(vectorObj);
                    }
                }
            }

            result["approachVectors"] = approachVectors;

            // Tactical recommendations
            var recommendations = new JSONArray();
            
            // Identify chokepoints (narrow passages, doorways)
            var doors = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.IsDoor)
                .ToList();

            if (doors.Any())
            {
                recommendations.Add($"Chokepoints detected: {doors.Count} doors can funnel enemy movement.");
                recommendations.Add("TACTICAL TIP: Keep doors open to direct enemy flow through killzones.");
            }

            // Check for defensive structures
            var turrets = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_TurretGun)
                .ToList();

            if (turrets.Any())
            {
                recommendations.Add($"Defensive turrets active: {turrets.Count} automated weapons available.");
                var turretPositions = new JSONArray();
                foreach (var turret in turrets.Take(5))
                {
                    turretPositions.Add($"({turret.Position.x}, {turret.Position.z})");
                }
                result["turretPositions"] = turretPositions;
            }
            else
            {
                recommendations.Add("No turrets detected. Consider building automated defenses.");
            }

            // Check for killbox structures (sandbags, walls in defensive patterns)
            var sandbags = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.defName.ToLower().Contains("sandbag"))
                .ToList();

            if (sandbags.Any())
            {
                recommendations.Add($"Sandbag cover detected: {sandbags.Count} defensive positions available.");
            }

            // Drop pod landing zones
            recommendations.Add("TIP: Drop pod raids bypass ground defenses. Build turrets in vulnerable interior zones.");

            // Sapper detection
            var sappers = hostiles.Where(p => p.kindDef?.defName?.ToLower().Contains("sapper") == true).ToList();
            if (sappers.Any())
            {
                recommendations.Add("WARNING: Sapper units detected! They will dig through walls. Reinforce interior defenses.");
            }

            // Breach detection  
            var breachers = hostiles.Where(p => 
                p.equipment?.Primary?.def.defName?.ToLower().Contains("breach") == true ||
                p.kindDef?.defName?.ToLower().Contains("breach") == true
            ).ToList();
            
            if (breachers.Any())
            {
                recommendations.Add("WARNING: Breach units detected! They will destroy walls rapidly. Prepare interior fallback positions.");
            }

            result["tacticalRecommendations"] = recommendations;

            // Optimal defensive positions (high cover near chokepoints)
            var defensivePositions = new JSONArray();
            foreach (var door in doors.Take(3))
            {
                // Find nearby cover
                var nearbyCovers = GenRadial.RadialCellsAround(door.Position, 5, true)
                    .Where(c => c.InBounds(map) && c.GetCover(map) != null)
                    .Take(3);

                foreach (var coverPos in nearbyCovers)
                {
                    var dpObj = new JSONObject();
                    dpObj["position"] = $"({coverPos.x}, {coverPos.z})";
                    dpObj["nearChokepoint"] = $"({door.Position.x}, {door.Position.z})";
                    dpObj["reason"] = "Cover position near door chokepoint - ideal for defensive ambush";
                    defensivePositions.Add(dpObj);
                }
            }

            if (defensivePositions.Count > 0)
            {
                result["optimalDefensivePositions"] = defensivePositions;
            }

            return result.ToString();
        }

        // Helper method to calculate distance from point to line segment
        private static float DistanceToLine(UnityEngine.Vector3 lineStart, UnityEngine.Vector3 lineEnd, UnityEngine.Vector3 point)
        {
            var lineDir = lineEnd - lineStart;
            var lineLength = lineDir.magnitude;
            lineDir.Normalize();

            var pointDir = point - lineStart;
            var dot = UnityEngine.Vector3.Dot(pointDir, lineDir);

            if (dot <= 0) return UnityEngine.Vector3.Distance(point, lineStart);
            if (dot >= lineLength) return UnityEngine.Vector3.Distance(point, lineEnd);

            var closestPoint = lineStart + lineDir * dot;
            return UnityEngine.Vector3.Distance(point, closestPoint);
        }
    }
}
