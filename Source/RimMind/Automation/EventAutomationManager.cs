using System;
using System.Collections.Generic;
using RimMind.Core;
using Verse;

namespace RimMind.Automation
{
    /// <summary>
    /// Tracks cooldowns and manages event automation triggers.
    /// </summary>
    public class EventAutomationManager : GameComponent
    {
        private Dictionary<string, int> lastTriggerTick = new Dictionary<string, int>();

        public EventAutomationManager(Game game) : base() { }

        /// <summary>
        /// Checks if an event type can trigger automation (respects cooldown).
        /// </summary>
        public bool CanTrigger(string eventType, int cooldownSeconds)
        {
            if (string.IsNullOrEmpty(eventType)) return false;

            int currentTick = Find.TickManager.TicksGame;
            int cooldownTicks = cooldownSeconds * 60; // Convert seconds to ticks (60 ticks/sec)

            if (lastTriggerTick.TryGetValue(eventType, out int lastTick))
            {
                if (currentTick - lastTick < cooldownTicks)
                {
                    return false; // Still in cooldown
                }
            }

            lastTriggerTick[eventType] = currentTick;
            return true;
        }

        /// <summary>
        /// Resets cooldown for a specific event type (for testing/manual triggers).
        /// </summary>
        public void ResetCooldown(string eventType)
        {
            if (lastTriggerTick.ContainsKey(eventType))
            {
                lastTriggerTick.Remove(eventType);
            }
        }

        /// <summary>
        /// Clears all cooldowns (for settings changes).
        /// </summary>
        public void ClearAllCooldowns()
        {
            lastTriggerTick.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastTriggerTick, "lastTriggerTick", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && lastTriggerTick == null)
            {
                lastTriggerTick = new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Gets or creates the singleton instance for the current game.
        /// </summary>
        public static EventAutomationManager Instance
        {
            get
            {
                if (Current.Game == null) return null;
                return Current.Game.GetComponent<EventAutomationManager>();
            }
        }
    }
}
