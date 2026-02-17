# Phase 1 Implementation Guide: Critical Real-Time Awareness

## Overview
This guide provides concrete implementation steps for Phase 1 of the visibility roadmap. Phase 1 adds always-on contextual awareness so RimMind knows critical game state without needing tool calls.

---

## 1.1 Auto-Context System

### Goal
Every AI request includes lightweight game state context automatically.

### Implementation

#### Step 1: Create `GameStateContext.cs`

**Location:** `Source/RimMind/Chat/GameStateContext.cs`

```csharp
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
                .Where(p => p.Spawned && p.MentalStateDef == MentalStateDefOf.Manhunter || 
                           p.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
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
                        .Where(l => l.CreatedTicksGame > tenMinutesAgo)
                        .Where(l => l.def.defName == "ThreatBig" || l.def.defName == "ThreatSmall" || 
                                   l.def.defName == "Death" || l.def.defName == "NegativeEvent")
                        .OrderByDescending(l => l.CreatedTicksGame)
                        .Take(5);

                    foreach (var letter in recentLetters)
                    {
                        int minutesAgo = (currentTick - letter.CreatedTicksGame) / (60 * 60);
                        events.Add($"{letter.Label} ({minutesAgo}m ago)");
                    }
                }
            }
            catch { }

            return events;
        }
    }
}
```

#### Step 2: Integrate into `PromptBuilder.cs`

**Location:** `Source/RimMind/API/PromptBuilder.cs`

**Modify `BuildChatSystemPrompt` method:**

```csharp
public static string BuildChatSystemPrompt(string colonyContext, string playerDirectives)
{
    var sb = new System.Text.StringBuilder();

    // Add auto-context at the very top (BEFORE general instructions)
    sb.Append(GameStateContext.GetAutoContext());
    sb.AppendLine();
    sb.AppendLine("===========================");
    sb.AppendLine();

    // Rest of system prompt follows...
    sb.Append(@"You are RimMind, an AI advisor embedded in a RimWorld colony...");
    
    // ... existing prompt content ...
}
```

#### Step 3: Update CLAUDE.md

Add to changelog:
```markdown
- **2026-02-17**: Added auto-context system — every AI request now includes real-time game state (date, colonist count, wealth, urgent alerts, active threats, recent events) without requiring tool calls. AI now has constant situational awareness of critical state changes.
```

---

## 1.2 Enhanced Alert Visibility

### Goal
Improve `get_active_alerts` tool to include severity, countdown timers, and colonist names.

### Implementation

#### Enhance `EventTools.GetActiveAlerts()`

**Location:** `Source/RimMind/Tools/EventTools.cs`

