# RimMind - RimWorld AI Mod

## Project Overview
**Steam Workshop**: https://steamcommunity.com/sharedfiles/filedetails/?id=3666997391
RimMind is a RimWorld mod that integrates LLM intelligence via OpenRouter. The AI can query 51 different colony data tools via function calling.

## Build & Development

### Build Command (using Roslyn csc.exe - no .NET SDK needed)
```bash
GAME_DIR="C:/Program Files (x86)/Steam/steamapps/common/RimWorld"
MANAGED="$GAME_DIR/RimWorldWin64_Data/Managed"
HARMONY="C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/1446523594/1.5/Assemblies"
SRC_DIR="$GAME_DIR/Mods/RimMind/Source/RimMind"
OUT_DIR="$GAME_DIR/Mods/RimMind/Assemblies"
CSC="C:/Program Files (x86)/Microsoft Visual Studio/2019/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe"
FWDIR="C:/Windows/Microsoft.NET/Framework64/v4.0.30319"

cd "$SRC_DIR" && "$CSC" \
  -target:library -out:"$OUT_DIR/RimMind.dll" \
  -reference:"$MANAGED/Assembly-CSharp.dll" \
  -reference:"$MANAGED/UnityEngine.dll" \
  -reference:"$MANAGED/UnityEngine.CoreModule.dll" \
  -reference:"$MANAGED/UnityEngine.IMGUIModule.dll" \
  -reference:"$MANAGED/UnityEngine.TextRenderingModule.dll" \
  -reference:"$HARMONY/0Harmony.dll" \
  -reference:"$MANAGED/netstandard.dll" \
  -reference:"$FWDIR/mscorlib.dll" \
  -reference:"$FWDIR/System.dll" \
  -reference:"$FWDIR/System.Core.dll" \
  -reference:"$FWDIR/System.Net.dll" \
  -langversion:latest -nowarn:0168,0219 \
  -recurse:"*.cs"
```
Output DLL goes to `Assemblies/RimMind.dll`

**Note:** No .NET SDK is installed. We use the Roslyn csc.exe from VS 2019 Build Tools. The built-in `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe` only supports C# 5 and won't work.

