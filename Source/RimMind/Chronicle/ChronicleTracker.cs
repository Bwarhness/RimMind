using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using RimMind.API;
using RimMind.Core;
using RimMind.Chat;

namespace RimMind.Chronicle
{
    /// <summary>
    /// The editorial voice for RimMind's Colony Chronicle.
    /// Defines how the AI writes the newspaper-style report.
    /// </summary>
    public static class ChronicleEditorialVoice
    {
        public const string SystemPrompt = "You are RimMind, Colony Chronicle Staff Editor. " +
            "You write with dry wit, weary experience, and occasional genuine warmth. " +
            "You have opinions. You are not neutral. You have survived 47 colonies and none of them are still standing. " +
            "Your voice: dry, slightly weary journalist. Occasionally passive-aggressive about colony decisions. " +
            "Self-aware. Occasionally genuinely moved. Use phrases like 'A bold strategy' or 'The colony finest minds decided'. " +
            "Write as frontier newspaper editor in 1800s who has witnessed collapse of dozens of settlements. " +
            "FORMAT: [TITLE] Week {weekNumber}, {season} Day {startDay}-{endDay}. " +
            "[HEADLINE] {headline}. [LEAD] {2-3 sentences}. " +
            "[SECTION:BATTLE REPORT] {content}. [SECTION:OBITUARIES] {content}. " +
            "[SECTION:ECONOMY] {content}. [SECTION:MILESTONES] {content}. [SECTION:WEATHER] {content}. " +
            "[SECTION:RUNNING JOKE] THE [NAME] INDEX: description (continuing ongoing coverage) or No persistent issues. " +
            "[SECTION:PREDICTIONS] - EVENT -- X% confidence (based on BASIS). [SECTION:EDITORIAL] FROM THE EDITOR'S DESK: hot takes. " +
            "[INTERVIEW] INTERVIEW WITH: name, age, job. Q and A. [QUOTES] quote - name. [ON THIS DAY] recap or archives lost. " +
            "Have opinions. Be funny.";

        public const string RunningJokeInstruction = "Identify ONE recurring issue from data as ongoing joke. " +
            "Examples: Marcus fire problem, brewery-fuel storage issue, psychopaths on committee, lifespan trends. " +
            "Format: THE [ISSUE] INDEX: description (continuing ongoing coverage). Or: No persistent issues.";
    }

    /// <summary>
    /// GameComponent that tracks colony events throughout each week and generates
    /// the weekly Colony Chronicle when a new week begins.
    /// </summary>
    public class ChronicleTracker : GameComponent
    {
        private const int TICKS_PER_DAY = 60000;
        private const int DAYS_PER_WEEK = 7;
        private const int MAX_RUNNING_JOKES = 4;
        private const int PREDICTIONS_COUNT = 3;

        public static ChronicleTracker Instance => Current.Game?.GetComponent<ChronicleTracker>();

        private WeeklyEventLog currentWeekLog;
        private WeeklyChronicle currentChronicle;
        private WeeklyChronicle previousChronicle;
        private List<string> historicalRunningJokes = new List<string>();
        private List<ChronicleArchive> chronicleArchives = new List<ChronicleArchive>();
        private int lastProcessedDay = 0;
        private HashSet<int> trackedColonistIds = new HashSet<int>();
        private bool isGeneratingChronicle = false;
        private WeeklyEventLog pendingLogForGeneration;
        private List<float> wealthSnapshots = new List<float>();
        private List<int> colonistCountSnapshots = new List<int>();
        private HashSet<string> processedDeathLetterIds = new HashSet<string>();
        private string colonyName = "Unnamed Colony";

        private string ArchiveFilePath
        {
            get
            {
                var folder = GenFilePaths.SaveDataFolderPath;
                return Path.Combine(folder, "RimMindChronicleArchive.json");
            }
        }

