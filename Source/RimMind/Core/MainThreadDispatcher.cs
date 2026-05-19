using System;
using System.Collections.Concurrent;
using Verse;

namespace RimMind.Core
{
    public class MainThreadDispatcher : GameComponent
    {
        private static readonly ConcurrentQueue<Action> actionQueue = new ConcurrentQueue<Action>();

        public MainThreadDispatcher(Game game) { }

        public static void Enqueue(Action action)
        {
            if (action != null)
                actionQueue.Enqueue(action);
        }

        /// <summary>
        /// Process queued callbacks on the calling thread. Used by both the in-game
        /// GameComponentUpdate path and any pre-game UI that needs to receive
        /// HTTP callbacks (Current.Game is null pre-game, so GameComponentUpdate
        /// never fires there — pre-game consumers must drain the queue themselves).
        /// </summary>
        public static void Drain(int maxPerCall = 10)
        {
            int processed = 0;
            while (actionQueue.TryDequeue(out Action action) && processed < maxPerCall)
            {
                try { action.Invoke(); }
                catch (Exception ex) { Log.Error("[RimMind] MainThreadDispatcher error: " + ex); }
                processed++;
            }
        }

        public override void GameComponentUpdate() => Drain();
    }
}
