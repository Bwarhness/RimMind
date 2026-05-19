using System.Collections.Generic;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Lightweight registry for pending narrative letter framing.
    /// When a planned event fires, we register framing data here.
    /// LetterStackPatch (unified patch for framing + automation) checks this registry when letters arrive.
    /// </summary>
    public static class PendingLetterFraming
    {
        private static readonly Dictionary<string, FramingEntry> pending = new Dictionary<string, FramingEntry>();
        private static readonly object lockObj = new object();

        public static void RegisterPendingFraming(string incidentDefName, PlannedEvent planned, IThemeProvider theme, PlotBeat beat)
        {
            if (string.IsNullOrEmpty(incidentDefName)) return;
            lock (lockObj)
            {
                pending[incidentDefName] = new FramingEntry
                {
                    Planned = planned,
                    Theme = theme,
                    Beat = beat,
                    RegisterTick = Find.TickManager.TicksGame
                };
            }
        }

        public static FramingEntry Consume(string incidentDefName)
        {
            if (string.IsNullOrEmpty(incidentDefName)) return null;
            lock (lockObj)
            {
                if (pending.TryGetValue(incidentDefName, out var entry))
                {
                    pending.Remove(incidentDefName);
                    return entry;
                }
                return null;
            }
        }

        public static void CleanupOldEntries(int maxAgeTicks = 6000)
        {
            lock (lockObj)
            {
                int currentTick = Find.TickManager.TicksGame;
                var keysToRemove = new List<string>();
                foreach (var kv in pending)
                {
                    if (currentTick - kv.Value.RegisterTick > maxAgeTicks)
                        keysToRemove.Add(kv.Key);
                }
                foreach (var key in keysToRemove)
                    pending.Remove(key);
            }
        }
    }

    public class FramingEntry
    {
        public PlannedEvent Planned;
        public IThemeProvider Theme;
        public PlotBeat Beat;
        public int RegisterTick;
    }
}