```csharp
public static string GetActiveAlerts()
{
    var arr = new JSONArray();

    try
    {
        var alertsReadout = ((UIRoot_Play)Find.UIRoot).alerts;
        if (alertsReadout != null)
        {
            var allAlertsField = typeof(AlertsReadout).GetField("AllAlerts", BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? typeof(AlertsReadout).GetField("allAlerts", BindingFlags.NonPublic | BindingFlags.Instance);

            System.Collections.Generic.List<Alert> allAlerts = null;
            if (allAlertsField != null)
                allAlerts = allAlertsField.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;

            if (allAlerts != null)
            {
                // Sort by priority (Critical first)
                var sortedAlerts = allAlerts
                    .Where(a => a.Active)
                    .OrderByDescending(a => a.Priority);

                foreach (var alert in sortedAlerts)
                {
                    var obj = new JSONObject();
                    obj["label"] = alert.GetLabel().ToString();
                    obj["priority"] = alert.Priority.ToString(); // Critical, High, Medium, Low
                    
                    // Severity number for easy filtering (4=Critical, 3=High, 2=Medium, 1=Low)
                    obj["severity"] = (int)alert.Priority;

                    TaggedString explanation = alert.GetExplanation();
                    string explanationStr = explanation.ToString();
                    if (!string.IsNullOrEmpty(explanationStr) && explanationStr.Length > 300)
                        explanationStr = explanationStr.Substring(0, 300) + "...";
                    obj["explanation"] = explanationStr;

                    // Try to extract colonist names from alert text
                    var colonistNames = ExtractColonistNames(alert.GetLabel().ToString(), explanation.ToString());
                    if (colonistNames.Count > 0)
                    {
                        var namesArray = new JSONArray();
                        foreach (var name in colonistNames)
                            namesArray.Add(name);
                        obj["affected_colonists"] = namesArray;
                    }

                    // Add alert-specific metadata
                    obj["alert_type"] = alert.GetType().Name;

                    arr.Add(obj);
                }
            }
        }
    }
    catch (System.Exception ex)
    {
        var errObj = new JSONObject();
        errObj["error"] = "Could not read alerts: " + ex.Message;
        arr.Add(errObj);
    }

    var result = new JSONObject();
    result["activeAlerts"] = arr;
    result["count"] = arr.Count;
    
    // Add summary counts by severity
    var summary = new JSONObject();
    summary["critical"] = arr.Count(a => a.AsObject["priority"]?.Value == "Critical");
    summary["high"] = arr.Count(a => a.AsObject["priority"]?.Value == "High");
    summary["medium"] = arr.Count(a => a.AsObject["priority"]?.Value == "Medium");
    summary["low"] = arr.Count(a => a.AsObject["priority"]?.Value == "Low");
    result["summary"] = summary;

    return result.ToString();
}

private static System.Collections.Generic.List<string> ExtractColonistNames(string label, string explanation)
{
    var names = new System.Collections.Generic.List<string>();
    var map = Find.CurrentMap;
    if (map == null) return names;

    var colonists = map.mapPawns.FreeColonists;
    foreach (var pawn in colonists)
    {
        string shortName = pawn.Name?.ToStringShort;
        if (!string.IsNullOrEmpty(shortName))
        {
            if (label.Contains(shortName) || explanation.Contains(shortName))
            {
                if (!names.Contains(shortName))
                    names.Add(shortName);
            }
        }
    }

    return names;
}
```

---

## 1.3 Colonist Location Tracking

### Goal
New tool to see all colonist positions in real-time.

### Implementation

#### Add `GetColonistLocations()` to `ColonistTools.cs`

```csharp
public static string GetColonistLocations()
{
    var map = Find.CurrentMap;
    if (map == null) return ToolExecutor.JsonError("No active map.");

    var colonists = map.mapPawns.FreeColonists;
    var arr = new JSONArray();

    foreach (var pawn in colonists)
    {
        var obj = new JSONObject();
        obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";
        obj["x"] = pawn.Position.x;
        obj["z"] = pawn.Position.z;

        // Status flags
        obj["drafted"] = pawn.drafter?.Drafted == true;
        obj["downed"] = pawn.Downed;
        obj["mentalState"] = pawn.InMentalState;

        if (pawn.CurJobDef != null)
            obj["currentJob"] = pawn.CurJobDef.reportString ?? pawn.CurJobDef.defName;
        else
            obj["currentJob"] = "Idle";

        // Distance from home area center (rough indicator of "far from home")
        var homeArea = map.areaManager.Home;
        if (homeArea != null && homeArea.TrueCount > 0)
        {
            // Find home area centroid (rough approximation)
            var homeCell = homeArea.ActiveCells.FirstOrDefault();
            float distance = pawn.Position.DistanceTo(homeCell);
            obj["distanceFromHome"] = distance.ToString("F0");
            
            if (distance > 100)
                obj["farFromHome"] = true;
        }

        // Temperature danger
        float temp;
        if (GenTemperature.TryGetTemperatureForCell(pawn.Position, map, out temp))
        {
            obj["currentTemp"] = temp.ToString("F0") + "°C";
            
            // Check if dangerously hot or cold
            var comfortRange = pawn.ComfortableTemperatureRange();
            if (temp < comfortRange.min - 10)
                obj["temperatureRisk"] = "freezing";
            else if (temp > comfortRange.max + 10)
                obj["temperatureRisk"] = "overheating";
        }

        arr.Add(obj);
    }

    var result = new JSONObject();
    result["colonists"] = arr;
    result["count"] = arr.Count;
    return result.ToString();
}
```

