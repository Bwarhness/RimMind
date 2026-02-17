# Blueprint Visibility Testing Guide

## Problem Statement
RimMind couldn't properly see and understand blueprints that players placed on the map. When asked "Do you see the blueprints?", RimMind would respond with "No blueprints on the map yet" even when blueprints were visible as lowercase characters in the grid.

## Solution Implemented

### 1. System Prompt Enhancement
**File:** `Source/RimMind/API/PromptBuilder.cs`

Added explicit MAP GRID READING section at the top of the system prompt explaining:
- Uppercase letters = BUILT structures (W, D, B, etc.)
- Lowercase letters = BLUEPRINTS (w, d, b, etc.)
- Blueprints are unbuilt designations that colonists will construct later
- When players ask about blueprints, check for lowercase codes or use get_blueprints tool

### 2. New Tool: get_blueprints
**Files:** 
- `Source/RimMind/Tools/MapTools.cs` (implementation)
- `Source/RimMind/Tools/ToolExecutor.cs` (registration)
- `Source/RimMind/Tools/ToolDefinitions.cs` (API definition)

**Purpose:** Dedicated tool to query all placed blueprints on the map

**Parameters:**
- `filter` (optional): Text filter matching defName or label
- `x1`, `z1`, `x2`, `z2` (optional): Bounds for search area

**Returns:**
```json
{
  "total": 15,
  "returned": 15,
  "blueprints": [
    {
      "defName": "Wall",
      "label": "wall",
      "x": 120,
      "z": 85,
      "material": "granite blocks",
      "size": "1x1",
      "rotation": 0
    },
    {
      "defName": "Door",
      "label": "door",
      "x": 125,
      "z": 85,
      "material": "wood",
      "rotation": 0
    }
  ]
}
```

### 3. Enhanced Cell Details
**File:** `Source/RimMind/Tools/MapTools.cs` - `GetSingleCellDetail` method

Cell details now separate blueprints from built structures:

**Before:**
```json
{
  "things": [
    {"name": "wall (blueprint)", "type": "Blueprint"},
    {"name": "table", "type": "Building"}
  ]
}
```

**After:**
```json
{
  "blueprints": [
    {
      "defName": "Wall",
      "label": "wall",
      "status": "unbuilt",
      "material": "granite blocks"
    }
  ],
  "things": [
    {
      "name": "table",
      "type": "Building",
      "status": "built",
      "hitPoints": "100/100"
    }
  ]
}
```

## Testing Procedure

### Test 1: Blueprint Detection via get_blueprints
1. In RimWorld, place several blueprint designations (walls, doors, beds, etc.)
2. Open RimMind chat
3. Ask: "Do you see any blueprints on the map?"
4. **Expected:** RimMind calls get_blueprints tool and reports all placed blueprints with positions and details

### Test 2: Blueprint vs Built Structure Distinction
1. Place a wall blueprint at (100, 100)
2. Build a wall at (105, 100)
3. Ask: "What's at coordinates (100, 100) and (105, 100)?"
4. **Expected:** 
   - (100, 100) shows blueprint with "unbuilt" status
   - (105, 100) shows built wall with "built" status and hit points

### Test 3: Map Grid Reading
1. Place a room blueprint with walls (w), door (d), and bed blueprints (b)
2. Ask: "Show me the map grid around coordinates (X, Z)"
3. **Expected:** RimMind shows grid with lowercase characters (w, d, b) and explains these are blueprints in the legend

### Test 4: Design Feedback
1. Place a bedroom blueprint (walls + door + bed + dresser + lamp)
2. Ask: "What do you think of the bedroom blueprint I placed?"
3. **Expected:** RimMind:
   - Calls get_blueprints or get_map_region
   - Identifies the blueprint layout
   - Provides feedback on the design (door position, furniture placement, etc.)
   - Understands this is a PLANNED room, not yet built

### Test 5: Filter by Blueprint Type
1. Place various blueprints: walls, doors, beds, tables, etc.
2. Ask: "Show me all door blueprints"
3. **Expected:** RimMind calls get_blueprints with filter="door" and lists only door blueprints

## Verification Checklist

- [ ] get_blueprints tool registered in ToolExecutor.cs
- [ ] get_blueprints tool defined in ToolDefinitions.cs
- [ ] System prompt explains uppercase/lowercase distinction
- [ ] GetSingleCellDetail separates blueprints array from things array
- [ ] Blueprint legend descriptions include "(blueprint)" suffix
- [ ] CLAUDE.md updated with changelog entry
- [ ] Tool count updated (51 → 52 tools, Map tools 6 → 7)

## Code Changes Summary

### MapTools.cs
- Added `GetBlueprints(JSONNode args)` method
- Modified `GetSingleCellDetail()` to separate blueprints from things
- Existing `ClassifyCell()` already handled lowercase blueprint codes

### PromptBuilder.cs
- Added MAP GRID READING section explaining uppercase/lowercase distinction

### ToolExecutor.cs
- Added get_blueprints handler in dictionary

### ToolDefinitions.cs
- Added get_blueprints tool definition with full parameter schema

### CLAUDE.md
- Added changelog entry for 2026-02-17
- Updated tool count from 51 to 52
- Updated Map tools from 6 to 7

## Expected Player Impact

**Before:**
- Player: "Do you see the blueprints I put down?"
- RimMind: "No blueprints on the map yet"
- Player: confused and frustrated

**After:**
- Player: "Do you see the blueprints I put down?"
- RimMind: "Yes! I see 12 blueprints: 8 walls, 2 doors, 1 bed, and 1 table at coordinates..."
- Player: confident RimMind understands their plans

**Additional Benefits:**
- RimMind can provide design feedback BEFORE construction
- RimMind can suggest improvements to blueprint layouts
- RimMind can help identify issues (blocked doors, missing walls, etc.)
- RimMind understands the difference between player intentions (blueprints) and reality (built structures)
