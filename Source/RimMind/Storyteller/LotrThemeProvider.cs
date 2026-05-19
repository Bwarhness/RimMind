using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// "LOTR" themed storyteller — epic fantasy voice, inspired by Tolkien's prose style.
    /// Proof-of-concept for the theme system.
    /// </summary>
    public class LotrThemeProvider : IThemeProvider
    {
        public string ThemeId => "lotr";
        public string ThemeName => "The Lay of the Ring";

        public string SystemPrompt =>
            "You are a Loremaster, an ancient voice recounting the deeds of a colony upon a perilous frontier. " +
            "You speak with the gravity of Tolkien's prose: elevated diction, echoes of old songs, " +
            "and a sense that greater powers move behind the events of men. " +
            "Name things with care — a raid is not merely a raid, but 'the shadow falling upon the settlement.' " +
            "Let each event feel like a verse in a longer lay.";

        public string PlannerPrompt =>
            "You are weaving the next 3 verses of a lay — the ongoing tale of a colony upon the edge of ruin. " +
            "Plan story beats that feel like chapters in an epic: shadow gathering, unexpected aid, " +
            "doom foretold, small victories against great darkness. " +
            "Each beat should resonate with what came before and cast shadows forward.\n\n" +
            "Respond with a JSON array of 3 beats. Each beat must have:\n" +
            "- whatHappened: the event in epic terms\n" +
            "- narrativeSignificance: why this matters to the greater tale\n" +
            "- consequenceTag: a poetic label ('the shadow deepens', 'a light in darkness', 'doom foretold')\n" +
            "- opensThreads: threads awakened by this beat\n" +
            "- closesThreads: threads resolved by this beat\n" +
            "- plantsSeeds: seeds sown for future verses\n" +
            "- suggestedIncidentDefName: a RimWorld IncidentDef name\n" +
            "- suggestedPoints: intensity (0-10000)\n";

        public string CampaignPrompt =>
            "You are a Loremaster designing the opening canto of an epic. " +
            "Given the player's prompt, craft a campaign frame in the style of the Red Book of Westmarch:\n" +
            "- setting: the lands and their character\n" +
            "- incitingIncident: the deed that set these folk upon this road\n" +
            "- activeForces: the powers, kindreds, and shadows at play\n" +
            "- currentAct: the verse of the tale (e.g. 'The Departure from the West')\n" +
            "- pendingThreat: the shadow drawing near\n" +
            "- opportunity: the hope that gleams afar\n" +
            "- plantedSeeds: 3-5 threads to be answered in later verses\n\n" +
            "Respond in JSON with these exact field names.";

        public string EventFramePrompt =>
            "You are a Loremaster recounting a deed of war or fortune. " +
            "Rewrite the event's letter label and text in elevated, Tolkien-esque prose. " +
            "Preserve all gameplay facts accurately beneath the poetry.";

        public string OutcomePrompt =>
            "Record the outcome of this verse in the lay. " +
            "How did the tale turn? What shadow grew or light faded? " +
            "Be brief — a single paragraph of loremaster's notes.";

        public string FrameLetterLabel(string incidentDefName, string baseLabel)
        {
            string styled;
            string lower = incidentDefName.ToLower();
            if (lower == "raidenemy")
                styled = "The Shadow Falls Upon the Settlement";
            else if (lower == "wandererjoin")
                styled = "A New Companion Upon the Road";
            else if (lower == "toxicfallout")
                styled = "A Dark Wind Blows from the East";
            else if (lower == "infestation")
                styled = "Evil Beneath the Earth Awakens";
            else if (lower == "solarflare")
                styled = "The Sun Rages in Anger";
            else if (lower == "eclipse")
                styled = "The Day Grows Dim and Cold";
            else if (lower == "coldsnap")
                styled = "The Frost of Long Winters";
            else if (lower == "heatwave")
                styled = "A Fierce Fire from the Sky";
            else if (lower == "firestarted")
                styled = "Flame Wakens in the Halls";
            else
                styled = baseLabel;
            return styled;
        }

        public string FrameLetterText(string incidentDefName, string baseText, PlotBeat beat)
        {
            if (beat == null)
                return baseText;

            var intro = string.IsNullOrEmpty(beat.NarrativeSignificance)
                ? "Thus it was in those days..."
                : beat.NarrativeSignificance;

            return $"{intro}\n\n{baseText}\n\n'Not all those who wander are lost,' the old proverb says. Yet in these hours, doubt creeps upon the heart.";
        }

        public string NameThread(string concept)
        {
            var names = new string[]
            {
                $"The Shadow of {concept}",
                $"The Lay of {concept}",
                $"The Doom of {concept}",
                $"The Siege of {concept}",
                $"The Pilgrimage to {concept}"
            };
            return names[Rand.Range(0, names.Length)];
        }
    }
}
