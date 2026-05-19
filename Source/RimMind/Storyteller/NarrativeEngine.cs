using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// The NarrativeEngine is the central GameComponent that orchestrates the AI storyteller loop:
    /// Plan -> Frame -> Execute -> Log Outcome -> Update Plot -> Replan.
    /// </summary>
    public class NarrativeEngine : GameComponent
    {
        private NarrativeState state;
        private DMPlanner planner;
        private EventQueue eventQueue;

        // Timing
        private int ticksSinceLastPlan = 0;
        private int ticksSinceLastOutcomeCheck = 0;
        private const int PLAN_INTERVAL_TICKS = 18000; // ~5 minutes at 60 ticks/sec real-time
        private const int OUTCOME_CHECK_INTERVAL = 6000; // ~1.6 minutes
        private const int INITIAL_DELAY_TICKS = 3600; // 1 minute grace period after game start

        // Tracking which events we've already processed outcomes for
        private HashSet<string> processedEventIds = new HashSet<string>();
        private const int MAX_PROCESSED_EVENTS = 500;

        // Public singleton access
        public static NarrativeEngine Instance => Current.Game?.GetComponent<NarrativeEngine>();
        public NarrativeState State => state;
        public EventQueue EventQueue => eventQueue;
        public DMPlanner Planner => planner;

        public NarrativeEngine(Game game) : base() { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            ThemeRegistry.Init();

            if (state == null)
            {
                state = new NarrativeState();
            }
            if (eventQueue == null)
            {
                eventQueue = new EventQueue();
            }
            if (planner == null)
            {
                planner = new DMPlanner(state);
            }

            // Pull the campaign frame from the active scenario's RimMind ScenPart, if any.
            // Runs once on new game; loaded saves keep the persisted state.Campaign.
            if (!state.IsInitialized)
            {
                var scen = Find.Scenario;
                if (scen != null)
                {
                    foreach (var part in scen.AllParts)
                    {
                        var rmPart = part as Scenarios.ScenPart_RimMindCampaign;
                        if (rmPart?.plan?.Campaign == null) continue;
                        state.Campaign = rmPart.plan.Campaign;
                        state.Campaign.IsLocked = true;
                        state.Campaign.DayLocked = 0;
                        state.IsInitialized = true;
                        Log.Message("[RimMind] Campaign frame consumed from RimMind scenario part.");
                        break;
                    }
                }
            }
            else if (state.Campaign != null && !state.Campaign.IsLocked)
            {
                // Save loaded mid-setup: opportunistically lock if the game is already underway.
                var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                if (map != null)
                {
                    int day = GenLocalDate.DayOfYear(map);
                    if (day > 0)
                        LockCampaignFrame(day);
                }
            }

            // Clear stale pending letter framing from previous sessions
            PendingLetterFraming.Clear();

            Log.Message("[RimMind] NarrativeEngine initialized.");
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!RimMindMod.Settings.storytellerEnabled) return;
            if (state == null || planner == null || eventQueue == null) return;
            if (Find.TickManager.TicksGame < INITIAL_DELAY_TICKS) return;

            // Only advance planning counter when game is not paused
            if (!Find.TickManager.Paused)
            {
                ticksSinceLastPlan++;
                ticksSinceLastOutcomeCheck++;
            }

            // Request new plan periodically
            if (ticksSinceLastPlan >= PLAN_INTERVAL_TICKS)
            {
                ticksSinceLastPlan = 0;
                TryRequestPlan();
            }

            // Check for event outcomes periodically
            if (ticksSinceLastOutcomeCheck >= OUTCOME_CHECK_INTERVAL)
            {
                ticksSinceLastOutcomeCheck = 0;
                PendingLetterFraming.CleanupOldEntries();
                CheckEventOutcomes();
            }
        }

        public void LockCampaignFrame(int day)
        {
            if (state?.Campaign == null) return;
            state.Campaign.IsLocked = true;
            state.Campaign.DayLocked = day;
            Log.Message($"[RimMind] Campaign frame locked on day {day}.");
        }

        public void EnqueueEvents(List<PlannedEvent> events)
        {
            eventQueue?.EnqueueRange(events);
        }

        /// <summary>
        /// Log the outcome of an executed event and update the plot graph.
        /// </summary>
        public void LogEventOutcome(PlannedEvent evt, string outcomeDescription, float outcomeSeverity)
        {
            if (state == null || evt == null) return;
            if (processedEventIds.Contains(evt.Id)) return;
            processedEventIds.Add(evt.Id);

            // Find the corresponding beat
            var beat = state.Plot.Beats.FirstOrDefault(b => b.Id == evt.BeatId);
            if (beat != null)
            {
                beat.IncidentOutcome = outcomeDescription;
                beat.ActualSeverity = outcomeSeverity;

                // Update tension based on outcome
                if (outcomeSeverity > 0.7f)
                    state.Plot.UpdateTension(0.1f, -0.05f, 0f);
                else if (outcomeSeverity < 0.3f)
                    state.Plot.UpdateTension(-0.05f, 0.1f, 0f);

                // Close threads if the beat had closure tags
                foreach (var threadId in beat.ClosesThreads)
                {
                    var thread = state.Plot.ActiveThreads.FirstOrDefault(t => t.Id == threadId);
                    if (thread != null && thread.Status == ThreadStatus.Open)
                    {
                        thread.Status = ThreadStatus.Closed;
                        var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        thread.DayClosed = map != null ? GenLocalDate.DayOfYear(map) : 0;
                    }
                }

                // Open threads from outcome
                if (outcomeSeverity > 0.5f && beat.OpensThreads.Count == 0)
                {
                    // Auto-generate a consequence thread for major events
                    var newThreadId = "consequence_" + evt.Id;
                    if (!state.Plot.ActiveThreads.Any(t => t.Id == newThreadId))
                    {
                        var theme = ThemeRegistry.Get(state.CurrentThemeId) ?? new ChronicleThemeProvider();
                        var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        int day = map != null ? GenLocalDate.DayOfYear(map) : 0;
                        state.Plot.ActiveThreads.Add(new StoryThread(
                            newThreadId,
                            theme.NameThread("consequence"),
                            "Consequence of: " + beat.WhatHappened,
                            day,
                            outcomeSeverity
                        ));
                        beat.OpensThreads.Add(newThreadId);
                    }
                }
            }

            evt.WasFired = true;
            state.EventHistory.Add(evt);
            state.TotalBeatsExecuted++;

            DebugLogger.Log("STORYTELLER", $"Logged outcome for {evt.IncidentDefName}: {outcomeDescription}");
        }

        private void TryRequestPlan()
        {
            if (planner == null) return;
            if (!state.Campaign.IsLocked)
            {
                // Don't plan until campaign frame is locked
                return;
            }
            if (planner.IsPlanning)
            {
                // Already planning
                return;
            }

            planner.RequestPlan();
        }

        private void CheckEventOutcomes()
        {
            // Check recently fired events to see if we can determine outcomes
            // This is a lightweight periodic check — heavy outcome analysis happens via Chronicle

            // Trim processedEventIds to prevent unbounded growth
            if (processedEventIds.Count > MAX_PROCESSED_EVENTS)
            {
                processedEventIds.Clear();
                var recent = state.EventHistory.Skip(Math.Max(0, state.EventHistory.Count - MAX_PROCESSED_EVENTS));
                foreach (var evt in recent)
                    processedEventIds.Add(evt.Id);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref state, "narrativeState");
            Scribe_Deep.Look(ref eventQueue, "eventQueue");
            Scribe_Collections.Look(ref processedEventIds, "processedEventIds", LookMode.Value);
            Scribe_Values.Look(ref ticksSinceLastPlan, "ticksSinceLastPlan", 0);
            Scribe_Values.Look(ref ticksSinceLastOutcomeCheck, "ticksSinceLastOutcomeCheck", 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (state == null) state = new NarrativeState();
                if (eventQueue == null) eventQueue = new EventQueue();
                if (processedEventIds == null) processedEventIds = new HashSet<string>();
                if (planner == null) planner = new DMPlanner(state);
            }
        }
    }
}
