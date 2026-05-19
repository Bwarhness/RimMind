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

        // Deep lore — populated by the scenario AI like a D&D session zero.
        // The storyteller engine doesn't read these directly, but they ground
        // every future story beat in a coherent world.
        public string WorldLore;          // 2-3 paragraph history of the world / region
        public string IdeologyName;       // becomes the in-game Ideo.name (RimWorld Ideology DLC)
        public string IdeologyDescription;// becomes the in-game Ideo.description
        public string RecentEvents;       // the political / historical lead-in before the colony begins
        public string TechLevel;          // tribal, medieval, industrial, spacer, glittertech, mixed
        public List<string> Themes = new List<string>();  // tonal motifs

        // Party / colony origin (the D&D "how did you meet" / "why are you together")
        public string ColonyOrigin;       // why these specific colonists ended up here
        public string HowTheyMet;         // the actual circumstances of the party forming
        public string SharedGoal;         // what binds them together
        public string InternalTension;    // what divides them despite the alliance

        public CampaignFrame() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Setting, "setting");
            Scribe_Values.Look(ref IncitingIncident, "incitingIncident");
            Scribe_Values.Look(ref CurrentAct, "currentAct", "Act I");
            Scribe_Values.Look(ref PendingThreat, "pendingThreat");
            Scribe_Values.Look(ref Opportunity, "opportunity");
            Scribe_Values.Look(ref UserPrompt, "userPrompt");
            Scribe_Values.Look(ref IsLocked, "isLocked", false);
            Scribe_Values.Look(ref DayLocked, "dayLocked", 0);
            Scribe_Values.Look(ref WorldLore, "worldLore");
            Scribe_Values.Look(ref IdeologyName, "ideologyName");
            Scribe_Values.Look(ref IdeologyDescription, "ideologyDescription");
            Scribe_Values.Look(ref RecentEvents, "recentEvents");
            Scribe_Values.Look(ref TechLevel, "techLevel");
            Scribe_Values.Look(ref ColonyOrigin, "colonyOrigin");
            Scribe_Values.Look(ref HowTheyMet, "howTheyMet");
            Scribe_Values.Look(ref SharedGoal, "sharedGoal");
            Scribe_Values.Look(ref InternalTension, "internalTension");
            Scribe_Collections.Look(ref ActiveForces, "activeForces", LookMode.Value);
            Scribe_Collections.Look(ref PlantedSeeds, "plantedSeeds", LookMode.Deep);
            Scribe_Collections.Look(ref Themes, "themes", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (ActiveForces == null) ActiveForces = new List<string>();
                if (PlantedSeeds == null) PlantedSeeds = new List<NarrativeSeed>();
                if (Themes == null) Themes = new List<string>();
            }
        }

        public string BuildPromptContext()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CAMPAIGN FRAME ===");
            sb.AppendLine($"Setting: {Setting}");
            if (!string.IsNullOrEmpty(TechLevel))
                sb.AppendLine($"Tech Level: {TechLevel}");
            if (!string.IsNullOrEmpty(WorldLore))
                sb.AppendLine($"World Lore: {WorldLore}");
            if (!string.IsNullOrEmpty(IdeologyName) || !string.IsNullOrEmpty(IdeologyDescription))
                sb.AppendLine($"Ideology: {IdeologyName} — {IdeologyDescription}");
            if (!string.IsNullOrEmpty(ColonyOrigin))
                sb.AppendLine($"Colony Origin: {ColonyOrigin}");
            sb.AppendLine($"Inciting Incident: {IncitingIncident}");
            sb.AppendLine($"Current Act: {CurrentAct}");
            if (Themes != null && Themes.Count > 0)
                sb.AppendLine($"Themes: {string.Join(", ", Themes)}");
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
