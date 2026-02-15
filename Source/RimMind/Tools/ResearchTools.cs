using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ResearchTools
    {
        public static string GetResearchStatus()
        {
            var obj = new JSONObject();
            var manager = Find.ResearchManager;

            var currentProject = Find.ResearchManager.GetProject();
            if (currentProject != null)
            {
                obj["currentProject"] = currentProject.LabelCap.ToString();
                obj["progress"] = (manager.GetProgress(currentProject) / currentProject.baseCost * 100f).ToString("F1") + "%";
                obj["totalCost"] = currentProject.baseCost.ToString("F0");
            }
            else
            {
                obj["currentProject"] = "None";
            }

            obj["techLevel"] = Faction.OfPlayer.def.techLevel.ToString();

            // Research benches
            var map = Find.CurrentMap;
            if (map != null)
            {
                int benches = map.listerBuildings.allBuildingsColonist
                    .Count(b => b.def.defName.Contains("ResearchBench") || b.def.defName.Contains("HiTechResearchBench"));
                obj["researchBenches"] = benches;
            }

            return obj.ToString();
        }

        public static string GetAvailableResearch()
        {
            var manager = Find.ResearchManager;
            var arr = new JSONArray();

            foreach (var project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (project.IsFinished) continue;
                if (!project.PrerequisitesCompleted) continue;

                var obj = new JSONObject();
                obj["name"] = project.LabelCap.ToString();
                obj["description"] = project.description?.Substring(0, System.Math.Min(project.description.Length, 100)) ?? "";
                obj["cost"] = project.baseCost.ToString("F0");
                obj["techLevel"] = project.techLevel.ToString();

                float progress = manager.GetProgress(project);
                if (progress > 0)
                    obj["progress"] = (progress / project.baseCost * 100f).ToString("F1") + "%";

                if (project.prerequisites != null && project.prerequisites.Count > 0)
                {
                    var prereqs = new JSONArray();
                    foreach (var prereq in project.prerequisites)
                        prereqs.Add(prereq.LabelCap.ToString());
                    obj["prerequisites"] = prereqs;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["availableResearch"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetCompletedResearch()
        {
            var arr = new JSONArray();

            foreach (var project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (!project.IsFinished) continue;
                arr.Add(project.LabelCap.ToString());
            }

            var result = new JSONObject();
            result["completedResearch"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }
    }
}
