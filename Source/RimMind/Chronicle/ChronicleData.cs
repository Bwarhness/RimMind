using System;
using System.Collections.Generic;
using Verse;

namespace RimMind.Chronicle
{
    /// <summary>
    /// Represents a single issue of the Colony Chronicle - a newspaper-style weekly report.
    /// </summary>
    public class WeeklyChronicle
    {
        public int weekNumber;
        public int gameDay;
        public string season;
        public int year;
        public int realYear;        // Real-world year when this was generated
        public int realWeekNumber;  // Real-world week number
        public string topHeadline;
        public string leadParagraph;
        public List<ChronicleSection> sections = new List<ChronicleSection>();
        public List<ColonyEvent> events = new List<ColonyEvent>();
        public List<ColonistQuote> quotes = new List<ColonistQuote>();
        public List<ColonistInterview> interviews = new List<ColonistInterview>();
        public List<string> milestones = new List<string>();
        public List<string> runningJokes = new List<string>(); // Last 4 weeks of running jokes
        public string runningJokeCurrent; // The active running joke for this week
        public List<Prediction> predictions = new List<Prediction>();
        public string editorial; // "FROM THE EDITOR'S DESK" content
        public string weatherPoem;
        public string oneYearAgoSummary; // "ON THIS DAY, ONE YEAR AGO" content
        public bool isGenerated;

        public WeeklyChronicle()
        {
        }

        public WeeklyChronicle(int weekNumber, int gameDay, string season, int year)
        {
            this.weekNumber = weekNumber;
            this.gameDay = gameDay;
            this.season = season;
            this.year = year;
            this.realYear = DateTime.Now.Year;
            this.realWeekNumber = GetRealWeekNumber();
            this.isGenerated = false;
        }