#### Register tool in `ToolExecutor.cs`

```csharp
{ "get_colonist_locations", args => ColonistTools.GetColonistLocations() },
```

#### Add tool definition in `ToolDefinitions.cs`

```csharp
tools.Add(MakeTool("get_colonist_locations", 
    "Get real-time location and status for all colonists. Returns position (x, z), drafted/downed/mental state, current job, distance from home, and temperature risk. Use this to check where everyone is and if anyone is in danger."));
```

---

## 1.4 Resource Consumption Rates

### Goal
New tool to track resource burn rates and predict shortages.

### Implementation

#### Create `ResourceTracker.cs` GameComponent

**Location:** `Source/RimMind/Core/ResourceTracker.cs`

```csharp
using System.Collections.Generic;
using Verse;

namespace RimMind.Core
{
    public class ResourceTracker : GameComponent
    {
        private Dictionary<string, int> resourceSnapshots = new Dictionary<string, int>();
        private int lastSnapshotTick = 0;
        private const int SnapshotInterval = 60000; // 1 game day

        public ResourceTracker(Game game) { }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Take snapshot every day
            if (currentTick - lastSnapshotTick >= SnapshotInterval)
            {
                TakeSnapshot();
                lastSnapshotTick = currentTick;
            }
        }

        private void TakeSnapshot()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            // Store snapshots (keep last 7 days)
            var key = Find.TickManager.TicksGame.ToString();
            
            // Count key resources
            resourceSnapshots["food_" + key] = CountResource(map, "Nutrition");
            resourceSnapshots["medicine_" + key] = CountResource(map, "Medicine");
            resourceSnapshots["wood_" + key] = CountResource(map, "WoodLog");
            resourceSnapshots["steel_" + key] = CountResource(map, "Steel");

            // Clean old snapshots (keep last 7 days)
            CleanOldSnapshots();
        }

        private int CountResource(Map map, string defName)
        {
            int total = 0;
            foreach (var thing in map.listerThings.AllThings)
            {
                if (thing.def.defName == defName || 
                    (defName == "Nutrition" && thing.def.IsNutritionGivingIngestible))
                {
                    total += thing.stackCount;
                }
            }
            return total;
        }

        private void CleanOldSnapshots()
        {
            int sevenDaysAgo = Find.TickManager.TicksGame - (7 * SnapshotInterval);
            var keysToRemove = new List<string>();
            
            foreach (var kvp in resourceSnapshots)
            {
                string[] parts = kvp.Key.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int tick))
                {
                    if (tick < sevenDaysAgo)
                        keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
                resourceSnapshots.Remove(key);
        }

        public int GetSnapshot(string resource, int daysAgo)
        {
            int tick = Find.TickManager.TicksGame - (daysAgo * SnapshotInterval);
            string key = resource + "_" + tick;
            return resourceSnapshots.ContainsKey(key) ? resourceSnapshots[key] : -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref resourceSnapshots, "resourceSnapshots");
            Scribe_Values.Look(ref lastSnapshotTick, "lastSnapshotTick");
        }
    }
}
```

#### Add `GetResourceTrends()` to `ColonyTools.cs`

