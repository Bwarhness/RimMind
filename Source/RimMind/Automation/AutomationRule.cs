using Verse;

namespace RimMind.Automation
{
    /// <summary>
    /// Defines automation behavior for a specific event type.
    /// </summary>
    public class AutomationRule : IExposable
    {
        public bool enabled = false;
        public string customPrompt = "";
        public int cooldownSeconds = 60; // Default 1-minute cooldown

        public AutomationRule() { }

        public AutomationRule(bool enabled, string prompt, int cooldown = 60)
        {
            this.enabled = enabled;
            this.customPrompt = prompt;
            this.cooldownSeconds = cooldown;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref customPrompt, "customPrompt", "");
            Scribe_Values.Look(ref cooldownSeconds, "cooldownSeconds", 60);
        }

        public AutomationRule Clone()
        {
            return new AutomationRule(enabled, customPrompt, cooldownSeconds);
        }
    }
}
