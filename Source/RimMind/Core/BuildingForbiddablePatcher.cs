using System.Linq;
using RimWorld;
using Verse;

namespace RimMind.Core
{
    [StaticConstructorOnStartup]
    public static class BuildingForbiddablePatcher
    {
        static BuildingForbiddablePatcher()
        {
            int patched = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (!typeof(ThingWithComps).IsAssignableFrom(def.thingClass)) continue;
                bool isBuilding = def.category == ThingCategory.Building;
                bool isBlueprint = typeof(Blueprint).IsAssignableFrom(def.thingClass);
                if (!isBuilding && !isBlueprint) continue;
                if (def.comps == null) continue;
                if (def.comps.Any(c => c.compClass == typeof(CompForbiddable))) continue;

                def.comps.Add(new CompProperties { compClass = typeof(CompForbiddable) });
                patched++;
            }
            Log.Message("[RimMind] Added CompForbiddable to " + patched + " building/blueprint defs.");
        }
    }
}
