using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public class StorytellerCompProperties_AIStoryteller : StorytellerCompProperties
    {
        public SimpleCurve curveIncidents;

        public StorytellerCompProperties_AIStoryteller()
        {
            compClass = typeof(AIStorytellerComp);
        }
    }
}
