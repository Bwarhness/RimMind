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

        public override void GameComponentUpdate()
        {
            int processed = 0;
            while (actionQueue.TryDequeue(out Action action) && processed < 10)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error("[RimMind] MainThreadDispatcher error: " + ex);
                }
                processed++;
            }
        }
    }
}
