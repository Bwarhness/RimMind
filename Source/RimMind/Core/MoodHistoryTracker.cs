using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimMind.Core
{
    public class MoodSnapshot
    {
        public int tick;
        public string pawnId;
        public float moodLevel;
        public float breakThreshold;

        public MoodSnapshot() { }

        public MoodSnapshot(int tick, string pawnId, float moodLevel, float breakThreshold)
        {
            this.tick = tick;
            this.pawnId = pawnId;
            this.moodLevel = moodLevel;
            this.breakThreshold = breakThreshold;
        }
    }

    public class MoodHistoryTracker : GameComponent
    {
        private List<MoodSnapshot> snapshots = new List<MoodSnapshot>();
        private int lastSnapshotTick = 0;
        private const int SNAPSHOT_INTERVAL = 2500; // ~1 hour in game (2500 ticks = ~41 seconds real time)
        private const int MAX_HISTORY_DAYS = 3;
        private const int TICKS_PER_DAY = 60000;

        public static MoodHistoryTracker Instance => Current.Game?.GetComponent<MoodHistoryTracker>();

        public MoodHistoryTracker(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int currentTick = Find.TickManager.TicksGame;

            // Take snapshots every ~1 hour
            if (currentTick - lastSnapshotTick >= SNAPSHOT_INTERVAL)
            {
                Map map = Find.CurrentMap;
                TakeSnapshot(currentTick, map);
                lastSnapshotTick = currentTick;
                CleanOldSnapshots(currentTick);
            }
        }

        private void TakeSnapshot(int currentTick, Map map)
        {
            if (map == null) return;

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.needs?.mood == null) continue;

                var snapshot = new MoodSnapshot(
                    currentTick,
                    pawn.ThingID,
                    pawn.needs.mood.CurLevel,
                    pawn.mindState.mentalBreaker.BreakThresholdExtreme
                );

                snapshots.Add(snapshot);
            }
        }

        private void CleanOldSnapshots(int currentTick)
        {
            int cutoffTick = currentTick - (MAX_HISTORY_DAYS * TICKS_PER_DAY);
            snapshots.RemoveAll(s => s.tick < cutoffTick);
        }

        public List<MoodSnapshot> GetHistory(string pawnId, int daysBack = 3)
        {
            int currentTick = Find.TickManager.TicksGame;
            int cutoffTick = currentTick - (daysBack * TICKS_PER_DAY);

            return snapshots
                .Where(s => s.pawnId == pawnId && s.tick >= cutoffTick)
                .OrderBy(s => s.tick)
                .ToList();
        }

        public List<MoodSnapshot> GetAllRecentHistory(int daysBack = 3)
        {
            int currentTick = Find.TickManager.TicksGame;
            int cutoffTick = currentTick - (daysBack * TICKS_PER_DAY);

            return snapshots
                .Where(s => s.tick >= cutoffTick)
                .OrderBy(s => s.tick)
                .ToList();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastSnapshotTick, "lastSnapshotTick", 0);
            Scribe_Collections.Look(ref snapshots, "snapshots", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && snapshots == null)
            {
                snapshots = new List<MoodSnapshot>();
            }
        }
    }
}
