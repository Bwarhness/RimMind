# RimMind Visibility Audit & Improvement Roadmap

## Executive Summary

**Goal:** Give RimMind the same "eyes" as a human player watching the screen.

**Current State:** RimMind has 52 tools covering colonists, work, resources, research, military, map, animals, events, zones, and building. However, much of this data requires explicit tool calls and lacks real-time visibility of critical game state.

**Key Finding:** RimMind has **pull-based visibility** (must call tools to see data) but lacks **push-based awareness** (automatic context about urgent/important state changes). Human players see alerts, status bars, notifications, and visual indicators constantly — RimMind doesn't.

---

## Part 1: Current Visibility Matrix

### ✅ **What RimMind CAN See** (with tool calls)

#### **Colonists & Social** (7 tools)
- ✅ List colonists with mood, current job, mental state
- ✅ Full colonist details: backstory, traits, skills, needs, thoughts
- ✅ Health details: injuries, diseases, bionics, pain, capacities
- ✅ Social relationships: opinions, bonds, rivalries
- ✅ Faction relations & diplomacy
- ✅ Draft/undraft control

**Strength:** Comprehensive colonist data  
**Weakness:** No real-time location tracking, no "colonist is in danger" awareness

#### **Work & Jobs** (11 tools)
- ✅ Work priorities grid (all colonists, all work types)
- ✅ Production bills at workbenches
- ✅ Daily schedules (sleep/work/joy/anything)
- ✅ Job prioritization (rescue, tend, haul, repair, clean)
- ✅ Set work priorities and schedules

**Strength:** Full work management  
**Weakness:** Can't see work queue depth, can't see what jobs are pending/blocked, can't see efficiency metrics

#### **Colony & Resources** (4 tools)
- ✅ Colony overview: colonist count, wealth, days survived, difficulty
- ✅ Resources by category: food, materials, weapons, apparel, medicine
- ✅ Rooms: type, impressiveness, beauty, cleanliness, space, owner
- ✅ Stockpiles: name, priority, filters

**Strength:** Good high-level view  
**Weakness:** No resource consumption rate, no "low on X" warnings, no wealth breakdown

#### **Research** (3 tools)
- ✅ Current research project & progress
- ✅ Available research projects
- ✅ Completed research

**Strength:** Complete research visibility  
**Weakness:** No research speed calculation, no optimal research path suggestions

#### **Military & Threats** (3 tools)
- ✅ Active threats: hostiles, sieges, infestations, manhunters
- ✅ Defenses: turrets, traps, barricades with locations
- ✅ Combat readiness: weapons, armor, skills, traits per colonist

**Strength:** Threat detection exists  
**Weakness:** No threat urgency/severity scoring, no "incoming raid in 5 seconds" alerts

#### **Map & Environment** (7 tools)
- ✅ Weather, season, temperature, biome
- ✅ Growing zones: crops, growth %, fertility
- ✅ Power status: generation, consumption, battery storage
- ✅ Map grid visualization (buildings, pawns, terrain, zones)
- ✅ Cell details: terrain, roof, temp, fertility, room stats, things present
- ✅ **Blueprint visibility** (just added!)
- ✅ Search map: find colonists, items, ore, plants, buildings by type/filter

**Strength:** Excellent spatial awareness and environment data  
**Weakness:** No temperature alerts (freezing/heatstroke warnings), no power failure alerts

#### **Animals** (2 tools)
- ✅ List tamed/colony animals with master & training
- ✅ Animal details: health, training progress, food needs, bonding

**Strength:** Basic animal management  
**Weakness:** No wild animal threat detection, no tameable animal opportunities

#### **Events & Alerts** (2 tools)
- ✅ Recent game events/letters (last 5-20)
- ✅ **Active alerts** (colonist needs rescue, starvation, tattered apparel, idle colonist)

**Strength:** Can see alerts when explicitly called  
**Weakness:** Alerts not pushed automatically, no alert priority/urgency

#### **Zones & Areas** (11 tools)
- ✅ List all zones (growing, stockpile, planning)
- ✅ Create zones (stockpile, growing, planning)
- ✅ Delete zones
- ✅ Zone configuration (crop selection, stockpile filters/priority)
- ✅ Area restrictions per colonist
- ✅ Plan designations (place/get/remove)

**Strength:** Complete zone management  
**Weakness:** No zone usage analytics (is stockpile full? is growing zone optimal?)