### Key Paths
- **Mod root**: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimMind`
- **Game install**: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
- **Game DLLs**: `RimWorld\RimWorldWin64_Data\Managed\` (Assembly-CSharp.dll, UnityEngine*.dll)
- **Harmony DLL**: `Steam\steamapps\workshop\content\294100\1446523594\1.5\Assemblies\0Harmony.dll`
- **Vanilla Defs** (reference): `RimWorld\Data\Core\Defs\`
- **Source code**: `Mods\RimMind\Source\RimMind\`

### Target Framework
- **.NET Framework 4.7.2** (critical - RimWorld uses this)
- Assembly references must have `Private=false` (Copy Local = false)

## RimWorld Modding Reference

### Core Architecture
- **Defs** = XML blueprints the game engine reads (items, buildings, research, etc.)
- **ThingDef** = most common def type (items, pawns, buildings, plants)
- **Harmony** = runtime method patching (Prefix/Postfix/Finalizer)
- **GameComponent** = persisted per-game singleton (ticks every frame)
- **ModSettings** = persisted mod configuration via `ExposeData()`

### Key Game Classes
| Class | Purpose | Access via |
|-------|---------|-----------|
| `Find.CurrentMap` | Current active map | Static |
| `Find.WindowStack` | UI window management | Static |
| `Find.FactionManager` | All factions | Static |
| `Find.ResearchManager` | Research state | Static |
| `Find.Storyteller` | Active storyteller | Static |
| `Map.mapPawns` | All pawns on map | `Find.CurrentMap.mapPawns` |
| `Map.listerBuildings` | All buildings | `Find.CurrentMap.listerBuildings` |
| `Map.listerThings` | All things by type | `Find.CurrentMap.listerThings` |
| `Map.zoneManager` | All zones | `Find.CurrentMap.zoneManager` |
| `Map.resourceCounter` | Resource totals | `Find.CurrentMap.resourceCounter` |
| `Map.weatherManager` | Current weather | `Find.CurrentMap.weatherManager` |
| `Map.powerNetManager` | Power grids | `Find.CurrentMap.powerNetManager` |
| `Map.planManager` | Plan overlays (1.6+) | `Find.CurrentMap.planManager` |

### Pawn System
```
Pawn
├── .Name (PawnName - ToStringShort/ToStringFull)
├── .story (backstory, traits, bodyType)
│   ├── .Childhood / .Adulthood
│   └── .traits.allTraits
├── .skills (SkillTracker)
│   └── .GetSkill(SkillDefOf.X).Level / .passion
├── .health (HealthTracker)
│   ├── .hediffSet.hediffs (injuries, diseases, bionics)
│   ├── .capacities.GetLevel(PawnCapacityDefOf.X)
│   └── .HasHediffsNeedingTend()
├── .needs (NeedsTracker)
│   ├── .mood.CurLevelPercentage
│   ├── .food / .rest / .joy
│   └── .mood.thoughts.memories.Memories
├── .relations (RelationsTracker)
│   ├── .OpinionOf(otherPawn)
│   └── .DirectRelations
├── .workSettings.GetPriority(WorkTypeDef)
├── .timetable.GetAssignment(hour)
├── .equipment.Primary (weapon)
├── .apparel.WornApparel
├── .playerSettings.Master (for animals)
├── .training (for animals)
├── .CurJobDef (current job)
└── .MentalStateDef (mental break)
```

### UI System
- **Window** = base class for all custom windows
  - Override `DoWindowContents(Rect inRect)`
  - Properties: `draggable`, `resizeable`, `doCloseX`, `absorbInputAroundWindow`
  - Open: `Find.WindowStack.Add(new MyWindow())`
- **Widgets** = static utility class for drawing UI elements
  - `Widgets.Label(rect, text)`, `Widgets.ButtonText(rect, label)`
  - `Widgets.TextField(rect, text)`, `Widgets.CheckboxLabeled(rect, label, ref val)`
  - `Widgets.DrawBoxSolid(rect, color)`, `Widgets.BeginScrollView/EndScrollView`
- **Listing_Standard** = simplified layout helper for settings pages
- **Text.Font** = GameFont.Tiny / Small / Medium
- **Text.CalcHeight(text, width)** = calculate text height for layout

### MainButtonDef
Adds button to bottom toolbar:
```xml
<MainButtonDef>
    <defName>MyButton</defName>
    <label>My Label</label>
    <workerClass>MyMod.MainButtonWorker_MyThing</workerClass>
    <order>1200</order>
</MainButtonDef>
```
Worker class must extend `MainButtonWorker` and override `Activate()`.

### Harmony Patching
```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.MethodName))]
static class MyPatch
{
    static void Postfix(ref bool __result) { /* modify result */ }
    static bool Prefix() { return true; /* false skips original */ }
}
```
- **Postfix** preferred (safest, most compatible)
- **DO NOT bundle 0Harmony.dll** - declare as Steam dependency
- Auto-patch: `new Harmony("mymod.id").PatchAll()`

### Threading
- RimWorld is **single-threaded** - never modify game objects from background threads
- Pattern: `ThreadPool.QueueUserWorkItem` -> do work -> `MainThreadDispatcher.Enqueue(callback)`
- Our MainThreadDispatcher is a GameComponent that processes a ConcurrentQueue<Action> each tick

### Testing & Debugging Mods
*(Source: https://rimworldwiki.com/wiki/Modding_Tutorials/Testing_mods)*

**Enable Dev Mode:** Options > Gameplay > check "Development Mode" (persists until disabled)

**Quick Testing:**
- Launch arg `-quicktest` - instantly loads a Crashlanded map with defaults
- Launch arg `-savedatafolder=path` - redirects save data for isolated testing
- Create a Steam shortcut with these args for fast iteration

**Debug Spawn Commands (in dev mode toolbar):**
- "Spawn weapon" / "Spawn apparel" - test items with spawn chance diagnostics
- "Spawn pawn" - lists all PawnKindDefs
- Incident debug commands - trigger specific raids/events on demand

**Pawn AI Debugging:**
- "Toggle job logging" - see AI decision-making in log
- "Draw pawn debug" - visual overlay of pawn behavior
- Lord view settings - observe group AI, duties, state transitions (caravans, raids)

**Logging:**
- RimWorld log: `~` key opens console (with dev mode)
- Our mod logs with `[RimMind]` prefix via `Log.Message()` / `Log.Warning()` / `Log.Error()`
- Log file: `%APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

