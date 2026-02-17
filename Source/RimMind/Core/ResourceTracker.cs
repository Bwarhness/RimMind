using System.Collections.Generic;
using Verse;

namespace RimMind.Core
{
    public class ResourceTracker : GameComponent
    {
        private Dictionary<string, int> resourceSnapshots = new Dictionary<string, int>();
        private int lastSnapshotTick = 0;
        private const int SnapshotInterval = 60000; // 1 game day

        public ResourceTracker(Game game) { }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Take snapshot every day
            if (currentTick - lastSnapshotTick >= SnapshotInterval)
            {
                TakeSnapshot();
                lastSnapshotTick = currentTick;
            }
        }

        private void TakeSnapshot()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            // Store snapshots (keep last 7 days)
            var key = Find.TickManager.TicksGame.ToString();
            
            // Count key resources
            resourceSnapshots["food_" + key] = CountResource(map, "Nutrition");
            resourceSnapshots["medicine_" + key] = CountResource(map, "Medicine");
            resourceSnapshots["wood_" + key] = CountResource(map, "WoodLog");
            resourceSnapshots["steel_" + key] = CountResource(map, "Steel");

            // Clean old snapshots (keep last 7 days)
            CleanOldSnapshots();
        }

        private int CountResource(Map map, string defName)
        {
            int total = 0;
            foreach (var thing in map.listerThings.AllThings)
            {
                if (thing.def.defName == defName || 
                    (defName == "Nutrition" && thing.def.IsNutritionGivingIngestible))
                {
                    total += thing.stackCount;
                }
            }
            return total;
        }

        private void CleanOldSnapshots()
        {
            int sevenDaysAgo = Find.TickManager.TicksGame - (7 * SnapshotInterval);
            var keysToRemove = new List<string>();
            
            foreach (var kvp in resourceSnapshots)
            {
                string[] parts = kvp.Key.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int tick))
                {
                    if (tick < sevenDaysAgo)
                        keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
                resourceSnapshots.Remove(key);
        }

        public int GetSnapshot(string resource, int daysAgo)
        {
            int tick = Find.TickManager.TicksGame - (daysAgo * SnapshotInterval);
            string key = resource + "_" + tick;
            return resourceSnapshots.ContainsKey(key) ? resourceSnapshots[key] : -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref resourceSnapshots, "resourceSnapshots", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastSnapshotTick, "lastSnapshotTick");
            
            if (resourceSnapshots == null) 
                resourceSnapshots = new Dictionary<string, int>();
        }

        public static ResourceTracker Instance => Current.Game?.GetComponent<ResourceTracker>();
    }
}
