using System;
using System.Collections.Generic;
using System.Linq;
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

        // Campaign setup prompt tracking
        private bool hasPromptedForCampaign = false;

        // Public singleton access
        public static NarrativeEngine Instance => Current.Game?.GetComponent<NarrativeEngine>();
        public NarrativeState State => state;
        public EventQueue EventQueue => eventQueue;
        public DMPlanner Planner => planner;

        public NarrativeEngine(Game game) : base(game) { }

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

            // Lock campaign frame if game is already underway and not yet locked
            if (state.Campaign != null && !state.Campaign.IsLocked)
            {
                var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                if (map != null)
                {
                    int day = GenLocalDate.DayOfYear(map);
                    if (day > 0)
                    {
                        LockCampaignFrame(day);
                    }
                }
            }

            Log.Message("[RimMind] NarrativeEngine initialized.");
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!RimMindMod.Settings.storytellerEnabled) return;
            if (state == null || planner == null || eventQueue == null) return;
            if (Find.TickManager.TicksGame < INITIAL_DELAY_TICKS) return;

            // Check if we should prompt for campaign setup
            if (!hasPromptedForCampaign && !state.Campaign.IsLocked)
            {
                if (IsRimMindStorytellerActive())
                {
                    hasPromptedForCampaign = true;
                    Find.WindowStack.Add(new CampaignSetupWindow());
                }
            }

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
                CheckEventOutcomes();
            }
        }

        /// <summary>
        /// Generate or regenerate the campaign frame from a user prompt. Async.
        /// </summary>
        public void GenerateCampaignFrame(string userPrompt, Action<CampaignFrame> callback)
        {
            if (!RimMindMod.Settings.storytellerEnabled)
            {
                MainThreadDispatcher.Enqueue(() => callback?.Invoke(null));
                return;
            }

            var theme = ThemeRegistry.Get(state?.CurrentThemeId ?? "chronicle") ?? new ChronicleThemeProvider();

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(theme.CampaignPrompt),
                ChatMessage.User($"Design a campaign frame for this prompt: \"{userPrompt}\"\n\nRespond in JSON with fields: setting, incitingIncident, activeForces (array), currentAct, pendingThreat, opportunity, plantedSeeds (array of objects with id, description, suggestedIncidentDefName).")
            };

            var request = new ChatRequest
            {
                model = RimMindMod.Settings.ActiveModelId,
                messages = messages,
                temperature = 0.9f,
                max_tokens = 2048
            };

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Action<ChatResponse> onResponse = response =>
                    {
                        if (!response.success)
                        {
                            Log.Warning("[RimMind] Campaign frame generation failed: " + response.error);
                            callback?.Invoke(null);
                            return;
                        }

                        var frame = ParseCampaignFrame(response.message?.content ?? "", userPrompt);
                        callback?.Invoke(frame);
                    };

                    if (RimMindMod.Settings.IsClaudeCode)
                        ClaudeCodeClient.SendAsync(request, r => MainThreadDispatcher.Enqueue(() => onResponse(r)));
                    else if (RimMindMod.Settings.IsAnthropic)
                        AnthropicClient.SendAsync(request, r => MainThreadDispatcher.Enqueue(() => onResponse(r)));
                    else if (RimMindMod.Settings.IsCustom)
                        CustomProviderClient.SendAsync(request, r => MainThreadDispatcher.Enqueue(() => onResponse(r)));
                    else
                        OpenRouterClient.SendAsync(request, r => MainThreadDispatcher.Enqueue(() => onResponse(r)));
                }
                catch (Exception ex)
                {
                    Log.Warning("[RimMind] Campaign generation dispatch failed: " + ex.Message);
                    MainThreadDispatcher.Enqueue(() => callback?.Invoke(null));
                }
            });
        }

        public void SetCampaignFrame(CampaignFrame frame)
        {
            if (state == null) return;
            state.Campaign = frame ?? new CampaignFrame();
            state.IsInitialized = frame != null;
            Log.Message("[RimMind] Campaign frame set.");
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
        }

        private CampaignFrame ParseCampaignFrame(string json, string userPrompt)
        {
            try
            {
                // Strip markdown if present
                if (json.Contains("```"))
                {
                    int start = json.IndexOf("{");
                    int end = json.LastIndexOf("}");
                    if (start >= 0 && end > start)
                        json = json.Substring(start, end - start + 1);
                }

                var root = JSONNode.Parse(json);
                if (root == null) return null;

                var frame = new CampaignFrame
                {
                    UserPrompt = userPrompt,
                    Setting = root["setting"]?.Value ?? "An untamed rim world",
                    IncitingIncident = root["incitingIncident"]?.Value ?? root["inciting_incident"]?.Value ?? "Crash landing",
                    CurrentAct = root["currentAct"]?.Value ?? root["current_act"]?.Value ?? "Act I",
                    PendingThreat = root["pendingThreat"]?.Value ?? root["pending_threat"]?.Value ?? "Unknown dangers",
                    Opportunity = root["opportunity"]?.Value ?? "Survival and hope",
                    ActiveForces = ParseStringArray(root["activeForces"] ?? root["active_forces"]),
                    PlantedSeeds = ParseSeedsArray(root["plantedSeeds"] ?? root["planted_seeds"])
                };

                return frame;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Failed to parse campaign frame: " + ex.Message);
                return null;
            }
        }

        private List<string> ParseStringArray(JSONNode node)
        {
            var list = new List<string>();
            if (node == null || !node.IsArray) return list;
            foreach (JSONNode n in node.AsArray)
            {
                if (n != null && !n.IsNull)
                    list.Add(n.Value);
            }
            return list;
        }

        private List<NarrativeSeed> ParseSeedsArray(JSONNode node)
        {
            var list = new List<NarrativeSeed>();
            if (node == null || !node.IsArray) return list;
            int idx = 0;
            foreach (JSONNode n in node.AsArray)
            {
                if (n == null || n.IsNull) continue;
                list.Add(new NarrativeSeed(
                    n["id"]?.Value ?? $"seed_{idx}",
                    n["description"]?.Value ?? "A mystery yet to unfold",
                    n["suggestedIncidentDefName"]?.Value ?? n["suggested_incident_def_name"]?.Value ?? "",
                    0
                ));
                idx++;
            }
            return list;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref state, "narrativeState");
            Scribe_Deep.Look(ref eventQueue, "eventQueue");
            Scribe_Collections.Look(ref processedEventIds, "processedEventIds", LookMode.Value);
            Scribe_Values.Look(ref ticksSinceLastPlan, "ticksSinceLastPlan", 0);
            Scribe_Values.Look(ref ticksSinceLastOutcomeCheck, "ticksSinceLastOutcomeCheck", 0);
            Scribe_Values.Look(ref hasPromptedForCampaign, "hasPromptedForCampaign", false);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (state == null) state = new NarrativeState();
                if (eventQueue == null) eventQueue = new EventQueue();
                if (processedEventIds == null) processedEventIds = new HashSet<string>();
                if (planner == null) planner = new DMPlanner(state);
            }
        }

        private bool IsRimMindStorytellerActive()
        {
            try
            {
                var storyteller = Find.Storyteller;
                if (storyteller == null || storyteller.storytellerDef == null) return false;
                return storyteller.storytellerDef.defName == "RimMind_AIStoryteller";
            }
            catch
            {
                return false;
            }
        }
    }
}
