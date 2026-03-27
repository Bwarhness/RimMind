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
    /// Phase 2 adds real event detection: deaths, raids, milestones.
    /// </summary>
    public class ChronicleTracker : GameComponent
    {
        private const int TICKS_PER_DAY = 60000;
        private const int DAYS_PER_WEEK = 7;
        private const int MILESTONE_CHECK_INTERVAL = 6000; // Check milestones every ~1 in-game day

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

        // Phase 2: Milestone tracking (cumulative across colony lifetime)
        private bool hasRecordedFirstDeath = false;
        private bool hasSurvivedFirstRaid = false;
        private bool hasFirstMechanoidKill = false;
        private bool hasFirstBanishment = false;
        private int peakColonistCount = 0;
        private HashSet<string> skilledColonists = new HashSet<string>(); // Track colonists with skill 20

        // Phase 2: Weekly tracking
        private int lastMilestoneCheckDay = 0;
        private HashSet<string> weekMechanoidKills = new HashSet<string>();
        private bool weekBanishmentRecorded = false;

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
                lastMilestoneCheckDay = currentDay;

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

                // Phase 2: Initialize colonist count tracking
                peakColonistCount = map.mapPawns.FreeColonists.Count;
                currentWeekLog.colonistCountStart = peakColonistCount;

                // Check for colony size milestones on init
                CheckColonySizeMilestones(peakColonistCount);
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
                lastMilestoneCheckDay = currentDay;
            }

            // Take periodic snapshots for the day
            if (Find.TickManager.TicksGame % (TICKS_PER_DAY / 4) == 0) // Every 6 hours
            {
                TakePeriodicSnapshot(map);
            }

            // Phase 2: Check milestones periodically
            if (Find.TickManager.TicksGame % MILESTONE_CHECK_INTERVAL == 0 && currentDay != lastMilestoneCheckDay)
            {
                CheckMilestones(map);
                lastMilestoneCheckDay = currentDay;
            }
        }

        /// <summary>
        /// Called from ChronicleEventPatches when a raid letter is received.
        /// </summary>
        public static void OnRaidLetter(Letter letter)
        {
            ChronicleEventPatches.OnRaidLetterReceived(letter);
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
            int currentDay = GenLocalDate.DayOfYear(map);

            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (!trackedColonistIds.Contains(colonist.thingIDNumber))
                {
                    trackedColonistIds.Add(colonist.thingIDNumber);

                    // Phase 2: Use proper join record
                    var join = new ColonistJoin(colonist.Name.ToStringShort, "recruit", currentDay);
                    RecordColonistJoin(join);

                    currentWeekLog?.milestones.Add($"{colonist.Name.ToStringShort} joined the colony!");
                    currentWeekLog?.events.Add(new ColonyEvent(
                        "recruitment",
                        $"{colonist.Name.ToStringShort} has joined the colony!",
                        colonist.Name.ToStringShort,
                        currentDay
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

            // Phase 2: Track mood extremes (best and worst mood)
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (colonist.needs?.mood == null) continue;

                float mood = colonist.needs.mood.CurLevel;
                string name = colonist.Name?.ToStringShort ?? "Unknown";

                // Track best mood (happiest colonist)
                if (mood > currentWeekLog.bestMoodValue)
                {
                    currentWeekLog.bestMoodColonist = name;
                    currentWeekLog.bestMoodValue = mood;
                }

                // Track worst mood (most troubled colonist)
                if (mood < currentWeekLog.worstMoodValue)
                {
                    currentWeekLog.worstMoodColonist = name;
                    currentWeekLog.worstMoodValue = mood;
                }
            }

            // Also update from MoodHistoryTracker if available
            if (MoodHistoryTracker.Instance != null)
            {
                var recentHistory = MoodHistoryTracker.Instance.GetAllRecentHistory(1);
                if (recentHistory.Count > 0)
                {
                    var worstSnapshot = recentHistory.OrderBy(s => s.moodLevel).FirstOrDefault();
                    var bestSnapshot = recentHistory.OrderByDescending(s => s.moodLevel).FirstOrDefault();

                    if (worstSnapshot != null && worstSnapshot.moodLevel < currentWeekLog.worstMoodValue)
                    {
                        currentWeekLog.worstMoodColonist = worstSnapshot.pawnId;
                        currentWeekLog.worstMoodValue = worstSnapshot.moodLevel;
                    }

                    if (bestSnapshot != null && bestSnapshot.moodLevel > currentWeekLog.bestMoodValue)
                    {
                        currentWeekLog.bestMoodColonist = bestSnapshot.pawnId;
                        currentWeekLog.bestMoodValue = bestSnapshot.moodLevel;
                    }
                }
            }
        }

        // ========================
        // PHASE 2: EVENT RECORDING
        // ========================

        /// <summary>
        /// Records a colonist death event.
        /// </summary>
        public void RecordColonistDeath(ColonistDeath death)
        {
            if (currentWeekLog == null) return;

            currentWeekLog.deathsList.Add(death);
            currentWeekLog.deaths++;
            currentWeekLog.colonistDeaths++;
            currentWeekLog.deathlessWeek = false;
            currentWeekLog.totalColonistDeaths++;

            // Check for first death milestone
            if (!hasRecordedFirstDeath)
            {
                hasRecordedFirstDeath = true;
                currentWeekLog.firstDeath = true;
                currentWeekLog.milestoneFlags.Add("FIRST DEATH: Colony experienced its first death");
                currentWeekLog.milestones.Add($"First Death: {death.name} has passed away");
            }

            // Add to events for LLM
            currentWeekLog.events.Add(new ColonyEvent(
                "death",
                $"{death.name} died from {death.cause}",
                death.name,
                death.day,
                $"Killed by: {death.killer}"
            ));
        }

        /// <summary>
        /// Records a raid event.
        /// </summary>
        public void RecordRaid(RaidEvent raid)
        {
            if (currentWeekLog == null) return;

            currentWeekLog.raidsList.Add(raid);
            currentWeekLog.raids++;
            currentWeekLog.raidsThisWeek++;

            // Check for raid marathon (3+ raids in one week)
            if (currentWeekLog.raidsThisWeek >= 3 && !currentWeekLog.raidMarathon)
            {
                currentWeekLog.raidMarathon = true;
                currentWeekLog.milestoneFlags.Add("RAID MARATHON: Survived 3+ raids in one week!");
                currentWeekLog.milestones.Add("Raid Marathon: Survived 3+ raids in a single week!");
            }

            // Add event
            currentWeekLog.events.Add(new ColonyEvent(
                "raid",
                $"Raid by {raid.enemyFaction} - {(raid.survived ? "SURVIVED" : "DEFEAT")}",
                null,
                raid.day,
                $"Enemies: {raid.enemyCount}, Outcome: {(raid.survived ? "Survived" : "Lost")}"
            ));
        }

        /// <summary>
        /// Records a mechanoid kill.
        /// </summary>
        public void RecordMechanoidKill(string colonistName)
        {
            if (currentWeekLog == null) return;

            if (!weekMechanoidKills.Contains(colonistName))
            {
                weekMechanoidKills.Add(colonistName);
            }

            if (!hasFirstMechanoidKill)
            {
                hasFirstMechanoidKill = true;
                currentWeekLog.firstMechanoidKill = true;
                currentWeekLog.milestoneFlags.Add("FIRST MECHANOID KILL: First mechanoid destroyed!");
                currentWeekLog.milestones.Add($"First Mechanoid Kill: {colonistName} destroyed a mechanoid!");
            }
        }

        /// <summary>
        /// Records a colonist banishment.
        /// </summary>
        public void RecordBanishment(string colonistName)
        {
            if (currentWeekLog == null) return;

            if (!weekBanishmentRecorded)
            {
                weekBanishmentRecorded = true;

                if (!hasFirstBanishment)
                {
                    hasFirstBanishment = true;
                    currentWeekLog.firstBanishment = true;
                    currentWeekLog.milestoneFlags.Add("FIRST BANISHMENT: First colonist banished from the colony!");
                    currentWeekLog.milestones.Add($"First Banishment: {colonistName} was banished from the colony");
                }
            }
        }

        /// <summary>
        /// Records a colonist joining the colony.
        /// </summary>
        public void RecordColonistJoin(ColonistJoin join)
        {
            if (currentWeekLog == null) return;

            currentWeekLog.joins.Add(join);

            // Check for skill milestone (passion level 20 in any skill)
            CheckSkillMilestones(join.name);

            // Check colony size milestones
            var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
            if (map != null)
            {
                int count = map.mapPawns.FreeColonists.Count;
                if (count > peakColonistCount)
                {
                    peakColonistCount = count;
                }
                CheckColonySizeMilestones(count);
            }
        }

        // ========================
        // PHASE 2: MILESTONE CHECKS
        // ========================

        private void CheckMilestones(Map map)
        {
            if (currentWeekLog == null) return;

            int currentDay = GenLocalDate.DayOfYear(map);

            // Check for day milestones (100, 200, etc.)
            CheckDayMilestones(currentDay);

            // Check skill milestones for all colonists
            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                CheckSkillMilestones(colonist.Name?.ToStringShort);
            }

            // Check raid survival milestone
            if (currentWeekLog.raidsThisWeek > 0 && !hasSurvivedFirstRaid)
            {
                // Check if any raid was survived
                bool anySurvived = currentWeekLog.raidsList.Any(r => r.survived);
                if (anySurvived)
                {
                    hasSurvivedFirstRaid = true;
                    currentWeekLog.survivedFirstRaid = true;
                    currentWeekLog.milestoneFlags.Add("FIRST RAID SURVIVED: The colony survived its first raid!");
                    currentWeekLog.milestones.Add("First Raid Survived: The colony has proven itself in battle!");
                }
            }
        }

        private void CheckDayMilestones(int currentDay)
        {
            if (currentWeekLog == null) return;

            // 100-day milestone
            if (currentDay >= 100 && currentDay % 100 < 7)
            {
                int milestone = (currentDay / 100) * 100;
                string key = $"day_{milestone}";
                if (!currentWeekLog.milestoneFlags.Any(m => m.Contains(key)))
                {
                    currentWeekLog.milestoneFlags.Add($"DAY {milestone}: Colony reached {milestone} days!");
                    currentWeekLog.milestones.Add($"Day {milestone} Milestone: The colony has endured for {milestone} days!");
                }
            }
        }

        private void CheckColonySizeMilestones(int count)
        {
            if (currentWeekLog == null) return;

            if (count >= 5 && !currentWeekLog.hasReached5Colonists)
            {
                currentWeekLog.hasReached5Colonists = true;
                currentWeekLog.milestoneFlags.Add("COLONY OF 5: Reached 5 colonists!");
                currentWeekLog.milestones.Add("Colony Milestone: The colony has grown to 5 colonists!");
            }

            if (count >= 10 && !currentWeekLog.hasReached10Colonists)
            {
                currentWeekLog.hasReached10Colonists = true;
                currentWeekLog.milestoneFlags.Add("COLONY OF 10: Reached 10 colonists!");
                currentWeekLog.milestones.Add("Colony Milestone: The colony has grown to 10 colonists!");
            }

            if (count >= 20 && !currentWeekLog.hasReached20Colonists)
            {
                currentWeekLog.hasReached20Colonists = true;
                currentWeekLog.milestoneFlags.Add("COLONY OF 20: Reached 20 colonists!");
                currentWeekLog.milestones.Add("Colony Milestone: The colony has grown to 20 colonists!");
            }
        }

        private void CheckSkillMilestones(string colonistName)
        {
            if (currentWeekLog == null || string.IsNullOrEmpty(colonistName)) return;

            string key = $"skill20_{colonistName}";
            if (skilledColonists.Contains(key)) return;

            var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
            if (map == null) return;

            foreach (var colonist in map.mapPawns.FreeColonists)
            {
                if (colonist.Name?.ToStringShort != colonistName) continue;
                if (colonist.skills == null) continue;

                foreach (var skill in colonist.skills.skills)
                {
                    // Skill level 20 = true mastery
                    if (skill.Level >= 20)
                    {
                        skilledColonists.Add(key);
                        currentWeekLog.milestoneFlags.Add($"SKILL MASTER: {colonistName} reached level 20 in {skill.def.label}!");
                        currentWeekLog.milestones.Add($"Skill Master: {colonistName} has achieved level 20 in {skill.def.LabelCap}!");
                        break; // Only record once per colonist
                    }
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

            // Phase 2: Finalize colonist counts
            currentWeekLog.colonistCountEnd = map?.mapPawns?.FreeColonists?.Count ?? 0;

            // Phase 2: Finalize achievement flags
            FinalizeWeekAchievements();

            // Create a log copy for generation (since currentWeekLog will be reset)
            var logForGeneration = currentWeekLog;

            // Send notification
            SendChronicleNotification(logForGeneration);

            // Generate the Chronicle via LLM
            GenerateChronicleAsync(logForGeneration);

            // Reset week-specific tracking for next week
            ResetWeekTracking();
        }

        private void FinalizeWeekAchievements()
        {
            if (currentWeekLog == null) return;

            // Check for deathless week
            if (currentWeekLog.deathlessWeek && currentWeekLog.raidsThisWeek > 0)
            {
                currentWeekLog.milestoneFlags.Add("DEATHLESS WEEK: No colonists died this week!");
                currentWeekLog.milestones.Add("Deathless Week: The colony survived without losing anyone!");
            }
        }

        private void ResetWeekTracking()
        {
            // Reset week-specific tracking
            weekMechanoidKills.Clear();
            weekBanishmentRecorded = false;
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

            // Phase 2: Populate extended fields
            chronicle.colonistCountStart = log.colonistCountStart;
            chronicle.colonistCountEnd = log.colonistCountEnd;
            chronicle.raidsThisWeek = log.raidsThisWeek;
            chronicle.raidMarathon = log.raidMarathon;
            chronicle.deathlessWeek = log.deathlessWeek;
            chronicle.firstDeath = log.firstDeath;
            chronicle.survivedFirstRaid = log.survivedFirstRaid;
            chronicle.colonistDeaths = log.colonistDeaths;
            chronicle.deaths = new List<ColonistDeath>(log.deathsList);
            chronicle.milestoneFlags = new List<string>(log.milestoneFlags);
            chronicle.raids = new List<RaidEvent>(log.raidsList);
            chronicle.bestMoodColonist = log.bestMoodColonist;
            chronicle.bestMoodValue = log.bestMoodValue;
            chronicle.worstMoodColonist = log.worstMoodColonist;
            chronicle.worstMoodValue = log.worstMoodValue;

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
                // Phase 2: Richer battle report
                string battleReport;
                if (log.raidsList.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var raid in log.raidsList)
                    {
                        string outcome = raid.survived ? "survived" : "was defeated by";
                        sb.AppendLine($"Day {raid.day}: {raid.enemyFaction} attacked ({raid.enemyCount} enemies) - colony {outcome}");
                        sb.AppendLine($"  Our colonists: {raid.colonistsInvolved} | Enemies slain: {raid.enemiesKilled} | Colonists lost: {raid.colonistsKilled}");
                    }
                    if (log.raidMarathon)
                        sb.AppendLine("\n⚠️ RAID MARATHON: 3+ raids in one week!");
                    battleReport = sb.ToString();
                }
                else
                {
                    battleReport = "A quiet week on the battlefield.";
                }
                chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "⚔️", battleReport));

                // Phase 2: Richer obituaries
                string obituaries;
                if (log.deathsList.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var death in log.deathsList)
                    {
                        sb.AppendLine($"Day {death.day}: {death.name} died from {death.cause}");
                        if (!string.IsNullOrEmpty(death.killer) && death.killer != "unknown")
                            sb.AppendLine($"  Claimed by: {death.killer}");
                        if (!string.IsNullOrEmpty(death.lastWords))
                            sb.AppendLine($"  Last words: \"{death.lastWords}\"");
                    }
                    obituaries = sb.ToString();
                }
                else if (log.deathlessWeek)
                {
                    obituaries = "No deaths this week. A blessing from the gods above.\n🙏 Deathless Week achieved!";
                }
                else
                {
                    obituaries = "No deaths this week.";
                }
                chronicle.sections.Add(new ChronicleSection("OBITUARIES", "😢", obituaries));

                chronicle.sections.Add(new ChronicleSection("ECONOMY", "📦",
                    $"Colony wealth: {log.highestWealth:N0} silver. {log.trades} trade(s) conducted."));

                // Phase 2: Richer milestones with achievement flags
                var allMilestones = new List<string>(log.milestones);
                allMilestones.AddRange(log.milestoneFlags);
                string milestoneSection = allMilestones.Count > 0 ? string.Join("\n", allMilestones) : "No major milestones.";

                // Add mood extremes to milestones
                if (!string.IsNullOrEmpty(log.bestMoodColonist))
                    milestoneSection += $"\n\nHappiest colonist: {log.bestMoodColonist} ({log.bestMoodValue:P0} mood)";
                if (!string.IsNullOrEmpty(log.worstMoodColonist))
                    milestoneSection += $"\nMost troubled: {log.worstMoodColonist} ({log.worstMoodValue:P0} mood)";

                chronicle.sections.Add(new ChronicleSection("MILESTONES", "🏆", milestoneSection));

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

            // Phase 2: Populate extended fields
            chronicle.colonistCountStart = log.colonistCountStart;
            chronicle.colonistCountEnd = log.colonistCountEnd;
            chronicle.raidsThisWeek = log.raidsThisWeek;
            chronicle.raidMarathon = log.raidMarathon;
            chronicle.deathlessWeek = log.deathlessWeek;
            chronicle.firstDeath = log.firstDeath;
            chronicle.survivedFirstRaid = log.survivedFirstRaid;
            chronicle.colonistDeaths = log.colonistDeaths;
            chronicle.deaths = new List<ColonistDeath>(log.deathsList);
            chronicle.milestoneFlags = new List<string>(log.milestoneFlags);
            chronicle.raids = new List<RaidEvent>(log.raidsList);
            chronicle.bestMoodColonist = log.bestMoodColonist;
            chronicle.bestMoodValue = log.bestMoodValue;
            chronicle.worstMoodColonist = log.worstMoodColonist;
            chronicle.worstMoodValue = log.worstMoodValue;

            chronicle.topHeadline = $"Week {log.weekNumber}: The Colony Endures";
            chronicle.leadParagraph = $"As Day {log.endDay} closes, the colonists of this settlement reflect on a week of challenges and small victories. The {log.season} season brings its own trials.";

            // Phase 2: Richer battle report
            string battleReport;
            if (log.raidsList.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var raid in log.raidsList)
                {
                    string outcome = raid.survived ? "survived" : "was defeated by";
                    sb.AppendLine($"Day {raid.day}: {raid.enemyFaction} attacked ({raid.enemyCount} enemies) - colony {outcome}");
                }
                if (log.raidMarathon)
                    sb.AppendLine("\n⚠️ RAID MARATHON: 3+ raids in one week!");
                battleReport = sb.ToString();
            }
            else
            {
                battleReport = "The enemy held back this week. Enjoy the peace while it lasts.";
            }
            chronicle.sections.Add(new ChronicleSection("BATTLE REPORT", "⚔️", battleReport));

            // Phase 2: Richer obituaries
            string obituaries;
            if (log.deathsList.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var death in log.deathsList)
                {
                    sb.AppendLine($"Day {death.day}: {death.name} died from {death.cause}");
                    if (!string.IsNullOrEmpty(death.killer) && death.killer != "unknown")
                        sb.AppendLine($"  Claimed by: {death.killer}");
                }
                obituaries = sb.ToString();
            }
            else if (log.deathlessWeek)
            {
                obituaries = "No deaths this week. A blessing from the gods above.\n🙏 Deathless Week achieved!";
            }
            else
            {
                obituaries = "No deaths recorded. The Reaper takes a holiday.";
            }
            chronicle.sections.Add(new ChronicleSection("OBITUARIES", "😢", obituaries));

            chronicle.sections.Add(new ChronicleSection("ECONOMY", "📦",
                $"Wealth peaked at {log.highestWealth:N0} silver. {log.trades} caravan(s) visited our trade depots."));

            // Phase 2: Richer milestones
            var allMilestones = new List<string>(log.milestones);
            allMilestones.AddRange(log.milestoneFlags);
            string milestoneSection = allMilestones.Count > 0 ? string.Join("\n", allMilestones) : "The colony grows, one day at a time.";

            // Add mood extremes
            if (!string.IsNullOrEmpty(log.bestMoodColonist))
                milestoneSection += $"\n\nHappiest colonist: {log.bestMoodColonist} ({log.bestMoodValue:P0} mood)";
            if (!string.IsNullOrEmpty(log.worstMoodColonist))
                milestoneSection += $"\nMost troubled: {log.worstMoodColonist} ({log.worstMoodValue:P0} mood)";

            chronicle.sections.Add(new ChronicleSection("MILESTONES", "🏆", milestoneSection));

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
