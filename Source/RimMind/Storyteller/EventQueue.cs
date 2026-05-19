using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Thread-safe queue of planned narrative events waiting to be executed.
    /// Prevents API latency from blocking gameplay.
    /// </summary>
    public class EventQueue : IExposable
    {
        private List<PlannedEvent> events = new List<PlannedEvent>();
        private readonly object lockObj = new object();

        public bool HasEvents => events.Any(e => !e.WasFired);
        public int Count => events.Count(e => !e.WasFired);

        public void Enqueue(PlannedEvent evt)
        {
            lock (lockObj)
            {
                events.Add(evt);
            }
        }

        public void EnqueueRange(IEnumerable<PlannedEvent> evts)
        {
            lock (lockObj)
            {
                events.AddRange(evts);
            }
        }

        public PlannedEvent Dequeue()
        {
            lock (lockObj)
            {
                var evt = events.FirstOrDefault(e => !e.WasFired);
                // Note: WasFired is NOT set here - it's set by the caller after successful execution
                // This prevents permanent loss of events if ToFiringIncident() fails or returns null
                return evt;
            }
        }

        public void MarkFired(PlannedEvent evt)
        {
            lock (lockObj)
            {
                if (evt != null)
                {
                    evt.WasFired = true;
                    evt.FireDay = GenLocalDate.DayOfYear(Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome));
                }
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                events.Clear();
            }
        }

        public void Remove(string eventId)
        {
            lock (lockObj)
            {
                events.RemoveAll(e => e.Id == eventId);
            }
        }

        public List<PlannedEvent> PeekUpcoming(int count = 3)
        {
            lock (lockObj)
            {
                return events.Where(e => !e.WasFired).Take(count).ToList();
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref events, "events", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && events == null)
                events = new List<PlannedEvent>();
        }
    }
}
