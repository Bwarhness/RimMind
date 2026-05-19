using System.Collections.Generic;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// A planted narrative seed that may grow into a future story beat.
    /// </summary>
    public class NarrativeSeed : IExposable
    {
        public string Id;
        public string Description;
        public string SuggestedIncidentDefName;
        public int DayPlanted;
        public bool IsResolved;
        public int DayResolved;
        public string ResolutionBeatId;

        public NarrativeSeed() { }

        public NarrativeSeed(string id, string description, string suggestedIncidentDefName, int dayPlanted)
        {
            Id = id;
            Description = description;
            SuggestedIncidentDefName = suggestedIncidentDefName;
            DayPlanted = dayPlanted;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Description, "description");
            Scribe_Values.Look(ref SuggestedIncidentDefName, "suggestedIncidentDefName");
            Scribe_Values.Look(ref DayPlanted, "dayPlanted");
            Scribe_Values.Look(ref IsResolved, "isResolved", false);
            Scribe_Values.Look(ref DayResolved, "dayResolved", 0);
            Scribe_Values.Look(ref ResolutionBeatId, "resolutionBeatId");
        }
    }
}
