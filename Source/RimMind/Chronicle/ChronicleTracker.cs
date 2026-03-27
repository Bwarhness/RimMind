using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimMind.API;
using RimMind.Core;
using RimMind.Chat;

namespace RimMind.Chronicle
{
    /// <summary>
    /// GameComponent that tracks colony events throughout each week and generates
    /// the weekly Colony Chronicle when a new week begins.
    /// </summary>
    public class ChronicleTracker : GameComponent
    {
        private const int TICKS_PER_DAY = 60000;
        private const int DAYS_PER_WEEK = 7;

        public static ChronicleTracker Instance => Current.Game?.GetComponent<ChronicleTracker>();

        // Current week's event log (being accumulated)
        private WeeklyEventLog currentWeekLog;

        // The most recently generated Chronicle (ready to display)
        private WeeklyChronicle currentChronicle;

        // Previous week's Chronicle for viewing
        private WeeklyChronicle previousChronicle;

        // Tracking state
        private int lastProcessedDay = 0;
        private HashSet<int> trackedColonistIds = new HashSet<int>();

        // LLM callback state
        private bool isGeneratingChronicle = false;
        private WeeklyEventLog pendingLogForGeneration;

        // Wealth snapshots for tracking
        private List<float> wealthSnapshots = new List<float>();
        private List<int> colonistCountSnapshots = new List<int>();

        // Track deaths detected via letters to avoid duplicates
        private HashSet<string> processedDeathLetterIds = new HashSet<string>();

        public ChronicleTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Initialize on game start
            var map = Find.CurrentMap;
            if (map != null)
            {
                int currentDay = GenLocalDate.DayOfYear(map);
                lastProcessedDay = currentDay;

                int weekNumber = (currentDay - 1) / DAYS_PER_WEEK + 1;
                int startDay = (weekNumber - 1) * DAYS_PER_WEEK + 1;
                int endDay = weekNumber * DAYS_PER_WEEK;

                currentWeekLog = new WeeklyEventLog(
                    weekNumber,
                    startDay,
                    endDay,
                    GenLocalDate.Season(map).LabelCap().ToString(),
                    GenLocalDate.Year(map)
                );

                TrackInitialColonists(map);
                TakeInitialSnapshot(map);
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            var map = Find.CurrentMap;
            if (map == null) return;

            int currentDay = GenLocalDate.DayOfYear(map);

            // Check for new day
            if (currentDay != lastProcessedDay)
            {
                OnNewDay(map, currentDay);
                lastProcessedDay = currentDay;
            }

            // Take periodic snapshots for the day
            if (Find.TickManager.TicksGame % (TICKS_PER_DAY / 4) == 0) // Every 6 hours
            {
                TakePeriodicSnapshot(map);
            }
        }

        private void OnNewDay(Map map, int newDay)
        {
            int weekNumber = (newDay - 1) / DAYS_PER_WEEK + 1;

            // Check if we've crossed into a new week
            if (currentWeekLog != null && weekNumber != currentWeekLog.weekNumber)
            {
                // Finalize the old week and generate Chronicle
                FinalizeWeekAndGenerateChronicle(map, newDay, weekNumber);
            }

            // Initialize new week log if needed
            if (currentWeekLog == null || currentWeekLog.weekNumber != weekNumber)
            {
                int startDay = (weekNumber - 1) * DAYS_PER_WEEK + 1;
                int endDay = weekNumber * DAYS_PER_WEEK;

                currentWeekLog = new WeeklyEventLog(
                    weekNumber,
                    startDay,
                    endDay,
                    GenLocalDate.Season(map).LabelCap().ToString(),
                    GenLocalDate.Year(map)
                );

                // Check for new colonists
                CheckForNewColonists(map);
            }

            // Daily event tracking
            TrackDailyEvents(map, newDay);
        }

        private void TrackInitialColonists(Map map)
        {
            trackedColonistIds.Clear();
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                trackedColonistIds.Add(colonist.thingIDNumber);
            }
        }

