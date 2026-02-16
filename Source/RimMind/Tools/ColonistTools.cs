using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ColonistTools
    {
        public static string ListColonists()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            var arr = new JSONArray();

            foreach (var pawn in colonists)
            {
                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                obj["mood"] = pawn.needs?.mood?.CurLevelPercentage.ToString("P0") ?? "N/A";
                obj["currentJob"] = pawn.CurJobDef?.reportString ?? pawn.CurJobDef?.defName ?? "Idle";

                if (pawn.MentalStateDef != null)
                    obj["mentalState"] = pawn.MentalStateDef.label;

                if (pawn.Downed)
                    obj["status"] = "Downed";
                else if (pawn.InMentalState)
                    obj["status"] = "Mental break";

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["colonists"] = arr;
            result["count"] = colonists.Count();
            return result.ToString();
        }

        public static string GetColonistDetails(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringFull ?? "Unknown";
            obj["nickname"] = pawn.Name?.ToStringShort ?? "Unknown";
            obj["age"] = pawn.ageTracker.AgeBiologicalYears;
            obj["gender"] = pawn.gender.ToString();

            // Backstory
            if (pawn.story != null)
            {
                obj["childhood"] = pawn.story.Childhood?.TitleFor(pawn.gender).ToString() ?? "Unknown";
                obj["adulthood"] = pawn.story.Adulthood?.TitleFor(pawn.gender).ToString() ?? "None";
            }

            // Traits
            if (pawn.story?.traits != null)
            {
                var traits = new JSONArray();
                foreach (var trait in pawn.story.traits.allTraits)
                    traits.Add(trait.LabelCap.ToString());
                obj["traits"] = traits;
            }

            // Skills
            if (pawn.skills != null)
            {
                var skills = new JSONObject();
                foreach (var skill in pawn.skills.skills)
                {
                    string passion = skill.passion == Passion.None ? "" : skill.passion == Passion.Minor ? " (interested)" : " (passionate)";
                    skills[skill.def.defName] = skill.Level + passion;
                }
                obj["skills"] = skills;
            }

            // Mood & Needs
            if (pawn.needs != null)
            {
                var needs = new JSONObject();
                if (pawn.needs.mood != null)
                    needs["mood"] = pawn.needs.mood.CurLevelPercentage.ToString("P0");
                if (pawn.needs.food != null)
                    needs["food"] = pawn.needs.food.CurLevelPercentage.ToString("P0");
                if (pawn.needs.rest != null)
                    needs["rest"] = pawn.needs.rest.CurLevelPercentage.ToString("P0");
                if (pawn.needs.joy != null)
                    needs["joy"] = pawn.needs.joy.CurLevelPercentage.ToString("P0");
                obj["needs"] = needs;
            }

            // Active thoughts
            if (pawn.needs?.mood?.thoughts?.memories != null)
            {
                var thoughts = new JSONArray();
                foreach (var thought in pawn.needs.mood.thoughts.memories.Memories.Take(10))
                {
                    thoughts.Add(thought.LabelCap.ToString() + " (" + thought.MoodOffset().ToString("+0.#;-0.#") + ")");
                }
                obj["thoughts"] = thoughts;
            }

            obj["currentJob"] = pawn.CurJobDef?.reportString ?? "Idle";
            return obj.ToString();
        }

        public static string GetColonistHealth(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

            if (pawn.health != null)
            {
                obj["overallCondition"] = pawn.health.State.ToString();

                // Hediffs (injuries, diseases, bionics)
                var injuries = new JSONArray();
                var diseases = new JSONArray();
                var bionics = new JSONArray();

                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    string entry = hediff.LabelCap.ToString();
                    if (hediff.Part != null)
                        entry += " (" + hediff.Part.Label + ")";

                    if (hediff is Hediff_Injury)
                        injuries.Add(entry);
                    else if (hediff is Hediff_AddedPart || hediff is Hediff_Implant)
                        bionics.Add(entry);
                    else if (hediff.def.makesSickThought || hediff.def.lethalSeverity > 0)
                        diseases.Add(entry);
                }

                if (injuries.Count > 0) obj["injuries"] = injuries;
                if (diseases.Count > 0) obj["diseases"] = diseases;
                if (bionics.Count > 0) obj["bionics"] = bionics;

                // Pain & consciousness
                obj["painLevel"] = pawn.health.hediffSet.PainTotal.ToString("P0");

                var consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                obj["consciousness"] = consciousness.ToString("P0");

                var moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
                obj["movementCapacity"] = moving.ToString("P0");

                var manipulation = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
                obj["manipulation"] = manipulation.ToString("P0");
            }

            return obj.ToString();
        }

        public static string DraftColonist(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.drafter == null) return ToolExecutor.JsonError("Colonist cannot be drafted.");
            if (pawn.Downed) return ToolExecutor.JsonError("Colonist is downed and cannot be drafted.");

            pawn.drafter.Drafted = true;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["status"] = "drafted";
            return result.ToString();
        }

        public static string UndraftColonist(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.drafter == null) return ToolExecutor.JsonError("Colonist cannot be drafted.");

            pawn.drafter.Drafted = false;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["status"] = "undrafted";
            return result.ToString();
        }

        public static string DraftAll()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            int drafted = 0;
            int failed = 0;

            foreach (var pawn in colonists)
            {
                if (pawn.drafter != null && !pawn.Downed)
                {
                    pawn.drafter.Drafted = true;
                    drafted++;
                }
                else
                {
                    failed++;
                }
            }

            var result = new JSONObject();
            result["success"] = true;
            result["drafted"] = drafted;
            result["failed"] = failed;
            result["total"] = colonists.Count();
            return result.ToString();
        }

        public static string UndraftAll()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            int undrafted = 0;

            foreach (var pawn in colonists)
            {
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = false;
                    undrafted++;
                }
            }

            var result = new JSONObject();
            result["success"] = true;
            result["undrafted"] = undrafted;
            result["total"] = colonists.Count();
            return result.ToString();
        }

        public static Pawn FindPawnByName(string name)
        {
            var map = Find.CurrentMap;
            if (map == null) return null;

            string lower = name.ToLower();
            return map.mapPawns.FreeColonists
                .FirstOrDefault(p =>
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.Name?.ToStringFull?.ToLower().Contains(lower) == true ||
                    p.LabelShort?.ToLower() == lower);
        }
    }
}
