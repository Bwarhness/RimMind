using HarmonyLib;
using RimMind.Automation;
using RimMind.Chat;
using RimMind.Core;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Unified Harmony patch for LetterStack.ReceiveLetter that handles both:
    /// 1. Narrative framing (modifies letter text/label based on AI storyteller plans)
    /// 2. Event automation (triggers AI chat responses for configured events)
    /// 
    /// Framing runs BEFORE automation so the AI sees the framed/narrative version of the letter.
    /// Uses explicit method targeting by parameter types instead of fragile heuristics.
    /// </summary>
    public static class LetterStackPatch
    {
        /// <summary>
        /// Apply the unified patch. Called from RimMindMod constructor.
        /// Targets ReceiveLetter(Letter, string, int, bool) explicitly.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                // Explicitly target the correct overload by parameter types
                // This is the main overload that all others funnel through in RimWorld 1.6
                var target = AccessTools.Method(
                    typeof(LetterStack), 
                    "ReceiveLetter", 
                    new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) }
                );

                if (target == null)
                {
                    Log.Error("[RimMind] Could not find ReceiveLetter(Letter, string, int, bool) to patch!");
                    return;
                }

                var postfix = typeof(LetterStackPatch).GetMethod("Postfix",
                    BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Log.Message($"[RimMind] Patched LetterStack.ReceiveLetter with unified framing+automation handler");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] LetterStackPatch apply failed: {ex}");
            }
        }

        /// <summary>
        /// Unified postfix that handles both framing and automation.
        /// Framing is applied first, then automation triggers (so AI sees framed text).
        /// Harmony parameter injection matches by source-method parameter name; the
        /// target's first parameter is named "let", so this postfix must use that name.
        /// </summary>
        public static void Postfix(Letter let)
        {
            if (let == null) return;

            try
            {
                // STEP 1: Apply narrative framing FIRST (so automation sees framed version)
                ApplyFraming(let);

                // STEP 2: Trigger event automation SECOND (sees the framed letter)
                ApplyAutomation(let);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] LetterStackPatch postfix failed: {ex}");
            }
        }

        /// <summary>
        /// Apply narrative framing to the letter if it corresponds to a planned AI storyteller event.
        /// </summary>
        private static void ApplyFraming(Letter let)
        {
            try
            {
                if (let.def == null) return;
                if (!RimMindMod.Settings.storytellerEnabled) return;

                string defName = let.def.defName;

                // Check if we have pending framing for this incident type
                var framing = PendingLetterFraming.Consume(defName);
                if (framing == null) return;

                // Apply narrative framing to the letter
                var theme = framing.Theme ?? new ChronicleThemeProvider();
                var beat = framing.Beat;
                var planned = framing.Planned;

                // Override label
                if (!string.IsNullOrEmpty(planned?.NarrativeLabel))
                {
                    let.def.label = theme.FrameLetterLabel(defName, planned.NarrativeLabel);
                }

                // Override text if it's a ChoiceLetter
                if (let is ChoiceLetter choiceLetter && !string.IsNullOrEmpty(planned?.NarrativeText))
                {
                    string baseText = choiceLetter.Text.ToString() ?? "";
                    string framedText = theme.FrameLetterText(defName, baseText, beat);
                    // Use Harmony Traverse to set the private 'text' field
                    try
                    {
                        var traverse = Traverse.Create(choiceLetter);
                        var textField = traverse.Field("text");
                        if (textField.FieldExists())
                        {
                            textField.SetValue(framedText);
                        }
                    }
                    catch { }
                }

                DebugLogger.Log("STORYTELLER", $"Applied narrative framing to letter: {defName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimMind] Letter framing failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Trigger event automation when a letter is received and automation is configured.
        /// </summary>
        private static void ApplyAutomation(Letter let)
        {
            try
            {
                // Safety checks
                if (let.def == null) return;
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
                Log.Error($"[RimMind] Automation failed: {ex}");
            }
        }
    }
}