**C# Debugging:**
- Rider: Install Gareth's RimWorld plugin for breakpoint debugging
- Unity version: 2022.3.35 with modified Mono runtime

**Workshop Publishing:**
- Use dev mode Upload tool, or PublisherPlus for selective file exclusion and multi-version support

### Common DefOf Classes
`ThingDefOf`, `StatDefOf`, `SkillDefOf`, `WorkTypeDefOf`, `PawnCapacityDefOf`, `RoomStatDefOf`, `MentalStateDefOf`, `PawnRelationDefOf`, `TrainableDef`

### JSON in .NET 4.7.2
- No System.Text.Json available
- We use bundled SimpleJSON (MIT, single file) at `Source/RimMind/API/SimpleJSON.cs`

## OpenRouter API Reference

### Endpoint
`POST https://openrouter.ai/api/v1/chat/completions`

### Auth
`Authorization: Bearer YOUR_API_KEY`

### Request Format
```json
{
  "model": "anthropic/claude-sonnet-4-5",
  "messages": [{"role": "user", "content": "Hello"}],
  "tools": [{"type": "function", "function": {"name": "...", "parameters": {...}}}],
  "tool_choice": "auto",
  "temperature": 0.7,
  "max_tokens": 1024,
  "stream": false
}
```

### Tool Call Response
When AI wants to use tools, `choices[0].message.tool_calls` is an array:
```json
{"id": "call_123", "type": "function", "function": {"name": "list_colonists", "arguments": "{}"}}
```
Send results back as: `{"role": "tool", "tool_call_id": "call_123", "content": "..."}`

### Model ID Format
`provider/model-name` e.g.: `anthropic/claude-sonnet-4-5`, `openai/gpt-4o`, `meta-llama/llama-3.1-70b-instruct`

## Mod File Structure
```
RimMind/
├── About/About.xml
├── Defs/MainButtonDefs/MainButtons.xml
├── Assemblies/RimMind.dll
└── Source/RimMind/
    ├── RimMind.csproj
    ├── Core/          (RimMindMod, Settings, MainThreadDispatcher)
    ├── API/           (OpenRouterClient, DTOs, SimpleJSON, PromptBuilder)
    ├── Tools/         (46 tools: Colonist, Social, Work, Colony, Research, Military, Map, Animal, Event, Medical, Plan, Zone, Building)
    └── Chat/          (ChatWindow, ChatManager, ColonyContext)
```

## Current Tool Catalog (51 tools)
- **Colonist** (3): list_colonists, get_colonist_details, get_colonist_health
- **Social** (2): get_relationships, get_faction_relations
- **Work** (6): get_work_priorities, set_work_priority, get_bills, get_schedules, set_schedule, copy_schedule
- **Colony** (4): get_colony_overview, get_resources, get_rooms, get_stockpiles
- **Research** (3): get_research_status, get_available_research, get_completed_research
- **Military** (3): get_threats, get_defenses, get_combat_readiness
- **Map** (6): get_weather_and_season, get_growing_zones, get_power_status, get_map_region, get_cell_details, search_map
- **Animals** (2): list_animals, get_animal_details
- **Events** (2): get_recent_events, get_active_alerts
- **Medical** (1): get_medical_overview
- **Directives** (3): get_directives, add_directive, remove_directive
- **Plan** (3): get_plans, place_plans, remove_plans
- **Zone** (3): list_zones, create_zone, delete_zone
- **Building** (6): list_buildable, get_building_info, place_building, place_structure, remove_building, approve_buildings
- **Area** (4): list_areas, get_area_restrictions, restrict_to_area, unrestrict

