using RimMind.Languages;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.UI
{
    /// <summary>
    /// Designator that appears in the Architect panel's RimMind tab.
    /// When clicked on a map location, opens a dialog to send a location-aware query to the AI.
    /// </summary>
    public class Designator_RimMindQuery : Designator
    {
        public Designator_RimMindQuery()
        {
            defaultLabel = RimMindTranslations.Get("RimMind_DesignatorLabel");
            defaultDesc = RimMindTranslations.Get("RimMind_DesignatorDesc");
            icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? TexCommand.Attack;
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd_Stockpile;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            // Any cell on the map is valid for querying
            if (!loc.InBounds(Map))
                return false;
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            Find.WindowStack.Add(new Dialog_RimMindLocationQuery(loc, Map));
        }

        // Don't draw any designation markers - this is a one-shot query, not a persistent designation
        protected override void FinalizeDesignationSucceeded()
        {
            // Intentionally empty - don't call base to avoid placing a designation marker
        }
    }
}
