using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class DrugTools
    {
        /// <summary>
        /// List all drug policies and which colonists use them.
        /// </summary>
        public static string ListDrugPolicies()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var policiesArray = new JSONArray();

            // Get all drug policies from the database
            var allPolicies = Current.Game.drugPolicyDatabase.AllPolicies.ToList();

            // Get colonists grouped by their current drug policy
            var colonistsByPolicy = new Dictionary<DrugPolicy, List<Pawn>>();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.drugs == null) continue;

                var policy = pawn.drugs.CurrentPolicy;
                if (policy == null) continue;

                if (!colonistsByPolicy.ContainsKey(policy))
                    colonistsByPolicy[policy] = new List<Pawn>();

                colonistsByPolicy[policy].Add(pawn);
            }

            // Build policy info
            foreach (var policy in allPolicies)
            {
                var policyObj = new JSONObject();
                policyObj["name"] = policy.label;

                var assignedColonists = new JSONArray();
                if (colonistsByPolicy.ContainsKey(policy))
                {
                    foreach (var colonist in colonistsByPolicy[policy])
                    {
                        assignedColonists.Add(colonist.Name?.ToStringShort ?? "Unknown");
                    }
                }
                policyObj["assignedColonists"] = assignedColonists;
                policyObj["colonistCount"] = assignedColonists.Count;

                policiesArray.Add(policyObj);
            }

            result["policies"] = policiesArray;
            result["totalPolicies"] = policiesArray.Count;
            result["totalColonists"] = map.mapPawns.FreeColonists.Count();

            return result.ToString();
        }
    }
}
