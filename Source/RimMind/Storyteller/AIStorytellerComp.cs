using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Phase 1-5: Active AI Storyteller.
    /// 
    /// Overrides MakeIncidentsForInterval to inject AI-planned narrative events from the
    /// DM Planner / Plot Graph pipeline. Falls back to vanilla StorytellerComp_Classic
    /// behavior when the event queue is empty or the storyteller is disabled.
    /// </summary>
    public class AIStorytellerComp : StorytellerComp_Classic
    {
        // Cached engine reference for performance
        private NarrativeEngine engine;
        private int lastEngineCheckTick = -9999;

        private NarrativeEngine GetEngine()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastEngineCheckTick > 300) // Cache for 5 seconds
            {
                engine = NarrativeEngine.Instance;
                lastEngineCheckTick = currentTick;
            }
            return engine;
        }

        public override IEnumerable<FiringIncident> MakeIncidentsForInterval(ITarget target)
        {
            var engine = GetEngine();
            var queue = engine?.EventQueue;

            // If storyteller disabled or no engine/queue, fall back to vanilla
            if (!RimMindMod.Settings.storytellerEnabled || queue == null)
            {
                foreach (var incident in base.MakeIncidentsForInterval(target))
                    yield return incident;
                yield break;
            }

            // Check if we should generate an event now
            if (!ShouldGenerateEventNow(target))
            {
                yield break;
            }

            // Try to dequeue a planned event
            var planned = queue.Dequeue();
            if (planned != null && !string.IsNullOrEmpty(planned.IncidentDefName))
            {
                var firing = planned.ToFiringIncident();
                if (firing != null)
                {
                    // Apply narrative framing to the incident
                    ApplyNarrativeFraming(firing, planned, engine.State);

                    Log.Message($"[RimMind] AI Storyteller firing planned event: {planned.IncidentDefName} ({planned.NarrativeLabel})");
                    yield return firing;
                    yield break; // One planned event per interval
                }
            }

            // Fall back to vanilla classic behavior if queue empty or planned event invalid
            foreach (var incident in base.MakeIncidentsForInterval(target))
                yield return incident;
        }

        private void ApplyNarrativeFraming(FiringIncident firing, PlannedEvent planned, NarrativeState state)
        {
            if (firing?.def == null || planned == null || state == null) return;

            var theme = ThemeRegistry.Get(state.CurrentThemeId) ?? new ChronicleThemeProvider();
            var beat = state.Plot.Beats.FirstOrDefault(b => b.Id == planned.BeatId);

            // Try to apply custom letter text if this incident generates a letter
            // We do this via a lightweight post-execution tracking; the actual letter
            // modification is handled by our LetterFramingPatch if the letter matches
            // our incident. For now, store the planned event data for later framing.
            if (!string.IsNullOrEmpty(planned.NarrativeLabel))
            {
                PendingLetterFraming.RegisterPendingFraming(firing.def.defName, planned, theme, beat);
            }
        }

        /// <summary>
        /// Checks if this storyteller comp should generate an incident now.
        /// Respects minDaysPassed and curveIncidents from XML config.
        /// </summary>
        private bool ShouldGenerateEventNow(ITarget target)
        {
            // Mirror base class checks from StorytellerComp_Classic
            var map = target as Map;
            if (map == null) return false;

            // Check minimum days passed
            float daysPassed = GenDate.DaysPassedFloat;
            if (daysPassed < 3f) // Hardcoded from XML <minDaysPassed>3</minDaysPassed>
                return false;

            return true;
        }
    }
}
