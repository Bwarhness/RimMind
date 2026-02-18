using System;
using System.Linq;
using RimMind.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Tools
{
    public static class DLCTools
    {
        // Helper to find pawn by name
        private static Pawn FindPawnByName(string name)
        {
            var map = Find.CurrentMap;
            if (map == null) return null;
            return map.mapPawns.AllPawns.FirstOrDefault(p => 
                p.Name != null && 
                (p.Name.ToStringShort.Equals(name, System.StringComparison.OrdinalIgnoreCase) || 
                 p.Name.ToStringFull.Equals(name, System.StringComparison.OrdinalIgnoreCase)));
        }

        public static string GetPsycasts(string name)
        {
            // Check for Royalty DLC
            if (!ModsConfig.RoyaltyActive)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Royalty DLC not installed";
                errObj["dlc_required"] = "Royalty";
                return errObj.ToString();
            }

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // If no name provided, list all psycasters
            if (string.IsNullOrEmpty(name))
            {
                var psycasters = new JSONArray();
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    if (pawn.psychicEntropy != null && pawn.GetPsylinkLevel() > 0)
                    {
                        psycasters.Add(pawn.Name?.ToStringShort ?? "Unknown");
                    }
                }

                var result = new JSONObject();
                result["psycasters"] = psycasters;
                result["count"] = psycasters.Count;
                return result.ToString();
            }

            // Get specific psycaster
            var psycaster = FindPawnByName(name);
            if (psycaster == null)
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (psycaster.psychicEntropy == null || psycaster.GetPsylinkLevel() == 0)
                return ToolExecutor.JsonError(name + " is not a psycaster (no psylink).");

            var obj = new JSONObject();
            obj["psycaster"] = psycaster.Name?.ToStringShort ?? "Unknown";
            obj["psylink_level"] = psycaster.GetPsylinkLevel();
            
            // Neural heat
            var currentHeat = psycaster.psychicEntropy.EntropyValue;
            var maxHeat = psycaster.psychicEntropy.MaxEntropy;
            obj["neural_heat"] = currentHeat.ToString("F0") + "/" + maxHeat.ToString("F0");
            obj["neural_heat_percent"] = (currentHeat / maxHeat * 100).ToString("F0") + "%";
            
            // Psyfocus
            var psyfocus = psycaster.psychicEntropy.CurrentPsyfocus;
            obj["psyfocus"] = (psyfocus * 100).ToString("F0") + "%";

            // Get all psycast abilities
            var psycasts = new JSONArray();
            if (psycaster.abilities != null)
            {
                foreach (var ability in psycaster.abilities.abilities)
                {
                    // Only include psycasts (not other abilities)
                    if (ability.def.defName.StartsWith("Psycast_") || ability.def.category?.defName == "Psycast")
                    {
                        var psycast = new JSONObject();
                        psycast["name"] = ability.def.LabelCap.ToString();
                        psycast["description"] = ability.def.description ?? "";
                        
                        // Neural heat cost
                        if (ability.def.statBases != null)
                        {
                            var heatStat = ability.def.statBases.FirstOrDefault(s => s.stat == StatDefOf.Ability_EntropyGain);
                            if (heatStat != null)
                                psycast["neural_heat_cost"] = heatStat.value.ToString("F0");
                        }

                        // Psyfocus cost
                        if (ability.def.statBases != null)
                        {
                            var focusStat = ability.def.statBases.FirstOrDefault(s => s.stat?.defName == "Ability_PsyfocusCost");
                            if (focusStat != null)
                                psycast["psyfocus_cost"] = (focusStat.value * 100).ToString("F0") + "%";
                        }

                        // Cooldown status
                        var cooldownTicks = ability.CooldownTicksRemaining;
                        if (cooldownTicks > 0)
                        {
                            psycast["cooldown_remaining"] = cooldownTicks.ToStringTicksToPeriod();
                            psycast["on_cooldown"] = true;
                        }
                        else
                        {
                            psycast["on_cooldown"] = false;
                        }

                        // Can cast right now?
                        var canCastReport = ability.CanCast;
                        psycast["can_cast"] = canCastReport.Accepted;
                        if (!canCastReport.Accepted)
                        {
                            // Use the report reason
                            psycast["cannot_cast_reason"] = canCastReport.Reason?.ToString() ?? "Cannot cast";
                        }

                        psycasts.Add(psycast);
                    }
                }
            }

            obj["available_psycasts"] = psycasts;
            obj["psycast_count"] = psycasts.Count;

            // Combat tactical suggestions
            if (psycasts.Count > 0)
            {
                var suggestions = new JSONArray();
                foreach (JSONObject psycast in psycasts)
                {
                    var psycastName = psycast["name"]?.Value ?? "";
                    
                    // Add tactical combat usage based on common psycasts
                    if (psycastName.Contains("Skip"))
                        suggestions.Add("Skip: Teleport colonists into/out of combat or relocate enemies");
                    else if (psycastName.Contains("Berserk"))
                        suggestions.Add("Berserk: Turn enemy raiders against each other");
                    else if (psycastName.Contains("Invisibility"))
                        suggestions.Add("Invisibility: Stealth rescues or assassination");
                    else if (psycastName.Contains("Wallraise"))
                        suggestions.Add("Wallraise: Create instant cover in killbox or block chokepoints");
                    else if (psycastName.Contains("Smokepop"))
                        suggestions.Add("Smokepop: Obscure enemy vision for retreats or advances");
                    else if (psycastName.Contains("Stun"))
                        suggestions.Add("Stun: Disable high-threat targets temporarily");
                }
                
                if (suggestions.Count > 0)
                    obj["combat_suggestions"] = suggestions;
            }

            return obj.ToString();
        }

        public static string GetGenes(string name)
        {
            // Check for Biotech DLC
            if (!ModsConfig.BiotechActive)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Biotech DLC not installed";
                errObj["dlc_required"] = "Biotech";
                return errObj.ToString();
            }

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // If no name provided, list all colonists with genes
            if (string.IsNullOrEmpty(name))
            {
                var geneCarriers = new JSONArray();
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    if (pawn.genes != null && pawn.genes.GenesListForReading.Any())
                    {
                        var p = new JSONObject();
                        p["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                        p["xenotype"] = pawn.genes.xenotypeName ?? pawn.genes.XenotypeLabel ?? "Baseline";
                        p["gene_count"] = pawn.genes.GenesListForReading.Count;
                        geneCarriers.Add(p);
                    }
                }

                var result = new JSONObject();
                result["colonists_with_genes"] = geneCarriers;
                result["count"] = geneCarriers.Count;
                return result.ToString();
            }

            // Get specific colonist's genes
            var colonist = FindPawnByName(name);
            if (colonist == null)
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (colonist.genes == null)
                return ToolExecutor.JsonError(name + " has no gene system (non-human pawn?).");

            var obj = new JSONObject();
            obj["colonist"] = colonist.Name?.ToStringShort ?? "Unknown";
            obj["xenotype"] = colonist.genes.xenotypeName ?? colonist.genes.XenotypeLabel ?? "Baseline";

            // All genes
            var allGenes = new JSONArray();
            var combatGenes = new JSONArray();

            foreach (var gene in colonist.genes.GenesListForReading)
            {
                var geneObj = new JSONObject();
                geneObj["name"] = gene.LabelCap.ToString();
                geneObj["description"] = gene.def.description ?? "";

                // Check if combat-relevant
                bool isCombatRelevant = false;
                var combatEffects = new JSONArray();

                // Check stat offsets
                if (gene.def.statOffsets != null)
                {
                    foreach (var statMod in gene.def.statOffsets)
                    {
                        var statName = statMod.stat.defName;
                        var value = statMod.value;

                        // Combat-relevant stats
                        if (statName.Contains("Shooting") || statName.Contains("Melee") || 
                            statName.Contains("Armor") || statName.Contains("Dodge") ||
                            statName == "MoveSpeed" || statName == "PainShockThreshold")
                        {
                            isCombatRelevant = true;
                            combatEffects.Add(statMod.stat.LabelCap.ToString() + " " + (value > 0 ? "+" : "") + value.ToString("F2"));
                        }

                        geneObj[statName] = value.ToString("F2");
                    }
                }

                // Check stat factors (multipliers)
                if (gene.def.statFactors != null)
                {
                    foreach (var statMod in gene.def.statFactors)
                    {
                        var statName = statMod.stat.defName;
                        var factor = statMod.value;

                        if (statName.Contains("Shooting") || statName.Contains("Melee") || 
                            statName.Contains("Armor") || statName == "MoveSpeed")
                        {
                            isCombatRelevant = true;
                            var percentChange = (factor - 1f) * 100f;
                            combatEffects.Add(statMod.stat.LabelCap.ToString() + " x" + factor.ToString("F2") + " (" + (percentChange > 0 ? "+" : "") + percentChange.ToString("F0") + "%)");
                        }

                        geneObj[statName + "_factor"] = factor.ToString("F2");
                    }
                }

                // Check for special abilities granted by genes
                if (gene.def.abilities != null && gene.def.abilities.Any())
                {
                    isCombatRelevant = true;
                    var abilities = new JSONArray();
                    foreach (var abilityDef in gene.def.abilities)
                    {
                        abilities.Add(abilityDef.LabelCap.ToString());
                        combatEffects.Add("Ability: " + abilityDef.LabelCap.ToString());
                    }
                    geneObj["abilities"] = abilities;
                }

                // Check for damage/environmental resistance
                if (gene.def.damageFactors != null)
                {
                    foreach (var damageFactor in gene.def.damageFactors)
                    {
                        isCombatRelevant = true;
                        var percentChange = (1f - damageFactor.factor) * 100f;
                        combatEffects.Add(damageFactor.damageDef.LabelCap.ToString() + " resistance " + percentChange.ToString("F0") + "%");
                    }
                }

                // Add to appropriate lists
                allGenes.Add(geneObj);

                if (isCombatRelevant)
                {
                    var combatGene = new JSONObject();
                    combatGene["gene"] = gene.LabelCap.ToString();
                    combatGene["effects"] = combatEffects;
                    
                    // Add combat usage suggestion
                    var geneName = gene.def.defName;
                    if (geneName.Contains("FireSpew") || gene.LabelCap.ToString().ToLower().Contains("fire"))
                        combatGene["combat_use"] = "Area damage, anti-melee";
                    else if (geneName.Contains("AcidSpray") || gene.LabelCap.ToString().ToLower().Contains("acid"))
                        combatGene["combat_use"] = "Armor penetration";
                    else if (geneName.Contains("Robust") || gene.LabelCap.ToString().ToLower().Contains("tough"))
                        combatGene["combat_use"] = "Frontline tank";
                    else if (geneName.Contains("Speed") || gene.LabelCap.ToString().ToLower().Contains("speed"))
                        combatGene["combat_use"] = "Hit-and-run tactics, kiting";
                    else if (geneName.Contains("ToxResist") || geneName.Contains("ToxImmune"))
                        combatGene["combat_use"] = "Effective in toxic areas (insect hives)";
                    else if (combatEffects.Count > 0)
                        combatGene["combat_use"] = "Combat advantage";

                    combatGenes.Add(combatGene);
                }
            }

            obj["all_genes"] = allGenes;
            obj["total_gene_count"] = allGenes.Count;
            obj["combat_genes"] = combatGenes;
            obj["combat_gene_count"] = combatGenes.Count;

            return obj.ToString();
        }

        public static string GetMechanitorInfo(string name)
        {
            // Check for Biotech DLC
            if (!ModsConfig.BiotechActive)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Biotech DLC not installed";
                errObj["dlc_required"] = "Biotech";
                return errObj.ToString();
            }

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // If no name provided, list all mechanitors
            if (string.IsNullOrEmpty(name))
            {
                var mechanitors = new JSONArray();
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    if (pawn.mechanitor != null)
                    {
                        var m = new JSONObject();
                        m["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                        m["controlled_mechs"] = pawn.mechanitor.ControlledPawns.Count;
                        m["bandwidth"] = pawn.mechanitor.UsedBandwidth + "/" + pawn.mechanitor.TotalBandwidth;
                        mechanitors.Add(m);
                    }
                }

                var result = new JSONObject();
                result["mechanitors"] = mechanitors;
                result["count"] = mechanitors.Count;
                return result.ToString();
            }

            // Get specific mechanitor
            var mechanitor = FindPawnByName(name);
            if (mechanitor == null)
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (mechanitor.mechanitor == null)
                return ToolExecutor.JsonError(name + " is not a mechanitor.");

            var obj = new JSONObject();
            obj["mechanitor"] = mechanitor.Name?.ToStringShort ?? "Unknown";
            obj["bandwidth_used"] = mechanitor.mechanitor.UsedBandwidth;
            obj["bandwidth_max"] = mechanitor.mechanitor.TotalBandwidth;
            obj["bandwidth_available"] = mechanitor.mechanitor.TotalBandwidth - mechanitor.mechanitor.UsedBandwidth;

            // List controlled mechs
            var mechs = new JSONArray();
            foreach (var mech in mechanitor.mechanitor.ControlledPawns)
            {
                var mechObj = new JSONObject();
                mechObj["name"] = mech.LabelShort;
                mechObj["type"] = mech.kindDef?.LabelCap.ToString() ?? "Unknown";
                
                // Health
                mechObj["health"] = mech.health.summaryHealth.SummaryHealthPercent.ToString("P0");
                mechObj["hit_points"] = Mathf.RoundToInt(mech.health.summaryHealth.SummaryHealthPercent * 100f) + "/100";

                // Weapon
                var weapon = mech.equipment?.Primary;
                if (weapon != null)
                {
                    mechObj["weapon"] = weapon.LabelCap.ToString();
                }

                // Combat role based on mech type
                var mechType = mech.kindDef?.defName ?? "";
                if (mechType.Contains("Scyther") || mechType.Contains("Scorcher"))
                    mechObj["role"] = "Anti-infantry, melee/close range";
                else if (mechType.Contains("Lancer") || mechType.Contains("Pikeman"))
                    mechObj["role"] = "Long-range support";
                else if (mechType.Contains("Centipede"))
                    mechObj["role"] = "Heavy assault, high HP tank";
                else if (mechType.Contains("Lifter") || mechType.Contains("Constructoid"))
                    mechObj["role"] = "Non-combat (hauling/construction)";
                else
                    mechObj["role"] = "Combat support";

                // Current status
                if (mech.Downed)
                    mechObj["status"] = "Downed";
                else if (mech.Dead)
                    mechObj["status"] = "Dead";
                else if (mech.Drafted)
                    mechObj["status"] = "Drafted";
                else
                    mechObj["status"] = "Active";

                // Bandwidth cost
                // Note: Bandwidth API changed in RimWorld 1.6 - skipping for now
                // TODO: Update when new API is documented
                if (mech.GetComp<CompOverseerSubject>() != null)
                {
                    mechObj["bandwidth_cost"] = 1; // Default assumption
                }

                mechs.Add(mechObj);
            }

            obj["controlled_mechs"] = mechs;
            obj["mech_count"] = mechs.Count;

            // Deployment suggestions
            if (mechs.Count > 0)
            {
                var suggestions = new JSONArray();
                suggestions.Add("Position ranged mechs (Lancers/Pikemen) behind cover for suppression");
                suggestions.Add("Use melee mechs (Scythers/Scorchers) to block chokepoints");
                suggestions.Add("Keep mechanitor safe - losing them cuts mech control");
                
                // Check for specific mech compositions
                var hasMelee = mechanitor.mechanitor.ControlledPawns.Any(m => 
                    m.kindDef?.defName.Contains("Scyther") == true || 
                    m.kindDef?.defName.Contains("Scorcher") == true);
                var hasRanged = mechanitor.mechanitor.ControlledPawns.Any(m => 
                    m.kindDef?.defName.Contains("Lancer") == true || 
                    m.kindDef?.defName.Contains("Pikeman") == true);
                var hasHeavy = mechanitor.mechanitor.ControlledPawns.Any(m => 
                    m.kindDef?.defName.Contains("Centipede") == true);

                if (hasMelee && hasRanged)
                    suggestions.Add("Good mix: Use melee mechs as frontline, ranged for support");
                if (hasHeavy)
                    suggestions.Add("Centipedes: High HP tanks, use to absorb damage");
                if (!hasMelee && hasRanged)
                    suggestions.Add("Consider adding melee mechs for frontline defense");

                obj["tactical_suggestions"] = suggestions;
            }

            return obj.ToString();
        }
    }
}