        private static int GetRealWeekNumber()
        {
            var now = DateTime.Now;
            var jan1 = new DateTime(now.Year, 1, 1);
            var daysOffset = (int)jan1.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysOffset < 0) daysOffset += 7;
            var weekStart = jan1.AddDays(-daysOffset);
            var weekDiff = now.Subtract(weekStart).Days / 7;
            return weekDiff + 1;
        }
    }

    /// <summary>
    /// A newspaper-style section within the Chronicle.
    /// </summary>
    public class ChronicleSection
    {
        public string title;
        public string emoji;
        public string content;

        public ChronicleSection()
        {
        }

        public ChronicleSection(string title, string emoji, string content)
        {
            this.title = title;
            this.emoji = emoji;
            this.content = content;
        }
    }

    /// <summary>
    /// Represents a significant event that occurred during the week.
    /// </summary>
    public class ColonyEvent
    {
        public string type;       // "death", "raid", "milestone", "trade", "birth", "mental_break"
        public string headline;
        public string colonist;
        public int day;
        public string details;

        public ColonyEvent()
        {
        }

        public ColonyEvent(string type, string headline, string colonist = null, int day = 0, string details = null)
        {
            this.type = type;
            this.headline = headline;
            this.colonist = colonist;
            this.day = day;
            this.details = details;
        }
    }

    /// <summary>
    /// A colonist's quote or saying from the week.
    /// </summary>
    public class ColonistQuote
    {
        public string colonistName;
        public string quote;
        public string mood; // "happy", "sad", "angry", "neutral"

        public ColonistQuote()
        {
        }

        public ColonistQuote(string colonistName, string quote, string mood = "neutral")
        {
            this.colonistName = colonistName;
            this.quote = quote;
            this.mood = mood;
        }
    }

    /// <summary>
    /// A procedural colonist "interview" generated from their traits and current state.
    /// </summary>
    public class ColonistInterview
    {
        public string colonistName;
        public string age;
        public string currentJob;
        public string daysOnColony;
        public string question;
        public string answer;

        public ColonistInterview()
        {
        }

        public ColonistInterview(string colonistName, string age, string currentJob, string daysOnColony, string question, string answer)
        {
            this.colonistName = colonistName;
            this.age = age;
            this.currentJob = currentJob;
            this.daysOnColony = daysOnColony;
            this.question = question;
            this.answer = answer;
        }
    }

    /// <summary>
    /// A prediction for the upcoming week with confidence scoring.
    /// </summary>
    public class Prediction
    {
        public string eventDescription;
        public int confidencePct;  // 10-95
        public string basis;      // "historical data", "current mood", "raid frequency", "weather patterns"
        public string reason;

        public Prediction()
        {
        }

        public Prediction(string eventDescription, int confidencePct, string basis, string reason)
        {
            this.eventDescription = eventDescription;
            this.confidencePct = Math.Clamp(confidencePct, 10, 95);
            this.basis = basis;
            this.reason = reason;
        }

        /// <summary>
        /// Returns an ASCII progress bar for the confidence level.
        /// </summary>
        public string GetConfidenceBar()
        {
            int barWidth = 10;
            int filledWidth = (confidencePct * barWidth) / 100;
            string bar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);
            return $"[{bar}] {confidencePct}%";
        }
    }

    /// <summary>
    /// Archived chronicle data for "Last Year on This Day" feature.
    /// Stored to disk with year stamp for cross-year comparisons.
    /// </summary>
    public class ChronicleArchive
    {
        public int realYear;
        public int realWeekNumber;
        public int weekNumber;
        public string colonyName;
        public string headline;
        public string summary; // Brief recap for "one year ago" display
        public int deaths;
        public int colonists;
        public float wealth;

        public ChronicleArchive()
        {
        }

        public ChronicleArchive(WeeklyChronicle chronicle, string colonyName)
        {
            this.realYear = chronicle.realYear;
            this.realWeekNumber = chronicle.realWeekNumber;
            this.weekNumber = chronicle.weekNumber;
            this.colonyName = colonyName;
            this.headline = chronicle.topHeadline;
            this.summary = BuildSummary(chronicle);
            this.deaths = chronicle.events?.FindAll(e => e.type == "death").Count ?? 0;
            this.colonists = chronicle.events?.Count ?? 0;
            this.wealth = 0f; // Will be filled if available
        }

        private string BuildSummary(WeeklyChronicle chronicle)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(chronicle.leadParagraph))
            {
                // Truncate to ~100 chars for archive
                var lead = chronicle.leadParagraph;
                if (lead.Length > 100)
                    lead = lead.Substring(0, 97) + "...";
                parts.Add(lead);
            }

            if (chronicle.events != null && chronicle.events.Count > 0)
            {
                var eventSummary = $"{chronicle.events.Count} notable event(s)";
                if (chronicle.sections != null)
                {
                    var battleSection = chronicle.sections.Find(s => s.title.Contains("BATTLE"));
                    if (battleSection != null && !string.IsNullOrEmpty(battleSection.content))
                        eventSummary += $". {battleSection.content}";
                }
                parts.Add(eventSummary);
            }

            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// Tracks event data accumulated throughout the current week.
    /// Used internally by ChronicleTracker to build the weekly report.
    /// </summary>
    public class WeeklyEventLog
    {
        public int weekNumber;
        public int startDay;
        public int endDay;
        public string season;
        public int year;

        public List<ColonyEvent> events = new List<ColonyEvent>();
        public List<ColonistQuote> quotes = new List<ColonistQuote>();
        public List<string> milestones = new List<string>();

        // Stats for the week
        public int deaths;
        public int raids;
        public int trades;
        public int births;
        public int mentalBreaks;
        public int surgeries;
        public int researchCompleted;
        public int animalsTamed;
        public int itemsCrafted;
        public int fires;
        public int mentalBreakCount;
        public int failedRecipes;

        // Weather tracking
        public List<string> weatherEvents = new List<string>();

        // Extremes
        public float highestWealth;
        public float lowestWealth;
        public int highestColonistCount;
        public int lowestColonistCount;
        public string mostExtremeMoodColonist;
        public float mostExtremeMoodValue;

        // Running joke tracking
        public Dictionary<string, int> fireCausesByColonist = new Dictionary<string, int>();
        public Dictionary<string, int> mentalBreakCountByColonist = new Dictionary<string, int>();

        public WeeklyEventLog()
        {
        }

        public WeeklyEventLog(int weekNumber, int startDay, int endDay, string season, int year)
        {
            this.weekNumber = weekNumber;
            this.startDay = startDay;
            this.endDay = endDay;
            this.season = season;
            this.year = year;
        }

        public void Reset(int weekNumber, int startDay, int endDay, string season, int year)
        {
            this.weekNumber = weekNumber;
            this.startDay = startDay;
            this.endDay = endDay;
            this.season = season;
            this.year = year;

            events.Clear();
            quotes.Clear();
            milestones.Clear();

            deaths = 0;
            raids = 0;
            trades = 0;
            births = 0;
            mentalBreaks = 0;
            surgeries = 0;
            researchCompleted = 0;
            animalsTamed = 0;
            itemsCrafted = 0;
            fires = 0;
            mentalBreakCount = 0;
            failedRecipes = 0;

            weatherEvents.Clear();

            highestWealth = 0f;
            lowestWealth = float.MaxValue;
            highestColonistCount = 0;
            lowestColonistCount = int.MaxValue;
            mostExtremeMoodColonist = null;
            mostExtremeMoodValue = 0.5f;

            fireCausesByColonist.Clear();
            mentalBreakCountByColonist.Clear();
        }

        public string BuildContextSummary()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"=== WEEK {weekNumber} CHRONICLE DATA ===");
            sb.AppendLine($"Period: Day {startDay} to Day {endDay}, {season}, Year {year}");
            sb.AppendLine();

            sb.AppendLine("-- EVENTS --");
            if (events.Count == 0)
            {
                sb.AppendLine("A quiet week with no major events.");
            }
            else
            {
                foreach (var evt in events)
                {
                    sb.AppendLine($"[{evt.type.ToUpper()}] Day {evt.day}: {evt.headline}");
                    if (!string.IsNullOrEmpty(evt.colonist))
                        sb.AppendLine($"  Colonist: {evt.colonist}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("-- COLONIST QUOTES --");
            if (quotes.Count == 0)
            {
                sb.AppendLine("No memorable quotes recorded this week.");
            }
            else
            {
                foreach (var q in quotes)
                {
                    sb.AppendLine($"\"{q.quote}\" — {q.colonistName}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("-- MILESTONES --");
            if (milestones.Count == 0)
            {
                sb.AppendLine("No major milestones reached.");
            }
            else
            {
                foreach (var m in milestones)
                {
                    sb.AppendLine($"* {m}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("-- WEEKLY STATISTICS --");
            sb.AppendLine($"Deaths: {deaths}");
            sb.AppendLine($"Raids/Attacks: {raids}");
            sb.AppendLine($"Trades: {trades}");
            sb.AppendLine($"Births: {births}");
            sb.AppendLine($"Mental Breaks: {mentalBreaks}");
            sb.AppendLine($"Surgeries: {surgeries}");
            sb.AppendLine($"Research Completed: {researchCompleted}");
            sb.AppendLine($"Animals Tamed: {animalsTamed}");
            sb.AppendLine($"Items Crafted: {itemsCrafted}");
            sb.AppendLine($"Fires: {fires}");

            sb.AppendLine();
            sb.AppendLine("-- FIRE CAUSE TRACKING (for running jokes) --");
            if (fireCausesByColonist.Count > 0)
            {
                foreach (var kvp in fireCausesByColonist)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value} fire(s) caused");
                }
            }
            else
            {
                sb.AppendLine("  No fire incidents tracked.");
            }

            sb.AppendLine();
            sb.AppendLine("-- WEATHER --");
            if (weatherEvents.Count == 0)
            {
                sb.AppendLine("No notable weather events.");
            }
            else
            {
                foreach (var w in weatherEvents)
                {
                    sb.AppendLine($"* {w}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("-- COLONY STANDINGS --");
            sb.AppendLine($"Wealth: {highestWealth:N0} (peak), {lowestWealth:N0} (low)");
            sb.AppendLine($"Colonists: {highestColonistCount} (peak), {lowestColonistCount} (low)");
            if (!string.IsNullOrEmpty(mostExtremeMoodColonist))
            {
                sb.AppendLine($"Most Emotional: {mostExtremeMoodColonist} at {mostExtremeMoodValue:P0} mood");
            }

            return sb.ToString();
        }
    }
}