        public ChronicleTracker(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            colonyName = GetColonyName();
            LoadChronicleArchives();

            var map = Find.CurrentMap;
            if (map != null)
            {
                int currentDay = GenLocalDate.DayOfYear(map);
                lastProcessedDay = currentDay;
                int weekNumber = (currentDay - 1) / DAYS_PER_WEEK + 1;
                int startDay = (weekNumber - 1) * DAYS_PER_WEEK + 1;
                int endDay = weekNumber * DAYS_PER_WEEK;

                currentWeekLog = new WeeklyEventLog(weekNumber, startDay, endDay, GenLocalDate.Season(map).LabelCap().ToString(), GenLocalDate.Year(map));
                TrackInitialColonists(map);
                TakeInitialSnapshot(map);
            }
        }

        private string GetColonyName()
        {
            if (Find.CurrentMap?.Parent?.LabelCap != null)
                return Find.CurrentMap.Parent.LabelCap.ToString();
            return "Unnamed Settlement";
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            var map = Find.CurrentMap;
            if (map == null) return;

            int currentDay = GenLocalDate.DayOfYear(map);
            if (currentDay != lastProcessedDay)
            {
                OnNewDay(map, currentDay);
                lastProcessedDay = currentDay;
            }

            if (Find.TickManager.TicksGame % (TICKS_PER_DAY / 4) == 0)
                TakePeriodicSnapshot(map);
        }

        private void OnNewDay(Map map, int newDay)
        {
            int weekNumber = (newDay - 1) / DAYS_PER_WEEK + 1;
            if (currentWeekLog != null && weekNumber != currentWeekLog.weekNumber)
                FinalizeWeekAndGenerateChronicle(map, newDay, weekNumber);

            if (currentWeekLog == null || currentWeekLog.weekNumber != weekNumber)
            {
                int startDay = (weekNumber - 1) * DAYS_PER_WEEK + 1;
                int endDay = weekNumber * DAYS_PER_WEEK;
                currentWeekLog = new WeeklyEventLog(weekNumber, startDay, endDay, GenLocalDate.Season(map).LabelCap().ToString(), GenLocalDate.Year(map));
                CheckForNewColonists(map);
            }

            TrackDailyEvents(map, newDay);
        }

        private void TrackInitialColonists(Map map)
        {
            trackedColonistIds.Clear();
            foreach (var colonist in map.mapPawns.FreeColonists)
                trackedColonistIds.Add(colonist.thingIDNumber);
        }

        private void CheckForNewColonists(Map map)
        {
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (!trackedColonistIds.Contains(colonist.thingIDNumber))
                {
                    trackedColonistIds.Add(colonist.thingIDNumber);
                    currentWeekLog?.milestones.Add(colonist.Name.ToStringShort + " joined the colony!");
                    currentWeekLog?.events.Add(new ColonyEvent("recruitment", colonist.Name.ToStringShort + " has joined the colony!", colonist.Name.ToStringShort, GenLocalDate.DayOfYear(map)));
                }
            }

            var currentIds = new HashSet<int>(map.mapPawns.FreeColonists.Select(c => c.thingIDNumber));
            var departedIds = trackedColonistIds.Except(currentIds).ToList();
            foreach (var id in departedIds)
                currentWeekLog?.milestones.Add("A colonist has left the colony.");

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

