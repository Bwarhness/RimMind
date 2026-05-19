using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// The campaign frame defines the overarching narrative context for the colony.
    /// Generated pre-game via AI and locked once play begins.
    /// </summary>
    public class CampaignFrame : IExposable
    {
        public string Setting;
        public string IncitingIncident;
        public List<string> ActiveForces = new List<string>();
        public string CurrentAct;
        public string PendingThreat;
        public string Opportunity;
        public List<NarrativeSeed> PlantedSeeds = new List<NarrativeSeed>();
        public string UserPrompt;
        public bool IsLocked;
        public int DayLocked;

        public CampaignFrame() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Setting, "setting");
            Scribe_Values.Look(ref IncitingIncident, "incitingIncident");
            Scribe_Collections.Look(ref ActiveForces, "activeForces", LookMode.Value);
            Scribe_Collections.Look(ref PlantedSeeds, "plantedSeeds", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (ActiveForces == null) ActiveForces = new List<string>();
                if (PlantedSeeds == null) PlantedSeeds = new List<NarrativeSeed>();
            }
            Scribe_Values.Look(ref CurrentAct, "currentAct", "Act I");
            Scribe_Values.Look(ref PendingThreat, "pendingThreat");
            Scribe_Values.Look(ref Opportunity, "opportunity");
            Scribe_Collections.Look(ref PlantedSeeds, "plantedSeeds", LookMode.Deep);
            Scribe_Values.Look(ref UserPrompt, "userPrompt");
            Scribe_Values.Look(ref IsLocked, "isLocked", false);
            Scribe_Values.Look(ref DayLocked, "dayLocked", 0);
        }

        public string BuildPromptContext()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CAMPAIGN FRAME ===");
            sb.AppendLine($"Setting: {Setting}");
            sb.AppendLine($"Inciting Incident: {IncitingIncident}");
            sb.AppendLine($"Current Act: {CurrentAct}");
            if (ActiveForces.Count > 0)
                sb.AppendLine($"Active Forces: {string.Join(", ", ActiveForces)}");
            if (!string.IsNullOrEmpty(PendingThreat))
                sb.AppendLine($"Pending Threat: {PendingThreat}");
            if (!string.IsNullOrEmpty(Opportunity))
                sb.AppendLine($"Opportunity: {Opportunity}");
            if (PlantedSeeds.Count > 0)
            {
                sb.AppendLine("Planted Seeds:");
                foreach (var seed in PlantedSeeds.Where(s => !s.IsResolved))
                    sb.AppendLine($"  - {seed.Description}");
            }
            return sb.ToString();
        }
    }
}
