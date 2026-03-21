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
                TakeSnapshot(currentTick);
                lastSnapshotTick = currentTick;
                CleanOldSnapshots(currentTick);
            }
        }

        private void TakeSnapshot(int currentTick)
        {
            var map = Find.CurrentMap;
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
            
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Convert to lists for saving
                var ticks = snapshots.Select(s => s.tick).ToList();
                var pawnIds = snapshots.Select(s => s.pawnId).ToList();
                var moodLevels = snapshots.Select(s => s.moodLevel).ToList();
                var breakThresholds = snapshots.Select(s => s.breakThreshold).ToList();

                Scribe_Collections.Look<int>(ref ticks, "snapshotTicks", LookMode.Value);
                Scribe_Collections.Look<string>(ref pawnIds, "snapshotPawnIds", LookMode.Value);
                Scribe_Collections.Look<float>(ref moodLevels, "snapshotMoodLevels", LookMode.Value);
                Scribe_Collections.Look<float>(ref breakThresholds, "snapshotBreakThresholds", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<int> ticks = null;
                List<string> pawnIds = null;
                List<float> moodLevels = null;
                List<float> breakThresholds = null;

                Scribe_Collections.Look<int>(ref ticks, "snapshotTicks", LookMode.Value);
                Scribe_Collections.Look<string>(ref pawnIds, "snapshotPawnIds", LookMode.Value);
                Scribe_Collections.Look<float>(ref moodLevels, "snapshotMoodLevels", LookMode.Value);
                Scribe_Collections.Look<float>(ref breakThresholds, "snapshotBreakThresholds", LookMode.Value);

                snapshots = new List<MoodSnapshot>();

                if (ticks != null && pawnIds != null && moodLevels != null && breakThresholds != null)
                {
                    int count = Math.Min(Math.Min(ticks.Count, pawnIds.Count), 
                                       Math.Min(moodLevels.Count, breakThresholds.Count));

                    for (int i = 0; i < count; i++)
                    {
                        snapshots.Add(new MoodSnapshot(
                            ticks[i],
                            pawnIds[i],
                            moodLevels[i],
                            breakThresholds[i]
                        ));
                    }
                }
            }
        }
    }
}
