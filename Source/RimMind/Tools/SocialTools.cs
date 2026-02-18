using System;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class SocialTools
    {
        public static string GetRelationships(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = ColonistTools.FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

            var relations = new JSONArray();
            var map = Find.CurrentMap;
            if (map != null && pawn.relations != null)
            {
                foreach (var other in map.mapPawns.FreeColonists)
                {
                    if (other == pawn) continue;

                    var rel = new JSONObject();
                    rel["name"] = other.Name?.ToStringShort ?? "Unknown";
                    rel["opinionOfThem"] = pawn.relations.OpinionOf(other);
                    rel["theirOpinionOfMe"] = other.relations.OpinionOf(pawn);

                    // Get direct relations
                    var directRelations = pawn.relations.DirectRelations
                        .Where(r => r.otherPawn == other)
                        .Select(r => r.def.label)
                        .ToList();

                    if (directRelations.Count > 0)
                    {
                        var relTypes = new JSONArray();
                        foreach (var r in directRelations) relTypes.Add(r);
                        rel["relationTypes"] = relTypes;
                    }

                    relations.Add(rel);
                }
            }

            obj["relationships"] = relations;
            return obj.ToString();
        }

        public static string GetFactionRelations()
        {
            var arr = new JSONArray();

            foreach (var faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (faction.IsPlayer) continue;

                var obj = new JSONObject();
                obj["name"] = faction.Name;
                obj["type"] = faction.def.label;
                obj["goodwill"] = faction.PlayerGoodwill;

                if (faction.HostileTo(Faction.OfPlayer))
                    obj["status"] = "Hostile";
                else if (faction.PlayerGoodwill >= 75)
                    obj["status"] = "Ally";
                else
                    obj["status"] = "Neutral";

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["factions"] = arr;
            return result.ToString();
        }

        public static string GetSocialRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var riskPairs = new JSONArray();

            var colonists = map.mapPawns.FreeColonists.ToList();

            foreach (var pawn1 in colonists)
            {
                if (pawn1.relations == null) continue;

                foreach (var pawn2 in colonists)
                {
                    if (pawn1 == pawn2) continue;
                    if (pawn1.ThingID.CompareTo(pawn2.ThingID) >= 0) continue; // Avoid duplicates

                    int opinion1 = pawn1.relations.OpinionOf(pawn2);
                    int opinion2 = pawn2.relations.OpinionOf(pawn1);

                    // Flag pairs where either has opinion < -20
                    if (opinion1 < -20 || opinion2 < -20)
                    {
                        var pair = new JSONObject();
                        pair["colonist1"] = pawn1.Name?.ToStringShort ?? "Unknown";
                        pair["colonist2"] = pawn2.Name?.ToStringShort ?? "Unknown";
                        pair["opinion1of2"] = opinion1;
                        pair["opinion2of1"] = opinion2;

                        // Calculate mutual hostility
                        int mutualHostility = Math.Min(opinion1, opinion2);
                        pair["mutualHostility"] = mutualHostility;

                        // Check for volatile/abrasive traits
                        var riskFactors = new JSONArray();

                        if (pawn1.story?.traits != null)
                        {
                            foreach (var trait in pawn1.story.traits.allTraits)
                            {
                                if (trait.def.defName == "Abrasive" || 
                                    trait.def.defName == "Volatile" ||
                                    trait.def.defName == "Bloodlust" ||
                                    trait.def.defName == "Psychopath")
                                {
                                    riskFactors.Add(pawn1.Name.ToStringShort + " is " + trait.LabelCap);
                                }
                            }
                        }

                        if (pawn2.story?.traits != null)
                        {
                            foreach (var trait in pawn2.story.traits.allTraits)
                            {
                                if (trait.def.defName == "Abrasive" || 
                                    trait.def.defName == "Volatile" ||
                                    trait.def.defName == "Bloodlust" ||
                                    trait.def.defName == "Psychopath")
                                {
                                    riskFactors.Add(pawn2.Name.ToStringShort + " is " + trait.LabelCap);
                                }
                            }
                        }

                        if (riskFactors.Count > 0)
                            pair["riskFactors"] = riskFactors;

                        // Determine risk level
                        string riskLevel = "low";
                        if (mutualHostility < -50 || (mutualHostility < -30 && riskFactors.Count > 0))
                            riskLevel = "critical";
                        else if (mutualHostility < -40 || riskFactors.Count > 0)
                            riskLevel = "high";
                        else if (mutualHostility < -30)
                            riskLevel = "moderate";

                        pair["riskLevel"] = riskLevel;

                        // Suggest interventions
                        var interventions = new JSONArray();
                        interventions.Add("Avoid scheduling shared recreation time");
                        interventions.Add("Assign to different work areas");
                        interventions.Add("Keep them in separate bedrooms far apart");
                        
                        if (opinion1 < -40 || opinion2 < -40)
                            interventions.Add("Consider separate zones/areas");
                        
                        if (riskFactors.Count > 0)
                            interventions.Add("Monitor closely - trait-related violence risk");

                        pair["suggestedInterventions"] = interventions;

                        riskPairs.Add(pair);
                    }
                }
            }

            result["riskPairs"] = riskPairs;
            result["totalRiskPairs"] = riskPairs.Count;

            // Summary
            if (riskPairs.Count == 0)
            {
                result["summary"] = "No significant social conflicts detected.";
            }
            else
            {
                var criticalPairs = new JSONArray();
                var highRiskPairs = new JSONArray();

                foreach (JSONObject pair in riskPairs)
                {
                    string level = pair["riskLevel"]?.Value;
                    string summary = pair["colonist1"].Value + " ↔ " + pair["colonist2"].Value;

                    if (level == "critical")
                        criticalPairs.Add(summary);
                    else if (level == "high")
                        highRiskPairs.Add(summary);
                }

                if (criticalPairs.Count > 0)
                    result["criticalConflicts"] = criticalPairs;
                if (highRiskPairs.Count > 0)
                    result["highRiskConflicts"] = highRiskPairs;
            }

            return result.ToString();
        }
    }
}
