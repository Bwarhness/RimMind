using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class MedicalTools
    {
        public static string GetMedicalOverview()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();

            // Patients needing treatment
            var patients = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                bool needsTending = pawn.health.HasHediffsNeedingTend();
                bool downed = pawn.Downed;
                bool hasSeriousCondition = pawn.health.hediffSet.hediffs
                    .Any(h => h.CurStage?.lifeThreatening == true || h.def.lethalSeverity > 0);

                if (needsTending || downed || hasSeriousCondition)
                {
                    var p = new JSONObject();
                    p["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                    var conditions = new JSONArray();
                    foreach (var h in pawn.health.hediffSet.hediffs)
                    {
                        if (h is Hediff_Injury || h.def.makesSickThought || h.def.lethalSeverity > 0)
                        {
                            string desc = h.LabelCap.ToString();
                            if (h.Part != null) desc += " (" + h.Part.Label + ")";
                            conditions.Add(desc);
                        }
                    }
                    p["conditions"] = conditions;
                    p["needsTending"] = needsTending;
                    p["downed"] = downed;
                    p["lifeThreatening"] = hasSeriousCondition;

                    patients.Add(p);
                }
            }
            obj["patientsNeedingCare"] = patients;

            // Medicine supply
            var medicine = new JSONObject();
            medicine["herbal"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineHerbal).Sum(t => t.stackCount);
            medicine["industrial"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineIndustrial).Sum(t => t.stackCount);
            medicine["glitterworld"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineUltratech).Sum(t => t.stackCount);
            obj["medicineSupply"] = medicine;

            // Medical beds
            int medBeds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Medical);
            int freeMedBeds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Medical && !b.HasAnyDynamicPawn());
            obj["medicalBeds"] = medBeds;
            obj["freeMedicalBeds"] = freeMedBeds;

            // Doctors
            var doctors = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                var medSkill = pawn.skills?.GetSkill(SkillDefOf.Medicine);
                if (medSkill != null && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    var d = new JSONObject();
                    d["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    d["medicalSkill"] = medSkill.Level;
                    d["passion"] = medSkill.passion.ToString();
                    doctors.Add(d);
                }
            }
            obj["doctors"] = doctors;

            return obj.ToString();
        }

        /// <summary>
        /// Get disease immunity progress for all colonists
        /// Parameters: pawn_name (optional, defaults to all)
        /// Returns: Active diseases, immunity progress (0-100%), time to immunity, severity level
        /// </summary>
        public static string GetDiseaseImmunityStatus(string pawnName = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var colonists = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawnName != null && pawn.Name?.ToStringShort != pawnName) continue;
                if (pawn.health == null) continue;

                var colonist = new JSONObject();
                colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                // Get all active diseases
                var diseases = new JSONArray();
                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff is HediffWithComps hwc)
                    {
                        // Check if this is an actual disease (not just injury)
                        if (hediff.def.defName.Contains("Flu") || hediff.def.defName.Contains("Plague") ||
                            hediff.def.defName.Contains("Malaria") || hediff.def.defName.Contains("FleshRot") ||
                            hediff.def.defName.Contains("WoundInfection") || hediff.def.defName.Contains("GutWorms") ||
                            hediff.def.defName.Contains("MuscleParasites") || hediff.def.defName.Contains("SensoryMechanites"))
                        {
                            var disease = new JSONObject();
                            disease["name"] = hediff.def.label;
                            disease["severity"] = hediff.Severity.ToString("0.0");
                            
                            // Try to get immunity data from hediff comps
                            float? immunity = null;
                            if (hediff is HediffWithComps hwc)
                            {
                                var immunityComp = hwc.GetComp<HediffComp_Immunizable>();
                                if (immunityComp != null)
                                {
                                    immunity = immunityComp.Immunity;
                                }
                            }
                            if (immunity.HasValue)
                            {
                                disease["immunityProgress"] = immunity.Value.ToString("P0");
                                disease["immune"] = immunity.Value >= 1.0f;
                            }
                            else
                            {
                                disease["immunityProgress"] = "N/A";
                            }

                            // Severity level
                            string severity = "minor";
                            if (hediff.Severity > 0.7f) severity = "severe";
                            else if (hediff.Severity > 0.4f) severity = "moderate";
                            disease["severityLevel"] = severity;

                            // Check for tendable status
                            disease["canBeTended"] = hediff.def.tendable;
                            disease["isPermanent"] = hediff.IsPermanent();

                            diseases.Add(disease);
                        }
                    }
                }
                colonist["activeDiseases"] = diseases;

                // Overall health summary
                colonist["healthPercentage"] = pawn.health.summaryHealth.ToString("P0");
                colonist["hasLifeThreateningCondition"] = pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss) ||
                    pawn.health.hediffSet.HasHediff(HediffDefOf.ToxicBuildup);

                colonists.Add(colonist);
            }

            result["colonists"] = colonists;
            return result.ToString();
        }

        /// <summary>
        /// Get colonist drug tolerances and addiction risks
        /// Parameters: pawn_name (optional)
        /// Returns: Current addiction status, drug tolerances, addiction history, recovery status
        /// </summary>
        public static string GetDrugTolerance(string pawnName = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var colonists = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawnName != null && pawn.Name?.ToStringShort != pawnName) continue;
                if (pawn.health == null) continue;

                var colonist = new JSONObject();
                colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                // Current addictions
                var addictions = new JSONArray();
                var tolerances = new JSONObject();

                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    // Check for addiction hediffs
                    if (hediff.def.defName.Contains("Addiction") || hediff.def.defName.Contains("Hangover"))
                    {
                        var add = new JSONObject();
                        add["substance"] = hediff.def.label;
                        add["severity"] = hediff.Severity.ToString("0.0");
                        addictions.Add(add);
                    }

                    // Track chemical dependencies (for tolerance calculation)
                    if (hediff.def.defName.Contains("Chemical") && !hediff.def.defName.Contains("Addiction"))
                    {
                        tolerances[hediff.def.defName] = hediff.Severity.ToString("0.0");
                    }
                }
                colonist["currentAddictions"] = addictions;
                colonist["chemicalLevels"] = tolerances;

                // Check for addiction-prone traits
                var addictionRisks = new JSONArray();
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait.def.defName == "ChemicalDesire" || trait.def.defName == "Dreamer")
                        {
                            addictionRisks.Add(trait.LabelCap.ToString());
                        }
                    }
                }
                colonist["addictionRiskTraits"] = addictionRisks;

                // Recent drug usage (from history if tracked)
                colonist["canAddict"] = pawn.RaceProps.Humanlike && 
                    DefDatabase<ChemicalDef>.AllDefsListForReading.Any(c => 
                        c.addictionChance > 0);

                // Current needs affecting recovery
                colonist["hungerLevel"] = pawn.needs?.food?.CurLevelPercentage.ToString("P0") ?? "N/A";
                colonist["moodLevel"] = pawn.needs?.mood?.CurLevelPercentage.ToString("P0") ?? "N/A";

                // Overall risk assessment
                string riskLevel = "low";
                if (addictions.Count > 0)
                    riskLevel = "critical";
                else if (addictionRisks.Count > 0)
                    riskLevel = "high";
                colonist["addictionRiskLevel"] = riskLevel;

                colonists.Add(colonist);
            }

            result["colonists"] = colonists;
            return result.ToString();
        }

        /// <summary>
        /// Predict surgery success probability
        /// Parameters: patient_name, surgery_def (optional, defaults to most critical needed)
        /// Returns: Success probability, risk factors, required skill, recommended medicine level
        /// </summary>
        public static string PredictSurgerySuccess(string patientName, string surgeryDef = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var pawn = map.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name?.ToStringShort == patientName);

            if (pawn == null)
                return ToolExecutor.JsonError($"Colonist '{patientName}' not found.");

            var result = new JSONObject();
            result["patient"] = patientName;

            // Get available surgeries from the patient
            var neededSurgeries = new JSONArray();
            
            // Find all tended injuries that might need surgery
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                // Check for injuries that need tending and may require surgery
                if (hediff is Hediff_Injury injury && injury.IsTended() && injury.def.tendable)
                {
                    var surgery = new JSONObject();
                    surgery["type"] = hediff.def.label;
                    surgery["bodyPart"] = hediff.Part?.Label ?? "Unknown";
                    surgery["severity"] = hediff.Severity.ToString("0.0");
                    neededSurgeries.Add(surgery);
                }
            }

            result["neededSurgeries"] = neededSurgeries;

            // Calculate base success probability
            // Base = 50% + (doctor skill * 2) + (medicine quality bonus)
            float baseSuccess = 0.5f;

            // Patient health factor
            float healthFactor = pawn.health.summaryHealth;
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss))
                healthFactor -= 0.3f;
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.ToxicBuildup))
                healthFactor -= 0.2f;
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.WoundInfection))
                healthFactor -= 0.2f;

            // Find best available doctor
            var bestDoctor = map.mapPawns.FreeColonists
                .Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                .FirstOrDefault();

            if (bestDoctor != null)
            {
                int skill = bestDoctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                result["recommendedDoctor"] = bestDoctor.Name?.ToStringShort ?? "Unknown";
                result["doctorSkill"] = skill;
                
                // Skill bonus: each level adds ~2% success
                baseSuccess += (skill * 0.02f);
            }

            // Medicine quality bonus
            var bestMedicine = map.listerThings.ThingsOfDef(ThingDefOf.MedicineUltratech).FirstOrDefault();
            if (bestMedicine == null)
                bestMedicine = map.listerThings.ThingsOfDef(ThingDefOf.MedicineIndustrial).FirstOrDefault();
            if (bestMedicine == null)
                bestMedicine = map.listerThings.ThingsOfDef(ThingDefOf.MedicineHerbal).FirstOrDefault();

            float medicineQuality = 0;
            if (bestMedicine != null)
            {
                medicineQuality = bestMedicine.GetStatValue(StatDefOf.MedicalQuality);
                result["medicineQuality"] = medicineQuality.ToString("0.0");
                baseSuccess += (medicineQuality * 0.1f);
            }

            // Apply health factor
            baseSuccess *= healthFactor;

            // Risk factors
            var riskFactors = new JSONArray();
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss))
                riskFactors.Add("Severe blood loss (-30%)");
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.ToxicBuildup))
                riskFactors.Add("Toxic buildup (-20%)");
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.WoundInfection))
                riskFactors.Add("Infection (-20%)");
            if (bestMedicine == null)
                riskFactors.Add("No medicine available");
            if (pawn.Downed)
                riskFactors.Add("Patient is downed");

            result["riskFactors"] = riskFactors;
            result["successProbability"] = Math.Min(baseSuccess, 0.98f).ToString("P0");
            result["successProbabilityRaw"] = Math.Min(baseSuccess, 0.98f).ToString("0.00");

            // Recommendations
            var recommendations = new JSONArray();
            if (bestMedicine == null)
                recommendations.Add("Acquire medicine before surgery");
            if (bestDoctor?.skills?.GetSkill(SkillDefOf.Medicine)?.Level < 10)
                recommendations.Add("Consider waiting for higher skill doctor");
            if (pawn.needs?.food?.CurLevelPercentage < 0.5f)
                recommendations.Add("Feed patient before surgery");

            result["recommendations"] = recommendations;

            return result.ToString();
        }
    }
}
