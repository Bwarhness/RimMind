using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Core
{
    public class ProposalTracker : GameComponent
    {
        private static ProposalTracker instance;

        private Dictionary<string, int> proposals = new Dictionary<string, int>();
        private Dictionary<int, string> thingIdToProposal = new Dictionary<int, string>();
        private int nextId = 1;

        // Cached map state for O(1) thing lookups
        private Map cachedMap;
        private int cachedMapTick = -1;
        private Dictionary<int, Thing> cachedThingById;

        public ProposalTracker(Game game) : base()
        {
            instance = this;
        }

        public static bool HasInstance => instance != null;

        private void EnsureThingCache(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            if (cachedMap == map && cachedMapTick == currentTick && cachedThingById != null)
                return;

            cachedMap = map;
            cachedMapTick = currentTick;
            cachedThingById = new Dictionary<int, Thing>();
            foreach (Thing t in map.listerThings.AllThings)
            {
                cachedThingById[t.thingIDNumber] = t;
            }
        }

        public static string Track(Thing thing)
        {
            if (instance == null) return null;
            string id = "rm_" + instance.nextId++;
            instance.proposals[id] = thing.thingIDNumber;
            instance.thingIdToProposal[thing.thingIDNumber] = id;
            return id;
        }

        public static void Untrack(string proposalId)
        {
            if (instance == null) return;
            int thingId;
            if (instance.proposals.TryGetValue(proposalId, out thingId))
            {
                instance.thingIdToProposal.Remove(thingId);
            }
            instance.proposals.Remove(proposalId);
        }

        public static bool IsProposal(Thing thing)
        {
            if (instance == null) return false;
            return instance.proposals.ContainsValue(thing.thingIDNumber);
        }

        public static Thing FindThing(string proposalId, Map map)
        {
            if (instance == null || map == null) return null;
            int thingId;
            if (!instance.proposals.TryGetValue(proposalId, out thingId))
                return null;
            instance.EnsureThingCache(map);
            if (instance.cachedThingById.TryGetValue(thingId, out Thing t) && !t.Destroyed)
                return t;
            return null;
        }

        public static List<KeyValuePair<string, Thing>> GetAll(Map map)
        {
            var result = new List<KeyValuePair<string, Thing>>();
            if (instance == null || map == null) return result;
            instance.EnsureThingCache(map);
            foreach (var kvp in instance.proposals)
            {
                if (instance.cachedThingById.TryGetValue(kvp.Value, out Thing t) && !t.Destroyed)
                    result.Add(new KeyValuePair<string, Thing>(kvp.Key, t));
            }
            return result;
        }

        public static List<KeyValuePair<string, Thing>> GetInRect(CellRect rect, Map map)
        {
            return GetAll(map).Where(kvp => rect.Contains(kvp.Value.Position)).ToList();
        }

        public static int ProposalCount
        {
            get { return instance != null ? instance.proposals.Count : 0; }
        }

        public static void CleanupDestroyed(Map map)
        {
            if (instance == null || map == null) return;
            instance.EnsureThingCache(map);
            var stale = new List<string>();
            foreach (var kvp in instance.proposals)
            {
                if (!instance.cachedThingById.TryGetValue(kvp.Value, out Thing t) || t.Destroyed)
                    stale.Add(kvp.Key);
            }
            foreach (var id in stale)
            {
                int thingId = instance.proposals[id];
                instance.thingIdToProposal.Remove(thingId);
                instance.proposals.Remove(id);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref proposals, "rimMindProposals", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextId, "rimMindNextProposalId", 1);
            if (proposals == null)
                proposals = new Dictionary<string, int>();
            // thingIdToProposal is rebuilt from proposals on next access
            if (thingIdToProposal == null)
                thingIdToProposal = new Dictionary<int, string>();
            instance = this;
        }
    }
}
