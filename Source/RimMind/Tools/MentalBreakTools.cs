using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class MentalBreakTools
    {
        /// <summary>
        /// Handle a mental break on a colonist with the specified intervention action.
        /// Actions: send_to_bed, cancel, recreation, restrain, arrest
        /// </summary>
        public static string HandleMentalBreak(string colonistName, string action)
        {
            if (string.IsNullOrEmpty(colonistName))
                return ToolExecutor.JsonError("Colonist name is required.");

            if (string.IsNullOrEmpty(action))
                return ToolExecutor.JsonError("Action is required. Options: send_to_bed, cancel, recreation, restrain, arrest");

            var pawn = ColonistTools.FindPawnByName(colonistName);
            if (pawn == null)
                return ToolExecutor.JsonError("Colonist '" + colonistName + "' not found.");

            // Check if colonist is actually having a mental break
            if (pawn.MentalStateDef == null)
                return ToolExecutor.JsonError("Colonist '" + colonistName + "' is not having a mental break.");

            var result = new JSONObject();
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["mentalState"] = pawn.MentalStateDef.label;
            result["action"] = action;

            bool success = false;
            string details = "";

            switch (action.ToLower())
            {
                case "send_to_bed":
                    success = SendToBed(pawn, out details);
                    break;

                case "cancel":
                    success = CancelCurrentJob(pawn, out details);
                    break;

                case "recreation":
                case "assign_recreation":
                case "joy":
                    success = AssignRecreation(pawn, out details);
                    break;

                case "restrain":
                    success = RestrainColonist(pawn, out details);
                    break;

                case "arrest":
                    success = ArrestColonist(pawn, out details);
                    break;

                default:
                    return ToolExecutor.JsonError("Unknown action '" + action + "'. Options: send_to_bed, cancel, recreation, restrain, arrest");
            }

            result["success"] = success;
            result["details"] = details;

            return result.ToString();
        }

        /// <summary>
        /// List colonists at risk of mental break with recommended interventions.
        /// </summary>
        public static string GetMentalBreakRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var atRiskColonists = new JSONArray();

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.needs?.mood == null) continue;

                var need_mood = pawn.needs.mood;
                float moodLevel = need_mood.CurLevel;
                float moodPercentage = need_mood.CurLevelPercentage;

                // Get break thresholds
                var mentalBreaker = pawn.mindState.mentalBreaker;
                float breakThresholdMinor = mentalBreaker.BreakThresholdMinor;
                float breakThresholdMajor = mentalBreaker.BreakThresholdMajor;
                float breakThresholdExtreme = mentalBreaker.BreakThresholdExtreme;

                // Determine risk level
                string riskLevel = null;
                float distanceToBreak = moodLevel - breakThresholdExtreme;

                if (pawn.InMentalState)
                {
                    riskLevel = "active";
                }
                else if (moodLevel <= breakThresholdExtreme)
                {
                    riskLevel = "critical";
                }
                else if (moodLevel <= breakThresholdMajor)
                {
                    riskLevel = "high";
                }
                else if (moodLevel <= breakThresholdMinor)
                {
                    riskLevel = "medium";
                }

                // Only include colonists with some level of risk
                if (riskLevel != null)
                {
                    var colonist = new JSONObject();
                    colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    colonist["moodLevel"] = moodPercentage.ToString("P0");
                    colonist["moodValue"] = moodLevel.ToString("0.00");
                    colonist["riskLevel"] = riskLevel;

                    // Current mental state
                    if (pawn.MentalStateDef != null)
                    {
                        colonist["currentMentalState"] = pawn.MentalStateDef.label;
                        colonist["mentalStateType"] = pawn.MentalStateDef.defName;

                        // Get recovery time estimate if currently in mental state
                        colonist["recoveryTimeEstimate"] = EstimateRecoveryTime(pawn);
                    }

                    colonist["breakThresholdExtreme"] = breakThresholdExtreme.ToString("0.00");
                    colonist["breakThresholdMajor"] = breakThresholdMajor.ToString("0.00");
                    colonist["breakThresholdMinor"] = breakThresholdMinor.ToString("0.00");
                    colonist["distanceToBreak"] = distanceToBreak.ToString("0.00");

                    // Get recommended intervention
                    string recommendedAction = GetRecommendedIntervention(pawn, riskLevel);
                    colonist["recommendedIntervention"] = recommendedAction;

                    // Get available actions based on current state
                    var availableActions = new JSONArray();
                    if (pawn.InMentalState)
                    {
                        availableActions.Add("send_to_bed");
                        availableActions.Add("cancel");
                        availableActions.Add("recreation");
                        availableActions.Add("restrain");
                        availableActions.Add("arrest");
                    }
                    else
                    {
                        availableActions.Add("recreation");
                        availableActions.Add("mood_improvement");
                    }
                    colonist["availableActions"] = availableActions;

                    // Get negative thoughts
                    var negativeThoughts = new JSONArray();
                    if (need_mood.thoughts?.memories?.Memories != null)
                    {
                        foreach (var memory in need_mood.thoughts.memories.Memories)
                        {
                            float moodEffect = memory.MoodOffset();
                            if (moodEffect < 0)
                            {
                                var thought = new JSONObject();
                                thought["label"] = memory.LabelCap.ToString();
                                thought["moodEffect"] = moodEffect.ToString("+0.#;-0.#");
                                thought["daysRemaining"] = ((memory.def.DurationTicks - memory.age) / 60000f).ToString("0.0");
                                negativeThoughts.Add(thought);
                            }
                        }
                    }
                    if (negativeThoughts.Count > 0)
                        colonist["negativeThoughts"] = negativeThoughts;

                    // Check for traits that affect mental breaks
                    var riskTraits = new JSONArray();
                    if (pawn.story?.traits != null)
                    {
                        foreach (var trait in pawn.story.traits.allTraits)
                        {
                            // Check for high-risk traits
                            if (trait.def.defName == "Neurotic" ||
                                trait.def.defName == "Volatile" ||
                                trait.def.defName == "Depressive" ||
                                trait.def.defName == "Pessimist" ||
                                trait.def.defName == "PsychicallySensitive")
                            {
                                riskTraits.Add(trait.LabelCap.ToString());
                            }
                        }
                    }
                    if (riskTraits.Count > 0)
                        colonist["riskTraits"] = riskTraits;

                    atRiskColonists.Add(colonist);
                }
            }

            result["atRiskColonists"] = atRiskColonists;
            result["totalAtRisk"] = atRiskColonists.Count;
            result["totalColonists"] = map.mapPawns.FreeColonists.Count();

            // Count by risk level
            var riskCounts = new JSONObject();
            int active = 0, critical = 0, high = 0, medium = 0;
            foreach (JSONObject colonist in atRiskColonists)
            {
                switch (colonist["riskLevel"].Value)
                {
                    case "active": active++; break;
                    case "critical": critical++; break;
                    case "high": high++; break;
                    case "medium": medium++; break;
                }
            }
            riskCounts["active"] = active;
            riskCounts["critical"] = critical;
            riskCounts["high"] = high;
            riskCounts["medium"] = medium;
            result["riskCounts"] = riskCounts;

            return result.ToString();
        }

        private static bool SendToBed(Pawn pawn, out string details)
        {
            // Find any available bed for the pawn
            var bed = RestUtility.FindBedFor(pawn, pawn, false, false);
            if (bed == null)
            {
                details = "No bed available for " + pawn.Name?.ToStringShort;
                return false;
            }

            // Use Goto to send them to the bed location - they'll rest naturally
            var job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(bed.Position));
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);

            details = pawn.Name?.ToStringShort + " is being directed to bed at (" + bed.Position.x + "," + bed.Position.z + ")";
            return true;
        }

        private static bool CancelCurrentJob(Pawn pawn, out string details)
        {
            if (pawn.jobs != null && pawn.jobs.curJob != null)
            {
                pawn.jobs.StopAll();
                details = "Cancelled current job for " + pawn.Name?.ToStringShort;
                return true;
            }

            details = "No current job to cancel for " + pawn.Name?.ToStringShort;
            return false;
        }

        private static bool AssignRecreation(Pawn pawn, out string details)
        {
            // Try to find a joy source
            var map = pawn.Map;
            if (map == null)
            {
                details = "No map found";
                return false;
            }

            // Find nearest joy building
            var joyBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.joyKind != null)
                .OrderBy(b => b.Position.DistanceTo(pawn.Position))
                .Take(5)
                .ToList();

            if (joyBuildings.Count > 0)
            {
                var nearestJoy = joyBuildings.FirstOrDefault();
                // Use Goto to send them to the joy building
                var job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(nearestJoy.Position));
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                details = "Assigned to recreation at " + nearestJoy.LabelCap;
                return true;
            }

            // No joy building found - cancel current job so they can find their own recreation
            if (pawn.jobs != null)
            {
                pawn.jobs.StopAll();
            }
            details = "No joy building found - colonist will find their own recreation";
            return true;
        }

        private static bool RestrainColonist(Pawn pawn, out string details)
        {
            // Check if colonist can be restrained
            if (!pawn.Spawned)
            {
                details = "Colonist is not spawned";
                return false;
            }

            // Try to create a restrain job
            // Note: RimWorld doesn't have a native "restrain" job for colonists
            // Instead, we'll try to arrest them which effectively removes their agency
            return ArrestColonist(pawn, out details);
        }

        private static bool ArrestColonist(Pawn pawn, out string details)
        {
            // Find a warden
            var map = pawn.Map;
            if (map == null)
            {
                details = "No map found";
                return false;
            }

            // Find any colonist who can do warden work
            var warden = map.mapPawns.FreeColonists
                .FirstOrDefault(p => p.workSettings?.GetPriority(WorkTypeDefOf.Warden) > 0);

            if (warden == null)
            {
                // Try any colonist with social skill
                warden = map.mapPawns.FreeColonists
                    .FirstOrDefault(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level >= 1);
            }

            if (warden == null)
            {
                details = "No warden available to arrest " + pawn.Name?.ToStringShort;
                return false;
            }

            // Create arrest job
            var job = JobMaker.MakeJob(JobDefOf.Arrest, pawn);
            warden.jobs.StartJob(job, JobCondition.InterruptForced);

            details = warden.Name?.ToStringShort + " is arresting " + pawn.Name?.ToStringShort;
            return true;
        }

        private static string EstimateRecoveryTime(Pawn pawn)
        {
            if (pawn.MentalStateDef == null)
                return "N/A";

            // Mental state recovery time varies by type
            string stateType = pawn.MentalStateDef.defName;

            // These are rough estimates based on RimWorld mental break mechanics
            switch (stateType)
            {
                case "Wander_Psychotic":
                    return "10-30 days (with treatment)";
                case "Binging_Drugs":
                    return "1-5 days (depends on drug)";
                case "Social_Fighting":
                    return "Immediate when separated";
                case "Scream":
                    return "1-4 hours";
                case "Wander_Sad":
                    return "2-12 hours";
                case "FireStartingSpree":
                    return "Varies - seek shelter immediately";
                case "Binger_Food":
                    return "1-3 days";
                case "MurderRage":
                    return "Immediate when stunned/restrained";
                case "Wander_Confused":
                    return "4-12 hours";
                default:
                    return "Unknown - manual intervention recommended";
            }
        }

        private static string GetRecommendedIntervention(Pawn pawn, string riskLevel)
        {
            if (pawn.InMentalState && pawn.MentalStateDef != null)
            {
                string stateType = pawn.MentalStateDef.defName;

                switch (stateType)
                {
                    case "Wander_Psychotic":
                        return "Send to bed and ensure safety. Remove weapons from area.";
                    case "Binging_Drugs":
                        return "Cancel current drug binge. Assign to safe area.";
                    case "Social_Fighting":
                        return "Cancel current job. Separate combatants.";
                    case "Scream":
                        return "Assign to recreation. Check for environmental stressors.";
                    case "Wander_Sad":
                        return "Assign to recreation. Boost mood with joy activities.";
                    case "FireStartingSpree":
                        return "ARREST IMMEDIATELY. Dangerous to colony.";
                    case "Binger_Food":
                        return "Cancel binge. Check for food quality issues.";
                    case "MurderRage":
                        return "RESTRAIN IMMEDIATELY. Dangerous to colony.";
                    case "Wander_Confused":
                        return "Send to bed. May recover on own.";
                    default:
                        return "Monitor closely. Cancel current job if dangerous.";
                }
            }

            // Not currently in mental state but at risk
            switch (riskLevel)
            {
                case "critical":
                    return "Immediate mood intervention required. Check for severe negative thoughts.";
                case "high":
                    return "Urgent mood improvement needed. Schedule recreation and address negative thoughts.";
                case "medium":
                    return "Monitor mood. Address negative thoughts and ensure recreation.";
                default:
                    return "Continue monitoring.";
            }
        }
    }
}