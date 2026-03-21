using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class MoodTools
    {
        public static string GetMoodRisks()
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
                float breakThresholdMinor = pawn.mindState.mentalBreaker.BreakThresholdMinor;
                float breakThresholdMajor = pawn.mindState.mentalBreaker.BreakThresholdMajor;
                float breakThresholdExtreme = pawn.mindState.mentalBreaker.BreakThresholdExtreme;

                // Calculate risk level based on how close to break threshold
                string riskLevel = null;
                float distanceToBreak = moodLevel - breakThresholdExtreme;

                if (moodLevel <= breakThresholdExtreme)
                    riskLevel = "critical";
                else if (moodLevel <= breakThresholdMajor)
                    riskLevel = "high";
                else if (moodLevel <= breakThresholdMinor)
                    riskLevel = "medium";
                else if (moodLevel < 0.35f) // Still worth flagging if mood is low
                    riskLevel = "low";

                // Only include pawns with some level of risk
                if (riskLevel != null)
                {
                    var colonist = new JSONObject();
                    colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    colonist["moodLevel"] = moodPercentage.ToString("P0");
                    colonist["moodValue"] = moodLevel.ToString("0.00");
                    colonist["riskLevel"] = riskLevel;
                    colonist["breakThresholdExtreme"] = breakThresholdExtreme.ToString("0.00");
                    colonist["breakThresholdMajor"] = breakThresholdMajor.ToString("0.00");
                    colonist["breakThresholdMinor"] = breakThresholdMinor.ToString("0.00");
                    colonist["distanceToBreak"] = distanceToBreak.ToString("0.00");

                    // Current mental state
                    if (pawn.MentalStateDef != null)
                        colonist["currentMentalState"] = pawn.MentalStateDef.label;

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
                                thought["age"] = memory.age.ToString();
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
                            // Traits that make mental breaks more likely or more severe
                            if (trait.def.defName == "Neurotic" || 
                                trait.def.defName == "Volatile" ||
                                trait.def.defName == "Depressive" ||
                                trait.def.defName == "PsychicallySensitive" ||
                                trait.def.defName == "Pessimist")
                            {
                                riskTraits.Add(trait.LabelCap.ToString());
                            }
                        }
                    }
                    if (riskTraits.Count > 0)
                        colonist["riskTraits"] = riskTraits;

                    // Estimate time to break (very rough)
                    // Calculate recent mood trend if possible
                    float estimatedDaysToBreak = -1;
                    if (moodLevel > breakThresholdExtreme && distanceToBreak > 0)
                    {
                        // Average mood loss from current thoughts
                        float totalNegativeMood = 0;
                        int negativeCount = 0;
                        if (need_mood.thoughts?.memories?.Memories != null)
                        {
                            foreach (var memory in need_mood.thoughts.memories.Memories)
                            {
                                float effect = memory.MoodOffset();
                                if (effect < 0)
                                {
                                    totalNegativeMood += effect;
                                    negativeCount++;
                                }
                            }
                        }

                        if (negativeCount > 0)
                        {
                            float avgDailyMoodLoss = Math.Abs(totalNegativeMood) / 10f; // Rough estimate
                            if (avgDailyMoodLoss > 0.01f)
                            {
                                estimatedDaysToBreak = distanceToBreak / avgDailyMoodLoss;
                            }
                        }
                    }
                    
                    if (estimatedDaysToBreak > 0)
                        colonist["estimatedDaysToBreak"] = estimatedDaysToBreak.ToString("0.0");
                    else if (riskLevel == "critical")
                        colonist["estimatedDaysToBreak"] = "imminent";

                    atRiskColonists.Add(colonist);
                }
            }

            result["atRiskColonists"] = atRiskColonists;
            result["totalAtRisk"] = atRiskColonists.Count;
            result["totalColonists"] = map.mapPawns.FreeColonists.Count();

            return result.ToString();
        }

        public static string SuggestMoodInterventions(string name)
        {
            if (string.IsNullOrEmpty(name)) 
                return ToolExecutor.JsonError("Name parameter required.");

            var pawn = ColonistTools.FindPawnByName(name);
            if (pawn == null) 
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.needs?.mood == null)
                return ToolExecutor.JsonError("Colonist has no mood need.");

            var result = new JSONObject();
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["currentMood"] = pawn.needs.mood.CurLevelPercentage.ToString("P0");

            var interventions = new JSONArray();

            // Analyze specific mood issues and suggest concrete actions
            var need_mood = pawn.needs.mood;
            
            // 1. Check recreation/joy
            if (pawn.needs.joy != null && pawn.needs.joy.CurLevel < 0.3f)
            {
                var rec = new JSONObject();
                rec["issue"] = "Recreation deprivation";
                rec["severity"] = pawn.needs.joy.CurLevel < 0.1f ? "critical" : "high";
                rec["joyLevel"] = pawn.needs.joy.CurLevelPercentage.ToString("P0");
                
                var suggestions = new JSONArray();
                suggestions.Add("Schedule more recreation time in daily schedule");
                suggestions.Add("Build more joy sources (chess tables, horseshoes pins, poker tables)");
                suggestions.Add("Ensure colonist isn't work-restricted from joy activities");
                rec["suggestions"] = suggestions;
                interventions.Add(rec);
            }

            // 2. Check bedroom quality
            var bedroom = pawn.ownership?.OwnedRoom;
            if (bedroom != null)
            {
                var impressiveness = bedroom.GetStat(RoomStatDefOf.Impressiveness);
                if (impressiveness < 30)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Poor bedroom quality";
                    rec["severity"] = impressiveness < 15 ? "high" : "medium";
                    rec["currentImpressiveness"] = impressiveness.ToString("0.0");
                    
                    var suggestions = new JSONArray();
                    suggestions.Add("Replace floors with wood or stone tiles");
                    suggestions.Add("Add better furniture (dresser, end tables, sculptures)");
                    suggestions.Add("Increase room size");
                    suggestions.Add("Add artwork or decorations");
                    suggestions.Add("Use better quality materials for furniture");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }
            else
            {
                var rec = new JSONObject();
                rec["issue"] = "No private bedroom";
                rec["severity"] = "high";
                var suggestions = new JSONArray();
                suggestions.Add("Build a private bedroom for this colonist");
                suggestions.Add("Assign an existing bedroom to this colonist");
                rec["suggestions"] = suggestions;
                interventions.Add(rec);
            }

            // 3. Check for ate awful meal / ate raw food thoughts
            if (need_mood.thoughts?.memories?.Memories != null)
            {
                bool hasAwfulMeal = need_mood.thoughts.memories.Memories.Any(m => 
                    m.def.defName.Contains("Awful") || m.def.defName.Contains("RawFood"));
                
                if (hasAwfulMeal)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Poor food quality";
                    rec["severity"] = "medium";
                    var suggestions = new JSONArray();
                    suggestions.Add("Ensure cooks are making meals at cooking stations");
                    suggestions.Add("Build an electric/fueled stove for better meals");
                    suggestions.Add("Assign a skilled cook (Cooking skill 6+)");
                    suggestions.Add("Grow/hunt for meal ingredients");
                    suggestions.Add("Consider fine or lavish meal bills for mood boost");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }

            // 4. Check pain and health
            if (pawn.health != null)
            {
                float painTotal = pawn.health.hediffSet.PainTotal;
                if (painTotal > 0.2f)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Pain from injuries or conditions";
                    rec["severity"] = painTotal > 0.5f ? "high" : "medium";
                    rec["painLevel"] = painTotal.ToString("P0");
                    
                    var suggestions = new JSONArray();
                    suggestions.Add("Treat injuries and diseases immediately");
                    suggestions.Add("Use medicine (herbal or better) for treatment");
                    suggestions.Add("Consider penoxycyline for disease prevention");
                    suggestions.Add("Use painkillers (penoxycyline, luciferium, or go-juice) if available");
                    suggestions.Add("Ensure medical beds are available");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }

                // Check for infections or diseases
                bool hasDisease = pawn.health.hediffSet.hediffs.Any(h => 
                    h.def.makesSickThought || h.def.lethalSeverity > 0);
                
                if (hasDisease)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Disease or infection";
                    rec["severity"] = "high";
                    var suggestions = new JSONArray();
                    suggestions.Add("Prioritize medical treatment immediately");
                    suggestions.Add("Use best available medicine");
                    suggestions.Add("Keep colonist in medical bed for rest");
                    suggestions.Add("Assign best doctor to treat");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }

            // 5. Check for tattered apparel
            bool hasTatteredApparel = need_mood.thoughts?.memories?.Memories != null &&
                need_mood.thoughts.memories.Memories.Any(m => m.def.defName.Contains("Tattered"));
            
            if (hasTatteredApparel)
            {
                var rec = new JSONObject();
                rec["issue"] = "Wearing tattered clothing";
                rec["severity"] = "medium";
                var suggestions = new JSONArray();
                suggestions.Add("Craft or buy new apparel");
                suggestions.Add("Strip tattered clothing and replace");
                suggestions.Add("Set up tailoring bench for clothing production");
                suggestions.Add("Hunt animals for leather to make clothes");
                rec["suggestions"] = suggestions;
                interventions.Add(rec);
            }

            // 6. Check social isolation
            if (pawn.needs.joy != null)
            {
                bool hasSocialNeeds = need_mood.thoughts?.memories?.Memories != null &&
                    need_mood.thoughts.memories.Memories.Any(m => m.def.defName.Contains("LowExpectations") == false && m.def.defName.Contains("Alone"));
                
                if (hasSocialNeeds)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Social isolation";
                    rec["severity"] = "medium";
                    var suggestions = new JSONArray();
                    suggestions.Add("Ensure colonist has recreation time with others");
                    suggestions.Add("Place them in dining room with others during meals");
                    suggestions.Add("Check they aren't restricted to isolated areas");
                    suggestions.Add("Build social joy sources (poker table, chess)");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }

            // 7. Check environment (temperature, darkness)
            var room = pawn.GetRoom();
            if (room != null && !room.PsychologicallyOutdoors)
            {
                float temp = room.Temperature;
                if (temp < -10 || temp > 40)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Extreme temperature in room";
                    rec["severity"] = "medium";
                    rec["temperature"] = temp.ToString("0.0") + "°C";
                    var suggestions = new JSONArray();
                    if (temp < -10)
                    {
                        suggestions.Add("Install heaters in the room");
                        suggestions.Add("Seal the room properly (no open doors/vents)");
                        suggestions.Add("Use campfires as temporary heating");
                    }
                    else
                    {
                        suggestions.Add("Install coolers in the room");
                        suggestions.Add("Ensure coolers have power");
                        suggestions.Add("Add passive cooling (vents to cooler areas)");
                    }
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }

            // 8. Check for substance withdrawal
            if (pawn.health?.hediffSet?.hediffs != null)
            {
                var withdrawal = pawn.health.hediffSet.hediffs.FirstOrDefault(h => 
                    h.def.defName.Contains("Withdrawal"));
                
                if (withdrawal != null)
                {
                    var rec = new JSONObject();
                    rec["issue"] = "Drug withdrawal - " + withdrawal.LabelCap;
                    rec["severity"] = "high";
                    var suggestions = new JSONArray();
                    suggestions.Add("Provide the drug to ease withdrawal symptoms");
                    suggestions.Add("Adjust drug policy to prevent addiction in future");
                    suggestions.Add("Keep colonist safe during withdrawal period");
                    suggestions.Add("Consider arrest and release to speed through mental break");
                    rec["suggestions"] = suggestions;
                    interventions.Add(rec);
                }
            }

            // 9. Last resort - arrest and release
            if (pawn.needs.mood.CurLevel < pawn.mindState.mentalBreaker.BreakThresholdExtreme)
            {
                var rec = new JSONObject();
                rec["issue"] = "Imminent mental break";
                rec["severity"] = "critical";
                var suggestions = new JSONArray();
                suggestions.Add("Consider arresting colonist, then releasing immediately");
                suggestions.Add("This resets mental break countdown but gives 'was imprisoned' debuff");
                suggestions.Add("Only use as last resort when break is imminent");
                suggestions.Add("Ensure prison is comfortable to minimize imprisonment mood penalty");
                rec["suggestions"] = suggestions;
                interventions.Add(rec);
            }

            result["interventions"] = interventions;
            result["totalInterventions"] = interventions.Count;

            // Summary priority
            if (interventions.Count == 0)
            {
                result["summary"] = "No urgent interventions needed. Colonist mood is stable.";
            }
            else
            {
                var priorities = new JSONArray();
                foreach (JSONObject intervention in interventions)
                {
                    if (intervention["severity"].Value == "critical")
                        priorities.Add(intervention["issue"].Value + " (CRITICAL)");
                    else if (intervention["severity"].Value == "high")
                        priorities.Add(intervention["issue"].Value + " (high priority)");
                }
                if (priorities.Count > 0)
                    result["urgentActions"] = priorities;
            }

            return result.ToString();
        }

        public static string GetMoodTrends()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var tracker = MoodHistoryTracker.Instance;
            if (tracker == null) return ToolExecutor.JsonError("Mood history tracker not initialized.");

            var result = new JSONObject();
            var colonistTrends = new JSONArray();

            int currentTick = Find.TickManager.TicksGame;

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.needs?.mood == null) continue;

                var history = tracker.GetHistory(pawn.ThingID, 3);

                var colonist = new JSONObject();
                colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                float currentMood = pawn.needs.mood.CurLevel;
                float breakThreshold = pawn.mindState.mentalBreaker.BreakThresholdExtreme;

                colonist["currentMood"] = pawn.needs.mood.CurLevelPercentage.ToString("P0");
                colonist["currentMoodValue"] = currentMood.ToString("0.00");
                colonist["breakThreshold"] = breakThreshold.ToString("0.00");
                colonist["distanceToBreak"] = (currentMood - breakThreshold).ToString("0.00");

                // Calculate trend if we have enough history
                if (history.Count >= 2)
                {
                    var oldestSnapshot = history[0];
                    var newestSnapshot = history[history.Count - 1];

                    float moodChange = newestSnapshot.moodLevel - oldestSnapshot.moodLevel;
                    int ticksElapsed = newestSnapshot.tick - oldestSnapshot.tick;
                    float daysElapsed = ticksElapsed / 60000f;

                    colonist["dataPoints"] = history.Count;
                    colonist["trackingDays"] = daysElapsed.ToString("0.1");
                    colonist["moodChange"] = moodChange.ToString("+0.00;-0.00");

                    // Calculate velocity (mood change per day)
                    float velocity = daysElapsed > 0 ? moodChange / daysElapsed : 0;
                    colonist["moodVelocity"] = velocity.ToString("+0.00;-0.00") + " per day";

                    // Determine trend
                    string trend = "stable";
                    if (Math.Abs(velocity) < 0.02f)
                        trend = "stable";
                    else if (velocity > 0)
                        trend = "rising";
                    else
                        trend = "falling";

                    colonist["trend"] = trend;

                    // Predict time to break if mood is falling
                    if (velocity < -0.01f && currentMood > breakThreshold)
                    {
                        float distanceToBreak = currentMood - breakThreshold;
                        float daysToBreak = distanceToBreak / Math.Abs(velocity);
                        float hoursToBreak = daysToBreak * 24f;

                        if (hoursToBreak < 24f)
                        {
                            colonist["timeToBreak"] = hoursToBreak.ToString("0.0") + " hours";
                            colonist["breakRisk"] = hoursToBreak < 4f ? "imminent" : "high";
                        }
                        else
                        {
                            colonist["timeToBreak"] = daysToBreak.ToString("0.1") + " days";
                            colonist["breakRisk"] = daysToBreak < 2f ? "high" : "moderate";
                        }
                    }
                    else if (currentMood <= breakThreshold)
                    {
                        colonist["timeToBreak"] = "imminent";
                        colonist["breakRisk"] = "critical";
                    }
                    else if (velocity >= 0)
                    {
                        colonist["breakRisk"] = "low";
                    }

                    // Get recent mood history snapshots (last 10)
                    var recentHistory = new JSONArray();
                    int startIdx = Math.Max(0, history.Count - 10);
                    for (int i = startIdx; i < history.Count; i++)
                    {
                        var snap = history[i];
                        var entry = new JSONObject();
                        float hoursAgo = (currentTick - snap.tick) / 2500f;
                        entry["hoursAgo"] = hoursAgo.ToString("0.0");
                        entry["mood"] = snap.moodLevel.ToString("0.00");
                        recentHistory.Add(entry);
                    }
                    colonist["recentHistory"] = recentHistory;
                }
                else
                {
                    colonist["dataPoints"] = history.Count;
                    colonist["trend"] = "insufficient_data";
                    colonist["note"] = "Collecting data... Need ~2-3 hours of gameplay for trend analysis";
                }

                // Get top negative thoughts
                var negativeThoughts = new JSONArray();
                if (pawn.needs.mood.thoughts?.memories?.Memories != null)
                {
                    var topNegative = pawn.needs.mood.thoughts.memories.Memories
                        .Where(m => m.MoodOffset() < 0)
                        .OrderBy(m => m.MoodOffset())
                        .Take(5);

                    foreach (var memory in topNegative)
                    {
                        var thought = new JSONObject();
                        thought["label"] = memory.LabelCap.ToString();
                        thought["moodEffect"] = memory.MoodOffset().ToString("+0.#;-0.#");
                        negativeThoughts.Add(thought);
                    }
                }
                if (negativeThoughts.Count > 0)
                    colonist["topNegativeThoughts"] = negativeThoughts;

                colonistTrends.Add(colonist);
            }

            result["colonistTrends"] = colonistTrends;
            result["totalColonists"] = colonistTrends.Count;

            // Summary of high-risk colonists
            var highRisk = new JSONArray();
            foreach (JSONObject trend in colonistTrends)
            {
                var risk = trend["breakRisk"]?.Value;
                if (risk == "critical" || risk == "imminent" || risk == "high")
                {
                    var summary = new JSONObject();
                    summary["name"] = trend["name"].Value;
                    summary["risk"] = risk;
                    if (trend["timeToBreak"] != null)
                        summary["timeToBreak"] = trend["timeToBreak"].Value;
                    highRisk.Add(summary);
                }
            }

            if (highRisk.Count > 0)
                result["highRiskColonists"] = highRisk;

            return result.ToString();
        }
    }
}