#### **Building & Construction** (6 tools)
- ✅ List buildable structures by category
- ✅ Get building info: size, materials, requirements, stats
- ✅ Place buildings (single/batch/structure shapes)
- ✅ Remove building blueprints
- ✅ Approve buildings (unforbid for construction)
- ✅ Get blueprints (see what's planned)

**Strength:** Excellent building tools  
**Weakness:** No construction queue visibility, no material availability check before building, no colonist availability for construction

#### **World & Trade** (7 tools)
- ✅ World destinations (settlements, distances, factions)
- ✅ Caravan info
- ✅ Trade status & trader inventory
- ✅ Faction list & diplomatic summary
- ✅ Diplomacy options

**Strength:** World awareness  
**Weakness:** No caravan risk assessment, no trade profitability analysis

#### **Player Directives** (3 tools)
- ✅ Get/add/remove colony-specific player preferences

**Strength:** Persistent memory of player intent  
**Weakness:** None (this is good!)

---

### ❌ **What RimMind CANNOT See** (gaps vs human player)

#### **Critical Real-Time State** ⚠️ HIGH PRIORITY
1. **No automatic context updates**
   - Human players see top bar constantly (date, wealth, colonist count, temp)
   - RimMind only sees this if it calls tools
   - **Impact:** Can't detect subtle changes over time

2. **No alert push system**
   - Human players see red/yellow alerts instantly when they appear
   - RimMind must call `get_active_alerts` to check
   - **Impact:** Slow to react to emergencies (fire, raid, starvation, medical)

3. **No colonist location awareness in real-time**
   - Human players see colonists on map constantly
   - RimMind can search for colonists but doesn't know "Pawn X is far from home" or "Pawn Y is standing in fire"
   - **Impact:** Can't protect colonists proactively

4. **No work queue visibility**
   - Human players see pending jobs in work tab
   - RimMind sees work priorities but not "20 haul jobs pending, 5 construction jobs blocked"
   - **Impact:** Can't diagnose bottlenecks or optimize workflow

5. **No resource consumption rates**
   - Human players see "food will last 3 days" mentally
   - RimMind sees current food count but not burn rate
   - **Impact:** Can't predict shortages

6. **No temperature warnings**
   - Human players see "colonist is freezing/overheating" alerts
   - RimMind sees outdoor temp but not per-colonist comfort
   - **Impact:** Can't prevent heatstroke/hypothermia

7. **No immediate threat detection**
   - Human players see raids as they spawn (red envelope)
   - RimMind must call `get_threats` — could be seconds late
   - **Impact:** Slow combat response

#### **Construction & Logistics** 🔨 MEDIUM PRIORITY
8. **No construction queue depth**
   - Can see blueprints, but not "5 walls waiting, 0 colonists assigned to construction"
   - **Impact:** Can't diagnose why nothing is being built

9. **No material availability pre-check**
   - Can place blueprints but doesn't know if materials exist
   - **Impact:** Places blueprints colonists can't build

10. **No hauling efficiency metrics**
    - Can't see "50 steel lying outside, no haulers assigned"
    - **Impact:** Can't optimize stockpile management

11. **No furniture placement validation**
    - Can place furniture but doesn't check "is this spot accessible?"
    - **Impact:** Places inaccessible furniture (though recent improvements help)

#### **Social & Mood** 😊 MEDIUM PRIORITY
12. **No mood trend analysis**
    - Can see current mood but not "colonist mood dropping 5%/day"
    - **Impact:** Can't prevent mental breaks

13. **No social fight warnings**
    - Can see relationships but not "these two are about to fight"
    - **Impact:** Can't prevent social violence

14. **No recreation need visibility**
    - Can see joy need but not "no recreation sources available"
    - **Impact:** Can't diagnose joy problems

#### **Animals & Wildlife** 🐾 LOW PRIORITY
15. **No wild animal threat assessment**
    - Can see animals via search_map but not "predator hunting colonist"
    - **Impact:** Can't warn about wildlife dangers

16. **No tameable animal opportunities**
    - Can't see "rare animal on map, worth taming"
    - **Impact:** Misses strategic animal captures

#### **Research & Tech** 🔬 LOW PRIORITY
17. **No research speed calculation**
    - Can see research progress % but not "will complete in 2 days"
    - **Impact:** Can't plan research timing

18. **No tech tree optimization**
    - Can see available research but no "optimal path for goal X"
    - **Impact:** Can't guide research strategy

#### **Power & Climate** ⚡ MEDIUM PRIORITY
19. **No power failure detection**
    - Can see total power but not "battery will die in 1 hour"
    - **Impact:** Can't prevent blackouts

20. **No room temperature monitoring**
    - Can see outdoor temp but not "bedroom is 40°C, colonist overheating"
    - **Impact:** Can't prevent climate injuries

#### **Wealth & Economy** 💰 LOW PRIORITY
21. **No wealth breakdown**
    - Can see total wealth but not "wealth = 80% buildings, 10% items, 10% pawns"
    - **Impact:** Can't advise on wealth management

22. **No trade profitability analysis**
    - Can see trader inventory but no "buy X, sell Y for +500 silver"
    - **Impact:** Can't optimize trading

#### **Storyteller & Events** 📖 MEDIUM PRIORITY
23. **No event prediction**
    - Can see recent events but not "major event likely soon based on storyteller"
    - **Impact:** Can't prepare for storyteller patterns

24. **No quest visibility**
    - RimWorld has quests — RimMind can't see them
    - **Impact:** Misses quest opportunities

25. **No incident recovery tracking**
    - Can see that a raid happened but not "still cleaning up from raid 2 days ago"
    - **Impact:** Can't track recovery progress

---

## Part 2: Data Structure Recommendations

### **Push vs Pull Data Model**

Current model: **100% pull** (AI must call tools to see anything)  
Recommended: **Hybrid push/pull**

#### **Push Data: Lightweight Context (always sent)**
Include in every AI request as system message context:

```json
{
  "game_state": {
    "tick": 15240000,
    "date": "Day 12, Aprimay 5505",
    "season": "Spring",
    "hour": 14,
    "colonist_count": 5,
    "wealth": 48500,
    "map_size": "250x250",
    "biome": "Temperate Forest",
    "difficulty": "Blood and Dust",
    "storyteller": "Cassandra Classic"
  },
  "urgent_alerts": [
    {"severity": "critical", "label": "Colonist needs rescue", "colonist": "Mira"},
    {"severity": "major", "label": "Low on food", "details": "2.1 days remaining"}
  ],
  "active_threats": [
    {"type": "raid", "faction": "Pirates", "count": 8, "distance": "incoming"}
  ],
  "colonist_summary": [
    {"name": "Mira", "status": "downed", "location": "(120,85)", "mood": "25%"},
    {"name": "Jonas", "status": "working", "job": "constructing", "mood": "75%"}
  ],
  "recent_changes": [
    "Raid started (8 pirates)",
    "Mira downed by gunshot",
    "Power: battery at 15%"
  ]
}
```

**Size:** ~500-800 characters (minimal context window cost)  
**Update:** Every AI request  
**Benefit:** AI always knows critical state without tool calls

#### **Pull Data: Detailed Tools (on demand)**
Keep existing tools for detailed queries:
- `get_colonist_details(name)` when analyzing specific colonist
- `get_work_priorities()` when optimizing work
- `get_map_region(x, z, w, h)` when planning construction

---

## Part 3: Priority Roadmap

### **Phase 1: Critical Real-Time Awareness** ⚠️ **HIGHEST IMPACT**

**Goal:** AI sees what's urgent without needing to ask

#### 1.1 Auto-Context System (NEW TOOL: `get_game_state`)
**Impact:** 🔥🔥🔥🔥🔥 (Foundational)  
**Complexity:** Low (30 minutes)  
**Implementation:**
- Create lightweight context builder that runs before every AI request
- Include: date, colonist count, wealth, active alerts (top 3), active threats (yes/no)
- Add to system prompt automatically
- No tool call needed — always present

**Benefit:** AI always knows:
- What day it is
- How many colonists alive
- If there are critical alerts
- If there's an active threat

#### 1.2 Enhanced Alert Visibility (IMPROVE: `get_active_alerts`)
**Impact:** 🔥🔥🔥🔥 (Emergency Response)  
**Complexity:** Low (20 minutes)  
**Implementation:**
- Add severity scoring to alerts (critical/major/minor)
- Add colonist names to relevant alerts
- Add countdown timers where applicable ("Pawn will starve in 4 hours")
- Sort by urgency

**Benefit:** AI can triage alerts and respond to most urgent first

#### 1.3 Colonist Location Tracking (NEW TOOL: `get_colonist_locations`)
**Impact:** 🔥🔥🔥🔥 (Safety & Situational Awareness)  
**Complexity:** Low (15 minutes)  
**Implementation:**
- Return all colonists with current position (x, z)
- Include: drafted status, downed status, mental state, current job
- Optionally flag "far from home" or "in danger zone"

**Benefit:** AI knows where everyone is and can spot danger

```json
{
  "colonists": [
    {"name": "Mira", "x": 120, "z": 85, "status": "downed", "drafted": false},
    {"name": "Jonas", "x": 95, "z": 110, "status": "working", "job": "Mining", "drafted": false}
  ]
}
```

#### 1.4 Resource Consumption Rates (NEW TOOL: `get_resource_trends`)
**Impact:** 🔥🔥🔥 (Predictive Planning)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Track resource changes over time (simple delta: current - 1 day ago)
- Calculate burn rates for food, medicine, mood resources
- Estimate "days until depleted"
- Flag "running low" vs "critically low"

**Benefit:** AI can predict shortages and warn before crisis

```json
{
  "food": {
    "current": 850,
    "change_24h": -120,
    "burn_rate_per_day": 120,
    "days_remaining": 7.1,
    "status": "adequate"
  },
  "medicine": {
    "current": 15,
    "change_24h": -3,
    "burn_rate_per_day": 3,
    "days_remaining": 5,
    "status": "low"
  }
}
```

---

### **Phase 2: Construction & Workflow Intelligence** 🔨 **HIGH IMPACT**

**Goal:** AI understands work bottlenecks and construction status

#### 2.1 Work Queue Visibility (NEW TOOL: `get_work_queue`)
**Impact:** 🔥🔥🔥🔥 (Bottleneck Detection)  
**Complexity:** Medium (1 hour)  
**Implementation:**
- Query RimWorld's designation manager for pending jobs
- Group by work type (hauling, construction, mining, planting, etc.)
- Show counts: total jobs, jobs in progress, jobs blocked (no path, no materials)
- Show which colonists assigned to each work type

**Benefit:** AI can diagnose "why isn't X getting done?"

```json
{
  "pending_jobs": {
    "hauling": {"total": 35, "in_progress": 2, "blocked": 0, "assigned_colonists": 3},
    "construction": {"total": 12, "in_progress": 1, "blocked": 5, "assigned_colonists": 2},
    "mining": {"total": 8, "in_progress": 3, "blocked": 0, "assigned_colonists": 4}
  }
}
```

#### 2.2 Construction Status (NEW TOOL: `get_construction_status`)
**Impact:** 🔥🔥🔥 (Build Progress Tracking)  
**Complexity:** Low (30 minutes)  
**Implementation:**
- List all blueprints with completion %
- Show which blueprints have materials available
- Show which blueprints are forbidden (AI-placed, awaiting approval)
- Show which colonists are currently constructing

**Benefit:** AI knows why buildings aren't progressing

```json
{
  "blueprints": [
    {"defName": "Wall", "x": 120, "z": 85, "completion": 75, "materials_available": true, "builder": "Jonas"},
    {"defName": "Door", "x": 125, "z": 85, "completion": 0, "materials_available": false, "builder": null}
  ],
  "summary": {
    "total_blueprints": 15,
    "in_progress": 3,
    "awaiting_materials": 5,
    "forbidden": 7
  }
}
```

#### 2.3 Material Pre-Check (ENHANCE: `place_building`)
**Impact:** 🔥🔥 (Prevents Impossible Builds)  
**Complexity:** Low (20 minutes)  
**Implementation:**
- Before placing blueprint, check if materials exist in stockpiles
- Return warning if materials insufficient
- Optionally suggest "need 20 more steel" in response

**Benefit:** AI doesn't place blueprints colonists can't build

---

### **Phase 3: Social & Mood Intelligence** 😊 **MEDIUM IMPACT**

**Goal:** AI predicts and prevents mental breaks

#### 3.1 Mood Trend Analysis (NEW TOOL: `get_mood_trends`)
**Impact:** 🔥🔥🔥 (Mental Break Prevention)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Track colonist mood over last 3 days
- Calculate mood velocity (rising/falling/stable)
- Flag colonists "trending toward break" (mood < 30% and falling)
- Show top negative thoughts contributing to low mood

**Benefit:** AI can intervene before mental breaks

```json
{
  "colonists": [
    {
      "name": "Mira",
      "mood_current": 28,
      "mood_24h_ago": 35,
      "mood_72h_ago": 45,
      "trend": "declining",
      "risk_level": "high",
      "top_negative_thoughts": [
        "Ate without table (-3)",
        "Slept in dirt (-4)",
        "Witnessed death (-6)"
      ]
    }
  ]
}
```

#### 3.2 Social Conflict Detection (NEW TOOL: `get_social_risks`)
**Impact:** 🔥🔥 (Prevents Social Violence)  
**Complexity:** Medium (30 minutes)  
**Implementation:**
- Find colonist pairs with opinion < -20
- Check for "last straw" traits (volatile, abrasive)
- Flag high-risk pairs

**Benefit:** AI can separate rivals before fights

#### 3.3 Recreation Analysis (ENHANCE: `get_colony_overview`)
**Impact:** 🔥 (Joy Problem Diagnosis)  
**Complexity:** Low (15 minutes)  
**Implementation:**
- Count recreation sources (chess, horseshoes, TV, etc.)
- Calculate recreation variety
- Flag "no joy sources" or "insufficient variety"

**Benefit:** AI can suggest building recreation

#### 3.4 Mental Break Risk Predictions (ENHANCE: `get_mood_trends`)
**Impact:** 🔥🔥🔥 (Proactive Prevention)  
**Complexity:** Medium (30 minutes)  
**Implementation:**
- Calculate time-to-break estimates based on mood velocity
- Predict "Mira will break in ~4 hours at current trend"
- Include break threshold warnings (minor/major/extreme)

**Benefit:** AI gives specific time windows for intervention

#### 3.5 Actionable Mitigation Suggestions (NEW TOOL: `suggest_mood_interventions`)
**Impact:** 🔥🔥🔥 (Prescriptive Guidance)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Analyze top negative thoughts for each at-risk colonist
- Suggest specific actions: "Build dining table (fixes 'Ate without table')", "Improve bedroom beauty", "Schedule recreation time"
- Prioritize by impact (which intervention helps most)
- Include social interaction needs ("Mira needs positive social - pair with Jonas who likes her")

**Benefit:** AI doesn't just warn - it tells you what to do

#### 3.6 Environment Quality Scoring (NEW TOOL: `get_environment_quality`)
**Impact:** 🔥🔥 (Root Cause Analysis)  
**Complexity:** Medium (30 minutes)  
**Implementation:**
- Score each room for beauty, cleanliness, space, impressiveness
- Flag rooms causing negative thoughts ("Disturbed sleep - bedroom too ugly")
- Suggest specific improvements ("Add sculptures, clean filth, increase room size")

**Benefit:** AI identifies environmental mood penalties

---

### **Phase 4: Power & Climate Monitoring** ⚡ **MEDIUM IMPACT**

**Goal:** Prevent power failures and temperature injuries

#### 4.1 Power Failure Prediction (ENHANCE: `get_power_status`)
**Impact:** 🔥🔥🔉 (Blackout Prevention)  
**Complexity:** Low (20 minutes)  
**Implementation:**
- Calculate battery drain rate (Wd consumed per hour)
- Estimate "hours until blackout" if no sun/wind
- Flag "critical" if < 2 hours remaining

**Benefit:** AI can warn before batteries die

#### 4.2 Temperature Alerts (NEW TOOL: `get_temperature_risks`)
**Impact:** 🔥🔥 (Prevent Heatstroke/Hypothermia)  
**Complexity:** Medium (30 minutes)  
**Implementation:**
- Check each colonist's current position temperature
- Compare to colonist's comfortable temp range
- Flag "colonist overheating" or "colonist freezing"

**Benefit:** AI can order colonists to safety

---

### **Phase 5: Combat Intelligence** ⚔️ **HIGH IMPACT**

**Goal:** Give AI complete combat awareness for tactical decision-making

#### 5.1 Weapon & Armor Stats (NEW TOOL: `get_weapon_stats` + `get_armor_stats`)
**Impact:** 🔥🔥🔥🔥 (Combat Calculations)  
**Complexity:** Medium (1 hour)  
**Implementation:**
- Weapon stats: damage, DPS, range, accuracy curve, cooldown, armor penetration
- Armor stats: sharp/blunt/heat protection percentages
- Quality modifiers (poor/normal/good/excellent/masterwork/legendary)
- Show for both colonist and enemy equipment

**Benefit:** AI can calculate actual combat effectiveness, not just "has a gun"

```json
{
  "weapon": {
    "defName": "Gun_Revolver",
    "label": "revolver",
    "damage": 12,
    "dps": 8.5,
    "range": 25.9,
    "accuracy_touch": 0.91,
    "accuracy_short": 0.83,
    "accuracy_medium": 0.64,
    "accuracy_long": 0.41,
    "cooldown": 1.3,
    "quality": "normal"
  }
}
```

#### 5.2 Raid Composition Analysis (ENHANCE: `get_threats`)
**Impact:** 🔥🔥🔥🔥 (Tactical Awareness)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Break down raid by role: melee/ranged/grenadiers/special units
- Show enemy weapon types and quality
- Show enemy armor coverage
- Identify dangerous units (centipedes, scythers, etc.)

**Benefit:** AI can say "3 melee pikemen, 5 riflemen, 2 grenadiers - focus fire on grenadiers first"

#### 5.3 Raid Strategy Detection (ENHANCE: `get_threats`)
**Impact:** 🔥🔥🔥🔥 (Counter-Strategy)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Detect raid type: assault, siege, sapper, breach, drop pod, tunneler
- Show raid behavior pattern and approach vector
- Suggest counter-tactics based on raid type

**Benefit:** AI can say "This is a sapper raid - they'll dig through walls. Reinforce interior defenses."

```json
{
  "raid_strategy": "sapper",
  "description": "Sappers will mine through walls to bypass defenses",
  "counter_tactics": ["Reinforce interior walls", "Set up fallback positions", "Don't rely on exterior defenses"]
}
```

#### 5.4 Enemy Morale & Fleeing (NEW TOOL: `get_enemy_morale`)
**Impact:** 🔥🔥🔥 (Predicting Retreat)  
**Complexity:** High (1.5 hours)  
**Implementation:**
- Track enemy casualties vs starting count
- Calculate morale threshold (typically flee at ~50% losses)
- Predict "enemy will flee soon" or "enemy committed to fight"
- Show per-raider morale if accessible

**Benefit:** AI can say "Enemy lost 4 of 8 - expect retreat soon. Push hard now."

#### 5.5 Friendly Fire Risk (NEW TOOL: `get_friendly_fire_risk`)
**Impact:** 🔥🔥🔥 (Positioning Safety)  
**Complexity:** High (1.5 hours)  
**Implementation:**
- Calculate shooting accuracy from current positions
- Identify colonists in line of fire
- Calculate friendly fire probability percentage
- Suggest safe firing positions or repositioning

**Benefit:** AI can warn "Don't shoot - 40% chance Jonas hits Mira who's in melee"

#### 5.6 Cover Effectiveness (NEW TOOL: `get_cover_analysis`)
**Impact:** 🔥🔥 (Positioning Optimization)  
**Complexity:** Medium (45 minutes)  
**Implementation:**
- Identify cover objects (walls, sandbags, trees, rocks)
- Calculate cover bonus percentage (25% half cover, 75% full cover)
- Show optimal cover positions for each colonist
- Highlight exposed positions

**Benefit:** AI can say "Move Jonas behind sandbags at (120,85) for 75% cover"

#### 5.7 Optimal Engagement Ranges (enhance previous tools)
**Impact:** 🔥🔥 (Tactical Positioning)  
**Complexity:** Low (20 minutes)  
**Implementation:**
- Calculate optimal range per weapon (where accuracy is best)
- Compare to enemy weapon ranges
- Suggest positioning: "Keep distance 15-25 for revolver advantage"

**Benefit:** AI understands range advantage/disadvantage

---

### **Phase 6: DLC Combat Intelligence** 🧬 **HIGH IMPACT** (for DLC players)

**Goal:** Full visibility into Royalty and Biotech DLC combat features

#### 6.1 Psycasts (Royalty DLC) (NEW TOOL: `get_psycasts`)
**Impact:** 🔥🔥🔥🔥🔥 (DLC Game-Changer)  
**Complexity:** High (2 hours)  
**Implementation:**
- List available psycasts per psycaster
- Show psycast effects and combat applications
- Show psylink level (1-6)
- Show neural heat (current/max)
- Show psycast cooldowns
- Show psyfocus level
- Suggest tactical psycast usage

**Benefit:** AI can say "Jonas has Skip - teleport the grenadier next to your colonists and focus fire"

```json
{
  "psycaster": "Jonas",
  "psylink_level": 3,
  "neural_heat": "15/30",
  "psyfocus": "85%",
  "available_psycasts": [
    {
      "name": "Skip",
      "effect": "Teleport target to selected location",
      "neural_heat_cost": 15,
      "range": 25,
      "cooldown": "ready",
      "combat_use": "Teleport enemies into killbox or away from colonists"
    },
    {
      "name": "Berserk",
      "effect": "Target goes berserk and attacks nearest pawn",
      "neural_heat_cost": 18,
      "cooldown": "3 hours",
      "combat_use": "Turn enemies against each other"
    }
  ]
}
```

#### 6.2 Xenotype Genes (Biotech DLC) (NEW TOOL: `get_genes`)
**Impact:** 🔥🔥🔥🔥 (Combat Abilities)  
**Complexity:** High (2 hours)  
**Implementation:**
- List genes per colonist
- Show gene-granted abilities (fire breath, toxic immunity, etc.)
- Show stat modifiers from genes
- Show xenotype combat bonuses/penalties
- Identify combat-relevant genes

**Benefit:** AI can say "Mira is a Dirtmole - toxic immune, send her against insects"

```json
{
  "colonist": "Mira",
  "xenotype": "Dirtmole",
  "combat_genes": [
    {
      "gene": "ToxicEnvironmentResistance",
      "effect": "Immune to toxic buildup",
      "combat_use": "Can fight in toxic environments, effective vs insects"
    },
    {
      "gene": "Brawler",
      "effect": "+4 melee hit chance",
      "combat_use": "Effective in melee combat"
    }
  ]
}
```

#### 6.3 Mechanitor Control (Biotech DLC) (NEW TOOL: `get_mechanitor_info`)
**Impact:** 🔥🔥🔥🔥 (Mech Army)  
**Complexity:** High (2 hours)  
**Implementation:**
- List controlled mechs per mechanitor
- Show mech types, weapons, health
- Show mech capabilities and combat roles
- Show bandwidth usage (current/max)
- Suggest mech deployment strategies

**Benefit:** AI can say "Deploy your 2 Scorchers (flamethrower mechs) against the clustered melee rush"

```json
{
  "mechanitor": "Jonas",
  "bandwidth": "4/6",
  "controlled_mechs": [
    {
      "type": "Scorcher",
      "weapon": "Mini-Flamethrower",
      "health": "100/100",
      "role": "Anti-infantry, area denial",
      "combat_use": "Effective against melee rushes, burns multiple targets"
    },
    {
      "type": "Lifter",
      "role": "Support, hauling",
      "combat_use": "Non-combat support"
    }
  ]
}
```

---

### **Phase 7: Advanced Analytics** 📊 **LOW IMPACT** (Nice to Have)

#### 7.1 Wealth Breakdown
- Show wealth by category (buildings, items, pawns)
- Track wealth velocity (gaining/losing value)

#### 7.2 Trade Profitability
- Analyze trader inventory vs colony needs
- Suggest profitable trades

#### 7.3 Quest Visibility
- Expose active quests from RimWorld's quest system
- Show rewards and risks

#### 7.4 Event Prediction
- Track storyteller adaptation level
- Estimate "major event likely in next 2 days"

---

## Part 4: Implementation Plan

### **Week 1: Critical Real-Time Awareness** (Phase 1)
**Focus:** Auto-context, alerts, locations, resource trends

**Day 1-2:**
- Implement `get_game_state` auto-context system
- Integrate into every AI request (modify PromptBuilder)
- Test context size and performance

**Day 3:**
- Enhance `get_active_alerts` with severity, countdown timers, colonist names
- Add alert sorting by urgency

**Day 4:**
- Implement `get_colonist_locations` with position, status, drafted state
- Add danger zone detection

**Day 5:**
- Implement `get_resource_trends` with burn rates and days remaining
- Add "running low" / "critically low" flags

**Deliverable:** RimMind has situational awareness without tool calls

---

### **Week 2: Construction & Workflow** (Phase 2)
**Focus:** Work queue, construction status, material checks

**Day 1-2:**
- Implement `get_work_queue` reading designation manager
- Group jobs by work type, count pending/blocked/in-progress

**Day 3:**
- Implement `get_construction_status` with completion %, materials, builders

**Day 4:**
- Enhance `place_building` with material pre-check
- Return warnings for insufficient materials

**Day 5:**
- Test and document Phase 2 tools

**Deliverable:** RimMind can diagnose construction bottlenecks

---

### **Week 3: Social & Mood Intelligence** (Phase 3)
**Focus:** Mood trends, social risks, recreation

**Day 1-2:**
- Implement mood tracking over time
- Implement `get_mood_trends` with velocity calculation

**Day 3:**
- Implement `get_social_risks` finding hostile colonist pairs

**Day 4-5:**
- Enhance recreation analysis in colony overview
- Test mental break prevention workflow

**Deliverable:** RimMind can prevent mental breaks

---

### **Week 4: Power & Climate** (Phase 4)
**Focus:** Power failure prediction, temperature monitoring

**Day 1-2:**
- Enhance `get_power_status` with battery drain rate and time-to-blackout

**Day 3-4:**
- Implement `get_temperature_risks` checking colonist positions vs comfort range

**Day 5:**
- Test climate injury prevention workflow

**Deliverable:** RimMind prevents blackouts and temperature injuries

---

### **Week 5-6: Combat Intelligence** (Phase 5)
**Focus:** Weapon/armor stats, raid analysis, enemy morale, friendly fire, cover

**Week 5, Day 1-2:**
- Implement `get_weapon_stats` and `get_armor_stats`
- Parse weapon damage, DPS, range, accuracy curves
- Parse armor protection values

**Week 5, Day 3-4:**
- Enhance `get_threats` with raid composition breakdown
- Show melee/ranged/grenadier counts, weapon types, armor

**Week 5, Day 5:**
- Enhance `get_threats` with raid strategy detection
- Detect assault/siege/sapper/breach/drop pod/tunneler types

**Week 6, Day 1-2:**
- Implement `get_enemy_morale` for fleeing predictions
- Calculate morale thresholds and retreat timing

**Week 6, Day 3:**
- Implement `get_friendly_fire_risk`
- Calculate line-of-fire and friendly fire probabilities

**Week 6, Day 4:**
- Implement `get_cover_analysis`
- Identify cover objects and calculate cover bonuses

**Week 6, Day 5:**
- Add optimal engagement range calculations
- Test and document Phase 5 tools

**Deliverable:** RimMind has tactical combat intelligence for positioning and strategy

---

### **Week 7-8: DLC Combat Intelligence** (Phase 6)
**Focus:** Psycasts, genes, mechanitor abilities

**Week 7, Day 1-3:**
- Implement `get_psycasts` (Royalty DLC)
- Parse available psycasts, effects, neural heat, psylink level
- Add combat application suggestions

**Week 7, Day 4-5:**
- Implement `get_genes` (Biotech DLC)
- Parse xenotype genes and combat abilities
- Identify combat-relevant genes

**Week 8, Day 1-3:**
- Implement `get_mechanitor_info` (Biotech DLC)
- Parse controlled mechs, weapons, health, roles
- Add mech deployment suggestions

**Week 8, Day 4-5:**
- Test DLC features across different scenarios
- Document Phase 6 tools
- Create compatibility notes for vanilla vs DLC

**Deliverable:** RimMind fully understands DLC combat features

---

## Part 5: Success Metrics

### **How to Measure Improvement**

**Before:**
- Player: "Why isn't my wall being built?"
- RimMind: "Let me check..." (calls get_blueprints, sees blueprint, but doesn't know why)

**After:**
- Player: "Why isn't my wall being built?"
- RimMind: "The wall blueprint needs 5 more steel, and you only have 2. Also, both your construction-priority colonists are busy hauling."

**Metrics:**
1. **Alert Response Time:** Time from alert appearing to AI mentioning it (target: < 10 seconds)
2. **Predictive Warnings:** AI warns of shortage before it happens (target: 80% of shortages predicted)
3. **Bottleneck Detection:** AI correctly diagnoses work bottlenecks without prompting (target: 90% accuracy)
4. **Mental Break Prevention:** AI suggests mood interventions before breaks (target: 50% reduction in breaks)
5. **Construction Success Rate:** AI-placed blueprints that get built without issues (target: 95%)

---

## Part 6: Long-Term Vision

### **The "Omniscient Advisor" Goal**

RimMind should eventually know:
- **Everything a human player sees on screen** (automatic context)
- **Everything a human player sees in menus** (tools)
- **Everything a human player intuits from experience** (analytics)

**Ultimate Test:**  
*"If a human expert player can notice it within 30 seconds of looking at the game, RimMind should be able to notice it too."*

---

## Appendix: Quick Reference

### **Tool Additions by Phase**

**Phase 1 (Week 1): 4 tools**
- Auto-context (built into prompt)
- Enhanced `get_active_alerts`
- `get_colonist_locations`
- `get_resource_trends`

**Phase 2 (Week 2): 3 tools**
- `get_work_queue`
- `get_construction_status`
- Enhanced `place_building` (material check)

**Phase 3 (Week 3): 5 tools**
- `get_mood_trends`
- `get_social_risks`
- `suggest_mood_interventions`
- `get_environment_quality`
- Enhanced `get_colony_overview` (recreation analysis)

**Phase 4 (Week 4): 2 tools**
- Enhanced `get_power_status` (drain rate)
- `get_temperature_risks`

**Phase 5 (Week 5-6): 7 tools**
- `get_weapon_stats`
- `get_armor_stats`
- Enhanced `get_threats` (raid composition + strategy detection)
- `get_enemy_morale`
- `get_friendly_fire_risk`
- `get_cover_analysis`
- Optimal engagement ranges (enhancement)

**Phase 6 (Week 7-8): 3 tools**
- `get_psycasts` (Royalty DLC)
- `get_genes` (Biotech DLC)
- `get_mechanitor_info` (Biotech DLC)

**Total New/Enhanced Tools:** 24 (52 → 76 tools)

---

## Conclusion

RimMind's visibility improvements follow a clear path:

1. **Phase 1 (Critical):** Always-on context + real-time alerts → AI knows what's urgent
2. **Phase 2 (High):** Work queue + construction status → AI diagnoses bottlenecks
3. **Phase 3 (High):** Mood trends + actionable interventions → AI prevents mental breaks
4. **Phase 4 (Medium):** Power/temp monitoring → AI prevents infrastructure failures
5. **Phase 5 (High):** Combat intelligence → AI provides tactical combat advice
6. **Phase 6 (High):** DLC combat → AI understands psycasts, genes, mechs
7. **Phase 7 (Low):** Analytics & optimization → AI becomes strategic advisor

**Implementation Time:** 8 weeks for Phases 1-6 (core + combat improvements)

**Expected Outcome:** RimMind sees the game world as clearly as a human player, responds to emergencies in real-time, and proactively prevents common colony failures.

---

**Document Version:** 2.0  
**Date:** 2026-02-17  
**Updated:** Added Phase 5 (Combat Intelligence) and Phase 6 (DLC Combat) based on combat visibility requirements. Enhanced Phase 3 with actionable mood interventions and environment quality analysis.  
**Author:** RimMind Development Team
