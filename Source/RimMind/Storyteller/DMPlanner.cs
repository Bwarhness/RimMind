using System;
using System.Collections.Generic;
using System.Threading;
using RimMind.API;
using RimMind.Core;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// The DM Planner calls the AI to generate the next 3 story beats.
    /// All calls are async via ThreadPool.QueueUserWorkItem to avoid blocking the main thread.
    /// </summary>
    public class DMPlanner
    {
        private readonly NarrativeState state;
        private bool isPlanning = false;
        private int lastPlanTick = 0;

        public DMPlanner(NarrativeState state)
        {
            this.state = state;
        }

        public bool IsPlanning => isPlanning;

        /// <summary>
        /// Request a new story plan from the AI. Non-blocking.
        /// </summary>
        public void RequestPlan()
        {
            if (isPlanning)
            {
                Log.Message("[RimMind] DMPlanner: plan already in progress, skipping request.");
                return;
            }

            if (!RimMindMod.Settings.storytellerEnabled)
                return;

            if (string.IsNullOrEmpty(RimMindMod.Settings.ActiveModelId))
            {
                Log.Warning("[RimMind] DMPlanner: no model configured. Skipping plan request.");
                return;
            }

            isPlanning = true;
            lastPlanTick = Find.TickManager.TicksGame;

            var snapshot = ColonySnapshot.Capture(Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome));
            var theme = ThemeRegistry.Get(state.CurrentThemeId) ?? new ChronicleThemeProvider();

            var messages = BuildPlanningMessages(state, snapshot, theme);

            var request = new ChatRequest
            {
                model = RimMindMod.Settings.ActiveModelId,
                messages = messages,
                temperature = RimMindMod.Settings.temperature,
                max_tokens = RimMindMod.Settings.maxTokens
            };

            DebugLogger.LogSeparator("DM PLANNER REQUEST");
            DebugLogger.Log("STORYTELLER", $"Requesting plan with model {request.model}");

            Action<ChatResponse> onResponse = response =>
            {
                isPlanning = false;
                HandlePlanResponse(response, state, theme);
            };

            // Dispatch to the appropriate API client
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
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
                    Log.Warning("[RimMind] DMPlanner API dispatch failed: " + ex.Message);
                    MainThreadDispatcher.Enqueue(() => isPlanning = false);
                }
            });
        }

        private List<ChatMessage> BuildPlanningMessages(NarrativeState state, ColonySnapshot snapshot, IThemeProvider theme)
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(theme.SystemPrompt + "\n\n" + theme.PlannerPrompt)
            };

            // Build context
            var context = new System.Text.StringBuilder();
            if (state.Campaign != null && state.Campaign.IsLocked)
                context.AppendLine(state.Campaign.BuildPromptContext());
            context.AppendLine(state.Plot.BuildPromptContext());
            if (snapshot != null)
                context.AppendLine(snapshot.BuildPromptContext());

            messages.Add(ChatMessage.User(context.ToString()));
            messages.Add(ChatMessage.User("Plan the next 3 story beats. Respond ONLY with a JSON array of beat objects. Do not wrap in markdown code blocks."));

            return messages;
        }

        private void HandlePlanResponse(ChatResponse response, NarrativeState state, IThemeProvider theme)
        {
            if (!response.success)
            {
                Log.Warning("[RimMind] DMPlanner: plan generation failed: " + response.error);
                return;
            }

            string content = response.message?.content ?? "";
            if (string.IsNullOrWhiteSpace(content))
            {
                Log.Warning("[RimMind] DMPlanner: empty response from AI.");
                return;
            }

            DebugLogger.LogSeparator("DM PLANNER RESPONSE");
            DebugLogger.Log("STORYTELLER", Truncate(content, 2000));

            var beats = ParseBeatsFromJson(content);
            if (beats.Count == 0)
            {
                Log.Warning("[RimMind] DMPlanner: could not parse beats from response.");
                return;
            }

            state.TotalPlansGenerated++;

            int currentDay = 0;
            var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            if (map != null)
                currentDay = GenLocalDate.DayOfYear(map);

            foreach (var beat in beats)
            {
                beat.DayExecuted = currentDay; // Will be updated when actually fired

                // Add to plot graph
                state.Plot.AddBeat(beat);

                // Register any new threads
                foreach (var threadId in beat.OpensThreads)
                {
                    if (!state.Plot.ActiveThreads.Any(t => t.Id == threadId))
                    {
                        state.Plot.ActiveThreads.Add(new StoryThread(
                            threadId,
                            theme.NameThread(threadId),
                            "Auto-generated thread from beat: " + beat.WhatHappened,
                            currentDay
                        ));
                    }
                }

                // Register any new seeds
                foreach (var seedId in beat.PlantsSeeds)
                {
                    if (!state.Plot.UnresolvedSeeds.Any(s => s.Id == seedId))
                    {
                        state.Plot.PlantSeed(new NarrativeSeed(
                            seedId,
                            "Seed planted by beat: " + beat.WhatHappened,
                            beat.IncidentDefName,
                            currentDay
                        ));
                    }
                }

                // Update tension based on consequence tag
                ApplyConsequenceTension(beat.ConsequenceTag, state.Plot);
            }

            // Now create planned events from the beats and enqueue them
            var plannedEvents = new List<PlannedEvent>();
            for (int i = 0; i < beats.Count; i++)
            {
                var beat = beats[i];
                if (string.IsNullOrEmpty(beat.IncidentDefName))
                    continue;

                var evt = new PlannedEvent
                {
                    Id = "planned_" + Find.TickManager.TicksGame + "_" + i,
                    BeatId = beat.Id,
                    IncidentDefName = beat.IncidentDefName,
                    NarrativeLabel = beat.WhatHappened,
                    NarrativeText = beat.NarrativeSignificance,
                    NarrativeWeight = 1f,
                    PlannedDay = currentDay + i + 1, // Space them out
                    TargetTag = "Map_PlayerHome"
                };
                plannedEvents.Add(evt);
            }

            var engine = NarrativeEngine.Instance;
            if (engine != null && plannedEvents.Count > 0)
            {
                engine.EnqueueEvents(plannedEvents);
                Log.Message($"[RimMind] DMPlanner: enqueued {plannedEvents.Count} planned events.");
            }

            state.LastPlanDay = currentDay;
        }

        private List<PlotBeat> ParseBeatsFromJson(string content)
        {
            var beats = new List<PlotBeat>();
            try
            {
                // Strip markdown code blocks if present
                if (content.Contains("```"))
                {
                    int start = content.IndexOf("[");
                    int end = content.LastIndexOf("]");
                    if (start >= 0 && end > start)
                        content = content.Substring(start, end - start + 1);
                }

                var root = JSONNode.Parse(content);
                if (root == null || !root.IsArray)
                {
                    // Maybe it's wrapped in an object with a "beats" key
                    var beatsNode = root?["beats"];
                    if (beatsNode != null && beatsNode.IsArray)
                        root = beatsNode;
                    else
                        return beats;
                }

                var array = root.AsArray;
                for (int i = 0; i < array.Count; i++)
                {
                    var node = array[i];
                    var beat = new PlotBeat
                    {
                        Id = "beat_" + Find.TickManager.TicksGame + "_" + i,
                        WhatHappened = node["whatHappened"]?.Value ?? node["what_happened"]?.Value ?? "Unknown event",
                        NarrativeSignificance = node["narrativeSignificance"]?.Value ?? node["narrative_significance"]?.Value ?? "",
                        ConsequenceTag = node["consequenceTag"]?.Value ?? node["consequence_tag"]?.Value ?? "neutral",
                        IncidentDefName = node["suggestedIncidentDefName"]?.Value ?? node["suggested_incident_def_name"]?.Value ?? "",
                        OpensThreads = ParseStringArray(node["opensThreads"] ?? node["opens_threads"]),
                        ClosesThreads = ParseStringArray(node["closesThreads"] ?? node["closes_threads"]),
                        PlantsSeeds = ParseStringArray(node["plantsSeeds"] ?? node["plants_seeds"])
                    };

                    // Parse suggested points if present
                    var pointsNode = node["suggestedPoints"] ?? node["suggested_points"];
                    if (pointsNode != null)
                    {
                        beat.ActualSeverity = pointsNode.AsFloat;
                    }

                    beats.Add(beat);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] DMPlanner: failed to parse beats JSON: " + ex.Message);
            }
            return beats;
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

        private void ApplyConsequenceTension(string tag, PlotGraph plot)
        {
            switch (tag?.ToLower() ?? "")
            {
                case "escalation":
                case "threat":
                case "loss":
                    plot.UpdateTension(0.15f, -0.05f, 0.05f);
                    break;
                case "revelation":
                case "discovery":
                    plot.UpdateTension(0.05f, 0f, 0.2f);
                    break;
                case "hope":
                case "relief":
                case "victory":
                    plot.UpdateTension(-0.1f, 0.15f, -0.05f);
                    break;
                case "tragedy":
                case "doom":
                    plot.UpdateTension(0.2f, -0.15f, 0.1f);
                    break;
                default:
                    plot.UpdateTension(0.02f, 0f, 0f);
                    break;
            }
        }

        private string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "...";
        }
    }
}
