using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMind.Chat
{
    /// <summary>
    /// Harmony patch to modify the RimMind main button label to include version number.
    /// MainButtonWorker.Label is not virtual, so we patch the getter.
    /// </summary>
    [HarmonyPatch(typeof(MainButtonWorker), "get_Label")]
    public static class MainButtonLabelPatch
    {
        // Cached label for RimMind button
        private static string cachedRimMindLabel;
        private static bool initialized;

        /// <summary>
        /// Postfix: If this is the RimMind button, return version-enriched label.
        /// </summary>
        public static void Postfix(MainButtonWorker __instance, ref string __result)
        {
            // Only modify if this is our RimMind button
            if (__instance?.def?.defName != "RimMind") return;

            // Initialize cached label once
            if (!initialized)
            {
                ModMetaData mod = ModLister.GetActiveModWithIdentifier("rimmind.ai")
                               ?? ModLister.GetActiveModWithIdentifier("rimmind.ai.dev");

                string version = mod?.ModVersion ?? "";
                bool isDev = ModLister.GetActiveModWithIdentifier("rimmind.ai.dev") != null;

                // Start with base label
                string label = __result ?? "RimMind";
                if (!string.IsNullOrEmpty(version))
                    label += " v" + version;
                if (isDev)
                    label += " [DEV]";

                cachedRimMindLabel = label;
                initialized = true;
            }

            __result = cachedRimMindLabel;
        }
    }
}