            if (wealth > currentWeekLog.highestWealth) currentWeekLog.highestWealth = wealth;
            if (wealth < currentWeekLog.lowestWealth && wealth > 0) currentWeekLog.lowestWealth = wealth;
            if (colonistCount > currentWeekLog.highestColonistCount) currentWeekLog.highestColonistCount = colonistCount;
            if (colonistCount < currentWeekLog.lowestColonistCount) currentWeekLog.lowestColonistCount = colonistCount;

            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (colonist.needs?.mood == null) continue;
                float mood = colonist.needs.mood.CurLevel;
                float deviation = Math.Abs(mood - 0.5f);
                if (deviation > Math.Abs(currentWeekLog.mostExtremeMoodValue - 0.5f))
                {
                    currentWeekLog.mostExtremeMoodColonist = colonist.Name.ToStringShort;
                    currentWeekLog.mostExtremeMoodValue = mood;
                }
                // Track pyromaniacs
                if (colonist.story?.traits?.HasTrait(TraitDefOf.Pyromaniac) == true)
                {
                    var key = colonist.Name.ToStringShort;
                    if (!currentWeekLog.fireCausesByColonist.ContainsKey(key))
                        currentWeekLog.fireCausesByColonist[key] = 0;
                }
                // Track mental breaks by colonist using InMentalState
                if (colonist.InMentalState)
                {
                    var key = colonist.Name.ToStringShort;
                    if (!currentWeekLog.mentalBreakCountByColonist.ContainsKey(key))
                        currentWeekLog.mentalBreakCountByColonist[key] = 0;
                    currentWeekLog.mentalBreakCountByColonist[key]++;
                }
            }
        }

        private void TrackDailyEvents(Map map, int day)
        {
            if (currentWeekLog == null) return;
            var weather = map.weatherManager.curWeather;
            if (weather != null)
            {
                string weatherLabel = weather.LabelCap.ToString();
                if (!weatherLabel.Contains("Clear") && !weatherLabel.Contains("Cloudy"))
                {
                    string weatherEntry = "Day " + day + ": " + weatherLabel;
                    if (!currentWeekLog.weatherEvents.Contains(weatherEntry))
                        currentWeekLog.weatherEvents.Add(weatherEntry);
                }
            }
        }

        private void FinalizeWeekAndGenerateChronicle(Map map, int newDay, int newWeekNumber)
        {
            if (currentWeekLog == null) return;
            previousChronicle = currentChronicle;

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

            var logForGeneration = currentWeekLog;
            SaveChronicleToArchive(logForGeneration);
            SendChronicleNotification(logForGeneration);
            PlayChronicleArrivalSound();
            GenerateChronicleAsync(logForGeneration);
        }

        private void SendChronicleNotification(WeeklyEventLog log)
        {
            Find.LetterStack.ReceiveLetter("Colony Chronicle", "Week " + log.weekNumber + " has ended. The " + colonyName + " Chronicle is being prepared...", LetterDefOf.NeutralEvent);
        }

        private void PlayChronicleArrivalSound()
        {
            // Chronicle sound effect - disabled as SoundDefOf.UI.TakeTurn is not available
            // Could be replaced with a custom SoundDef in the mod's Defs folder
            // Log.Message("[RimMind] Chronicle has arrived.");
        }

        private void GenerateChronicleAsync(WeeklyEventLog log)
        {
            if (isGeneratingChronicle) return;
            isGeneratingChronicle = true;
            pendingLogForGeneration = log;
            string prompt = BuildChroniclePrompt(log);
            string systemPrompt = ChronicleEditorialVoice.SystemPrompt;

            var messages = new List<ChatMessage> { ChatMessage.System(systemPrompt), ChatMessage.User(prompt) };
            var request = new ChatRequest { model = RimMindMod.Settings.ActiveModelId, messages = messages, temperature = 0.9f, max_tokens = 2500 };

            Action<ChatResponse> handleResponse = response =>
            {
                isGeneratingChronicle = false;
                if (!response.success)
                {
                    Log.Warning("[RimMind] Chronicle generation failed: " + response.error);
                    currentChronicle = CreateFallbackChronicle(log);
                    return;
                }
                string content = response.message?.content ?? "";
                currentChronicle = ParseChronicleResponse(content, log);
            };

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
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(log.BuildContextSummary());

            if (historicalRunningJokes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("-- ONGOING JOKE COVERAGE --");
                foreach (var joke in historicalRunningJokes)
                    sb.AppendLine("* " + joke);
            }

            sb.AppendLine();
            sb.AppendLine(ChronicleEditorialVoice.RunningJokeInstruction);
            sb.AppendLine();
            sb.AppendLine("-- PREDICTION GENERATION --");
            sb.AppendLine("Generate exactly " + PREDICTIONS_COUNT + " predictions for the upcoming week based on:");
            sb.AppendLine("- Historical raid frequency: " + log.raids + " raids this week");
            sb.AppendLine("- Current mood trends: " + (log.mostExtremeMoodValue < 0.3 ? "colony morale appears low" : "colony morale appears stable"));
            sb.AppendLine("- Weather patterns this week: " + log.weatherEvents.Count + " notable weather events");
            sb.AppendLine("- Wealth level: " + log.highestWealth.ToString("N0") + " silver (higher wealth attracts raids)");
            sb.AppendLine();
            sb.AppendLine("-- ONE YEAR AGO --");
            var oneYearAgo = GetOneYearAgoSummary();
            if (!string.IsNullOrEmpty(oneYearAgo))
                sb.AppendLine("ON THIS DAY, ONE YEAR AGO: " + oneYearAgo);
            else
                sb.AppendLine("The Chronicle archives from this week last year appear to have been lost in a tragic raid on our filing system.");

            return sb.ToString();
        }

        private string GetOneYearAgoSummary()
        {
            var currentRealWeek = GetRealWeekNumber();
            var lastYear = DateTime.Now.Year - 1;
            var archive = chronicleArchives.FirstOrDefault(a => a.realYear == lastYear && a.realWeekNumber == currentRealWeek);
            return archive?.summary;
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

        private void SaveChronicleToArchive(WeeklyEventLog log)
        {
            if (currentChronicle == null) return;
            try
            {
                var archive = new ChronicleArchive(currentChronicle, colonyName);
                var existing = chronicleArchives.FirstOrDefault(a => a.realYear == archive.realYear && a.realWeekNumber == archive.realWeekNumber);
                if (existing != null) chronicleArchives.Remove(existing);
                chronicleArchives.Add(archive);

                var cutoffYear = DateTime.Now.Year - 2;
                chronicleArchives.RemoveAll(a => a.realYear < cutoffYear);
                SaveChronicleArchives();
            }
            catch (Exception ex) { Log.Warning("[RimMind] Failed to save chronicle archive: " + ex.Message); }
        }

        private void LoadChronicleArchives()
        {
            // Archive loading disabled - requires proper RimWorld Scribe handling
            // Archives will be rebuilt each session
            chronicleArchives = new List<ChronicleArchive>();
        }

        private void SaveChronicleArchives()
        {
            // Archive saving disabled - requires proper RimWorld Scribe handling
            // See ChronicleArchiveContainer class below for future implementation
        }

        /// <summary>
        /// Container for serialized chronicle archives.
        /// </summary>
        public class ChronicleArchiveContainer : IExposable
        {
            public List<ChronicleArchive> archives = new List<ChronicleArchive>();

            public void ExposeData()
            {
                Scribe_Collections.Look(ref archives, "archives", LookMode.Deep);
            }
        }

        private WeeklyChronicle ParseChronicleResponse(string content, WeeklyEventLog log)
        {
            var chronicle = new WeeklyChronicle(log.weekNumber, log.endDay, log.season, log.year);
            chronicle.events = new List<ColonyEvent>(log.events);
            chronicle.quotes = new List<ColonistQuote>(log.quotes);
            chronicle.milestones = new List<string>(log.milestones);
            chronicle.weatherPoem = "Weather was uneventful this week.";
            chronicle.interviews = new List<ColonistInterview>();
            chronicle.predictions = new List<Prediction>();
            chronicle.runningJokes = new List<string>(historicalRunningJokes);

            if (string.IsNullOrEmpty(content)) return CreateFallbackChronicle(log);

            var lines = content.Split('\n');
            ChronicleSection currentSection = null;
            string interviewSection = null;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[HEADLINE]"))
                    chronicle.topHeadline = trimmed.Substring("[HEADLINE]".Length).Trim();
                else if (trimmed.StartsWith("[LEAD]"))
                    chronicle.leadParagraph = trimmed.Substring("[LEAD]".Length).Trim();
                else if (trimmed.StartsWith("[INTERVIEW]"))
                {
                    interviewSection = trimmed.Substring("[INTERVIEW]".Length).Trim();
                    if (!string.IsNullOrEmpty(interviewSection))
                    {
                        var interview = ParseInterviewSection(interviewSection);
                        if (interview != null) chronicle.interviews.Add(interview);
                    }
                }
                else if (trimmed.StartsWith("[ON THIS DAY]"))
                    chronicle.oneYearAgoSummary = trimmed.Substring("[ON THIS DAY]".Length).Trim();
                else if (trimmed.StartsWith("[QUOTES]"))
                    currentSection = null;
                else if (trimmed.StartsWith("[SECTION:"))
                {
                    int firstColon = trimmed.IndexOf(':');
                    int secondColon = trimmed.IndexOf(':', firstColon + 1);
                    int closingBracket = trimmed.IndexOf(']', secondColon + 1);
                    if (firstColon > 0 && secondColon > firstColon && closingBracket > secondColon)
                    {
                        string name = trimmed.Substring(firstColon + 1, secondColon - firstColon - 1);
                        string emoji = trimmed.Substring(secondColon + 1, closingBracket - secondColon - 1);

                        if (name == "EDITORIAL")
                        {
                            currentSection = null;
                            var editorialContent = trimmed.Substring(closingBracket + 1).Trim();
                            if (!string.IsNullOrEmpty(editorialContent)) chronicle.editorial = editorialContent;
                        }
                        else if (name == "PREDICTIONS" || name == "RUNNING JOKE")
                        {
                            currentSection = new ChronicleSection(name, emoji, "");
                            chronicle.sections.Add(currentSection);
                        }
                        else
                        {
                            currentSection = new ChronicleSection(name, emoji, "");
                            chronicle.sections.Add(currentSection);
                        }
                    }
                }
                else if (currentSection != null)
                {
                    if (currentSection.title == "PREDICTIONS" && (trimmed.StartsWith("-") || trimmed.StartsWith("*")))
                    {
                        var prediction = ParsePredictionLine(trimmed);
                        if (prediction != null) chronicle.predictions.Add(prediction);
                    }
                    else if (currentSection.title == "RUNNING JOKE" && trimmed.Contains("INDEX:"))
                    {
                        chronicle.runningJokeCurrent = trimmed;
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains("No persistent issues"))
                            if (!chronicle.runningJokes.Contains(trimmed)) chronicle.runningJokes.Add(trimmed);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(currentSection.content))
                            currentSection.content = trimmed;
                        else
                            currentSection.content += "\n" + trimmed;
                    }
                }
                else if (trimmed.StartsWith("FROM THE EDITOR'S DESK:") || trimmed.StartsWith("FROM THE EDITOR"))
                    chronicle.editorial = trimmed.Contains(":") ? trimmed.Substring(trimmed.IndexOf(':') + 1).Trim() : trimmed;
                else if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    var quoteText = trimmed.Trim('"');
                    var parts = quoteText.Split(new[] { " - ", " -- ", " - " }, StringSplitOptions.None);
                    if (parts.Length >= 2) chronicle.quotes.Add(new ColonistQuote(parts[parts.Length - 1], parts[0]));
                }
            }

            UpdateHistoricalRunningJokes(chronicle.runningJokeCurrent);

            if (chronicle.interviews.Count == 0 && !string.IsNullOrEmpty(interviewSection))
            {
                var interview = ParseInterviewSection(interviewSection);
                if (interview != null) chronicle.interviews.Add(interview);
            }

            if (chronicle.sections.Count == 0)
            {
                chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "Sword", log.raids > 0 ? log.raids + " raid(s) occurred this week." : "A quiet week on the battlefield."));
                chronicle.sections.Add(new ChronicleSection("OBITUARIES", "Skull", log.deaths > 0 ? log.deaths + " colonist(s) passed away." : "No deaths this week. A blessing."));
                chronicle.sections.Add(new ChronicleSection("ECONOMY", "Box", "Colony wealth: " + log.highestWealth.ToString("N0") + " silver. " + log.trades + " trade(s) conducted."));
                chronicle.sections.Add(new ChronicleSection("MILESTONES", "Trophy", log.milestones.Count > 0 ? string.Join("\n", log.milestones) : "No major milestones."));
                if (log.weatherEvents.Count > 0) chronicle.sections.Add(new ChronicleSection("WEATHER", "Cloud", string.Join("\n", log.weatherEvents)));
            }

            if (!string.IsNullOrEmpty(chronicle.editorial) && !chronicle.editorial.StartsWith("FROM THE EDITOR'S DESK:"))
                chronicle.editorial = "FROM THE EDITOR'S DESK:\n" + chronicle.editorial;

            chronicle.isGenerated = true;
            return chronicle;
        }

        private ColonistInterview ParseInterviewSection(string section)
        {
            try
            {
                var lines = section.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3) return null;
                var headerLine = lines[0].Trim();
                if (!headerLine.StartsWith("INTERVIEW WITH:")) return null;
                var headerParts = headerLine.Substring("INTERVIEW WITH:".Length).Trim().Split(',');
                if (headerParts.Length < 3) return null;
                var name = headerParts[0].Trim();
                var age = headerParts[1].Trim();
                var job = headerParts[2].Trim();
                var question = lines.Length > 1 ? lines[1].Trim().Trim('"') : "";
                var answer = lines.Length > 2 ? lines[2].Trim().Trim('"') : "";
                return new ColonistInterview(name, age, job, "several", question, answer);
            }
            catch { return null; }
        }

        private Prediction ParsePredictionLine(string line)
        {
            try
            {
                line = line.TrimStart('-', '*', ' ', '.');
                var parts = line.Split(new[] { "--", "-" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return null;
                var eventDesc = parts[0].Trim();
                var remaining = parts[1].Trim();
                int confidence = 50;
                string basis = "unknown";
                var confMatch = System.Text.RegularExpressions.Regex.Match(remaining, @"(\d+)%");
                if (confMatch.Success) int.TryParse(confMatch.Groups[1].Value, out confidence);
                var basisMatch = System.Text.RegularExpressions.Regex.Match(remaining, @"\(([^)]+)\)");
                if (basisMatch.Success) basis = basisMatch.Groups[1].Value.Replace("based on", "").Trim();
                return new Prediction(eventDesc, confidence, basis, "");
            }
            catch { return null; }
        }

        private void UpdateHistoricalRunningJokes(string newJoke)
        {
            if (string.IsNullOrEmpty(newJoke) || newJoke.Contains("No persistent issues")) return;
            if (!historicalRunningJokes.Contains(newJoke))
            {
                historicalRunningJokes.Insert(0, newJoke);
                while (historicalRunningJokes.Count > MAX_RUNNING_JOKES)
                    historicalRunningJokes.RemoveAt(historicalRunningJokes.Count - 1);
            }
        }

        private WeeklyChronicle CreateFallbackChronicle(WeeklyEventLog log)
        {
            var chronicle = new WeeklyChronicle(log.weekNumber, log.endDay, log.season, log.year);
            chronicle.topHeadline = "Week " + log.weekNumber + ": The Colony Endures";
            chronicle.leadParagraph = "As Day " + log.endDay + " closes, the colonists of " + colonyName + " reflect on a week of challenges and small victories. The " + log.season + " season brings its own trials.";

            chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "Sword", log.raids > 0 ? log.raids + " raid(s) tested our defenses." : "The enemy held back this week. Enjoy the peace while it lasts."));
            chronicle.sections.Add(new ChronicleSection("OBITUARIES", "Skull", log.deaths > 0 ? log.deaths + " soul(s) departed this mortal colony." : "No deaths recorded. The Reaper takes a holiday."));
            chronicle.sections.Add(new ChronicleSection("ECONOMY", "Box", "Wealth peaked at " + log.highestWealth.ToString("N0") + " silver. " + log.trades + " caravan(s) visited our trade depots."));
            chronicle.sections.Add(new ChronicleSection("MILESTONES", "Trophy", log.milestones.Count > 0 ? string.Join("\n", log.milestones) : "The colony grows, one day at a time."));
            if (log.weatherEvents.Count > 0) chronicle.sections.Add(new ChronicleSection("WEATHER", "Cloud", string.Join("\n", log.weatherEvents)));

            chronicle.runningJokeCurrent = "THE QUIET WEEK INDEX: Nothing particularly newsworthy occurred this week. (continuing our ongoing lack of excitement coverage)";
            chronicle.runningJokes = new List<string>(historicalRunningJokes);
            chronicle.editorial = "FROM THE EDITOR'S DESK:\nThe decision to continue existing is, as always, commendable. This publication recommends more exciting events for next week's coverage.";
            chronicle.predictions.Add(new Prediction("A raid is always possible", 65, "historical data", "Raids happen with troubling regularity"));
            chronicle.predictions.Add(new Prediction("Someone will complain about food", 80, "current mood", "It is what colonists do best"));
            chronicle.predictions.Add(new Prediction("The weather will change", 90, "weather patterns", "It has been doing that a lot lately"));

            chronicle.events = new List<ColonyEvent>(log.events);
            chronicle.quotes = new List<ColonistQuote>(log.quotes);
            chronicle.milestones = new List<string>(log.milestones);
            chronicle.isGenerated = true;
            return chronicle;
        }

        // PUBLIC API
        public void RecordEvent(ColonyEvent evt)
        {
            currentWeekLog?.events.Add(evt);
            if (currentWeekLog == null) return;
            switch (evt.type)
            {
                case "death": currentWeekLog.deaths++; break;
                case "raid": case "attack": currentWeekLog.raids++; break;
                case "trade": currentWeekLog.trades++; break;
                case "birth": currentWeekLog.births++; break;
                case "mental_break": currentWeekLog.mentalBreaks++; break;
                case "fire": currentWeekLog.fires++; break;
            }
        }

        public void RecordQuote(ColonistQuote quote) { currentWeekLog?.quotes.Add(quote); }
        public void RecordMilestone(string milestone) { currentWeekLog?.milestones.Add(milestone); }
        public void RecordResearchCompletion(string researchName)
        {
            if (currentWeekLog != null) { currentWeekLog.researchCompleted++; currentWeekLog.milestones.Add("Completed research: " + researchName); }
        }
        public void RecordSurgery() { if (currentWeekLog != null) currentWeekLog.surgeries++; }
        public void RecordAnimalTamed(string animalName)
        {
            if (currentWeekLog != null) { currentWeekLog.animalsTamed++; currentWeekLog.milestones.Add("Tamed a " + animalName); }
        }

        public WeeklyChronicle GetCurrentChronicle() => currentChronicle;
        public WeeklyChronicle GetPreviousChronicle() => previousChronicle;
        public bool IsGeneratingChronicle => isGeneratingChronicle;
        public WeeklyEventLog GetCurrentWeekLog() => currentWeekLog;
        public List<string> GetHistoricalRunningJokes() => new List<string>(historicalRunningJokes);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastProcessedDay, "lastProcessedDay");
            Scribe_Values.Look(ref colonyName, "colonyName");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var idsList = trackedColonistIds.ToList();
                Scribe_Collections.Look(ref idsList, "trackedColonistIds", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var idsList = new List<int>();
                Scribe_Collections.Look(ref idsList, "trackedColonistIds", LookMode.Value);
                if (idsList != null) trackedColonistIds = new HashSet<int>(idsList);
            }

            Scribe_Collections.Look(ref wealthSnapshots, "wealthSnapshots", LookMode.Value);
            Scribe_Collections.Look(ref colonistCountSnapshots, "colonistCountSnapshots", LookMode.Value);
            Scribe_Collections.Look(ref historicalRunningJokes, "historicalRunningJokes", LookMode.Value);
        }
    }
}