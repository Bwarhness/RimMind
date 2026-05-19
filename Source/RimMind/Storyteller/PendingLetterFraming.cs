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

        /// <summary>
        /// Register framing for a pending incident. Uses a composite key (incidentDefName_eventId)
        /// so that multiple incidents of the same type don't overwrite each other.
        /// </summary>
        public static void RegisterPendingFraming(string incidentDefName, PlannedEvent planned, IThemeProvider theme, PlotBeat beat)
        {
            if (string.IsNullOrEmpty(incidentDefName) || planned == null) return;
            string key = $"{incidentDefName}_{planned.Id}";
            lock (lockObj)
            {
                pending[key] = new FramingEntry
                {
                    IncidentDefName = incidentDefName,
                    Planned = planned,
                    Theme = theme,
                    Beat = beat,
                    RegisterTick = Find.TickManager.TicksGame
                };
            }
        }

        /// <summary>
        /// Consume the first pending framing entry matching the given incidentDefName (FIFO).
        /// Used by letter patches which only know the defName, not the event ID.
        /// </summary>
        public static FramingEntry Consume(string incidentDefName)
        {
            if (string.IsNullOrEmpty(incidentDefName)) return null;
            lock (lockObj)
            {
                foreach (var kv in pending)
                {
                    if (kv.Value.IncidentDefName == incidentDefName)
                    {
                        pending.Remove(kv.Key);
                        return kv.Value;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Remove all pending entries. Called on game start/load to prevent stale data
        /// from leaking across save/load cycles.
        /// </summary>
        public static void Clear()
        {
            lock (lockObj)
            {
                pending.Clear();
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
        public string IncidentDefName;
        public PlannedEvent Planned;
        public IThemeProvider Theme;
        public PlotBeat Beat;
        public int RegisterTick;
    }
}
