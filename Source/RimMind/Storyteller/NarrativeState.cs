using System.Collections.Generic;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Top-level saveable state container for the AI storyteller.
    /// Attached to the game via NarrativeEngine GameComponent.
    /// </summary>
    public class NarrativeState : IExposable
    {
        public CampaignFrame Campaign;
        public PlotGraph Plot;
        public List<PlannedEvent> EventHistory = new List<PlannedEvent>();
        public int TotalBeatsExecuted;
        public int TotalPlansGenerated;
        public int LastPlanDay;
        public bool IsInitialized;
        public string CurrentThemeId = "chronicle";

        public NarrativeState()
        {
            Campaign = new CampaignFrame();
            Plot = new PlotGraph();
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref Campaign, "campaign");
            Scribe_Deep.Look(ref Plot, "plot");
            Scribe_Collections.Look(ref EventHistory, "eventHistory", LookMode.Deep);
            Scribe_Values.Look(ref TotalBeatsExecuted, "totalBeatsExecuted", 0);
            Scribe_Values.Look(ref TotalPlansGenerated, "totalPlansGenerated", 0);
            Scribe_Values.Look(ref LastPlanDay, "lastPlanDay", 0);
            Scribe_Values.Look(ref IsInitialized, "isInitialized", false);
            Scribe_Values.Look(ref CurrentThemeId, "currentThemeId", "chronicle");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (Campaign == null) Campaign = new CampaignFrame();
                if (Plot == null) Plot = new PlotGraph();
                if (EventHistory == null) EventHistory = new List<PlannedEvent>();
            }
        }
    }
}
