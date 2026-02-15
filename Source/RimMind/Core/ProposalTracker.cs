using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Core
{
    public class ProposalTracker : GameComponent
    {
        private static ProposalTracker instance;

        private Dictionary<string, int> proposals = new Dictionary<string, int>();
        private int nextId = 1;

        public ProposalTracker(Game game) : base()
        {
            instance = this;
        }

        public static bool HasInstance => instance != null;

        public static string Track(Thing thing)
        {
            if (instance == null) return null;
            string id = "rm_" + instance.nextId++;
            instance.proposals[id] = thing.thingIDNumber;
            return id;
        }

        public static void Untrack(string proposalId)
        {
            if (instance == null) return;
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
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t.thingIDNumber == thingId)
                    return t;
            }
            return null;
        }

        public static List<KeyValuePair<string, Thing>> GetAll(Map map)
        {
            var result = new List<KeyValuePair<string, Thing>>();
            if (instance == null || map == null) return result;
            foreach (var kvp in instance.proposals)
            {
                Thing t = FindThing(kvp.Key, map);
                if (t != null && !t.Destroyed)
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
            var stale = new List<string>();
            foreach (var kvp in instance.proposals)
            {
                Thing t = FindThing(kvp.Key, map);
                if (t == null || t.Destroyed)
                    stale.Add(kvp.Key);
            }
            foreach (var id in stale)
                instance.proposals.Remove(id);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref proposals, "rimMindProposals", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextId, "rimMindNextProposalId", 1);
            if (proposals == null)
                proposals = new Dictionary<string, int>();
            instance = this;
        }
    }
}
