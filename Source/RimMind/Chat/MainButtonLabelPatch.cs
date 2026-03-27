using System.Linq;
using RimWorld;
using Verse;

namespace RimMind.Chat
{
    /// <summary>
    /// Modifies the RimMind main button label to include version number.
    /// Runs once after all defs are loaded via StaticConstructorOnStartup.
    /// Calls ChatWindow.GetVersionTitle() to reuse the cached mod metadata
    /// instead of making redundant ModLister lookups.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MainButtonLabelPatch
    {
        static MainButtonLabelPatch()
        {
            var def = DefDatabase<MainButtonDef>.AllDefs.FirstOrDefault(d => d.defName == "RimMind");
            if (def == null) return;

            // Call GetVersionTitle() to initialize and reuse ChatWindow's cached mod
            // metadata, avoiding a separate ModLister lookup.
            def.label = ChatWindow.GetVersionTitle();
        }
    }
}
