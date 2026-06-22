using System;
using System.Collections.Generic;
using Verse;

namespace RimMind.Core
{
    /// <summary>
    /// Holds deferred actions for pawns that need to execute after their current job completes.
    /// Used to sequence instant state changes (draft, undraft) after job-based actions (equip, haul).
    /// </summary>
    public static class PendingCallbackQueue
    {
        private static readonly Dictionary<Pawn, Queue<Action>> _pending = new Dictionary<Pawn, Queue<Action>>();

        public static void Register(Pawn pawn, Action callback)
        {
            if (!_pending.ContainsKey(pawn))
                _pending[pawn] = new Queue<Action>();
            _pending[pawn].Enqueue(callback);
        }

        public static void FireNext(Pawn pawn)
        {
            if (!_pending.TryGetValue(pawn, out var queue) || queue.Count == 0) return;
            var next = queue.Dequeue();
            if (queue.Count == 0) _pending.Remove(pawn);
            try { next?.Invoke(); }
            catch (Exception ex) { Log.Error("[RimMind] PendingCallbackQueue callback failed: " + ex); }
        }

        public static bool HasPending(Pawn pawn) =>
            _pending.TryGetValue(pawn, out var q) && q.Count > 0;

        public static void Clear(Pawn pawn) => _pending.Remove(pawn);

        public static void ClearAll() => _pending.Clear();
    }
}
