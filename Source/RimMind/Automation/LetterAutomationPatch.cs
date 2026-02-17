using HarmonyLib;
using RimMind.Chat;
using RimMind.Core;
using RimWorld;
using System;
using Verse;

namespace RimMind.Automation
{
    /// <summary>
    /// Harmony patch for LetterStack.ReceiveLetter to detect incoming letters
    /// and trigger event automation when configured.
    /// </summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter))]
    public static class LetterAutomationPatch
    {
        static void Postfix(Letter let)
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

                // Build context-enriched prompt
                string automationPrompt = BuildAutomationPrompt(eventType, let, rule.customPrompt);

                // Send to AI via ChatManager (must be on main thread)
                DebugLogger.Log("AUTOMATION", $" Triggering automation for event: {eventType}");
                DebugLogger.Log("AUTOMATION", $" Prompt: {automationPrompt}");

                // Enqueue on main thread to ensure thread safety
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Get ChatManager from ChatWindow if it exists
                        var chatManager = ChatWindow.Instance?.Manager;
                        if (chatManager != null)
                        {
                            chatManager.SendMessage(automationPrompt);

                            // Show notification
                            Messages.Message(
                                $"RimMind automation: {let.Label}",
                                MessageTypeDefOf.NeutralEvent
                            );
                        }
                        else
                        {
                            DebugLogger.Log("AUTOMATION", $" ChatWindow not open - automation message queued but not sent");
                            Messages.Message(
                                $"RimMind automation triggered but chat window is closed. Open chat to see response.",
                                MessageTypeDefOf.CautionInput
                            );
                        }
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
