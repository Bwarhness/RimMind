using HarmonyLib;
using RimMind.Chat;
using RimMind.Core;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace RimMind.Automation
{
    /// <summary>
    /// Harmony patch for LetterStack.ReceiveLetter to detect incoming letters
    /// and trigger event automation when configured.
    /// Patched manually in RimMindMod constructor due to multiple overloads.
    /// </summary>
    public static class LetterAutomationPatch
    {
        /// <summary>
        /// Manually apply the patch. Called from RimMindMod constructor.
        /// We target ReceiveLetter(Letter, string) specifically since LetterStack
        /// has 3 overloads and attribute-based patching causes AmbiguousMatchException.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            // Find the smallest ReceiveLetter overload — in RimWorld 1.6, there are 3 overloads
            // with 4, 6, and 10 params. The 4-param version (Letter, string, int, bool) is the
            // one all others funnel through.
            MethodInfo target = null;
            var allMethods = typeof(LetterStack).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var m in allMethods)
            {
                if (m.Name != "ReceiveLetter") continue;
                try
                {
                    var parms = m.GetParameters();
                    if (target == null || parms.Length < target.GetParameters().Length)
                        target = m;
                }
                catch { }
            }

            if (target == null)
            {
                Log.Error("[RimMind] Could not find ReceiveLetter to patch!");
                return;
            }

            var postfix = typeof(LetterAutomationPatch).GetMethod("PostfixGeneric",
                BindingFlags.Static | BindingFlags.Public);

            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Log.Message($"[RimMind] Patched LetterStack.ReceiveLetter ({target.GetParameters().Length} params)");
        }

        /// <summary>
        /// Generic postfix that works with any ReceiveLetter overload.
        /// Grabs the most recently added letter from the LetterStack instance.
        /// Harmony injects __instance automatically for instance methods.
        /// </summary>
        public static void PostfixGeneric(LetterStack __instance)
        {
            try
            {
                // Get the last letter that was just added
                var letters = __instance.LettersListForReading;
                if (letters == null || letters.Count == 0) return;
                var let = letters[letters.Count - 1];
                Postfix(let);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] PostfixGeneric failed: {ex}");
            }
        }

        public static void Postfix(Letter let)
        {
            try
            {
                // Safety checks
                if (let == null || let.def == null) return;
                if (!RimMindMod.Settings.enableEventAutomation) return;
                if (EventAutomationManager.Instance == null) return;

                string eventType = let.def.defName;
                if (string.IsNullOrEmpty(eventType)) return;

                DebugLogger.Log("AUTOMATION", $" Letter received: {eventType} - {let.Label}");

                // Check if automation is configured for this event type
                if (!RimMindMod.Settings.automationRules.TryGetValue(eventType, out var rule))
                {
                    // No rule configured yet - create one with default template but disabled
                    rule = new AutomationRule
                    {
                        enabled = false,
                        customPrompt = DefaultAutomationPrompts.Get(eventType),
                        cooldownSeconds = 60
                    };
                    RimMindMod.Settings.automationRules[eventType] = rule;
                    DebugLogger.Log("AUTOMATION", $" Auto-registered event type: {eventType}");
                    return;
                }

                // Check if this specific rule is enabled
                if (!rule.enabled)
                {
                    DebugLogger.Log("AUTOMATION", $" Event {eventType} received but automation disabled for this type");
                    return;
                }

                // Check custom prompt exists
                if (string.IsNullOrWhiteSpace(rule.customPrompt))
                {
                    DebugLogger.Log("AUTOMATION", $" Event {eventType} has no custom prompt configured");
                    return;
                }

                // Check cooldown via EventAutomationManager
                if (!EventAutomationManager.Instance.CanTrigger(eventType, rule.cooldownSeconds))
                {
                    DebugLogger.Log("AUTOMATION", $" Event {eventType} on cooldown (waiting {rule.cooldownSeconds}s between triggers)");
                    return;
                }

                // Use the custom prompt directly
                string automationPrompt = rule.customPrompt;

                // Send to AI via ChatManager (must be on main thread)
                DebugLogger.Log("AUTOMATION", $" Triggering automation for event: {eventType}");
                DebugLogger.Log("AUTOMATION", $" Prompt: {automationPrompt}");

                // Enqueue on main thread to ensure thread safety
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Get ChatManager — works even when chat window is closed
                        var chatManager = ChatWindow.SharedManager;
                        chatManager.SendMessage(automationPrompt);

                        Messages.Message(
                            $"RimMind automation: {let.Label}",
                            MessageTypeDefOf.NeutralEvent
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimMind] Automation execution failed: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] Automation patch failed: {ex}");
                // Never crash game due to automation error
            }
        }

        /// <summary>
        /// Build a context-enriched automation prompt by prepending event details to user's custom prompt.
        /// </summary>
        private static string BuildAutomationPrompt(string eventType, Letter letter, string customPrompt)
        {
            string letterLabel = letter.Label.ToString();
            if (string.IsNullOrEmpty(letterLabel)) letterLabel = "Unknown Event";
            string letterText = "";

            try
            {
                // GetMouseoverText() is protected; use ChoiceLetter.Text for detailed letters
                if (letter is ChoiceLetter choiceLetter)
                {
                    string text = choiceLetter.Text.ToString();
                    if (!string.IsNullOrEmpty(text))
                        letterText = text;
                }
            }
            catch
            {
                // Some letters may not have text content
            }

            // Build structured prompt
            string prompt = $"[EVENT AUTOMATION TRIGGERED]\n\n" +
                           $"Event Type: {eventType}\n" +
                           $"Event Title: {letterLabel}\n";

            // Include letter text if available (provides context like raid faction, fire location, etc.)
            if (!string.IsNullOrWhiteSpace(letterText) && letterText.Length < 500)
            {
                prompt += $"Event Details: {letterText}\n";
            }

            prompt += $"\nYour Instructions (from automation rule):\n{customPrompt}";

            return prompt;
        }
    }
}
