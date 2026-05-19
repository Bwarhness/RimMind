using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// Gates the new-game flow on the user committing a pitch when they've
    /// chosen the RimMind AI scenario.
    ///
    /// We patch Page.DoNext (the base class method — Page_SelectScenario does
    /// not declare its own DoNext) rather than BeginScenarioConfiguration:
    /// BeginScenarioConfiguration is the side-effecting helper that assigns
    /// `this.next`, and CanDoNext() calls it. If we block BeginScenarioConfiguration,
    /// `this.next` stays null and DoNext transitions to nothing (kicks the user
    /// back to the main menu). Blocking DoNext itself leaves the page chain
    /// intact and just defers the transition until the dialog is committed.
    /// </summary>
    [HarmonyPatch(typeof(Page), "DoNext")]
    public static class Page_DoNext_Patch
    {
        private static System.Reflection.FieldInfo curScenField;

        public static bool Prefix(Page __instance)
        {
            try
            {
                var page = __instance as Page_SelectScenario;
                if (page == null) return true;

                var scen = GetSelectedScenario(page);
                if (scen == null) return true;

                var part = scen.AllParts.OfType<ScenPart_RimMindCampaign>().FirstOrDefault();
                if (part == null) return true;

                if (part.plan != null && part.plan.HasContent)
                    return true;

                Find.WindowStack.Add(new Dialog_RimMindCampaignPrompt(part, onCommitted: () =>
                {
                    Messages.Message("RimMind_Scenario_ReadyToast".Translate(),
                        MessageTypeDefOf.PositiveEvent, false);
                }));
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Page.DoNext prefix failed: " + ex.Message);
                return true;
            }
        }

        private static Scenario GetSelectedScenario(Page_SelectScenario page)
        {
            if (curScenField == null)
            {
                curScenField = typeof(Page_SelectScenario)
                    .GetFields(System.Reflection.BindingFlags.Instance
                             | System.Reflection.BindingFlags.Public
                             | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => typeof(Scenario).IsAssignableFrom(f.FieldType));
            }
            return curScenField?.GetValue(page) as Scenario;
        }
    }
}