```csharp
public static string GetResourceTrends()
{
    var map = Find.CurrentMap;
    if (map == null) return ToolExecutor.JsonError("No active map.");

    var tracker = Current.Game.GetComponent<RimMind.Core.ResourceTracker>();
    if (tracker == null)
        return ToolExecutor.JsonError("Resource tracking not available yet. Please wait one game day for data collection.");

    var result = new JSONObject();

    // Food
    result["food"] = AnalyzeResource("food", tracker, map, "Nutrition");
    
    // Medicine
    result["medicine"] = AnalyzeResource("medicine", tracker, map, "Medicine");
    
    // Wood
    result["wood"] = AnalyzeResource("wood", tracker, map, "WoodLog");
    
    // Steel
    result["steel"] = AnalyzeResource("steel", tracker, map, "Steel");

    return result.ToString();
}

private static JSONObject AnalyzeResource(string name, RimMind.Core.ResourceTracker tracker, Map map, string defName)
{
    var obj = new JSONObject();

    // Current count
    int current = CountResource(map, defName);
    obj["current"] = current;

    // Count from 1 day ago
    int oneDayAgo = tracker.GetSnapshot(name, 1);
    
    if (oneDayAgo >= 0)
    {
        int change = current - oneDayAgo;
        obj["change_24h"] = change;
        obj["burn_rate_per_day"] = -change; // Negative change = consumption

        if (change < 0)
        {
            float daysRemaining = current / (float)(-change);
            obj["days_remaining"] = daysRemaining.ToString("F1");

            if (daysRemaining < 2)
                obj["status"] = "critically_low";
            else if (daysRemaining < 5)
                obj["status"] = "low";
            else if (daysRemaining < 10)
                obj["status"] = "adequate";
            else
                obj["status"] = "plentiful";
        }
        else if (change > 0)
        {
            obj["status"] = "increasing";
        }
        else
        {
            obj["status"] = "stable";
        }
    }
    else
    {
        obj["status"] = "data_pending";
    }

    return obj;
}

private static int CountResource(Map map, string defName)
{
    int total = 0;
    foreach (var thing in map.listerThings.AllThings)
    {
        if (thing.def.defName == defName || 
            (defName == "Nutrition" && thing.def.IsNutritionGivingIngestible))
        {
            total += thing.stackCount;
        }
    }
    return total;
}
```

#### Register ResourceTracker GameComponent

Add to `RimMindMod.cs` or create a def:

**File:** `Defs/GameComponents.xml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Defs>
    <GameComponentDef>
        <defName>ResourceTracker</defName>
        <gameComponentClass>RimMind.Core.ResourceTracker</gameComponentClass>
    </GameComponentDef>
</Defs>
```

---

## Testing Phase 1

### Test 1: Auto-Context Visibility
1. Start RimWorld with mod
2. Open RimMind chat
3. Say "What's the current situation?"
4. **Expected:** AI response includes game state data without calling tools

### Test 2: Enhanced Alerts
1. Create alert scenario (colonist downed, low food, etc.)
2. Call `get_active_alerts`
3. **Expected:** Alert includes severity, affected colonists, sorted by urgency

### Test 3: Colonist Locations
1. Draft some colonists, leave others working
2. Call `get_colonist_locations`
3. **Expected:** See all colonists with positions, drafted status, jobs

### Test 4: Resource Trends
1. Wait one game day for data collection
2. Call `get_resource_trends`
3. **Expected:** See burn rates and days remaining for food/medicine/wood/steel

---

## Summary

Phase 1 adds four critical features:
1. **Auto-context** - Always-on game state in every AI request
2. **Enhanced alerts** - Severity scoring, colonist names, urgency sorting
3. **Colonist locations** - Real-time position tracking with danger indicators
4. **Resource trends** - Burn rate tracking and shortage prediction

**Implementation time:** ~4 hours  
**New tools:** 2 (get_colonist_locations, get_resource_trends)  
**Enhanced tools:** 1 (get_active_alerts)  
**New components:** 2 (GameStateContext, ResourceTracker)

This foundation enables RimMind to have situational awareness comparable to a human player watching the screen.
