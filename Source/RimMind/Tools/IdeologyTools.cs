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
        private static object GetIdeoTracker(Pawn pawn)
        {
            if (pawn == null) return null;
            var prop = pawn.GetType().GetProperty("ideos",
                BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetValue(pawn);
        }

        private static T GetTrackerProp<T>(object tracker, string propName, T defaultValue = default)
        {
            if (tracker == null) return defaultValue;
            try
            {
                var prop = tracker.GetType().GetProperty(propName,
                    BindingFlags.Instance | BindingFlags.Public);
                if (prop == null) return defaultValue;
                var value = prop.GetValue(tracker);
                if (value is T t) return t;
                return defaultValue;
            }
            catch { return defaultValue; }
        }

        private static object InvokeTrackerMethod(object tracker, string methodName, params object[] args)
        {
            if (tracker == null) return null;
            try
            {
                var method = tracker.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public);
                return method?.Invoke(tracker, args);
            }
            catch { return null; }
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

            var ideoTracker = GetIdeoTracker(pawn);
            if (ideoTracker == null)
                return ToolExecutor.JsonError("No ideology information for this colonist.");

            var result = new JSONObject();
            result["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;

            try
            {
                var ideology = GetTrackerProp<Ideo>(ideoTracker, "Ideo");
                if (ideology == null)
                    return ToolExecutor.JsonError("Colonist has no ideology.");

                result["ideology"] = ideology.name.ToString();

                float certainty = GetTrackerProp<float>(ideoTracker, "Certainty", 0f);
                result["certainty"] = certainty.ToString("P0");

                // Role via GetRole reflection
                try
                {
                    var role = InvokeTrackerMethod(ideoTracker, "GetRole", ideology);
                    if (role != null)
                    {
                        var defProp = role.GetType().GetProperty("def");
                        var roleDef = defProp?.GetValue(role);
                        if (roleDef != null)
                        {
                            var labelProp = roleDef.GetType().GetProperty("label");
                            var defNameProp = roleDef.GetType().GetProperty("defName");
                            result["role"] = labelProp?.GetValue(roleDef)?.ToString() ?? "Unknown";
                            result["role_def"] = defNameProp?.GetValue(roleDef)?.ToString() ?? "Unknown";
                        }
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
                var ideoTracker = GetIdeoTracker(p);
                if (ideoTracker == null) continue;

                try
                {
                    var obligationsList = InvokeTrackerMethod(ideoTracker, "Obligations") as System.Collections.IEnumerable;
                    if (obligationsList == null) continue;
                    foreach (var obl in obligationsList)
                    {
                        var o = new JSONObject();
                        o["colonist"] = p.Name?.ToStringShort ?? p.LabelShort;
                        try
                        {
                            var defProp = obl.GetType().GetProperty("def");
                            var def = defProp?.GetValue(obl);
                            var labelCapProp = def?.GetType().GetProperty("LabelCap");
                            o["ritual"] = labelCapProp?.GetValue(def)?.ToString() ?? "Unknown";
                        }
                        catch { o["ritual"] = "Unknown"; }
                        try
                        {
                            var activeProp = obl.GetType().GetProperty("active");
                            o["active"] = (bool)(activeProp?.GetValue(obl) ?? false);
                        }
                        catch { }
                        obligations.Add(o);
                    }
                }
                catch
                {
                    // Obligations() may not be available; skip
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
                    var ideoTracker = GetIdeoTracker(p);
                    if (ideoTracker == null) continue;
                    try
                    {
                        var oblList = InvokeTrackerMethod(ideoTracker, "Obligations") as System.Collections.IEnumerable;
                        if (oblList != null)
                        {
                            foreach (var obl in oblList)
                            {
                                try
                                {
                                    var activeProp = obl.GetType().GetProperty("active");
                                    if ((bool)(activeProp?.GetValue(obl) ?? false))
                                    { upcomingCount++; break; }
                                }
                                catch { }
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
                var ideoTracker = GetIdeoTracker(p);
                if (ideoTracker == null) continue;

                float certainty = GetTrackerProp<float>(ideoTracker, "Certainty", 1f);
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

            // Check for precept-related issues (low certainty)
            var preceptIssues = new JSONArray();
            foreach (var p in colonists)
            {
                var ideoTracker = GetIdeoTracker(p);
                if (ideoTracker == null) continue;

                float certainty = GetTrackerProp<float>(ideoTracker, "Certainty", 1f);
                if (certainty < 0.7f)
                {
                    var pi = new JSONObject();
                    pi["name"] = p.Name?.ToStringShort ?? p.LabelShort;
                    pi["certainty"] = certainty.ToString("P0");
                    pi["note"] = "Low certainty - may need precept review";
                    preceptIssues.Add(pi);
                }
            }
            result["precept_related_issues"] = preceptIssues;

            // Summary
            result["total_colonists"] = colonists.Count;
            result["at_risk_count"] = atRisk.Count;

            return result.ToString();
        }
    }
}
