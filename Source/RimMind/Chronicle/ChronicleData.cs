using System;
using System.Collections.Generic;
using Verse;

namespace RimMind.Chronicle
{
    /// <summary>
    /// Represents a colonist death event for obituaries.
    /// </summary>
    public class ColonistDeath
    {
        public string name;
        public string cause;      // "raider", "wild animal", "infection", "blight", "unknown"
        public string killer;      // Name of killer or killer type
        public int day;
        public string lastWords;

        public ColonistDeath()
        {
        }

        public ColonistDeath(string name, string cause, string killer, int day, string lastWords = null)
        {
            this.name = name;
            this.cause = cause;
            this.killer = killer;
            this.day = day;
            this.lastWords = lastWords;
        }
    }

    /// <summary>
    /// Represents a raid/battle event for battle reports.
    /// </summary>
    public class RaidEvent
    {
        public int day;
        public string enemyFaction;
        public int enemyCount;
        public bool survived;
        public int colonistsInvolved;
        public int damageDealt;
        public int colonistsKilled;
        public int enemiesKilled;
        public string letterLabel;

        public RaidEvent()
        {
        }
    }

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

        // Phase 2: Extended tracking
        public int colonistCountStart;
        public int colonistCountEnd;
        public int raidsThisWeek;
        public bool raidMarathon;      // 3+ raids in one week
        public bool deathlessWeek;
        public bool firstDeath;        // First death ever in colony
        public bool survivedFirstRaid;
        public int colonistDeaths;
        public List<ColonistDeath> deaths = new List<ColonistDeath>();
        public List<string> milestoneFlags = new List<string>();
        public List<RaidEvent> raids = new List<RaidEvent>();

        // Mood extremes for the week
        public string bestMoodColonist;
        public float bestMoodValue;
        public string worstMoodColonist;
        public float worstMoodValue;

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
    /// Represents a colonist joining the colony.
    /// </summary>
    public class ColonistJoin
    {
        public string name;
        public string reason;      // "recruit", "guest", "rescue", "migration"
        public int day;

        public ColonistJoin()
        {
        }

        public ColonistJoin(string name, string reason, int day)
        {
            this.name = name;
            this.reason = reason;
            this.day = day;
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

        // Phase 2: Extended tracking
        public int colonistCountStart;
        public int colonistCountEnd;
        public int raidsThisWeek;
        public int colonistDeaths;
        public List<ColonistDeath> deathsList = new List<ColonistDeath>();
        public List<RaidEvent> raidsList = new List<RaidEvent>();
        public List<ColonistJoin> joins = new List<ColonistJoin>();
        public List<string> milestoneFlags = new List<string>();

        // Mood extremes
        public string bestMoodColonist;
        public float bestMoodValue = 1.0f;  // Start at max
        public string worstMoodColonist;
        public float worstMoodValue = 0.0f; // Start at min

        // Achievement flags
        public bool raidMarathon;        // 3+ raids in one week
        public bool deathlessWeek;
        public bool firstDeath;         // First death ever in colony
        public bool survivedFirstRaid;
        public bool firstMechanoidKill;
        public bool firstBanishment;

        // Cumulative stats for milestone detection
        public int totalColonistDeaths;
        public int totalRaidsSurvived;
        public bool hasReached5Colonists;
        public bool hasReached10Colonists;
        public bool hasReached20Colonists;

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

            // Phase 2 reset
            deathsList.Clear();
            raidsList.Clear();
            joins.Clear();
            milestoneFlags.Clear();
            bestMoodColonist = null;
            bestMoodValue = 1.0f;
            worstMoodColonist = null;
            worstMoodValue = 0.0f;
            raidMarathon = false;
            deathlessWeek = true;  // Assume deathless until a death occurs
            firstDeath = false;
            survivedFirstRaid = false;
            firstMechanoidKill = false;
            firstBanishment = false;
            colonistCountStart = 0;
            colonistCountEnd = 0;
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
            sb.AppendLine("-- OBITUARIES (DEATHS) --");
            if (deathsList.Count == 0)
            {
                sb.AppendLine("No deaths this week. A blessing from the gods.");
            }
            else
            {
                foreach (var d in deathsList)
                {
                    sb.AppendLine($"Day {d.day}: {d.name} died from {d.cause}");
                    if (!string.IsNullOrEmpty(d.killer) && d.killer != "unknown")
                        sb.AppendLine($"  Killed by: {d.killer}");
                    if (!string.IsNullOrEmpty(d.lastWords))
                        sb.AppendLine($"  Last words: \"{d.lastWords}\"");
                }
            }

            sb.AppendLine();
            sb.AppendLine("-- BATTLE REPORTS (RAIDS) --");
            if (raidsList.Count == 0)
            {
                sb.AppendLine("No raids this week. The colony rests easy.");
            }
            else
            {
                foreach (var r in raidsList)
                {
                    string outcome = r.survived ? "SURVIVED" : "DEFEAT";
                    sb.AppendLine($"Day {r.day}: Raid by {r.enemyFaction} - {outcome}");
                    sb.AppendLine($"  Enemies: {r.enemyCount}, Our colonists involved: {r.colonistsInvolved}");
                    sb.AppendLine($"  Enemies killed: {r.enemiesKilled}, Colonists lost: {r.colonistsKilled}");
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
            if (milestones.Count == 0 && milestoneFlags.Count == 0)
            {
                sb.AppendLine("No major milestones reached.");
            }
            else
            {
                foreach (var m in milestones)
                {
                    sb.AppendLine($"* {m}");
                }
                foreach (var m in milestoneFlags)
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
            sb.AppendLine("-- COLONIST CHANGES --");
            sb.AppendLine($"Colonist count: {colonistCountStart} at start, {colonistCountEnd} at end");
            if (joins.Count > 0)
            {
                sb.AppendLine("Colonists joined:");
                foreach (var j in joins)
                    sb.AppendLine($"  - {j.name} ({j.reason})");
            }

            sb.AppendLine();
            sb.AppendLine("-- ACHIEVEMENT FLAGS --");
            if (deathlessWeek) sb.AppendLine("* DEATHLESS WEEK - No colonists died!");
            if (raidMarathon) sb.AppendLine("* RAID MARATHON - Survived 3+ raids in one week!");
            if (firstDeath) sb.AppendLine("* FIRST DEATH - Colony experienced its first death");
            if (survivedFirstRaid) sb.AppendLine("* SURVIVED FIRST RAID - The colony's first raid survived!");
            if (firstMechanoidKill) sb.AppendLine("* FIRST MECHANOID KILL - First mechanoid destroyed!");
            if (firstBanishment) sb.AppendLine("* FIRST BANISHMENT - First colonist banished!");
            if (hasReached5Colonists) sb.AppendLine("* COLONY OF 5 - Reached 5 colonists!");
            if (hasReached10Colonists) sb.AppendLine("* COLONY OF 10 - Reached 10 colonists!");
            if (hasReached20Colonists) sb.AppendLine("* COLONY OF 20 - Reached 20 colonists!");

            sb.AppendLine();
            sb.AppendLine("-- MOOD EXTREMES --");
            if (!string.IsNullOrEmpty(bestMoodColonist))
                sb.AppendLine($"Happiest colonist: {bestMoodColonist} at {bestMoodValue:P0} mood");
            if (!string.IsNullOrEmpty(worstMoodColonist))
                sb.AppendLine($"Most troubled colonist: {worstMoodColonist} at {worstMoodValue:P0} mood");

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

            return sb.ToString();
        }
    }
}
