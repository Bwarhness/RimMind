using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class PrisonerSlaveTools
    {
        public static string GetPrisonerStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var prisoners = map.mapPawns.AllPawns.Where(p => 
                p.IsPrisonerOfColony && p.Spawned).ToList();

            if (prisoners.Count == 0)
            {
                var result = new JSONObject();
                result["prisoners"] = new JSONArray();
                result["count"] = 0;
                result["message"] = "No prisoners in colony";
                return result.ToString();
            }

            var result = new JSONObject();
            var prisonerList = new JSONArray();

            foreach (var prisoner in prisoners)
            {
                var p = new JSONObject();
                p["name"] = prisoner.Name?.ToStringShort ?? prisoner.LabelShort;
                p["race"] = prisoner.kindDef?.label ?? prisoner.def.label;
                p["resistance"] = prisoner.guest?.Resistance.ToString("F1") ?? "0";
                p["will"] = prisoner.guest?.Will.ToString("F1") ?? "0";
                p["recruitable"] = prisoner.guest?.Recruitable;

                // Recruitment chance
                float recruitChance = RecruitUtility.RecruitChanceFactorForPawn(prisoner);
                p["recruit_chance"] = (recruitChance * 100f).ToString("F0") + "%";

                // Estimated recruitment time (days)
                // Rough estimate: resistance / (warden_social * 0.3 + 1)
                var wardens = GetAvailableWardens();
                float bestWarden = wardens.Count > 0 ? wardens[0].socialSkill : 0f;
                float daysToRecruit = (prisoner.guest?.Resistance ?? 0) / (bestWarden * 0.3f + 1f);
                p["estimated_days_to_recruit"] = daysToRecruit.ToString("F1");

                // Health status
                p["health_percent"] = prisoner.health.summaryHealth.SummaryHealthPercent.ToString("P0");

                // Mental state risk
                p["mental_state_risk"] = GetMentalStateRisk(prisoner);

                prisonerList.Add(p);
            }

            result["prisoners"] = prisonerList;
            result["count"] = prisonerList.Count;

            // Warden info
            var wardenInfo = new JSONObject();
            var wardens = GetAvailableWardens();
            wardenInfo["count"] = wardens.Count;
            var wardenList = new JSONArray();
            foreach (var w in wardens)
            {
                wardenList.Add(w.Name?.ToStringShort ?? w.LabelShort);
            }
            wardenInfo["wardens"] = wardenList;
            result["wardens"] = wardenInfo;

            return result.ToString();
        }

        public static string GetSlaveStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Check for Ideology DLC
            bool ideologyActive = ModsConfig.IdeologyActive;
            var result = new JSONObject();
            result["ideology_active"] = ideologyActive;

            var slaves = map.mapPawns.AllPawns.Where(p => 
                p.IsSlaveOfColony && p.Spawned).ToList();

            if (slaves.Count == 0)
            {
                result["slaves"] = new JSONArray();
                result["count"] = 0;
                result["message"] = ideologyActive ? "No slaves in colony" : "Ideology DLC not active";
                return result.ToString();
            }

            var slaveList = new JSONArray();

            foreach (var slave in slaves)
            {
                var s = new JSONObject();
                s["name"] = slave.Name?.ToStringShort ?? slave.LabelShort;
                s["race"] = slave.kindDef?.label ?? slave.def.label;

                if (ideologyActive)
                {
                    // Suppression level
                    var suppression = slave.needs?.TryGetNeed<Need_Suppression>();
                    if (suppression != null)
                    {
                        s["suppression"] = suppression.CurLevel.ToString("F1");
                        s["suppression_percent"] = suppression.CurLevelPercentage.ToString("P0");

                        // Rebellion risk
                        string risk;
                        if (suppression.CurLevelPercentage < 0.3f)
                            risk = "critical";
                        else if (suppression.CurLevelPercentage < 0.5f)
                            risk = "high";
                        else if (suppression.CurLevelPercentage < 0.7f)
                            risk = "medium";
                        else
                            risk = "low";
                        s["rebellion_risk"] = risk;
                    }
                    else
                    {
                        s["suppression"] = "unknown";
                        s["rebellion_risk"] = "unknown";
                    }

                    // Interaction mode
                    s["interaction_mode"] = slave.guest?.SlaveInteractionMode?.defName ?? "None";
                }

                // Health
                s["health_percent"] = slave.health.summaryHealth.SummaryHealthPercent.ToString("P0");

                // Work
                s["work_type"] = slave.workSettings?.CurrentWork.ToString() ?? "None";

                slaveList.Add(s);
            }

            result["slaves"] = slaveList;
            result["count"] = slaveList.Count;

            // Count high-risk slaves
            if (ideologyActive)
            {
                int critical = 0;
                int high = 0;
                foreach (var slave in slaves)
                {
                    var suppression = slave.needs?.TryGetNeed<Need_Suppression>();
                    if (suppression != null)
                    {
                        if (suppression.CurLevelPercentage < 0.3f) critical++;
                        else if (suppression.CurLevelPercentage < 0.5f) high++;
                    }
                }
                result["critical_suppression"] = critical;
                result["high_suppression"] = high;
            }

            return result.ToString();
        }

        public static string AnalyzePrisonRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            var prisoners = map.mapPawns.AllPawns.Where(p => 
                p.IsPrisonerOfColony && p.Spawned).ToList();
            var wardens = GetAvailableWardens();

            result["prisoner_count"] = prisoners.Count;
            result["warden_count"] = wardens.Count;

            // Warden-to-prisoner ratio
            float ratio = wardens.Count > 0 && prisoners.Count > 0 
                ? (float)wardens.Count / prisoners.Count 
                : 0f;
            result["warden_ratio"] = ratio.ToString("F2");

            // Risk assessment
            string overallRisk;
            if (prisoners.Count == 0)
                overallRisk = "none";
            else if (wardens.Count == 0)
                overallRisk = "critical";
            else if (ratio < 0.2f)
                overallRisk = "high";
            else if (ratio < 0.5f)
                overallRisk = "medium";
            else
                overallRisk = "low";
            result["overall_break_risk"] = overallRisk;

            // High-risk prisoners
            var highRisk = new JSONArray();
            foreach (var prisoner in prisoners)
            {
                var risk = GetMentalStateRisk(prisoner);
                if (risk == "critical" || risk == "high")
                {
                    var r = new JSONObject();
                    r["name"] = prisoner.Name?.ToStringShort ?? prisoner.LabelShort;
                    r["risk"] = risk;
                    highRisk.Add(r);
                }
            }
            result["high_risk_prisoners"] = highRisk;

            // Recommendations
            var recommendations = new JSONArray();
            if (wardens.Count == 0 && prisoners.Count > 0)
                recommendations.Add("Assign at least 1 warden to prevent prison breaks");
            else if (ratio < 0.3f)
                recommendations.Add("Increase warden coverage - current ratio is too low");
            
            foreach (var r in highRisk)
                recommendations.Add("Monitor " + r["name"] + " for mental breaks");
            
            result["recommendations"] = recommendations;

            return result.ToString();
        }

        public static string GetRecruitmentForecast(string name)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var prisoners = map.mapPawns.AllPawns.Where(p => 
                p.IsPrisonerOfColony && p.Spawned).ToList();

            var wardens = GetAvailableWardens();
            if (wardens.Count == 0)
                return ToolExecutor.JsonError("No wardens available for recruitment.");

            var result = new JSONObject();

            // Filter by name if provided
            if (!string.IsNullOrEmpty(name))
            {
                string lower = name.ToLower();
                prisoners = prisoners.Where(p => 
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.LabelShort?.ToLower() == lower).ToList();
            }

            if (prisoners.Count == 0)
                return ToolExecutor.JsonError("No prisoners found.");

            var forecasts = new JSONArray();

            foreach (var prisoner in prisoners)
            {
                var f = new JSONObject();
                f["name"] = prisoner.Name?.ToStringShort ?? prisoner.LabelShort;
                f["current_resistance"] = prisoner.guest?.Resistance.ToString("F1") ?? "0";

                // Calculate best warden
                var bestWarden = wardens.OrderByDescending(w => w.socialSkill).First();
                f["best_warden"] = bestWarden.Name?.ToStringShort ?? bestWarden.LabelShort;
                f["warden_social"] = bestWarden.socialSkill.ToString("F1");

                // Resistance reduction rate per day (roughly)
                float reductionRate = bestWarden.socialSkill * 0.3f + 1f;
                f["resistance_per_day"] = reductionRate.ToString("F1");

                // Days to recruit
                float resistance = prisoner.guest?.Resistance ?? 0;
                float days = resistance / reductionRate;
                f["days_to_zero_resistance"] = days.ToString("F1");

                // Recruitment chance on next attempt
                float chance = RecruitUtility.RecruitChanceFactorForPawn(prisoner);
                f["recruit_chance_next_attempt"] = (chance * 100f).ToString("F0") + "%";

                // Success probability
                f["likely_to_succeed"] = chance > 0.8f ? "yes" : (chance > 0.5f ? "uncertain" : "no");

                forecasts.Add(f);
            }

            result["forecasts"] = forecasts;
            return result.ToString();
        }

        private static List<Pawn> GetAvailableWardens()
        {
            var map = Find.CurrentMap;
            if (map == null) return new List<Pawn>();

            return map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .Where(p => p.workSettings?.CurrentWork == WorkTypeDefOf.Warden && p.Spawned)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                .ToList();
        }

        private static string GetMentalStateRisk(Pawn pawn)
        {
            if (pawn.InMentalState)
                return "critical";

            // Check for mental break threshold via pawn's mindState
            var mentalBreakChance = pawn.mindState?.mentalBreakManager?.def?.baseMentalBreakChance ?? 0;
            var recentBreaks = pawn.mindState?.mentalStateHandler?.RecentMentalBreaks ?? 0;

            if (recentBreaks > 0 || mentalBreakChance > 0.3f)
                return "critical";
            else if (mentalBreakChance > 0.15f)
                return "high";
            else if (mentalBreakChance > 0.05f)
                return "medium";
            else
                return "low";
        }
    }
}
