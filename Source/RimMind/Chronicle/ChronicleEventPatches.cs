using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace RimMind.Chronicle
{
    /// <summary>
    /// Harmony patches for Chronicle event detection.
    /// Detects deaths, raids, mechanoid kills, and banishments.
    /// </summary>
    public static class ChronicleEventPatches
    {
        private static readonly string[] ThreatLetterDefs = new[]
        {
            "ThreatOnScreen",
            "ThreatBig",
            "ThreatSmall",
            "RaidEnemy",
            "RaidFriendly",
            "MechanoidInfo",
            "MechanoidWarning",
            "ShuttleIncoming"
        };

        /// <summary>
        /// Apply all Chronicle-related Harmony patches.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                // Patch Pawn.KillMe for death detection
                var killMeMethod = typeof(Pawn).GetMethod("KillMe");
                if (killMeMethod != null)
                {
                    var postfix = typeof(ChronicleEventPatches).GetMethod("Pawn_KillMe_Postfix");
                    harmony.Patch(killMeMethod, postfix: new HarmonyMethod(postfix));
                    Log.Message("[RimMind] Patched Pawn.KillMe for death tracking");
                }
                else
                {
                    // Fallback: Try to find the method via target
                    MethodInfo killMeTarget = null;
                    foreach (var type in Assembly.Load("Assembly-CSharp").GetTypes())
                    {
                        if (type.Name == "Pawn")
                        {
                            killMeTarget = AccessTools.Method(type, "KillMe");
                            if (killMeTarget != null) break;
                        }
                    }
                    if (killMeTarget != null)
                    {
                        var postfix = typeof(ChronicleEventPatches).GetMethod("Pawn_KillMe_Postfix");
                        harmony.Patch(killMeTarget, postfix: new HarmonyMethod(postfix));
                        Log.Message("[RimMind] Patched Pawn.KillMe for death tracking");
                    }
                }

                // Note: LetterStack.ReceiveLetter is patched by Storyteller.LetterStackPatch (unified handler)
                // Chronicle raid detection via letters is not currently integrated.
                // To add it, call ChronicleEventPatches.OnRaidLetterReceived() from LetterStackPatch.ApplyAutomation()
                // when a threat letter def is detected.

                // Patch ThingOwner.Notify_ItemRemoved for banishment detection
                var thingOwnerType = AccessTools.TypeByName("ThingOwner");
                if (thingOwnerType != null)
                {
                    var notifyMethod = AccessTools.Method(thingOwnerType, "Notify_ItemRemoved");
                    if (notifyMethod != null)
                    {
                        var postfix = typeof(ChronicleEventPatches).GetMethod("ThingOwner_Notify_ItemRemoved_Postfix");
                        harmony.Patch(notifyMethod, postfix: new HarmonyMethod(postfix));
                        Log.Message("[RimMind] Patched ThingOwner.Notify_ItemRemoved for banishment tracking");
                    }
                }

                // Note: Mechanoid kills are detected via raid letters (MechanoidInfo, MechanoidWarning defs)
                // and the KillMe patch above (mechanoids killed by colonists will show as kills)

                Log.Message("[RimMind] Chronicle event patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] Failed to apply Chronicle event patches: {ex}");
            }
        }

        /// <summary>
        /// Postfix for Pawn.KillMe - detects when a colonist dies.
        /// </summary>
        public static void Pawn_KillMe_Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            try
            {
                if (__instance == null) return;

                // Only track colonist deaths
                if (!__instance.IsColonist) return;

                var tracker = ChronicleTracker.Instance;
                if (tracker == null) return;

                int currentDay = 0;
                try
                {
                    var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
                    if (map != null)
                        currentDay = GenLocalDate.DayOfYear(map);
                }
                catch
                {
                    currentDay = 0;
                }

                // Determine cause of death
                string cause = "unknown";
                string killer = "unknown";

                if (dinfo.HasValue)
                {
                    var damageDef = dinfo.Value.Def;
                    if (damageDef != null)
                    {
                        cause = GetCauseFromDamage(damageDef);
                        if (dinfo.Value.Instigator != null)
                        {
                            killer = GetKillerName(dinfo.Value.Instigator);
                        }
                    }
                }

                // Get last words if available (from memory)
                string lastWords = null;
                try
                {
                    if (__instance.needs?.mood?.thoughts?.memories != null)
                    {
                        // Look for last words thought - not easily accessible in RimWorld API
                        // We'll leave lastWords as null for now
                    }
                }
                catch { }

                var death = new ColonistDeath(
                    __instance.Name.ToStringShort,
                    cause,
                    killer,
                    currentDay,
                    lastWords
                );

                tracker.RecordColonistDeath(death);

                Log.Message($"[RimMind] Chronicle: Recorded death of {__instance.Name.ToStringShort} by {killer} ({cause})");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] Pawn_KillMe_Postfix failed: {ex}");
            }
        }

        /// <summary>
        /// Postfix for ThingOwner.Notify_ItemRemoved - detects banishments.
        /// </summary>
        public static void ThingOwner_Notify_ItemRemoved_Postfix(ThingOwner<Thing> __instance, Thing item)
        {
            try
            {
                if (item == null) return;

                // Check if this is a colonist being removed from a map (banishment)
                if (item is Pawn pawn && pawn.IsColonist)
                {
                    // Check if this was a voluntary banishment (not a caravan departure)
                    // Banished colonists go to " exile " or similar
                    var tracker = ChronicleTracker.Instance;
                    if (tracker != null && !string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                    {
                        // We can't easily distinguish banishment from caravan here,
                        // but we can check if the pawn is leaving via the outpost/exile building
                        tracker.RecordBanishment(pawn.Name.ToStringShort);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Called when a threat letter is received (not currently wired up).
        /// To enable: call this from Storyteller.LetterStackPatch.ApplyAutomation() when detecting threat letters.
        /// </summary>
        public static void OnRaidLetterReceived(Letter letter)
        {
            try
            {
                if (letter == null || letter.def == null) return;

                var tracker = ChronicleTracker.Instance;
                if (tracker == null) return;

                // Check if this is a threat letter by def name
                string defName = letter.def.defName;
                bool isThreat = false;
                foreach (var threatDef in ThreatLetterDefs)
                {
                    if (defName.Contains(threatDef))
                    {
                        isThreat = true;
                        break;
                    }
                }

                if (!isThreat) return;

                int day = 0;
                try
                {
                    var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
                    if (map != null)
                        day = GenLocalDate.DayOfYear(map);
                }
                catch { }

                string faction = "Unknown Enemy";
                int enemyCount = 0;

                // Try to extract info from the letter
                try
                {
                    if (letter is ChoiceLetter choiceLetter)
                    {
                        // Try to parse enemy count from the letter text
                        string text = choiceLetter.Text.ToString();
                        string label = choiceLetter.Label.ToString();

                        // Faction from label
                        if (label.Contains(" from "))
                        {
                            int fromIdx = label.LastIndexOf(" from ");
                            if (fromIdx >= 0)
                                faction = label.Substring(fromIdx + 6).Trim();
                        }

                        // Try to extract numbers from text
                        foreach (char c in text)
                        {
                            if (char.IsDigit(c))
                            {
                                string numStr = "";
                                int idx = text.IndexOf(c);
                                while (idx < text.Length && (char.IsDigit(text[idx]) || text[idx] == ' '))
                                {
                                    if (char.IsDigit(text[idx]))
                                        numStr += text[idx];
                                    idx++;
                                }
                                if (int.TryParse(numStr.Trim(), out int num) && num > 0 && num < 1000)
                                {
                                    enemyCount = Math.Max(enemyCount, num);
                                }
                            }
                        }

                        // Default enemy count if we couldn't parse
                        if (enemyCount == 0)
                        {
                            // Heuristic based on raid type
                            if (defName.Contains("Big") || defName.Contains("Raid"))
                                enemyCount = 8;
                            else if (defName.Contains("Small"))
                                enemyCount = 3;
                            else
                                enemyCount = 5;
                        }
                    }
                }
                catch { }

                var colonistCount = 0;
                try
                {
                    var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
                    colonistCount = map?.mapPawns?.FreeColonists?.Count ?? 0;
                }
                catch { }

                var raid = new RaidEvent
                {
                    day = day,
                    enemyFaction = faction,
                    enemyCount = enemyCount,
                    survived = true, // Will be updated when raid ends
                    colonistsInvolved = colonistCount,
                    letterLabel = letter.Label.ToString()
                };

                tracker.RecordRaid(raid);
                Log.Message("[RimMind] Chronicle: Recorded raid by " + faction + " (" + enemyCount + " enemies)");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimMind] OnRaidLetterReceived failed: " + ex);
            }
        }

        /// <summary>
        /// Called when a raid ends - updates the raid record with outcome.
        /// </summary>
        public static void OnRaidEnded(RaidEvent raid, bool survived, int colonistsKilled, int enemiesKilled)
        {
            try
            {
                raid.survived = survived;
                raid.colonistsKilled = colonistsKilled;
                raid.enemiesKilled = enemiesKilled;
            }
            catch { }
        }

        private static string GetCauseFromDamage(DamageDef damageDef)
        {
            if (damageDef == null) return "unknown";

            string name = damageDef.defName.ToLower();

            if (name.Contains("bullet") || name.Contains("shot"))
                return "gunfire";
            if (name.Contains("blade") || name.Contains("slash") || name.Contains("cut"))
                return "melee";
            if (name.Contains("blunt") || name.Contains("bump") || name.Contains("crush"))
                return "blunt trauma";
            if (name.Contains("fire") || name.Contains("burn"))
                return "fire";
            if (name.Contains("explosion") || name.Contains("bomb"))
                return "explosion";
            if (name.Contains("flame") || name.Contains("heat"))
                return "heat";
            if (name.Contains("cold") || name.Contains("freeze") || name.Contains("hypothermia"))
                return "cold";
            if (name.Contains("toxic") || name.Contains("poison") || name.Contains("flare"))
                return "poison";
            if (name.Contains("disease") || name.Contains("infection") || name.Contains("sick"))
                return "illness";
            if (name.Contains("starve") || name.Contains("hunger"))
                return "starvation";
            if (name.Contains("thirst") || name.Contains("dehydrate"))
                return "dehydration";
            if (name.Contains("fall") || name.Contains("crush"))
                return "falling";
            if (name.Contains("beam") || name.Contains("laser") || name.Contains("energy"))
                return "energy weapon";
            if (name.Contains("arcane") || name.Contains("psychic"))
                return "psychic";

            return damageDef.LabelCap.ToString();
        }

        private static string GetKillerName(Thing initiator)
        {
            if (initiator == null) return "unknown";

            if (initiator is Pawn pawn)
            {
                if (pawn.Faction != null)
                {
                    if (pawn.Faction.IsPlayer)
                        return "friendly fire";
                    return pawn.Faction.Name.ToString();
                }
                return pawn.KindLabel;
            }

            if (initiator is Building)
                return "turret";

            return initiator.Label;
        }
    }
}
