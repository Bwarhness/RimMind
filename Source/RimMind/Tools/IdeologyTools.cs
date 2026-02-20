using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class IdeologyTools
    {
        public static string GetIdeologyInfo()
        {
            // Check if Ideology DLC is active
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Get colony ideology
            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            var result = new JSONObject();
            result["ideology_name"] = ideology.name;
            result["ideo_class"] = ideology.ideoClass?.defName ?? "Unknown";

            // Memes
            var memes = new JSONArray();
            foreach (var meme in ideology.memes)
                memes.Add(meme.defName);
            result["memes"] = memes;

            // Precepts
            var precepts = new JSONArray();
            foreach (var precept in ideology.PreceptsListForReading)
            {
                var p = new JSONObject();
                p["name"] = precept.def.LabelCap;
                p["issue"] = precept.def.issue?.defName ?? "None";
                p["impact"] = precept.def.impact.ToString();
                p["count"] = precept.Count;
                
                // Check if it's a major restriction
                if (precept.def.impact == MemeImpact.None)
                    p["severity"] = "minor";
                else if (precept.def.impact == MemeImpact.Low)
                    p["severity"] = "low";
                else if (precept.def.impact == MemeImpact.Medium)
                    p["severity"] = "medium";
                else
                    p["severity"] = "high";
                
                precepts.Add(p);
            }
            result["precepts"] = precepts;

            // Roles
            var roles = new JSONArray();
            foreach (var role in ideology.roles)
            {
                var r = new JSONObject();
                r["role_def"] = role.def.defName;
                r["label"] = role.def.label;
                r["required"] = role.def.required;
                r["slots"] = role.def.slots;
                
                // Find current assignees
                var assignees = new JSONArray();
                foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                {
                    var assignedRole = p.ideos?.GetRole(ideology);
                    if (assignedRole != null && assignedRole.def == role.def)
                        assignees.Add(p.Name?.ToStringShort ?? p.LabelShort);
                }
                r["assignees"] = assignees;
                
                roles.Add(r);
            }
            result["roles"] = roles;

            // Structure
            result["structure"] = ideology.structure?.defName ?? "None";

            return result.ToString();
        }

        public static string GetPawnIdeologyStatus(string name)
        {
            if (string.IsNullOrEmpty(name))
                return ToolExecutor.JsonError("Pawn name required.");

            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string lower = name.ToLower();
            var pawn = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .FirstOrDefault(p => 
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.LabelShort?.ToLower() == lower);

            if (pawn == null)
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.ideos == null)
                return ToolExecutor.JsonError("No ideology information for this colonist.");

            var ideology = pawn.ideos.Ideo;
            if (ideology == null)
                return ToolExecutor.JsonError("Colonist has no ideology.");

            var result = new JSONObject();
            result["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
            result["ideology"] = ideology.name;
            result["certainty"] = pawn.ideos.Certainty.ToString("P0");

            // Role
            var role = pawn.ideos.GetRole(ideology);
            if (role != null)
            {
                result["role"] = role.def.label;
                result["role_def"] = role.def.defName;
                
                // Role requirements
                var requirements = new JSONObject();
                if (role.def.apparelTags != null && role.def.apparelTags.Count > 0)
                    requirements["apparel_tags"] = role.def.apparelTags;
                result["role_requirements"] = requirements;
            }
            else
            {
                result["role"] = "None";
            }

            // Precept comforts
            var pawnIdeo = pawn.ideos?.Ideo;
            var preceptComfort = new JSONArray();
            if (pawnIdeo != null)
            {
                foreach (var p in pawnIdeo.PreceptsListForReading)
                {
                if (p.def.comfort != null)
                {
                    var pc = new JSONObject();
                    pc["precept"] = p.def.LabelCap;
                    pc["comfort"] = p.def.comfort.Value.ToString("P0");
                    preceptComfort.Add(pc);
                }
                }
            }
            if (preceptComfort.Count > 0)
                result["precept_comfort"] = preceptComfort;

            // Certainty change reasons
            var certaintyFactors = new JSONArray();
            foreach (var cf in pawn.ideos.CertaintyReasons())
            {
                certaintyFactors.Add(cf);
            }
            if (certaintyFactors.Count > 0)
                result["certainty_factors"] = certaintyFactors;

            return result.ToString();
        }

        public static string GetRitualStatus()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Get ritual obligations
            var obligations = new JSONArray();
            foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (p.ideos == null) continue;
                
                foreach (var obl in p.ideos.Obligations())
                {
                    var o = new JSONObject();
                    o["colonist"] = p.Name?.ToStringShort ?? p.LabelShort;
                    o["ritual"] = obl.def?.LabelCap ?? "Unknown";
                    o["active"] = obl.active;
                    
                    if (obl.target != null)
                        o["target"] = obl.target.LabelCap;
                    
                    obligations.Add(o);
                }
            }
            result["obligations"] = obligations;

            // Get currently active rituals
            var activeRituals = new JSONArray();
            foreach (var lord in map.lordManager.lords)
            {
                if (lord.LordJob != null && lord.LordJob.def.defName.Contains("Ritual"))
                {
                    var r = new JSONObject();
                    r["ritual_type"] = lord.LordJob.def.defName;
                    r["participants"] = lord.ownedPawns.Count;
                    activeRituals.Add(r);
                }
            }
            result["active_rituals"] = activeRituals;

            // Count upcoming obligations
            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology != null)
            {
                var upcomingCount = 0;
                foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                {
                    if (p.ideos != null && p.ideos.Obligations().Any(o => o.active))
                        upcomingCount++;
                }
                result["colonists_with_active_obligations"] = upcomingCount;
            }

            return result.ToString();
        }

        public static string AnalyzeIdeologyConflicts()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Get colonists with ideological roles
            var colonists = map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();
            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            // Find colonists at risk of certainty loss
            var atRisk = new JSONArray();
            foreach (var p in colonists)
            {
                if (p.ideos == null) continue;
                
                var certainty = p.ideos.Certainty;
                if (certainty < 0.5f)
                {
                    var r = new JSONObject();
                    r["name"] = p.Name?.ToStringShort ?? p.LabelShort;
                    r["certainty"] = certainty.ToString("P0");
                    r["status"] = certainty < 0.3f ? "critical" : "at_risk";
                    atRisk.Add(r);
                }
            }
            result["certainty_at_risk"] = atRisk;

            // Find colonists without roles
            var roleNeeded = new JSONArray();
            foreach (var p in colonists)
            {
                if (p.ideos == null) continue;
                
                var role = p.ideos.GetRole(ideology);
                if (role == null)
                {
                    // Check if there are unfilled role slots
                    var hasUnfilledRole = ideology.roles.Any(r => 
                        r.def.slots > 0 && 
                        !colonists.Any(c => c.ideos?.GetRole(ideology)?.def == r.def));
                    
                    if (hasUnfilledRole)
                    {
                        roleNeeded.Add(p.Name?.ToStringShort ?? p.LabelShort);
                    }
                }
            }
            result["colonists_needing_roles"] = roleNeeded;

            // Check for precept-related issues
            var preceptIssues = new JSONArray();
            foreach (var p in colonists)
            {
                if (p.ideos == null) continue;
                
                // Check recent certainty change
                // This is simplified - full implementation would track actual changes
                if (p.ideos.Certainty < 0.7f)
                {
                    // Could be precept-related
                    var issues = p.ideos.CertaintyReasons();
                    if (issues != null && issues.Count > 0)
                    {
                        var pi = new JSONObject();
                        pi["name"] = p.Name?.ToStringShort ?? p.LabelShort;
                        pi["reasons"] = issues;
                        preceptIssues.Add(pi);
                    }
                }
            }
            result["precept_related_issues"] = preceptIssues;

            return result.ToString();
        }
    }
}
