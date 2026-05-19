using System.Collections.Generic;
using RimMind.Storyteller;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// Structured AI response describing the full pre-game state:
    /// narrative campaign frame + concrete starting pawns/items + biome hint.
    /// Serialized into the save via ScenPart_RimMindCampaign.
    /// </summary>
    public class RimMindScenarioPlan : IExposable
    {
        public string UserPrompt;
        public CampaignFrame Campaign;
        public List<PawnSpec> Pawns = new List<PawnSpec>();
        public List<ItemSpec> Items = new List<ItemSpec>();

        // Hints for the player — applied opportunistically, not forced.
        public string BiomeHint;     // BiomeDef defName, e.g. "TemperateForest"
        public string SeasonHint;    // e.g. "Spring", "Winter"
        public string LocationHint;  // freeform — "near the equator", "mountain valley", etc.

        public bool HasContent => Campaign != null
            || (Pawns != null && Pawns.Count > 0)
            || (Items != null && Items.Count > 0);

        public void ExposeData()
        {
            Scribe_Values.Look(ref UserPrompt, "userPrompt");
            Scribe_Deep.Look(ref Campaign, "campaign");
            Scribe_Collections.Look(ref Pawns, "pawns", LookMode.Deep);
            Scribe_Collections.Look(ref Items, "items", LookMode.Deep);
            Scribe_Values.Look(ref BiomeHint, "biomeHint");
            Scribe_Values.Look(ref SeasonHint, "seasonHint");
            Scribe_Values.Look(ref LocationHint, "locationHint");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (Pawns == null) Pawns = new List<PawnSpec>();
                if (Items == null) Items = new List<ItemSpec>();
            }
        }
    }

    public class PawnSpec : IExposable
    {
        public string FirstName;
        public string NickName;
        public string LastName;
        public string Gender;             // "Male" / "Female" / null = random
        public int? Age;                  // optional biological age in years
        public string Appearance;         // physical description (1-2 sentences)
        public string Xenotype;                 // vanilla XenotypeDef defName OR a custom name when XenotypeGenes is populated
        public List<string> XenotypeGenes = new List<string>();  // GeneDef defNames; non-empty -> treat Xenotype as custom
        public string ChildhoodBackstory; // multi-paragraph prose
        public string AdulthoodBackstory; // multi-paragraph prose
        public string DefiningMoment;     // one pivotal event that shaped them
        public List<string> Traits = new List<string>();           // TraitDef defNames
        public Dictionary<string, int> Skills = new Dictionary<string, int>(); // SkillDef defName -> level
        public Dictionary<string, string> Passions = new Dictionary<string, string>(); // SkillDef defName -> "Minor"/"Major"
        public string Narrative;          // short tagline summary

        public void ExposeData()
        {
            Scribe_Values.Look(ref FirstName, "firstName");
            Scribe_Values.Look(ref NickName, "nickName");
            Scribe_Values.Look(ref LastName, "lastName");
            Scribe_Values.Look(ref Gender, "gender");
            Scribe_Values.Look(ref Age, "age");
            Scribe_Values.Look(ref Appearance, "appearance");
            Scribe_Values.Look(ref Xenotype, "xenotype");
            Scribe_Collections.Look(ref XenotypeGenes, "xenotypeGenes", LookMode.Value);
            Scribe_Values.Look(ref ChildhoodBackstory, "childhoodBackstory");
            Scribe_Values.Look(ref AdulthoodBackstory, "adulthoodBackstory");
            Scribe_Values.Look(ref DefiningMoment, "definingMoment");
            Scribe_Collections.Look(ref Traits, "traits", LookMode.Value);
            Scribe_Collections.Look(ref Skills, "skills", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref Passions, "passions", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref Narrative, "narrative");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (Traits == null) Traits = new List<string>();
                if (Skills == null) Skills = new Dictionary<string, int>();
                if (Passions == null) Passions = new Dictionary<string, string>();
                if (XenotypeGenes == null) XenotypeGenes = new List<string>();
            }
        }
    }

    public class ItemSpec : IExposable
    {
        public string DefName;
        public int Count = 1;
        public string Stuff; // Material defName, optional

        public void ExposeData()
        {
            Scribe_Values.Look(ref DefName, "defName");
            Scribe_Values.Look(ref Count, "count", 1);
            Scribe_Values.Look(ref Stuff, "stuff");
        }
    }
}
