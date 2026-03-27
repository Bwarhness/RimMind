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
        public string topHeadline;
        public string leadParagraph;
        public List<ChronicleSection> sections = new List<ChronicleSection>();
        public List<ColonyEvent> events = new List<ColonyEvent>();
        public List<ColonistQuote> quotes = new List<ColonistQuote>();
        public List<string> milestones = new List<string>();
        public string weatherPoem;
        public string prediction;
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
            this.isGenerated = false;
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

        // Weather tracking
        public List<string> weatherEvents = new List<string>();

        // Extremes
        public float highestWealth;
        public float lowestWealth;
        public int highestColonistCount;
        public int lowestColonistCount;
        public string mostExtremeMoodColonist;
        public float mostExtremeMoodValue;

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

            weatherEvents.Clear();

            highestWealth = 0f;
            lowestWealth = float.MaxValue;
            highestColonistCount = 0;
            lowestColonistCount = int.MaxValue;
            mostExtremeMoodColonist = null;
            mostExtremeMoodValue = 0.5f;
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
