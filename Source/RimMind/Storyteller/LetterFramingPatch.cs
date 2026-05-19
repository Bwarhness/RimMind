using HarmonyLib;
using RimMind.Core;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Harmony patch that applies narrative framing to incoming letters
    /// when they correspond to a planned AI storyteller event.
    /// </summary>
    public static class LetterFramingPatch
    {
        public static void Apply(Harmony harmony)
        {
            try
            {
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
                    Log.Warning("[RimMind] Could not find ReceiveLetter for framing patch.");
                    return;
                }

                var postfix = typeof(LetterFramingPatch).GetMethod("Postfix",
                    BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Log.Message($"[RimMind] Patched LetterStack.ReceiveLetter for narrative framing ({target.GetParameters().Length} params)");
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] LetterFramingPatch apply failed: " + ex.Message);
            }
        }

        public static void Postfix(LetterStack __instance)
        {
            try
            {
                var letters = __instance.LettersListForReading;
                if (letters == null || letters.Count == 0) return;
                var let = letters[letters.Count - 1];
                ApplyFraming(let);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] LetterFramingPatch postfix failed: " + ex.Message);
            }
        }

        private static void ApplyFraming(Letter let)
        {
            try
            {
                if (let == null || let.def == null) return;
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

                // Also update the look target label if applicable
                DebugLogger.Log("STORYTELLER", $"Applied narrative framing to letter: {defName}");
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] LetterFramingPatch ApplyFraming failed: " + ex.Message);
            }
        }
    }
}
