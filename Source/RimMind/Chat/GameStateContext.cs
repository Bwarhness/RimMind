using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimMind.Chat
{
    public static class GameStateContext
    {
        public static string GetAutoContext()
        {
            var map = Find.CurrentMap;
            if (map == null) return "No active colony map.";

            var sb = new StringBuilder();

            // Basic game state
            sb.AppendLine("=== CURRENT GAME STATE ===");
            sb.AppendFormat("Date: Day {0}, {1}, {2}\n", 
                GenDate.DaysPassed, 
                GenLocalDate.Season(map).LabelCap(), 
                GenDate.Year(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x));
            
            sb.AppendFormat("Time: {0}:00\n", GenLocalDate.HourInteger(map));
            
            // Colony basics
            var colonists = map.mapPawns.FreeColonists.ToList();
            sb.AppendFormat("Colonists: {0} alive", colonists.Count);
            
            int downed = colonists.Count(p => p.Downed);
            int drafted = colonists.Count(p => p.drafter?.Drafted == true);
            int mentalBreak = colonists.Count(p => p.InMentalState);
            
            if (downed > 0 || drafted > 0 || mentalBreak > 0)
            {
                sb.Append(" (");
                var states = new System.Collections.Generic.List<string>();
                if (downed > 0) states.Add($"{downed} downed");
                if (drafted > 0) states.Add($"{drafted} drafted");
                if (mentalBreak > 0) states.Add($"{mentalBreak} mental break");
                sb.Append(string.Join(", ", states));
                sb.Append(")");
            }
            sb.AppendLine();

            // Wealth
            float wealth = map.wealthWatcher.WealthTotal;
            sb.AppendFormat("Colony Wealth: {0:N0}\n", wealth);

            // Environment
            sb.AppendFormat("Weather: {0}, {1}°C outside\n", 
                map.weatherManager.curWeather?.LabelCap.ToString() ?? "Clear",
                map.mapTemperature.OutdoorTemp.ToString("F0"));

            // Critical alerts (top 3 most urgent)
            var criticalAlerts = GetCriticalAlerts();
            if (criticalAlerts.Count > 0)
            {
                sb.AppendLine("\n⚠️ URGENT ALERTS:");
                foreach (var alert in criticalAlerts.Take(3))
                {
                    sb.AppendFormat("  • {0}\n", alert);
                }
            }

            // Active threats
            var threats = GetActiveThreats(map);
            if (threats.Count > 0)
            {
                sb.AppendLine("\n🚨 ACTIVE THREATS:");
                foreach (var threat in threats)
                {
                    sb.AppendFormat("  • {0}\n", threat);
                }
            }

            // Recent critical events (last 10 minutes of game time)
            var recentEvents = GetRecentCriticalEvents();
            if (recentEvents.Count > 0)
            {
                sb.AppendLine("\n📋 RECENT EVENTS:");
                foreach (var evt in recentEvents.Take(3))
                {
                    sb.AppendFormat("  • {0}\n", evt);
                }
            }

            return sb.ToString();
        }

        private static System.Collections.Generic.List<string> GetCriticalAlerts()
        {
            var alerts = new System.Collections.Generic.List<string>();

            try
            {
                var alertsReadout = ((UIRoot_Play)Find.UIRoot).alerts;
                if (alertsReadout != null)
                {
                    var allAlertsField = typeof(AlertsReadout).GetField("AllAlerts", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?? typeof(AlertsReadout).GetField("allAlerts", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    System.Collections.Generic.List<Alert> allAlerts = null;
                    if (allAlertsField != null)
                        allAlerts = allAlertsField.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;

                    if (allAlerts != null)
                    {
                        // Only include Critical and High priority alerts
                        var urgentAlerts = allAlerts
                            .Where(a => a.Active && (a.Priority == AlertPriority.Critical || a.Priority == AlertPriority.High))
                            .OrderByDescending(a => a.Priority)
                            .Take(5);

                        foreach (var alert in urgentAlerts)
                        {
                            alerts.Add($"{alert.Priority}: {alert.GetLabel()}");
                        }
                    }
                }
            }
            catch { }

            return alerts;
        }

        private static System.Collections.Generic.List<string> GetActiveThreats(Map map)
        {
            var threats = new System.Collections.Generic.List<string>();

            // Hostile pawns
            var hostiles = map.mapPawns.AllPawns
                .Where(p => p.Spawned && !p.Downed && p.HostileTo(Faction.OfPlayer))
                .ToList();

            if (hostiles.Count > 0)
            {
                var factionGroups = hostiles.GroupBy(p => p.Faction?.Name ?? "Unknown");
                foreach (var group in factionGroups)
                {
                    threats.Add($"{group.Count()} {group.Key} hostiles on map");
                }
            }

            // Manhunter animals
            var manhunters = map.mapPawns.AllPawns
                .Where(p => p.Spawned && (p.MentalStateDef == MentalStateDefOf.Manhunter || 
                           p.MentalStateDef == MentalStateDefOf.ManhunterPermanent))
                .ToList();

            if (manhunters.Count > 0)
            {
                threats.Add($"{manhunters.Count} manhunter animals");
            }

            return threats;
        }

        private static System.Collections.Generic.List<string> GetRecentCriticalEvents()
        {
            var events = new System.Collections.Generic.List<string>();

            try
            {
                var archive = Find.Archive;
                if (archive != null)
                {
                    int currentTick = Find.TickManager.TicksGame;
                    int tenMinutesAgo = currentTick - (600 * 60); // 10 minutes = 600 seconds * 60 ticks/sec

                    var recentLetters = archive.ArchivablesListForReading
                        .OfType<Letter>()
                        .Where(l => l.arrivalTime > tenMinutesAgo)
                        .Where(l => l.def.defName == "ThreatBig" || l.def.defName == "ThreatSmall" || 
                                   l.def.defName == "Death" || l.def.defName == "NegativeEvent")
                        .OrderByDescending(l => l.arrivalTime)
                        .Take(5);

                    foreach (var letter in recentLetters)
                    {
                        int minutesAgo = (currentTick - letter.arrivalTime) / (60 * 60);
                        events.Add($"{letter.Label} ({minutesAgo}m ago)");
                    }
                }
            }
            catch { }

            return events;
        }
    }
}
