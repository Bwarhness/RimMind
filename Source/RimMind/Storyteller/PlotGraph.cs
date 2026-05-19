using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// The plot graph tracks all narrative beats, active threads, tension levels,
    /// and unresolved seeds. Core data structure for the AI storyteller.
    /// </summary>
    public class PlotGraph : IExposable
    {
        public List<PlotBeat> Beats = new List<PlotBeat>();
        public List<StoryThread> ActiveThreads = new List<StoryThread>();
        public float DramaticTension;
        public float HopeLevel;
        public float MysteryLevel;
        public List<NarrativeSeed> UnresolvedSeeds = new List<NarrativeSeed>();
        public List<string> StoryPromises = new List<string>();
        public List<string> ChapterSummaries = new List<string>();

        public PlotGraph() { }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Beats, "beats", LookMode.Deep);
            Scribe_Collections.Look(ref ActiveThreads, "activeThreads", LookMode.Deep);
            Scribe_Values.Look(ref DramaticTension, "dramaticTension", 0.5f);
            Scribe_Values.Look(ref HopeLevel, "hopeLevel", 0.5f);
            Scribe_Values.Look(ref MysteryLevel, "mysteryLevel", 0.3f);
            Scribe_Collections.Look(ref UnresolvedSeeds, "unresolvedSeeds", LookMode.Deep);
            Scribe_Collections.Look(ref StoryPromises, "storyPromises", LookMode.Value);
            Scribe_Collections.Look(ref ChapterSummaries, "chapterSummaries", LookMode.Value);
        }

        public void AddBeat(PlotBeat beat)
        {
            Beats.Add(beat);
            foreach (var threadId in beat.OpensThreads)
            {
                var thread = ActiveThreads.FirstOrDefault(t => t.Id == threadId);
                if (thread != null && thread.Status == ThreadStatus.Dormant)
                    thread.Status = ThreadStatus.Open;
            }
            foreach (var threadId in beat.ClosesThreads)
            {
                var thread = ActiveThreads.FirstOrDefault(t => t.Id == threadId);
                if (thread != null)
                {
                    thread.Status = ThreadStatus.Closed;
                    thread.DayClosed = beat.DayExecuted;
                }
            }
            foreach (var seedId in beat.PlantsSeeds)
            {
                var seed = UnresolvedSeeds.FirstOrDefault(s => s.Id == seedId);
                if (seed != null && !seed.IsResolved)
                {
                    seed.IsResolved = true;
                    seed.DayResolved = beat.DayExecuted;
                    seed.ResolutionBeatId = beat.Id;
                }
            }
            PruneOldBeatsIfNeeded();
        }

        public void UpdateTension(float deltaTension, float deltaHope, float deltaMystery)
        {
            DramaticTension = System.Math.Max(0f, System.Math.Min(1f, DramaticTension + deltaTension));
            HopeLevel = System.Math.Max(0f, System.Math.Min(1f, HopeLevel + deltaHope));
            MysteryLevel = System.Math.Max(0f, System.Math.Min(1f, MysteryLevel + deltaMystery));
        }

        public void PlantSeed(NarrativeSeed seed)
        {
            UnresolvedSeeds.Add(seed);
        }

        public void AddStoryPromise(string promise)
        {
            if (!StoryPromises.Contains(promise))
                StoryPromises.Add(promise);
        }

        public void FulfillPromise(string promise)
        {
            StoryPromises.Remove(promise);
        }

        /// <summary>
        /// After 20+ beats, compress old history into chapter summaries to stay within context limits.
        /// </summary>
        private void PruneOldBeatsIfNeeded()
        {
            const int maxFullBeats = 20;
            const int summaryThreshold = 30;

            if (Beats.Count <= summaryThreshold) return;

            var executedBeats = Beats.Where(b => b.WasExecuted).OrderBy(b => b.DayExecuted).ToList();
            if (executedBeats.Count <= maxFullBeats) return;

            var beatsToSummarize = executedBeats.Take(executedBeats.Count - maxFullBeats).ToList();
            if (beatsToSummarize.Count < 10) return;

            var summary = BuildChapterSummary(beatsToSummarize);
            ChapterSummaries.Add(summary);

            foreach (var beat in beatsToSummarize)
                Beats.Remove(beat);
        }

        private string BuildChapterSummary(List<PlotBeat> beats)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Chapter Summary: Days {beats.First().DayExecuted}-{beats.Last().DayExecuted}]");
            foreach (var beat in beats)
            {
                sb.AppendLine($"- {beat.WhatHappened} ({beat.ConsequenceTag})");
            }
            sb.AppendLine($"  Threads touched: {string.Join(", ", beats.SelectMany(b => b.OpensThreads.Concat(b.ClosesThreads)).Distinct())}");
            return sb.ToString();
        }

        public string BuildPromptContext(int maxBeats = 10)
        {
            var sb = new System.Text.StringBuilder();

            if (ChapterSummaries.Count > 0)
            {
                sb.AppendLine("=== PREVIOUS CHAPTERS ===");
                foreach (var summary in ChapterSummaries)
                    sb.AppendLine(summary);
                sb.AppendLine();
            }

            var recentBeats = Beats.Where(b => b.WasExecuted).OrderByDescending(b => b.DayExecuted).Take(maxBeats).Reverse().ToList();
            if (recentBeats.Count > 0)
            {
                sb.AppendLine("=== RECENT BEATS ===");
                foreach (var beat in recentBeats)
                {
                    sb.AppendLine($"Day {beat.DayExecuted}: {beat.WhatHappened}");
                    sb.AppendLine($"  Significance: {beat.NarrativeSignificance}");
                    if (!string.IsNullOrEmpty(beat.IncidentOutcome))
                        sb.AppendLine($"  Outcome: {beat.IncidentOutcome}");
                }
                sb.AppendLine();
            }

            var openThreads = ActiveThreads.Where(t => t.Status == ThreadStatus.Open).ToList();
            if (openThreads.Count > 0)
            {
                sb.AppendLine("=== ACTIVE THREADS ===");
                foreach (var thread in openThreads)
                    sb.AppendLine($"  [{thread.Id}] {thread.Name}: {thread.Description} (weight: {thread.DramaticWeight:F1})");
                sb.AppendLine();
            }

            var dormantThreads = ActiveThreads.Where(t => t.Status == ThreadStatus.Dormant).ToList();
            if (dormantThreads.Count > 0)
            {
                sb.AppendLine("=== DORMANT THREADS ===");
                foreach (var thread in dormantThreads)
                    sb.AppendLine($"  [{thread.Id}] {thread.Name}: {thread.Description}");
                sb.AppendLine();
            }

            if (UnresolvedSeeds.Count > 0)
            {
                sb.AppendLine("=== UNRESOLVED SEEDS ===");
                foreach (var seed in UnresolvedSeeds.Where(s => !s.IsResolved))
                    sb.AppendLine($"  [{seed.Id}] {seed.Description}");
                sb.AppendLine();
            }

            if (StoryPromises.Count > 0)
            {
                sb.AppendLine("=== STORY PROMISES ===");
                foreach (var promise in StoryPromises)
                    sb.AppendLine($"  - {promise}");
                sb.AppendLine();
            }

            sb.AppendLine("=== TENSION LEVELS ===");
            sb.AppendLine($"Dramatic Tension: {DramaticTension:F2}");
            sb.AppendLine($"Hope Level: {HopeLevel:F2}");
            sb.AppendLine($"Mystery Level: {MysteryLevel:F2}");

            return sb.ToString();
        }
    }
}
