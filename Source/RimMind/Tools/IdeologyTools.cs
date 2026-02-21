using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class IdeologyTools
    {
        // Helper: get Pawn_IdeoTracker via reflection (DLC-only field 'ideos' on Pawn)
        private static dynamic GetIdeoTracker(Pawn pawn)
        {
            if (pawn == null) return null;
            var prop = pawn.GetType().GetProperty("ideos",
                BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetValue(pawn);
        }

        public static string GetIdeologyInfo()
        {
            // Check if Ideology DLC is active
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Get colony ideology (Faction.ideos is FactionIdeosTracker, PrimaryIdeo works)
            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            var result = new JSONObject();
            result["ideology_name"] = ideology.name.ToString();

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
                p["name"] = precept.def.LabelCap.ToString();
                p["issue"] = precept.def.issue?.defName ?? "None";
                p["impact"] = precept.def.impact.ToString();

                // Severity based on impact string
                var impactStr = precept.def.impact.ToString();
                if (impactStr == "None" || impactStr == "0")
                    p["severity"] = "minor";
                else if (impactStr == "Low" || impactStr == "1")
                    p["severity"] = "low";
                else if (impactStr == "Medium" || impactStr == "2")
                    p["severity"] = "medium";
                else
                    p["severity"] = "high";

                precepts.Add(p);
            }
            result["precepts"] = precepts;

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

            dynamic ideos = GetIdeoTracker(pawn);
            if (ideos == null)
                return ToolExecutor.JsonError("No ideology information for this colonist.");

            var result = new JSONObject();
            result["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;

            try
            {
                var ideology = (Ideo)ideos.Ideo;
                if (ideology == null)
                    return ToolExecutor.JsonError("Colonist has no ideology.");

                result["ideology"] = ideology.name.ToString();

                float certainty = (float)ideos.Certainty;
                result["certainty"] = certainty.ToString("P0");

                // Role via GetRole
                try
                {
                    var role = ideos.GetRole(ideology);
                    if (role != null)
                    {
                        result["role"] = role.def.label;
                        result["role_def"] = role.def.defName;
                    }
                    else
                    {
                        result["role"] = "None";
                    }
                }
                catch
                {
                    result["role"] = "Unknown";
                }

                // Precept comfort info
                var preceptComfort = new JSONArray();
                foreach (var p in ideology.PreceptsListForReading)
                {
                    if (p.def.comfort != null)
                    {
                        var pc = new JSONObject();
                        pc["precept"] = p.def.LabelCap.ToString();
                        pc["comfort"] = p.def.comfort.Value.ToString("P0");
                        preceptComfort.Add(pc);
                    }
                }
                if (preceptComfort.Count > 0)
                    result["precept_comfort"] = preceptComfort;
            }
            catch (Exception ex)
            {
                result["error"] = "Failed to read ideology details: " + ex.Message;
            }

            return result.ToString();
        }

        public static string GetRitualStatus()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Get ritual obligations via reflection
            var obligations = new JSONArray();
            foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                dynamic ideos = GetIdeoTracker(p);
                if (ideos == null) continue;

                try
                {
                    var obligationsList = ideos.Obligations();
                    if (obligationsList == null) continue;
                    foreach (var obl in obligationsList)
                    {
                        var o = new JSONObject();
                        o["colonist"] = p.Name?.ToStringShort ?? p.LabelShort;
                        try { o["ritual"] = obl.def?.LabelCap?.ToString() ?? "Unknown"; } catch { o["ritual"] = "Unknown"; }
                        try { o["active"] = (bool)obl.active; } catch { }
                        try { if (obl.target != null) o["target"] = obl.target.LabelCap.ToString(); } catch { }
                        obligations.Add(o);
                    }
                }
                catch
                {
                    // ObligationsActive may not exist; skip
                }
            }
            result["obligations"] = obligations;

            // Get currently active rituals
            var activeRituals = new JSONArray();
            foreach (var lord in map.lordManager.lords)
            {
                if (lord.LordJob == null) continue;
                var jobTypeName = lord.LordJob.GetType().Name;
                if (jobTypeName.Contains("Ritual") || jobTypeName.Contains("Ideo"))
                {
                    var r = new JSONObject();
                    r["ritual_type"] = jobTypeName;
                    r["participants"] = lord.ownedPawns.Count;
                    activeRituals.Add(r);
                }
            }
            result["active_rituals"] = activeRituals;

            // Count colonists with active obligations
            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology != null)
            {
                var upcomingCount = 0;
                foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                {
                    dynamic ideos = GetIdeoTracker(p);
                    if (ideos == null) continue;
                    try
                    {
                        var oblList = ideos.Obligations();
                        if (oblList != null)
                        {
                            foreach (var o in oblList)
                            {
                                try { if ((bool)o.active) { upcomingCount++; break; } } catch { }
                            }
                        }
                    }
                    catch { }
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

            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            var result = new JSONObject();
            var colonists = map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();

            // Find colonists at risk of certainty loss
            var atRisk = new JSONArray();
            foreach (var p in colonists)
            {
                dynamic ideos = GetIdeoTracker(p);
                if (ideos == null) continue;

                try
                {
                    float certainty = (float)ideos.Certainty;
                    if (certainty < 0.5f)
                    {
                        var r = new JSONObject();
                        r["name"] = p.Name?.ToStringShort ?? p.LabelShort;
                        r["certainty"] = certainty.ToString("P0");
                        r["status"] = certainty < 0.3f ? "critical" : "at_risk";
                        atRisk.Add(r);
                    }
                }
                catch { }
            }
            result["certainty_at_risk"] = atRisk;

            // Check for precept-related issues (low certainty)
            var preceptIssues = new JSONArray();
            foreach (var p in colonists)
            {
                dynamic ideos = GetIdeoTracker(p);
                if (ideos == null) continue;

                try
                {
                    float certainty = (float)ideos.Certainty;
                    if (certainty < 0.7f)
                    {
                        var pi = new JSONObject();
                        pi["name"] = p.Name?.ToStringShort ?? p.LabelShort;
                        pi["certainty"] = certainty.ToString("P0");
                        pi["note"] = "Low certainty - may need precept review";
                        preceptIssues.Add(pi);
                    }
                }
                catch { }
            }
            result["precept_related_issues"] = preceptIssues;

            // Summary
            result["total_colonists"] = colonists.Count;
            result["at_risk_count"] = atRisk.Count;

            return result.ToString();
        }
    }
}
