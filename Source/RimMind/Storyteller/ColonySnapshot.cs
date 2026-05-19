using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// A snapshot of the colony's current state, sent to the AI planner for context.
    /// </summary>
    public class ColonySnapshot
    {
        public int Day;
        public int Year;
        public string Season;
        public int ColonistCount;
        public int PrisonerCount;
        public int AnimalCount;
        public float TotalWealth;
        public float AverageMood;
        public float AverageHealth;
        public int RecentDeaths;
        public int RecentRaids;
        public int RecentTrades;
        public string CurrentWeather;
        public List<string> ActiveQuests = new List<string>();
        public List<string> RecentResearch = new List<string>();
        public List<string> TopNeeds = new List<string>();
        public int MapSize;
        public TechLevel TechLevel;
        public int DaysSinceLastThreat;
        public int DaysSinceLastTrade;
        public string FactionName;

        public static ColonySnapshot Capture(Map map)
        {
            if (map == null) return null;

            var snapshot = new ColonySnapshot
            {
                Day = GenLocalDate.DayOfYear(map),
                Year = GenLocalDate.Year(map),
                Season = GenLocalDate.Season(map).LabelCap.ToString(),
                ColonistCount = map.mapPawns.FreeColonists.Count,
                PrisonerCount = map.mapPawns.PrisonersOfColony.Count,
                AnimalCount = map.mapPawns.ColonyAnimals.Count,
                TotalWealth = map.wealthWatcher.WealthTotal,
                CurrentWeather = map.weatherManager.curWeather?.LabelCap?.ToString() ?? "Unknown",
                MapSize = map.Size.x,
                TechLevel = Faction.OfPlayer.def.techLevel,
                FactionName = Faction.OfPlayer.Name
            };

            var colonists = map.mapPawns.FreeColonists.ToList();
            if (colonists.Count > 0)
            {
                snapshot.AverageMood = colonists.Average(c => c.needs?.mood?.CurLevel ?? 0.5f);
                snapshot.AverageHealth = colonists.Average(c => c.health.summaryHealth.SummaryHealthPercent);

                // Top needs (most common need deficits)
                var needCounts = new Dictionary<string, int>();
                foreach (var c in colonists)
                {
                    if (c.needs == null) continue;
                    foreach (var need in c.needs.AllNeeds)
                    {
                        if (need.CurLevel < 0.3f)
                        {
                            string key = need.def?.label ?? "unknown need";
                            if (needCounts.ContainsKey(key))
                                needCounts[key]++;
                            else
                                needCounts[key] = 1;
                        }
                    }
                }
                snapshot.TopNeeds = needCounts.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key).ToList();
            }

            // Pull recent events from Chronicle if available
            var chronicle = Chronicle.ChronicleTracker.Instance;
            if (chronicle != null)
            {
                var log = chronicle.GetCurrentWeekLog();
                if (log != null)
                {
                    snapshot.RecentDeaths = log.deaths;
                    snapshot.RecentRaids = log.raids;
                    snapshot.RecentTrades = log.trades;
                }
            }

            // Active quests
            try
            {
                var questManager = Find.QuestManager;
                if (questManager != null)
                {
                    var questsField = questManager.GetType().GetField("quests", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (questsField != null)
                    {
                        var quests = questsField.GetValue(questManager) as System.Collections.IEnumerable;
                        if (quests != null)
                        {
                            foreach (var quest in quests)
                            {
                                if (quest == null) continue;
                                var stateProp = quest.GetType().GetProperty("State");
                                var nameProp = quest.GetType().GetProperty("name");
                                if (stateProp != null && nameProp != null)
                                {
                                    var state = stateProp.GetValue(quest);
                                    if (state != null && state.ToString() == "Ongoing")
                                    {
                                        var name = nameProp.GetValue(quest)?.ToString();
                                        if (!string.IsNullOrEmpty(name))
                                            snapshot.ActiveQuests.Add(name);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Recent research
            try
            {
                if (Find.ResearchManager != null)
                {
                    var currentProj = Find.ResearchManager.currentProj;
                    if (currentProj != null)
                        snapshot.RecentResearch.Add($"In progress: {currentProj.label}");
                }
            }
            catch { }

            return snapshot;
        }

        public string BuildPromptContext()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== COLONY SNAPSHOT ===");
            sb.AppendLine($"Day {Day}, {Season}, Year {Year}");
            sb.AppendLine($"Faction: {FactionName}");
            sb.AppendLine($"Colonists: {ColonistCount} | Prisoners: {PrisonerCount} | Animals: {AnimalCount}");
            sb.AppendLine($"Wealth: {TotalWealth:N0} silver");
            sb.AppendLine($"Average Mood: {AverageMood:P0} | Average Health: {AverageHealth:P0}");
            sb.AppendLine($"Weather: {CurrentWeather}");
            sb.AppendLine($"Tech Level: {TechLevel}");

            if (RecentDeaths > 0)
                sb.AppendLine($"Recent deaths: {RecentDeaths}");
            if (RecentRaids > 0)
                sb.AppendLine($"Recent raids: {RecentRaids}");
            if (RecentTrades > 0)
                sb.AppendLine($"Recent trades: {RecentTrades}");

            if (TopNeeds.Count > 0)
                sb.AppendLine($"Pressing needs: {string.Join(", ", TopNeeds)}");

            if (ActiveQuests.Count > 0)
                sb.AppendLine($"Active quests: {string.Join(", ", ActiveQuests)}");

            if (RecentResearch.Count > 0)
                sb.AppendLine($"Research: {string.Join(", ", RecentResearch)}");

            return sb.ToString();
        }
    }
}
