using System.Collections.Generic;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// An active narrative thread in the campaign story.
    /// </summary>
    public class StoryThread : IExposable
    {
        public string Id;
        public string Name;
        public string Description;
        public ThreadStatus Status;
        public int DayOpened;
        public int DayClosed;
        public List<string> RelatedBeatIds = new List<string>();
        public float DramaticWeight;

        public StoryThread() { }

        public StoryThread(string id, string name, string description, int dayOpened, float dramaticWeight = 1f)
        {
            Id = id;
            Name = name;
            Description = description;
            Status = ThreadStatus.Open;
            DayOpened = dayOpened;
            DramaticWeight = dramaticWeight;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref Description, "description");
            Scribe_Values.Look(ref Status, "status", ThreadStatus.Open);
            Scribe_Values.Look(ref DayOpened, "dayOpened");
            Scribe_Values.Look(ref DayClosed, "dayClosed", 0);
            Scribe_Collections.Look(ref RelatedBeatIds, "relatedBeatIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && RelatedBeatIds == null)
                RelatedBeatIds = new List<string>();
            Scribe_Values.Look(ref DramaticWeight, "dramaticWeight", 1f);
        }
    }

    public enum ThreadStatus
    {
        Open,
        Dormant,
        Closed,
        Abandoned
    }
}
