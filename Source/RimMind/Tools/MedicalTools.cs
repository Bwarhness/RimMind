using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class MedicalTools
    {
        public static string GetMedicalOverview()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var obj = new JSONObject();

            // Patients needing treatment
            var patients = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                bool needsTending = pawn.health.HasHediffsNeedingTend();
                bool downed = pawn.Downed;
                bool hasSeriousCondition = pawn.health.hediffSet.hediffs
                    .Any(h => h.CurStage?.lifeThreatening == true || h.def.lethalSeverity > 0);

                if (needsTending || downed || hasSeriousCondition)
                {
                    var p = new JSONObject();
                    p["name"] = pawn.Name?.ToStringShort ?? "Unknown";

                    var conditions = new JSONArray();
                    foreach (var h in pawn.health.hediffSet.hediffs)
                    {
                        if (h is Hediff_Injury || h.def.makesSickThought || h.def.lethalSeverity > 0)
                        {
                            string desc = h.LabelCap.ToString();
                            if (h.Part != null) desc += " (" + h.Part.Label + ")";
                            conditions.Add(desc);
                        }
                    }
                    p["conditions"] = conditions;
                    p["needsTending"] = needsTending;
                    p["downed"] = downed;
                    p["lifeThreatening"] = hasSeriousCondition;

                    patients.Add(p);
                }
            }
            obj["patientsNeedingCare"] = patients;

            // Medicine supply
            var medicine = new JSONObject();
            medicine["herbal"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineHerbal).Sum(t => t.stackCount);
            medicine["industrial"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineIndustrial).Sum(t => t.stackCount);
            medicine["glitterworld"] = map.listerThings.ThingsOfDef(ThingDefOf.MedicineUltratech).Sum(t => t.stackCount);
            obj["medicineSupply"] = medicine;

            // Medical beds
            int medBeds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Medical);
            int freeMedBeds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Medical && !b.AnyOccupants);
            obj["medicalBeds"] = medBeds;
            obj["freeMedicalBeds"] = freeMedBeds;

            // Doctors
            var doctors = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                var medSkill = pawn.skills?.GetSkill(SkillDefOf.Medicine);
                if (medSkill != null && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    var d = new JSONObject();
                    d["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    d["medicalSkill"] = medSkill.Level;
                    d["passion"] = medSkill.passion.ToString();
                    doctors.Add(d);
                }
            }
            obj["doctors"] = doctors;

            return obj.ToString();
        }
    }
}
