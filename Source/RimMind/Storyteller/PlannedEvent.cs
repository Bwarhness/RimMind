using RimWorld;
using System.Linq;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// A planned narrative event waiting to be executed by the storyteller.
    /// </summary>
    public class PlannedEvent : IExposable
    {
        public string Id;
        public string BeatId;
        public string IncidentDefName;
        public string NarrativeLabel;
        public string NarrativeText;
        public float NarrativeWeight;
        public int PlannedDay;
        public bool WasFired;
        public int FireDay;
        public string TargetTag;
        public float Points;

        public PlannedEvent() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref BeatId, "beatId");
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName");
            Scribe_Values.Look(ref NarrativeLabel, "narrativeLabel");
            Scribe_Values.Look(ref NarrativeText, "narrativeText");
            Scribe_Values.Look(ref NarrativeWeight, "narrativeWeight", 1f);
            Scribe_Values.Look(ref PlannedDay, "plannedDay", 0);
            Scribe_Values.Look(ref WasFired, "wasFired", false);
            Scribe_Values.Look(ref FireDay, "fireDay", 0);
            Scribe_Values.Look(ref TargetTag, "targetTag", "Map_PlayerHome");
            Scribe_Values.Look(ref Points, "points", 0f);
        }

        public FiringIncident ToFiringIncident()
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed(IncidentDefName, false);
            if (def == null)
            {
                Log.Warning($"[RimMind] PlannedEvent references unknown IncidentDef: {IncidentDefName}");
                // Fallback to a generic threat if possible
                def = DefDatabase<IncidentDef>.GetNamed("RaidEnemy", false);
                if (def == null)
                    return null;
            }

            // Determine target based on incident's targetTags
            Map targetMap = null;
            if (def.targetTags.Any(tag => tag == TargetTagDefOf.World))
            {
                // World-targeted incident
                targetMap = null;
            }
            else
            {
                // Map-targeted incident - use current map or first player home map
                targetMap = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            }

            var parms = StorytellerUtility.DefaultParmsNow(def.category, targetMap);
            parms.points = Points > 0 ? Points : parms.points;
            parms.target = targetMap;

            // Set faction if relevant
            if (def.category == IncidentCategoryDefOf.ThreatBig || def.category == IncidentCategoryDefOf.ThreatSmall)
            {
                if (parms.faction == null)
                {
                    parms.faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Undefined);
                }
            }

            return new FiringIncident(def, null, parms);
        }
    }
}