## Development Rules
- **Keep this file updated.** Every time a feature is built, a bug is fixed, or a tool is added, update the relevant sections of this CLAUDE.md. This file is the living index of the project — future AI sessions rely on it to understand what exists, how it works, and what has changed.

## Changelog
- **2026-02-15**: Added `get_map_region` tool — character-grid map visualization with 28 cell codes for buildings, pawns, items, zones, terrain. Supports full map or sub-region queries.
- **2026-02-15**: Added `get_cell_details` tool — drill-down for single cell or range (up to 15x15). Returns terrain, roof, temperature, fertility, room stats, zone, and all things present.
- **2026-02-15**: Fixed Enter key in chat window — overrode `OnAcceptKeyPressed()` and set `forceCatchAcceptAndCancelEventEvenIfUnfocused = true`. RimWorld's `WindowStack.Notify_PressedAccept` skips windows where both `closeOnAccept` and `forceCatch` are false. The keybinding system consumes Return before `DoWindowContents` runs, so KeyDown handlers there never see it.
- **2026-02-15**: Fixed chat scroll-to-bottom on reopen — added `PostOpen()` override that sets `scrollToBottom = true`.
- **2026-02-15**: Added `place_plans` and `remove_plans` tools — first write-action tools. Place plan designations with shape support (single, rect, filled_rect, line via Bresenham) and remove by cell, area, or all. Plan designations now visible in map grid as 'p' character and in cell details as designations array.
- **2026-02-15**: Added zone tools (`list_zones`, `create_zone`, `delete_zone`) — AI can now view all native zones (growing/stockpile) with bounds, and create/delete labeled planning zones for housing, defense, prison, etc. Planning zones persist with save files via ZoneTracker GameComponent. Zones optionally draw plan designation outlines on map. Custom zones show as 'z' in map grid.
- **2026-02-15**: Added building placement system — 5 new tools (list_buildable, get_building_info, place_building, remove_building, approve_buildings). AI places forbidden blueprints that colonists won't build until player approves. Added BuildingForbiddablePatcher to ensure all building/blueprint defs support forbid toggle. Added ProposalTracker GameComponent for save-persistent tracking of AI-placed blueprints.
- **2026-02-15**: Added `get_plans` tool — AI can now see all plan designations on the map (including manually placed by player). Returns total count, bounding box, and cell coordinates.
- **2026-02-15**: Reworked zone tools to integrate with real RimWorld zone system — `create_zone` now creates actual stockpile and growing zones (not just custom labels). `list_zones` now shows Areas (Home, Allowed, Snow Clear, Roof). `delete_zone` can remove real game zones.
- **2026-02-15**: Added DebugLogger — writes timestamped logs to `RimMind/Logs/debug.log`, covering all API requests/responses, tool calls with args/results/timing, and chat messages. Clears on each startup.
- **2026-02-15**: Increased tool call loop limit from 5 to 15 and history trim from 40 to 500 messages.
- **2026-02-15**: Rewrote plan tools to use RimWorld 1.6's native `Plan` API (`Map.planManager`, `Verse.Plan`) instead of old `DesignationDefOf.Plan` designations. Plans placed by AI are now fully interactable — player can click, rename, recolor, copy/paste, and remove them using the in-game planning tools. `get_plans` reads from `planManager.AllPlans`. `remove_plans` now supports removal by label. Map grid uses `planManager.PlanAt()` for 'p' character.
- **2026-02-15**: Increased default max_tokens from 1024 to 4096 to support large building operations. Added BUILDING GUIDELINES to system prompt instructing AI to batch placements (20-30 per call) and build room-by-room. Added full communication logging — raw JSON request/response bodies now logged to debug.log without truncation. Increased tool arg/result truncation limits (500→2000 / 1000→5000).
- **2026-02-15**: Added player directives system — per-save "colony personality" that persists with save files. Players define playstyle rules and preferences (e.g., "melee only", "no pyromaniacs") that get injected into every AI system prompt. 3 new tools (get_directives, add_directive, remove_directive) let the AI manage directives during conversation. Auto-detection (togglable in settings) prompts the AI to offer saving preferences it notices in chat. DirectivesWindow provides manual viewing/editing with a button in the chat header (turns green when directives are active).
- **2026-02-16**: Major building system intelligence overhaul — Added `place_structure` tool with shape primitives (`room`, `wall_line`, `wall_rect`). Fixed double-encoded JSON arrays in batch ops. Fuzzy defName/stuff matching with suggestions. Shared wall detection (overlapping rooms auto-skip existing walls). Blueprint visibility in map grid (lowercase chars for blueprints). Enriched build results: `existing_in_area` pre-scan, `area_after` grid, `buildings_in_area` structured list with defNames/sizes/materials. Enriched dynamic legend shows actual defNames. Auto-rotation for furniture placement (tries all 4 rotations before failing). Interaction spot info in `get_building_info`. Richer error messages ("Occupied by table (2x4)"). `auto_approve` parameter. `stuffHint` in list_buildable. Expanded system prompt with common buildings, coordinate system, room templates, multi-cell footprints, and interaction spot guidance. Increased batch limit to 100.
- **2026-02-16**: Added token usage tracking — ChatResponse now parses `usage` from OpenRouter (prompt_tokens/completion_tokens/total_tokens) and Anthropic (input_tokens/output_tokens) responses. ChatManager exposes LastPromptTokens/LastCompletionTokens/LastTotalTokens. ChatWindow displays compact token counter below title bar.
- **2026-02-16**: Added Context Inspector window — tabbed view (System/Tools/Chat) showing the full context sent to the LLM. System tab shows system prompt with building guidelines, colony context, directives. Tools tab shows all tool definitions with parameters. Chat tab shows conversation history with tool calls. Uses Consolas monospace font for grid/JSON readability. Content split into 2k-char chunks for reliable scrolling. Opened via "Context" button in chat header.
- **2026-02-16**: Rewrote building system prompt from rules-based to workflow-based — replaced 50+ specific rules with a mandatory 6-step LOOK-PLAN-BUILD-VERIFY workflow. AI must: (1) call get_map_region before building, (2) read area_after grid after placing structure to find door position, (3) plan furniture using the grid data ensuring door clearance, (4) verify final layout. Door clearance derived from reading 'd' characters in the grid rather than memorized coordinate rules.
- **2026-02-16**: Added quick prompt buttons to ChatWindow — 10 test prompts (Bedroom, Dining+Kitchen, Barracks, Power Setup, Workshop, Hospital, Killbox, Base Layout, Colony Status, Map Scout) shown in a toggleable scrollable panel. Click to insert prompt text.
- **2026-02-16**: Added `search_map` tool — search the map for entities by type (colonists, hostiles, animals, items, buildings, minerals, plants) with optional text filter and bounds. Returns exact coordinates instead of requiring grid scanning. Items are grouped by defName with aggregate counts. Minerals filter to ore deposits (mineableThing != null). Also added `pawns` field to `get_map_region` response listing all pawns in the region with name/position/type, so the AI doesn't need to hunt for '@' characters.

## Future Plans (Deferred)
- Phase 3: LLM-powered colonist dialogue (Harmony patch on social interactions)
- Phase 4: AI storyteller (custom StorytellerComp querying LLM for event decisions)
