# Jacob's Visibility Gap Analysis

## Summary
Comprehensive list of visibility gaps identified through Q&A with Jacob. These gaps reveal what RimMind cannot currently see that a human player naturally observes.

---

## Gap Categories

### 1. Combat Intelligence Gaps ⚔️

**Weapon & Armor Stats:**
- [ ] Weapon damage, DPS, range, accuracy curves
- [ ] Armor protection values (sharp/blunt/heat)
- [ ] Hit chance calculations
- [ ] Quality modifiers (poor/excellent/masterwork)

**Raid Intelligence:**
- [ ] Raid composition breakdown (3 melee, 5 ranged, 2 grenadiers)
- [ ] Enemy weapon types and armor
- [ ] Raid strategy detection (assault vs siege vs sapper vs breach vs drop pod) ⭐ **CRITICAL**

**Tactical Awareness:**
- [ ] Enemy morale/fleeing predictions
- [ ] Friendly fire risk calculations
- [ ] Cover effectiveness analysis
- [ ] Optimal engagement ranges
- [ ] Tactical pathfinding advice ⭐ **CRITICAL** (not full algorithm, just "keep doors open to funnel enemies")

**Status:** Phase 5 (Issue #58) - ~12 hours implementation

---

### 2. DLC Combat Gaps (Royalty & Biotech) 🧬

**Royalty DLC:**
- [ ] Available psycasts per psycaster
- [ ] Psycast effects and tactical applications
- [ ] Psylink level (1-6)
- [ ] Neural heat (current/max)
- [ ] Psycast cooldowns
- [ ] Psyfocus level

**Biotech DLC:**
- [ ] Xenotype genes and combat abilities
- [ ] Gene-granted stat modifiers (toxic immunity, brawler, etc.)
- [ ] Mechanitor bandwidth and controlled mechs
- [ ] Mech types, weapons, health, combat roles

**Status:** Phase 6 (Issue #59) - ~12 hours implementation

---

### 3. Event & Disaster Intelligence Gaps 🌪️

**Active Event Tracking:**
- [ ] Event duration remaining ("Cold snap: 2.5 days left")
- [ ] Temperature/weather impact projections
- [ ] Crop survival predictions
- [ ] Colonist risk assessment (hypothermia, heatstroke)

**Specific Events:**
- [ ] Cold snaps / heat waves (duration, severity, crop impact)
- [ ] Toxic fallout (duration, toxicity rate, safe zones)
- [ ] Solar flares (duration, power grid impact, when it ends)
- [ ] Nuclear/volcanic winter (duration, crop death, hydroponics need)
- [ ] Eclipses (duration, solar panel impact)

**Disaster Mechanics:**
- [ ] Bug infestation risk (overhead mountain tiles, spawn locations)
- [ ] Zzzt event risk (stored power, battery explosion probability)
- [ ] Mitigation strategies ("reduce overhead mountain", "use circuit breakers")

**Status:** Phase 7 (Issue #61) - ~4 hours implementation

---

### 4. Animal Intelligence Gaps 🐾

**Animal Stats:**
- [ ] Carrying capacity (for pack animals)
- [ ] Movement speed
- [ ] Combat stats (melee damage, armor, DPS)
- [ ] Animal abilities (wool, milk, eggs, nuzzle for mood)
- [ ] Wildness level and trainability intelligence
- [ ] Filth rate, manhunter chance, revenge chance

**Wild Animal Visibility:**
- [ ] Tameable animals currently on map
- [ ] Huntable animal herds
- [ ] Taming difficulty and success chance
- [ ] Rare animal alerts ("Thrumbo on map - worth attempting tame")

**Production Tracking:**
- [ ] Current carrying load for pack animals
- [ ] Wool/milk/egg production schedules
- [ ] When animals are ready for shearing/milking
- [ ] Optimal pack animals for caravans

**Status:** Phase 8 (Issue #60) - ~5 hours implementation

---

### 5. Mood & Mental Break Gaps 😊

**Already Planned in Phase 3 (Issue #56):**
- [x] Mood trend analysis over time
- [x] Mental break time-to-break predictions
- [x] Actionable mitigation suggestions ⭐ **KEY IMPROVEMENT**
- [x] Recreation variety analysis
- [x] Environment quality scoring
- [x] Social interaction needs

**Status:** Phase 3 - ~6 hours implementation (already planned)

---

## Priority Assessment

### 🔥🔥🔥🔥🔥 **CRITICAL** (Highest Impact)

1. **Raid strategy detection** (Phase 5)
   - "This is a sapper raid - they'll dig through walls"
   - Player impact: Huge - changes entire defensive strategy
   - Implementation: Medium difficulty

2. **Tactical pathfinding advice** (Phase 5)
   - "Keep east door open to funnel enemies"
   - Player impact: Huge - enables killbox design
   - Implementation: Medium difficulty (tactical results only, not full pathfinding)

3. **DLC psycasts** (Phase 6, for DLC players)
   - "Jonas has Skip - teleport the grenadier"
   - Player impact: Game-changing for Royalty players
   - Implementation: High difficulty

### 🔥🔥🔥🔥 **HIGH** (Major Impact)

4. **Weapon/armor stats** (Phase 5)
   - Enables actual combat calculations
   - Player impact: High - informed weapon choices
   - Implementation: Medium difficulty

5. **Enemy morale/fleeing** (Phase 5)
   - "Enemy lost 4 of 8 - expect retreat soon"
   - Player impact: High - tactical decisions
   - Implementation: High difficulty

6. **Event duration tracking** (Phase 7)
   - "Cold snap for 2.5 more days - harvest corn now"
   - Player impact: High - prevents crop loss
   - Implementation: Low difficulty

7. **Friendly fire risk** (Phase 5)
   - "40% chance Jonas hits Mira in melee"
   - Player impact: High - prevents teamkills
   - Implementation: High difficulty

### 🔥🔥🔥 **MEDIUM-HIGH** (Significant Impact)

8. **Raid composition** (Phase 5)
   - "3 melee, 5 ranged, 2 grenadiers"
   - Player impact: Medium-High - tactical awareness
   - Implementation: Low difficulty

9. **Disaster risk assessment** (Phase 7)
   - "High infestation risk - 65% overhead mountain"
   - Player impact: Medium-High - prevention
   - Implementation: Medium difficulty

10. **Wild animal detection** (Phase 8)
    - "Rare Thrumbo on map - worth attempting tame"
    - Player impact: Medium-High - opportunities
    - Implementation: Low difficulty

### 🔥🔥 **MEDIUM** (Useful Impact)

11. **Animal stats** (Phase 8)
    - Know before you tame
    - Player impact: Medium - informed decisions
    - Implementation: Low difficulty

12. **Cover effectiveness** (Phase 5)
    - "Move behind sandbags for 75% cover"
    - Player impact: Medium - positioning
    - Implementation: Medium difficulty

13. **Production animal tracking** (Phase 8)
    - "Muffalo ready for milking"
    - Player impact: Medium - efficiency
    - Implementation: Low difficulty

---

## Implementation Roadmap

### Week 1-4: Core Awareness (Phases 1-4)
**Already planned - foundational features**

### Week 5-6: Combat Intelligence (Phase 5) ⚔️
**Priority: CRITICAL**
- ~12 hours implementation
- Covers gaps: 1, 2, 4, 5, 7, 8, 12
- **Biggest impact on combat gameplay**

### Week 7-8: DLC Combat (Phase 6) 🧬
**Priority: CRITICAL (for DLC players)**
- ~12 hours implementation
- Covers gap: 3
- **Game-changing for Royalty/Biotech players**

### Week 9: Events & Disasters (Phase 7) 🌪️
**Priority: HIGH**
- ~4 hours implementation
- Covers gaps: 6, 9
- **Prevents common colony failures**

### Week 10: Animal Intelligence (Phase 8) 🐾
**Priority: MEDIUM**
- ~5 hours implementation
- Covers gaps: 10, 11, 13
- **Important for animal-focused playstyles**

---

## Gap Pattern Analysis

### **Pattern 1: "Why?" Questions**
Jacob consistently asks "why did X happen?" questions:
- "Why can't RimMind see blueprints?" → Blueprint visibility (fixed)
- "Why can't RimMind calculate combat?" → Weapon/armor stats
- "Why can't RimMind predict disasters?" → Event tracking
- "Why can't RimMind understand raid types?" → Strategy detection

**Insight:** RimMind sees *what* but not *why* or *how*. It observes but doesn't understand mechanics.

### **Pattern 2: Tactical vs Strategic**
Jacob wants *tactical* advice, not just data:
- Not: "Enemy approaching"
- But: "This is a sapper raid - reinforce interior"
- Not: "8 enemies"
- But: "3 melee, 5 ranged - focus fire grenadiers"
- Not: "Need pathfinding"
- But: "Keep doors open to funnel enemies"

**Insight:** Focus on actionable tactical results, not raw data or complex algorithms.

### **Pattern 3: DLC Blindness**
For DLC players, RimMind is missing *the most powerful features*:
- Psycasts can teleport, control minds, turn invisible
- Genes grant immunity, abilities, stat bonuses
- Mechs provide combat support

**Insight:** Vanilla coverage is good, but DLC players feel RimMind is "incomplete."

### **Pattern 4: Time & Duration**
Jacob frequently asks "how long?" questions:
- "How long until cold snap ends?"
- "When will solar flare be over?"
- "How many days of food left?"

**Insight:** Duration predictions and time-based warnings are highly valued.

---

## Recommendations

### **Immediate Priority (Weeks 5-6)**
**Phase 5: Combat Intelligence**
- Addresses the most critical gaps (raid strategy, tactical pathfinding)
- Highest player impact
- Covers 7 of the top 13 gaps

### **Second Priority (Weeks 7-8)**
**Phase 6: DLC Combat**
- Game-changing for DLC players
- Addresses complete blindness to powerful features

### **Third Priority (Week 9)**
**Phase 7: Event & Disaster Intelligence**
- Prevents common colony failures
- Answers frequent "why/when/how" questions
- Relatively quick implementation (4 hours)

### **Fourth Priority (Week 10)**
**Phase 8: Animal Intelligence**
- Important but not critical
- Affects specific playstyles more than others
- Can be deferred if needed

---

## Success Metrics

### Before Jacob's Questions:
- RimMind: "8 enemies approaching"
- RimMind: "Jonas has a revolver"
- RimMind: "Cold snap event occurred"

### After Implementation:
- RimMind: "Sapper raid (8 enemies: 3 melee pikemen, 5 riflemen with 18dmg/shot). They'll dig through walls - reinforce interior defenses. Keep east door open to funnel them to killbox. Jonas (revolver: 12dmg, best at range 15-25) should stay behind sandbags at (120,85) for 75% cover."
- RimMind: "Cold snap: 2.5 days remaining, temperature dropping to -25°C. Your corn will die - harvest immediately. Colonists comfortable range is 16-26°C - ensure all bedrooms have heaters."

---

## Conclusion

Jacob's questions revealed **4 major blind spots:**
1. **Combat mechanics** - RimMind sees "enemies" but not "tactics"
2. **DLC features** - Complete blindness to psycasts, genes, mechs
3. **Event intelligence** - No duration tracking or disaster risk assessment
4. **Animal management** - Missing stats, wild detection, production tracking

**Total gaps identified:** 40+ specific visibility issues  
**Phases created:** 4 new phases (5, 6, 7, 8)  
**Tools to add:** 25 new tools + 7 enhancements  
**Implementation time:** 33 hours across 6 weeks  

**Impact:** Transforms RimMind from "information reporter" to "tactical advisor."

---

**Document Version:** 1.0  
**Date:** 2026-02-17  
**Created from:** Jacob's visibility questions during roadmap discussion  
**Status:** All gaps tracked in GitHub Issues #58, #59, #60, #61