        private void CheckForNewColonists(Map map)
        {
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (!trackedColonistIds.Contains(colonist.thingIDNumber))
                {
                    trackedColonistIds.Add(colonist.thingIDNumber);

                    currentWeekLog?.milestones.Add($"{colonist.Name.ToStringShort} joined the colony!");
                    currentWeekLog?.events.Add(new ColonyEvent(
                        "recruitment",
                        $"{colonist.Name.ToStringShort} has joined the colony!",
                        colonist.Name.ToStringShort,
                        GenLocalDate.DayOfYear(map)
                    ));
                }
            }

            // Check for departed colonists (raided, escaped, etc.)
            var currentIds = new HashSet<int>(map.mapPawns.FreeColonists.Select(c => c.thingIDNumber));
            var departedIds = trackedColonistIds.Except(currentIds).ToList();

            foreach (var id in departedIds)
            {
                // We don't have the name anymore, but we can note a departure
                currentWeekLog?.milestones.Add($"A colonist has left the colony.");
            }

            trackedColonistIds = currentIds;
        }

        private void TakeInitialSnapshot(Map map)
        {
            if (map == null) return;

            float wealth = map.wealthWatcher.WealthTotal;
            int colonistCount = map.mapPawns.FreeColonists.Count;

            wealthSnapshots.Clear();
            colonistCountSnapshots.Clear();

            wealthSnapshots.Add(wealth);
            colonistCountSnapshots.Add(colonistCount);

            if (currentWeekLog != null)
            {
                currentWeekLog.highestWealth = wealth;
                currentWeekLog.lowestWealth = wealth;
                currentWeekLog.highestColonistCount = colonistCount;
                currentWeekLog.lowestColonistCount = colonistCount;
            }
        }

        private void TakePeriodicSnapshot(Map map)
        {
            if (map == null || currentWeekLog == null) return;

            float wealth = map.wealthWatcher.WealthTotal;
            int colonistCount = map.mapPawns.FreeColonists.Count;

            wealthSnapshots.Add(wealth);
            colonistCountSnapshots.Add(colonistCount);

            if (wealth > currentWeekLog.highestWealth)
                currentWeekLog.highestWealth = wealth;
            if (wealth < currentWeekLog.lowestWealth && wealth > 0)
                currentWeekLog.lowestWealth = wealth;

            if (colonistCount > currentWeekLog.highestColonistCount)
                currentWeekLog.highestColonistCount = colonistCount;
            if (colonistCount < currentWeekLog.lowestColonistCount)
                currentWeekLog.lowestColonistCount = colonistCount;

            // Track mood extremes
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (colonist.needs?.mood == null) continue;

                float mood = colonist.needs.mood.CurLevel;
                float deviation = Math.Abs(mood - 0.5f); // Deviation from neutral

                if (deviation > Math.Abs(currentWeekLog.mostExtremeMoodValue - 0.5f))
                {
                    currentWeekLog.mostExtremeMoodColonist = colonist.Name.ToStringShort;
                    currentWeekLog.mostExtremeMoodValue = mood;
                }
            }
        }

        private void TrackDailyEvents(Map map, int day)
        {
            if (currentWeekLog == null) return;

            // Weather tracking
            var weather = map.weatherManager.curWeather;
            if (weather != null)
            {
                string weatherLabel = weather.LabelCap.ToString();
                if (!weatherLabel.Contains("Clear") && !weatherLabel.Contains("Cloudy"))
                {
                    string weatherEntry = $"Day {day}: {weatherLabel}";
                    if (!currentWeekLog.weatherEvents.Contains(weatherEntry))
                    {
                        currentWeekLog.weatherEvents.Add(weatherEntry);
                    }
                }
            }
        }

        private void FinalizeWeekAndGenerateChronicle(Map map, int newDay, int newWeekNumber)
        {
            if (currentWeekLog == null) return;

            // Move current to previous
            previousChronicle = currentChronicle;

            // Finalize current week stats
            if (wealthSnapshots.Count > 0)
            {
                currentWeekLog.highestWealth = wealthSnapshots.Max();
                currentWeekLog.lowestWealth = wealthSnapshots.Where(w => w > 0).DefaultIfEmpty(0).Min();
            }

            if (colonistCountSnapshots.Count > 0)
            {
                currentWeekLog.highestColonistCount = colonistCountSnapshots.Max();
                currentWeekLog.lowestColonistCount = colonistCountSnapshots.Min();
            }

            // Create a log copy for generation (since currentWeekLog will be reset)
            var logForGeneration = currentWeekLog;

            // Send notification
            SendChronicleNotification(logForGeneration);

            // Generate the Chronicle via LLM
            GenerateChronicleAsync(logForGeneration);
        }

        private void SendChronicleNotification(WeeklyEventLog log)
        {
            Find.LetterStack.ReceiveLetter(
                "Colony Chronicle",
                $"Week {log.weekNumber} has ended. The Colony Chronicle is being prepared...",
                LetterDefOf.NeutralEvent
            );
        }

        private void GenerateChronicleAsync(WeeklyEventLog log)
        {
            if (isGeneratingChronicle) return;
            isGeneratingChronicle = true;
            pendingLogForGeneration = log;

            // Build the prompt for chronicle generation
            string prompt = BuildChroniclePrompt(log);

            // Create a simple chat request for chronicle generation
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(@"You are the editor of the Colony Chronicle, a newspaper for a RimWorld colony. 
Generate a newspaper-style weekly chronicle based on the provided data.
Write in an old-timey, witty newspaper style - think 1800s frontier newspaper meets dark humor.
Be entertaining and dramatic, but mostly accurate to the data provided.
Use section headers with emojis like newspapers do.

FORMAT YOUR RESPONSE EXACTLY LIKE THIS (use this structure, fill in the content):

[TITLE]
The Colony Chronicle - Week {weekNumber}
{season}, Day {startDay}-{endDay}

[HEADLINE]
{Your exciting headline here}

[LEAD]
{2-3 sentence lead paragraph summarizing the week}

[SECTION:name:BATTLE REPORT:⚔️]
{Section content here - describe battles, raids, fights}

[SECTION:name:OBITUARIES:😢]
{Section content here - list the dead, be appropriately somber}

[SECTION:name:ECONOMY:📦]
{Section content here - trades, wealth changes, resources}

[SECTION:name:MILESTONES:🏆]
{Section content here - achievements, births, notable events}

[SECTION:name:WEATHER:🌤️]
{Section content here - a brief, possibly poetic description of the weather}

[SECTION:name:LOOKING AHEAD:🔮]
{Section content here - a prediction or ominous warning about next week}

[QUOTES]
""Quote 1"" - ColonistName
""Quote 2"" - ColonistName

Use creative, entertaining language. This is a fictional newspaper for a harsh frontier colony."),
                ChatMessage.User(prompt)
            };

            var request = new ChatRequest
            {
                model = RimMindMod.Settings.ActiveModelId,
                messages = messages,
                temperature = 0.9f,
                max_tokens = 2000
            };

            Action<ChatResponse> handleResponse = response =>
            {
                isGeneratingChronicle = false;

                if (!response.success)
                {
                    Log.Warning("[RimMind] Chronicle generation failed: " + response.error);
                    // Create a fallback chronicle
                    currentChronicle = CreateFallbackChronicle(log);
                    return;
                }

                string content = response.message?.content ?? "";
                currentChronicle = ParseChronicleResponse(content, log);
            };

            // Send to the appropriate API
            if (RimMindMod.Settings.IsClaudeCode)
                ClaudeCodeClient.SendAsync(request, handleResponse);
            else if (RimMindMod.Settings.IsAnthropic)
                AnthropicClient.SendAsync(request, handleResponse);
            else if (RimMindMod.Settings.IsCustom)
                CustomProviderClient.SendAsync(request, handleResponse);
            else
                OpenRouterClient.SendAsync(request, handleResponse);
        }

        private string BuildChroniclePrompt(WeeklyEventLog log)
        {
            return log.BuildContextSummary();
        }

        private WeeklyChronicle ParseChronicleResponse(string content, WeeklyEventLog log)
        {
            var chronicle = new WeeklyChronicle(
                log.weekNumber,
                log.endDay,
                log.season,
                log.year
            );

            chronicle.events = new List<ColonyEvent>(log.events);
            chronicle.quotes = new List<ColonistQuote>(log.quotes);
            chronicle.milestones = new List<string>(log.milestones);
            chronicle.weatherPoem = "Weather was uneventful this week.";

            // Parse the content into sections
            // The LLM should format it with [SECTION:name:emoji] markers
            // But we'll parse what we can from plain text

            if (string.IsNullOrEmpty(content))
            {
                return CreateFallbackChronicle(log);
            }

            // Simple parsing - look for section markers
            var lines = content.Split('\n');
            ChronicleSection currentSection = null;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("[HEADLINE]"))
                {
                    chronicle.topHeadline = trimmed.Substring("[HEADLINE]".Length).Trim();
                }
                else if (trimmed.StartsWith("[LEAD]"))
                {
                    chronicle.leadParagraph = trimmed.Substring("[LEAD]".Length).Trim();
                }
                else if (trimmed.StartsWith("[QUOTES]"))
                {
                    currentSection = null; // Quotes handled separately
                }
                else if (trimmed.StartsWith("[SECTION:"))
                {
                    // Parse [SECTION:name:emoji]
                    int firstColon = trimmed.IndexOf(':');
                    int secondColon = trimmed.IndexOf(':', firstColon + 1);
                    int closingBracket = trimmed.IndexOf(']', secondColon + 1);

                    if (firstColon > 0 && secondColon > firstColon && closingBracket > secondColon)
                    {
                        string name = trimmed.Substring(firstColon + 1, secondColon - firstColon - 1);
                        string emoji = trimmed.Substring(secondColon + 1, closingBracket - secondColon - 1);

                        currentSection = new ChronicleSection(name, emoji, "");
                        chronicle.sections.Add(currentSection);
                    }
                }
                else if (currentSection != null)
                {
                    if (string.IsNullOrEmpty(currentSection.content))
                        currentSection.content = trimmed;
                    else
                        currentSection.content += "\n" + trimmed;
                }
                else if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    // Likely a quote
                    var quoteText = trimmed.Trim('"');
                    var parts = quoteText.Split(new[] { " - ", " — " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        chronicle.quotes.Add(new ColonistQuote(parts[1], parts[0]));
                    }
                }
            }

            // If we couldn't parse sections, create some default ones
            if (chronicle.sections.Count == 0)
            {
                chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "⚔️",
                    log.raids > 0 ? $"{log.raids} raid(s) occurred this week." : "A quiet week on the battlefield."));

                chronicle.sections.Add(new ChronicleSection("OBITUARIES", "😢",
                    log.deaths > 0 ? $"{log.deaths} colonist(s) passed away." : "No deaths this week. A blessing."));

                chronicle.sections.Add(new ChronicleSection("ECONOMY", "📦",
                    $"Colony wealth: {log.highestWealth:N0} silver. {log.trades} trade(s) conducted."));

                chronicle.sections.Add(new ChronicleSection("MILESTONES", "🏆",
                    log.milestones.Count > 0 ? string.Join("\n", log.milestones) : "No major milestones."));

                if (log.weatherEvents.Count > 0)
                {
                    chronicle.sections.Add(new ChronicleSection("WEATHER", "🌤️",
                        string.Join("\n", log.weatherEvents)));
                }
            }

            chronicle.isGenerated = true;
            return chronicle;
        }

        private WeeklyChronicle CreateFallbackChronicle(WeeklyEventLog log)
        {
            var chronicle = new WeeklyChronicle(
                log.weekNumber,
                log.endDay,
                log.season,
                log.year
            );

            chronicle.topHeadline = $"Week {log.weekNumber}: The Colony Endures";
            chronicle.leadParagraph = $"As Day {log.endDay} closes, the colonists of this settlement reflect on a week of challenges and small victories. The {log.season} season brings its own trials.";

            chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "⚔️",
                log.raids > 0 ? $"{log.raids} raid(s) tested our defenses." : "The enemy held back this week. Enjoy the peace while it lasts."));

            chronicle.sections.Add(new ChronicleSection("OBITUARIES", "😢",
                log.deaths > 0 ? $"{log.deaths} soul(s) departed this mortal colony." : "No deaths recorded. The Reaper takes a holiday."));

            chronicle.sections.Add(new ChronicleSection("ECONOMY", "📦",
                $"Wealth peaked at {log.highestWealth:N0} silver. {log.trades} caravan(s) visited our trade depots."));

            chronicle.sections.Add(new ChronicleSection("MILESTONES", "🏆",
                log.milestones.Count > 0 ? string.Join("\n", log.milestones) : "The colony grows, one day at a time."));

            if (log.weatherEvents.Count > 0)
            {
                chronicle.sections.Add(new ChronicleSection("WEATHER", "🌤️",
                    string.Join("\n", log.weatherEvents)));
            }

            chronicle.events = new List<ColonyEvent>(log.events);
            chronicle.quotes = new List<ColonistQuote>(log.quotes);
            chronicle.milestones = new List<string>(log.milestones);
            chronicle.isGenerated = true;

            return chronicle;
        }

        // ========================
        // PUBLIC API
        // ========================

        /// <summary>
        /// Records a significant event for the current week.
        /// </summary>
        public void RecordEvent(ColonyEvent evt)
        {
            currentWeekLog?.events.Add(evt);

            switch (evt.type)
            {
                case "death":
                    currentWeekLog.deaths++;
                    break;
                case "raid":
                case "attack":
                    currentWeekLog.raids++;
                    break;
                case "trade":
                    currentWeekLog.trades++;
                    break;
                case "birth":
                    currentWeekLog.births++;
                    break;
                case "mental_break":
                    currentWeekLog.mentalBreaks++;
                    break;
            }
        }

        /// <summary>
        /// Records a colonist's quote for the current week.
        /// </summary>
        public void RecordQuote(ColonistQuote quote)
        {
            currentWeekLog?.quotes.Add(quote);
        }

        /// <summary>
        /// Records a milestone achievement for the current week.
        /// </summary>
        public void RecordMilestone(string milestone)
        {
            currentWeekLog?.milestones.Add(milestone);
        }

        /// <summary>
        /// Records a research completion.
        /// </summary>
        public void RecordResearchCompletion(string researchName)
        {
            if (currentWeekLog != null)
            {
                currentWeekLog.researchCompleted++;
                currentWeekLog.milestones.Add($"Completed research: {researchName}");
            }
        }

        /// <summary>
        /// Records a successful surgery.
        /// </summary>
        public void RecordSurgery()
        {
            if (currentWeekLog != null)
            {
                currentWeekLog.surgeries++;
            }
        }

        /// <summary>
        /// Records an animal taming.
        /// </summary>
        public void RecordAnimalTamed(string animalName)
        {
            if (currentWeekLog != null)
            {
                currentWeekLog.animalsTamed++;
                currentWeekLog.milestones.Add($"Tamed a {animalName}");
            }
        }

        /// <summary>
        /// Gets the most recently generated chronicle, or null if none exists.
        /// </summary>
        public WeeklyChronicle GetCurrentChronicle()
        {
            return currentChronicle;
        }

        /// <summary>
        /// Gets the previous week's chronicle.
        /// </summary>
        public WeeklyChronicle GetPreviousChronicle()
        {
            return previousChronicle;
        }

        /// <summary>
        /// Checks if a chronicle is currently being generated.
        /// </summary>
        public bool IsGeneratingChronicle => isGeneratingChronicle;

        /// <summary>
        /// Gets the current week's event log.
        /// </summary>
        public WeeklyEventLog GetCurrentWeekLog()
        {
            return currentWeekLog;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Note: WeeklyChronicle and WeeklyEventLog contain complex nested types
            // that aren't directly Scribe-compatible. We skip saving them for now
            // and let them rebuild at week end, or implement custom serialization later.
            // For a Phase 1, we'll just preserve tracking state.

            Scribe_Values.Look(ref lastProcessedDay, "lastProcessedDay");

            // Handle tracked colonist IDs
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var idsList = trackedColonistIds.ToList();
                Scribe_Collections.Look(ref idsList, "trackedColonistIds", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var idsList = new List<int>();
                Scribe_Collections.Look(ref idsList, "trackedColonistIds", LookMode.Value);
                if (idsList != null)
                    trackedColonistIds = new HashSet<int>(idsList);
            }

            Scribe_Collections.Look(ref wealthSnapshots, "wealthSnapshots", LookMode.Value);
            Scribe_Collections.Look(ref colonistCountSnapshots, "colonistCountSnapshots", LookMode.Value);
        }
    }
}
