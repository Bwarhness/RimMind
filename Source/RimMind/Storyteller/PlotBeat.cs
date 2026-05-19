using System.Collections.Generic;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// A single beat in the plot graph — something that happened or is planned to happen.
    /// </summary>
    public class PlotBeat : IExposable
    {
        public string Id;
        public string WhatHappened;
        public string NarrativeSignificance;
        public string ConsequenceTag;
        public List<string> OpensThreads = new List<string>();
        public List<string> ClosesThreads = new List<string>();
        public List<string> PlantsSeeds = new List<string>();
        public int DayExecuted;
        public bool WasExecuted;
        public string IncidentDefName;
        public string IncidentOutcome;
        public float ActualSeverity;

        public PlotBeat() { }

        public PlotBeat(string id, string whatHappened, string narrativeSignificance, string consequenceTag)
        {
            Id = id;
            WhatHappened = whatHappened;
            NarrativeSignificance = narrativeSignificance;
            ConsequenceTag = consequenceTag;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref WhatHappened, "whatHappened");
            Scribe_Values.Look(ref NarrativeSignificance, "narrativeSignificance");
            Scribe_Values.Look(ref ConsequenceTag, "consequenceTag");
            Scribe_Collections.Look(ref OpensThreads, "opensThreads", LookMode.Value);
            Scribe_Collections.Look(ref ClosesThreads, "closesThreads", LookMode.Value);
            Scribe_Collections.Look(ref PlantsSeeds, "plantsSeeds", LookMode.Value);
            Scribe_Values.Look(ref DayExecuted, "dayExecuted", 0);
            Scribe_Values.Look(ref WasExecuted, "wasExecuted", false);
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName");
            Scribe_Values.Look(ref IncidentOutcome, "incidentOutcome");
            Scribe_Values.Look(ref ActualSeverity, "actualSeverity", 0f);
        }
    }
}
