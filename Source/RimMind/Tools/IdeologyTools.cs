using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimMind.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Tools
{
    /// <summary>
    /// Tools for querying RimWorld Ideology DLC data:
    /// colony ideology, precepts, roles, rituals, certainty, and ideological conflicts.
    /// All methods check <c>ModsConfig.IdeologyActive</c> first and return a JSON error if the DLC is inactive.
    /// Reflection is used throughout to access DLC-only types that are not present in non-DLC builds.
    /// </summary>
    public static class IdeologyTools
    {
        // ─────────────────────────── Reflection helpers ───────────────────────────

        /// <summary>Gets Pawn_IdeoTracker from <c>pawn.ideo</c> (Ideology DLC field).</summary>
        private static object GetIdeoTracker(Pawn pawn)
        {
            if (pawn == null) return null;
            var prop = pawn.GetType().GetProperty("ideo",
                BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetValue(pawn);
        }

        private static T GetProp<T>(object obj, string propName, T defaultValue = default)
        {
            if (obj == null) return defaultValue;
            try
            {
                var prop = obj.GetType().GetProperty(propName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop == null) return defaultValue;
                var value = prop.GetValue(obj);
                if (value is T t) return t;
                return defaultValue;
            }
            catch { return defaultValue; }
        }

        private static object InvokeMethod(object obj, string methodName, params object[] args)
        {
            if (obj == null) return null;
            try
            {
                var method = obj.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return method?.Invoke(obj, args);
            }
            catch { return null; }
        }

        private static object GetStaticProp(Type type, string propName)
        {
            try
            {
                var prop = type.GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return prop?.GetValue(null);
            }
            catch { return null; }
        }

        /// <summary>Finds a colonist on the current map by short name (case-insensitive).</summary>
        private static Pawn FindColonist(string name)
        {
            var map = Find.CurrentMap;
            if (map == null || string.IsNullOrEmpty(name)) return null;
            string lower = name.ToLower();
            return map.mapPawns.FreeColonists.FirstOrDefault(p =>
                (p.Name?.ToStringShort?.ToLower() == lower) ||
                (p.LabelShort?.ToLower() == lower));
        }

        // ─────────────────────────── Tool 1: get_ideology_info ────────────────────

        /// <summary>
        /// Returns colony ideology details: name, memes, all precepts with impact/severity,
        /// ideological roles with filled/unfilled status, and precept-sourced rituals.
        /// </summary>
        public static string GetIdeologyInfo()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC not active");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            var result = new JSONObject();
            result["ideology_name"] = ideology.name ?? "Unknown";

            // ── Memes ──────────────────────────────────────────────────────────────
            var memes = new JSONArray();
            try
            {
                if (ideology.memes != null)
                    foreach (var meme in ideology.memes)
                        memes.Add(meme?.label ?? meme?.defName ?? "Unknown");
            }
            catch { /* ignore meme read failures */ }
            result["memes"] = memes;

            // ── Precepts ───────────────────────────────────────────────────────────
            var precepts = new JSONArray();
            List<object> preceptList = null;
            try
            {
                preceptList = ideology.PreceptsListForReading?.Cast<object>().ToList();
            }
            catch { }

            if (preceptList != null)
            {
                foreach (var precept in preceptList)
                {
                    try
                    {
                        var def = GetProp<object>(precept, "def");
                        if (def == null) continue;

                        var typeName = precept.GetType().Name;
                        // Skip roles and rituals — those go in their own sections
                        if (typeName.Contains("Role") || typeName.Contains("Ritual"))
                            continue;

                        var p = new JSONObject();
                        p["name"] = GetProp<string>(def, "label") ?? GetProp<string>(def, "defName") ?? "Unknown";

                        // impact (PreceptImpact enum)
                        var impact = GetProp<object>(def, "impact");
                        var impactStr = impact?.ToString() ?? "None";
                        p["impact"] = impactStr;
                        p["severity"] = MapImpactToSeverity(impactStr);

                        // Issue (e.g., "Meat", "Cannibalism")
                        var issue = GetProp<object>(def, "issue");
                        if (issue != null)
                            p["issue"] = GetProp<string>(issue, "defName") ?? issue.ToString();

                        // Description snippet
                        try
                        {
                            var desc = GetProp<string>(def, "description");
                            if (!string.IsNullOrEmpty(desc) && desc.Length > 120)
                                desc = desc.Substring(0, 117) + "...";
                            if (!string.IsNullOrEmpty(desc))
                                p["description"] = desc;
                        }
                        catch { }

                        precepts.Add(p);
                    }
                    catch { }
                }
            }
            result["precepts"] = precepts;

            // ── Roles ─────────────────────────────────────────────────────────────
            var roles = new JSONArray();
            try
            {
                if (preceptList != null)
                {
                    var colonists = map.mapPawns.FreeColonists.ToList();
                    foreach (var precept in preceptList)
                    {
                        try
                        {
                            var typeName = precept.GetType().Name;
                            if (!typeName.Contains("Role")) continue;

                            var def = GetProp<object>(precept, "def");
                            if (def == null) continue;

                            var roleObj = new JSONObject();
                            roleObj["role_name"] = GetProp<string>(def, "label") ??
                                                   GetProp<string>(def, "defName") ?? "Unknown";

                            // Try to find the assigned pawn via RequiredPawn property
                            Pawn assignedPawn = null;
                            try
                            {
                                var requiredPawn = GetProp<Pawn>(precept, "ChosenPawn");
                                if (requiredPawn == null)
                                    requiredPawn = GetProp<Pawn>(precept, "pawn");
                                assignedPawn = requiredPawn;
                            }
                            catch { }

                            // Fallback: scan colonists for this role
                            if (assignedPawn == null)
                            {
                                foreach (var colonist in colonists)
                                {
                                    try
                                    {
                                        var tracker = GetIdeoTracker(colonist);
                                        if (tracker == null) continue;
                                        var pawnIdeo = GetProp<Ideo>(tracker, "Ideo");
                                        if (pawnIdeo != ideology) continue;
                                        var role = InvokeMethod(tracker, "GetRole", ideology);
                                        if (role == null) continue;
                                        var roleDef = GetProp<object>(role, "def");
                                        if (roleDef == null) continue;
                                        var roleDefName = GetProp<string>(roleDef, "defName");
                                        var preceptDefName = GetProp<string>(def, "defName");
                                        if (roleDefName == preceptDefName)
                                        {
                                            assignedPawn = colonist;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }

                            if (assignedPawn != null)
                            {
                                roleObj["status"] = "filled";
                                roleObj["assigned_pawn"] = assignedPawn.Name?.ToStringShort ?? assignedPawn.LabelShort;
                            }
                            else
                            {
                                roleObj["status"] = "unfilled";
                                roleObj["assigned_pawn"] = null;
                            }

                            // Requirements (role-specific precept requirements)
                            try
                            {
                                var requiresApparelProp = GetProp<bool>(def, "requiresApparelList");
                                if (requiresApparelProp)
                                    roleObj["requires_apparel"] = true;
                            }
                            catch { }

                            roles.Add(roleObj);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            result["roles"] = roles;

            // ── Rituals ───────────────────────────────────────────────────────────
            var rituals = new JSONArray();
            try
            {
                if (preceptList != null)
                {
                    foreach (var precept in preceptList)
                    {
                        try
                        {
                            var typeName = precept.GetType().Name;
                            if (!typeName.Contains("Ritual")) continue;

                            var def = GetProp<object>(precept, "def");
                            if (def == null) continue;

                            var ritualObj = new JSONObject();
                            ritualObj["ritual_name"] = GetProp<string>(def, "label") ??
                                                       GetProp<string>(def, "defName") ?? "Unknown";

                            // Ritual description
                            try
                            {
                                var desc = GetProp<string>(def, "description");
                                if (!string.IsNullOrEmpty(desc) && desc.Length > 100)
                                    desc = desc.Substring(0, 97) + "...";
                                if (!string.IsNullOrEmpty(desc))
                                    ritualObj["description"] = desc;
                            }
                            catch { }

                            // Obligation tracker
                            try
                            {
                                var obligationTracker = GetProp<object>(precept, "obligationTracker");
                                if (obligationTracker != null)
                                {
                                    var obligations = GetProp<System.Collections.IEnumerable>(
                                        obligationTracker, "AllObligations");
                                    if (obligations != null)
                                    {
                                        int obligationCount = 0;
                                        foreach (var obl in obligations) obligationCount++;
                                        ritualObj["pending_obligations"] = obligationCount;
                                    }
                                }
                            }
                            catch { }

                            rituals.Add(ritualObj);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            result["rituals"] = rituals;

            // ── Summary ───────────────────────────────────────────────────────────
            result["precept_count"] = precepts.Count;
            result["role_count"] = roles.Count;
            result["unfilled_roles"] = CountUnfilled(roles);
            result["ritual_count"] = rituals.Count;

            return result.ToString();
        }

        private static string MapImpactToSeverity(string impactStr)
        {
            switch (impactStr)
            {
                case "Critical": case "4": return "critical";
                case "High": case "3": return "high";
                case "Medium": case "2": return "medium";
                case "Low": case "1": return "low";
                default: return "minor";
            }
        }

        private static int CountUnfilled(JSONArray roles)
        {
            int count = 0;
            foreach (JSONNode role in roles)
            {
                if (role["status"]?.Value == "unfilled") count++;
            }
            return count;
        }

        // ─────────────────── Tool 2: get_pawn_ideology_status ─────────────────────

        /// <summary>
        /// Returns a colonist's ideological status: assigned role, certainty level,
        /// certainty factors, and ideological compatibility with other colonists.
        /// </summary>
        public static string GetPawnIdeologyStatus(string name)
        {
            if (string.IsNullOrEmpty(name))
                return ToolExecutor.JsonError("Parameter 'name' is required.");

            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC not active");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var pawn = FindColonist(name);
            if (pawn == null)
                return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var tracker = GetIdeoTracker(pawn);
            if (tracker == null)
                return ToolExecutor.JsonError("No ideology data for '" + name + "'.");

            var result = new JSONObject();
            result["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;

            // Ideology name
            Ideo pawnIdeo = null;
            try { pawnIdeo = GetProp<Ideo>(tracker, "Ideo"); }
            catch { }
            result["ideology"] = pawnIdeo?.name ?? "None";

            // Certainty (0–1 float)
            float certainty = 0f;
            try { certainty = GetProp<float>(tracker, "Certainty"); }
            catch { }
            result["certainty"] = certainty;
            result["certainty_percent"] = (certainty * 100f).ToString("F0") + "%";
            result["certainty_status"] = CertaintyStatus(certainty);

            // Assigned role
            try
            {
                var role = InvokeMethod(tracker, "GetRole", pawnIdeo);
                if (role != null)
                {
                    var roleDef = GetProp<object>(role, "def");
                    result["role"] = GetProp<string>(roleDef, "label") ??
                                     GetProp<string>(roleDef, "defName") ?? "Unknown";
                }
                else
                {
                    result["role"] = "None";
                }
            }
            catch { result["role"] = "Unknown"; }

            // Certainty factors (recent modifiers)
            var certaintyFactors = new JSONArray();
            try
            {
                var factors = GetProp<System.Collections.IEnumerable>(tracker, "CertaintyChangeFactors");
                if (factors != null)
                {
                    foreach (var factor in factors)
                    {
                        var factorObj = new JSONObject();
                        factorObj["label"] = GetProp<string>(factor, "label") ?? factor.ToString();
                        var change = GetProp<float>(factor, "certaintyChangePerDay");
                        factorObj["change_per_day"] = change;
                        certaintyFactors.Add(factorObj);
                    }
                }
            }
            catch { }
            result["certainty_factors"] = certaintyFactors;

            // Ideological compatibility with other colonists
            var compatibility = new JSONArray();
            try
            {
                var allColonists = map.mapPawns.FreeColonists.Where(p => p != pawn).ToList();
                foreach (var other in allColonists)
                {
                    try
                    {
                        var otherTracker = GetIdeoTracker(other);
                        if (otherTracker == null) continue;

                        Ideo otherIdeo = null;
                        try { otherIdeo = GetProp<Ideo>(otherTracker, "Ideo"); }
                        catch { }

                        var compatObj = new JSONObject();
                        compatObj["colonist"] = other.Name?.ToStringShort ?? other.LabelShort;
                        compatObj["ideology"] = otherIdeo?.name ?? "None";

                        // Same or different ideology?
                        bool sameIdeo = (pawnIdeo != null && otherIdeo != null && pawnIdeo == otherIdeo);
                        compatObj["same_ideology"] = sameIdeo;

                        // Opinion from pawn → other
                        int opinion = pawn.relations?.OpinionOf(other) ?? 0;
                        compatObj["opinion"] = opinion;
                        compatObj["opinion_label"] = OpinionLabel(opinion);

                        // Ideo opinion modifier via RelationsUtility reflection
                        try
                        {
                            var relUtils = typeof(RelationsUtility);
                            var ideoCompatMethod = relUtils.GetMethod("Compat_OfIdeo",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                null,
                                new[] { typeof(Pawn), typeof(Pawn) },
                                null);
                            if (ideoCompatMethod != null)
                            {
                                var compatResult = ideoCompatMethod.Invoke(null, new object[] { pawn, other });
                                if (compatResult is float compatFloat)
                                {
                                    compatObj["ideology_opinion_modifier"] = Mathf.RoundToInt(compatFloat);
                                }
                            }
                        }
                        catch { }

                        compatibility.Add(compatObj);
                    }
                    catch { }
                }
            }
            catch { }
            result["compatibility_with_colonists"] = compatibility;

            // Precept-related mood thoughts (ideology-sourced debuffs)
            var ideologyThoughts = new JSONArray();
            try
            {
                var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                if (memories != null)
                {
                    foreach (var memory in memories)
                    {
                        try
                        {
                            var defName = memory.def?.defName ?? "";
                            var sourcePrecept = GetProp<object>(memory, "sourcePrecept");
                            if (sourcePrecept == null) continue; // only ideology thoughts

                            var tObj = new JSONObject();
                            tObj["thought"] = memory.LabelCap.ToString();
                            tObj["mood_effect"] = memory.MoodOffset();
                            ideologyThoughts.Add(tObj);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            result["ideology_thoughts"] = ideologyThoughts;

            return result.ToString();
        }

        private static string CertaintyStatus(float certainty)
        {
            if (certainty >= 0.85f) return "devout";
            if (certainty >= 0.60f) return "stable";
            if (certainty >= 0.40f) return "wavering";
            if (certainty >= 0.20f) return "at_risk";
            return "critical";
        }

        private static string OpinionLabel(int opinion)
        {
            if (opinion >= 75) return "beloved";
            if (opinion >= 25) return "friend";
            if (opinion >= 5) return "acquaintance";
            if (opinion >= -5) return "neutral";
            if (opinion >= -25) return "disliked";
            if (opinion >= -75) return "rival";
            return "hated";
        }

        // ─────────────────────── Tool 3: get_ritual_status ────────────────────────

        /// <summary>
        /// Returns upcoming ritual obligations with countdown in days, active ritual details,
        /// quality factors, and colonists with pending obligations.
        /// </summary>
        public static string GetRitualStatus()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC not active");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideology == null)
                return ToolExecutor.JsonError("No colony ideology found.");

            // ── Ritual precepts ───────────────────────────────────────────────────
            var ritualPrecepts = new JSONArray();
            List<object> preceptList = null;
            try { preceptList = ideology.PreceptsListForReading?.Cast<object>().ToList(); }
            catch { }

            if (preceptList != null)
            {
                foreach (var precept in preceptList)
                {
                    try
                    {
                        if (!precept.GetType().Name.Contains("Ritual")) continue;

                        var def = GetProp<object>(precept, "def");
                        if (def == null) continue;

                        var ritualObj = new JSONObject();
                        ritualObj["name"] = GetProp<string>(def, "label") ??
                                            GetProp<string>(def, "defName") ?? "Unknown";

                        // ── Obligation tracker ────────────────────────────────────
                        var obligationTracker = GetProp<object>(precept, "obligationTracker");
                        if (obligationTracker != null)
                        {
                            var obligations = GetProp<System.Collections.IEnumerable>(
                                obligationTracker, "AllObligations");
                            var oblArray = new JSONArray();

                            if (obligations != null)
                            {
                                foreach (var obl in obligations)
                                {
                                    try
                                    {
                                        var oblObj = new JSONObject();

                                        // Trigger ticks (time until obligation expires/becomes active)
                                        int triggerTick = GetProp<int>(obl, "triggerTick");
                                        int ticksLeft = triggerTick - Find.TickManager.TicksGame;
                                        if (ticksLeft > 0)
                                        {
                                            float daysLeft = ticksLeft / 60000f;
                                            oblObj["countdown_days"] = daysLeft.ToString("F1");
                                        }
                                        else
                                        {
                                            oblObj["overdue"] = true;
                                        }

                                        // Tag / reason
                                        var tag = GetProp<string>(obl, "tag");
                                        if (!string.IsNullOrEmpty(tag))
                                            oblObj["reason"] = tag;

                                        // Associated pawn
                                        var oblPawn = GetProp<Pawn>(obl, "pawn");
                                        if (oblPawn != null)
                                            oblObj["pawn"] = oblPawn.Name?.ToStringShort ?? oblPawn.LabelShort;

                                        oblArray.Add(oblObj);
                                    }
                                    catch { }
                                }
                            }
                            ritualObj["obligations"] = oblArray;
                            ritualObj["obligation_count"] = oblArray.Count;

                            // Outstanding obligation?
                            var outstanding = GetProp<object>(obligationTracker, "CurrentObligation");
                            ritualObj["has_outstanding_obligation"] = outstanding != null;
                        }

                        // ── Quality modifiers ─────────────────────────────────────
                        var qualityFactors = GetProp<object>(def, "ritualPatternDef");
                        if (qualityFactors == null)
                            qualityFactors = GetProp<object>(def, "patternDef");

                        if (qualityFactors != null)
                        {
                            var factors = GetProp<System.Collections.IEnumerable>(qualityFactors, "qualityFactors");
                            if (factors != null)
                            {
                                var qfArray = new JSONArray();
                                foreach (var qf in factors)
                                {
                                    try
                                    {
                                        var qfObj = new JSONObject();
                                        qfObj["label"] = GetProp<string>(qf, "label") ?? qf.ToString();
                                        qfArray.Add(qfObj);
                                    }
                                    catch { }
                                }
                                if (qfArray.Count > 0)
                                    ritualObj["quality_factors"] = qfArray;
                            }
                        }

                        ritualPrecepts.Add(ritualObj);
                    }
                    catch { }
                }
            }
            result["rituals"] = ritualPrecepts;

            // ── Active rituals (running LordJobs) ─────────────────────────────────
            var activeRituals = new JSONArray();
            try
            {
                foreach (var lord in map.lordManager.lords)
                {
                    if (lord.LordJob == null) continue;
                    var jobTypeName = lord.LordJob.GetType().Name;
                    if (!jobTypeName.Contains("Ritual") && !jobTypeName.Contains("Ideo")) continue;

                    var activeObj = new JSONObject();
                    activeObj["ritual_type"] = jobTypeName;
                    activeObj["participant_count"] = lord.ownedPawns?.Count ?? 0;

                    var participants = new JSONArray();
                    if (lord.ownedPawns != null)
                        foreach (var p in lord.ownedPawns)
                            participants.Add(p.Name?.ToStringShort ?? p.LabelShort);
                    activeObj["participants"] = participants;

                    // Quality (if accessible)
                    try
                    {
                        var quality = GetProp<float>(lord.LordJob, "Quality");
                        if (quality > 0f)
                            activeObj["current_quality"] = quality.ToString("P0");
                    }
                    catch { }

                    activeRituals.Add(activeObj);
                }
            }
            catch { }
            result["active_rituals"] = activeRituals;
            result["active_ritual_count"] = activeRituals.Count;

            // ── Colonists with pending obligations ────────────────────────────────
            var obligatedColonists = new JSONArray();
            try
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    try
                    {
                        var tracker = GetIdeoTracker(pawn);
                        if (tracker == null) continue;

                        // Check for missed-ritual mood thoughts
                        var missedThoughts = new JSONArray();
                        var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                        if (memories != null)
                        {
                            foreach (var memory in memories)
                            {
                                try
                                {
                                    var defName = memory.def?.defName ?? "";
                                    if (defName.Contains("MissedRitual") || defName.Contains("ObligationMissed"))
                                    {
                                        var tObj = new JSONObject();
                                        tObj["thought"] = memory.LabelCap.ToString();
                                        tObj["mood_effect"] = memory.MoodOffset();
                                        missedThoughts.Add(tObj);
                                    }
                                }
                                catch { }
                            }
                        }

                        if (missedThoughts.Count > 0)
                        {
                            var oblColonist = new JSONObject();
                            oblColonist["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                            oblColonist["missed_ritual_debuffs"] = missedThoughts;
                            obligatedColonists.Add(oblColonist);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            result["colonists_with_missed_ritual_debuffs"] = obligatedColonists;

            return result.ToString();
        }

        // ─────────────────── Tool 4: analyze_ideology_conflicts ───────────────────

        /// <summary>
        /// Detects ideological conflicts:
        /// - Colonist pairs with ideological opinion incompatibility
        /// - Colonists with certainty below 0.5 (at risk of ideology drift)
        /// - Colonists suffering missed-ritual mood debuffs
        /// </summary>
        public static string AnalyzeIdeologyConflicts()
        {
            if (!ModsConfig.IdeologyActive)
                return ToolExecutor.JsonError("Ideology DLC not active");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var colonists = map.mapPawns.FreeColonists.ToList();

            // ── Certainty at risk ─────────────────────────────────────────────────
            var atRisk = new JSONArray();
            foreach (var pawn in colonists)
            {
                try
                {
                    var tracker = GetIdeoTracker(pawn);
                    if (tracker == null) continue;

                    float certainty = GetProp<float>(tracker, "Certainty", 1f);
                    if (certainty < 0.5f)
                    {
                        var riskObj = new JSONObject();
                        riskObj["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                        riskObj["certainty"] = certainty;
                        riskObj["certainty_percent"] = (certainty * 100f).ToString("F0") + "%";
                        riskObj["status"] = CertaintyStatus(certainty);

                        // Ideology name
                        try
                        {
                            var pawnIdeo = GetProp<Ideo>(tracker, "Ideo");
                            riskObj["ideology"] = pawnIdeo?.name ?? "None";
                        }
                        catch { }

                        // What's driving certainty down?
                        var negFactors = new JSONArray();
                        try
                        {
                            var factors = GetProp<System.Collections.IEnumerable>(tracker, "CertaintyChangeFactors");
                            if (factors != null)
                            {
                                foreach (var factor in factors)
                                {
                                    float change = GetProp<float>(factor, "certaintyChangePerDay");
                                    if (change < 0f)
                                    {
                                        var factorObj = new JSONObject();
                                        factorObj["label"] = GetProp<string>(factor, "label") ?? factor.ToString();
                                        factorObj["change_per_day"] = change;
                                        negFactors.Add(factorObj);
                                    }
                                }
                            }
                        }
                        catch { }
                        riskObj["negative_certainty_factors"] = negFactors;

                        atRisk.Add(riskObj);
                    }
                }
                catch { }
            }
            result["certainty_at_risk"] = atRisk;
            result["certainty_at_risk_count"] = atRisk.Count;

            // ── Ideological incompatibility pairs ─────────────────────────────────
            var incompatiblePairs = new JSONArray();
            try
            {
                var relUtils = typeof(RelationsUtility);
                // Try to find Compat_OfIdeo(Pawn, Pawn) overload
                MethodInfo ideoCompatMethod = null;
                try
                {
                    ideoCompatMethod = relUtils.GetMethod("Compat_OfIdeo",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(Pawn), typeof(Pawn) },
                        null);
                }
                catch { }

                for (int i = 0; i < colonists.Count; i++)
                {
                    for (int j = i + 1; j < colonists.Count; j++)
                    {
                        var pawnA = colonists[i];
                        var pawnB = colonists[j];
                        try
                        {
                            float ideoOpinionMod = 0f;
                            bool hasIdeoConflict = false;

                            if (ideoCompatMethod != null)
                            {
                                var compatResult = ideoCompatMethod.Invoke(null, new object[] { pawnA, pawnB });
                                if (compatResult is float cf)
                                {
                                    ideoOpinionMod = cf;
                                    hasIdeoConflict = cf < -10f;
                                }
                            }
                            else
                            {
                                // Fallback: compare ideology references
                                var trackerA = GetIdeoTracker(pawnA);
                                var trackerB = GetIdeoTracker(pawnB);
                                var ideoA = trackerA != null ? GetProp<Ideo>(trackerA, "Ideo") : null;
                                var ideoB = trackerB != null ? GetProp<Ideo>(trackerB, "Ideo") : null;
                                hasIdeoConflict = (ideoA != null && ideoB != null && ideoA != ideoB);
                                if (hasIdeoConflict) ideoOpinionMod = -20f; // generic incompatibility
                            }

                            if (hasIdeoConflict)
                            {
                                var pairObj = new JSONObject();
                                pairObj["colonist_a"] = pawnA.Name?.ToStringShort ?? pawnA.LabelShort;
                                pairObj["colonist_b"] = pawnB.Name?.ToStringShort ?? pawnB.LabelShort;
                                pairObj["ideology_opinion_modifier"] = Mathf.RoundToInt(ideoOpinionMod);
                                pairObj["opinion_a_of_b"] = pawnA.relations?.OpinionOf(pawnB) ?? 0;
                                pairObj["opinion_b_of_a"] = pawnB.relations?.OpinionOf(pawnA) ?? 0;

                                // Ideologies
                                try
                                {
                                    var tA = GetIdeoTracker(pawnA);
                                    var tB = GetIdeoTracker(pawnB);
                                    pairObj["ideology_a"] = tA != null ? (GetProp<Ideo>(tA, "Ideo")?.name ?? "None") : "None";
                                    pairObj["ideology_b"] = tB != null ? (GetProp<Ideo>(tB, "Ideo")?.name ?? "None") : "None";
                                }
                                catch { }

                                incompatiblePairs.Add(pairObj);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            result["incompatible_pairs"] = incompatiblePairs;
            result["incompatible_pair_count"] = incompatiblePairs.Count;

            // ── Missed ritual debuffs ─────────────────────────────────────────────
            var missedRituals = new JSONArray();
            foreach (var pawn in colonists)
            {
                try
                {
                    var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                    if (memories == null) continue;

                    var debuffs = new JSONArray();
                    foreach (var memory in memories)
                    {
                        try
                        {
                            var defName = memory.def?.defName ?? "";
                            // Ideology thoughts sourced from a precept (ritual missed, precept violated, etc.)
                            var sourcePrecept = GetProp<object>(memory, "sourcePrecept");
                            if (sourcePrecept == null)
                            {
                                // Also catch thoughts with "Ritual" or "Obligation" in defName
                                if (!defName.Contains("Ritual") && !defName.Contains("Obligation")
                                    && !defName.Contains("Precept") && !defName.Contains("Ideo"))
                                    continue;
                            }

                            float moodOffset = memory.MoodOffset();
                            if (moodOffset >= 0f) continue; // only debuffs

                            var debuffObj = new JSONObject();
                            debuffObj["thought"] = memory.LabelCap.ToString();
                            debuffObj["mood_effect"] = moodOffset;
                            debuffs.Add(debuffObj);
                        }
                        catch { }
                    }

                    if (debuffs.Count > 0)
                    {
                        var pawnObj = new JSONObject();
                        pawnObj["name"] = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                        pawnObj["ideology_debuffs"] = debuffs;

                        float totalDebuff = 0f;
                        foreach (JSONObject d in debuffs)
                            totalDebuff += d["mood_effect"]?.AsFloat ?? 0f;
                        pawnObj["total_ideology_mood_penalty"] = totalDebuff;

                        missedRituals.Add(pawnObj);
                    }
                }
                catch { }
            }
            result["pawns_with_ideology_debuffs"] = missedRituals;
            result["ideology_debuff_count"] = missedRituals.Count;

            // ── Unfilled roles (which also affect colony mood) ────────────────────
            var unfilledRoles = new JSONArray();
            try
            {
                var ideology = Faction.OfPlayer.ideos?.PrimaryIdeo;
                List<object> preceptList = null;
                try { preceptList = ideology?.PreceptsListForReading?.Cast<object>().ToList(); }
                catch { }

                if (preceptList != null)
                {
                    foreach (var precept in preceptList)
                    {
                        try
                        {
                            if (!precept.GetType().Name.Contains("Role")) continue;
                            var def = GetProp<object>(precept, "def");
                            if (def == null) continue;

                            // Check if role is filled
                            Pawn assignedPawn = null;
                            try { assignedPawn = GetProp<Pawn>(precept, "ChosenPawn"); }
                            catch { }
                            if (assignedPawn == null)
                                try { assignedPawn = GetProp<Pawn>(precept, "pawn"); }
                                catch { }

                            if (assignedPawn == null)
                            {
                                var roleObj = new JSONObject();
                                roleObj["role"] = GetProp<string>(def, "label") ??
                                                  GetProp<string>(def, "defName") ?? "Unknown";
                                unfilledRoles.Add(roleObj);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            result["unfilled_roles"] = unfilledRoles;
            result["unfilled_role_count"] = unfilledRoles.Count;

            // ── Summary ───────────────────────────────────────────────────────────
            result["total_colonists"] = colonists.Count;
            bool hasIssues = atRisk.Count > 0 || incompatiblePairs.Count > 0 ||
                             missedRituals.Count > 0 || unfilledRoles.Count > 0;
            result["has_ideological_issues"] = hasIssues;
            result["recommendation"] = hasIssues
                ? BuildRecommendation(atRisk.Count, incompatiblePairs.Count, missedRituals.Count, unfilledRoles.Count)
                : "Colony ideology is stable.";

            return result.ToString();
        }

        private static string BuildRecommendation(int atRiskCount, int pairCount, int debuffCount, int unfilledCount)
        {
            var parts = new List<string>();
            if (unfilledCount > 0)
                parts.Add(unfilledCount + " ideological role(s) need assignment.");
            if (atRiskCount > 0)
                parts.Add(atRiskCount + " colonist(s) have dangerously low certainty (<50%); consider praying or rituals.");
            if (debuffCount > 0)
                parts.Add(debuffCount + " colonist(s) have ideology-sourced mood debuffs; perform missing rituals.");
            if (pairCount > 0)
                parts.Add(pairCount + " colonist pair(s) have ideological incompatibility; consider separating them.");
            return string.Join(" ", parts);
        }
    }
}
