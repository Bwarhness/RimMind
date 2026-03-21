using System.Linq;
using RimWorld;
using Verse;

namespace RimMind.Chat
{
    /// <summary>
    /// Modifies the RimMind main button label to include version number.
    /// Runs once after all defs are loaded via StaticConstructorOnStartup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MainButtonLabelPatch
    {
        static MainButtonLabelPatch()
        {
            var def = DefDatabase<MainButtonDef>.AllDefs.FirstOrDefault(d => d.defName == "RimMind");
            if (def == null) return;

            ModMetaData mod = ModLister.GetActiveModWithIdentifier("rimmind.ai")
                           ?? ModLister.GetActiveModWithIdentifier("rimmind.ai.dev");

            string version = mod?.ModVersion ?? "";
            bool isDev = ModLister.GetActiveModWithIdentifier("rimmind.ai.dev") != null;

            string label = def.label ?? "RimMind";
            if (!string.IsNullOrEmpty(version))
                label += " v" + version;
            if (isDev)
                label += " [DEV]";

            def.label = label;
        }
    }
}
