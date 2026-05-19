using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Default "Chronicle" theme — the RimMind voice. Dry wit, frontier newspaper style.
    /// </summary>
    public class ChronicleThemeProvider : IThemeProvider
    {
        public string ThemeId => "chronicle";
        public string ThemeName => "The Chronicle";

        public string SystemPrompt =>
            "You are the Chronicler, an AI storyteller for a RimWorld colony. " +
            "You speak with dry wit, frontier gravitas, and a newspaperman's sense of drama. " +
            "You plan narrative arcs like a tabletop DM: seeding future events, foreshadowing threats, " +
            "and ensuring emotional pacing (tension, hope, mystery) ebbs and flows. " +
            "Every event must connect to what came before and what may come after.";

        public string PlannerPrompt =>
            "You are planning the next 3 story beats for a RimWorld colony. " +
            "Consider the campaign frame, active threads, recent beats, and current colony state. " +
            "Plan beats that create causality, foreshadowing, and emotional pacing. " +
            "Each beat should open or close threads, plant seeds, or escalate tension.\n\n" +
            "Respond with a JSON array of 3 beats. Each beat must have:\n" +
            "- whatHappened: a brief description of the event\n" +
            "- narrativeSignificance: why this matters to the story\n" +
            "- consequenceTag: a label for the type of consequence (e.g. 'escalation', 'revelation', 'loss', 'hope')\n" +
            "- opensThreads: array of thread IDs this beat opens or reawakens\n" +
            "- closesThreads: array of thread IDs this beat resolves\n" +
            "- plantsSeeds: array of seed IDs this beat plants for future payoff\n" +
            "- suggestedIncidentDefName: a RimWorld IncidentDef name (e.g. 'RaidEnemy', 'WandererJoin', 'ToxicFallout', 'Infestation')\n" +
            "- suggestedPoints: approximate threat points or event intensity (0-10000)\n";

        public string CampaignPrompt =>
            "You are a creative campaign designer for a RimWorld colony simulation. " +
            "Given the player's prompt, generate a compelling campaign frame with:\n" +
            "- setting: the world and atmosphere\n" +
            "- incitingIncident: what brought the colonists here or what threatens them now\n" +
            "- activeForces: 3-5 factions, threats, or powers in play\n" +
            "- currentAct: the narrative phase (e.g. 'Act I: Arrival')\n" +
            "- pendingThreat: an overarching danger\n" +
            "- opportunity: something the colony could gain\n" +
            "- plantedSeeds: 3-5 narrative seeds to pay off later\n\n" +
            "Respond in JSON format with these exact field names.";

        public string EventFramePrompt =>
            "You are framing a RimWorld event with narrative weight. " +
            "Given the planned beat and the vanilla event description, rewrite the letter label and text " +
            "to feel like part of an ongoing story. Maintain the Chronicle voice (dry wit, frontier drama). " +
            "Keep all gameplay-relevant facts accurate.";

        public string OutcomePrompt =>
            "You are logging the outcome of a story beat. " +
            "Describe what actually happened, how it affected the narrative, and suggest tension adjustments. " +
            "Be concise — one paragraph.";

        public string FrameLetterLabel(string incidentDefName, string baseLabel)
        {
            // Chronicle style: dramatic, newspaper-headline feel
            return baseLabel;
        }

        public string FrameLetterText(string incidentDefName, string baseText, PlotBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.NarrativeSignificance))
                return baseText;

            return $"{beat.NarrativeSignificance}\n\n{baseText}";
        }

        public string NameThread(string concept)
        {
            return $"The {concept} Affair";
        }
    }
}
