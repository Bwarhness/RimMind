# RimMind - Future Improvements

Tracked ideas and enhancements for future development. Items are grouped by category and roughly prioritized within each section.

---

## Animal & Designation System
- [ ] Add numeric ID system for animals (e.g., `list_animals` returns `id: 1, 2, 3` so the AI can target specific individuals when multiple of the same species exist)
- [ ] Batch hunt/tame designations (e.g., "hunt all hares" or "tame 3 muffalo")
- [ ] Designate slaughter for colony animals
- [ ] Cancel mining/chopping/harvesting designations (currently only animal designations can be cancelled)

## Tool System
- [ ] Add `undo_last_action` tool — reverse the most recent tool call (e.g., unplace a building, cancel a designation)
- [ ] Tool call retry with exponential backoff for transient failures
- [ ] Add `haul_to` tool — haul specific items to a target stockpile or cell
- [ ] Add `forbid`/`unforbid` tools for items on the map
- [ ] Add `deconstruct` tool for player-built structures (vs `remove_building` which only removes AI-proposed blueprints)

## Combat & Military
- [ ] Auto-draft/position colonists for detected threats
- [ ] Flee/shelter command — send all non-combatants to a safe area
- [ ] Target priority tool — tell drafted colonists which enemy to focus
- [ ] Caravan formation tool — create caravans with specified colonists, animals, and supplies

## Colony Management
- [ ] Bulk work priority assignment (e.g., "set all colonists to priority 1 for firefighting")
- [ ] Schedule templates (e.g., "apply night owl schedule to X")
- [ ] Auto-zone suggestions based on colony layout analysis
- [ ] Room quality optimizer — suggest furniture placement to hit target impressiveness

## Building System
- [ ] Building templates — save and replay room layouts (e.g., "build another bedroom like the last one")
- [ ] Auto-furniture placement for rooms (e.g., "furnish this bedroom" adds bed, dresser, end table, light)
- [ ] Hallway/corridor tool — connect two rooms with a roofed walkway
- [ ] Blueprint cost estimator — show total material requirements before placing

## AI & LLM Integration
- [ ] Streaming responses — show AI text as it generates instead of waiting for full response
- [ ] Multi-model support — let users pick different models per task (cheap model for simple queries, expensive for complex planning)
- [ ] Context window management — smarter conversation pruning to keep relevant history
- [ ] Colonist dialogue system — AI-generated social interactions via Harmony patches (Phase 3)
- [ ] AI storyteller — custom StorytellerComp querying LLM for event decisions (Phase 4)

## UI & UX
- [ ] Chat history persistence — save/load conversations across game sessions
- [ ] Minimap overlay showing AI's planned actions before execution
- [ ] Tool execution progress indicator (show which tool is running, how many remaining)
- [ ] Quick action buttons — context-sensitive buttons that change based on selected pawn/building
- [ ] Voice input support via speech-to-text

## Performance & Reliability
- [ ] Lazy tool definition loading — only serialize tool definitions that the AI actually requests
- [ ] Connection retry with user-visible status when OpenRouter is down
- [ ] Rate limiting awareness — gracefully handle API rate limits with queue
- [ ] Offline mode — cache colony state for read-only queries without API calls

## Mod Compatibility
- [ ] Detect and integrate with popular mods (e.g., Combat Extended weapon stats, Dubs Bad Hygiene needs)
- [ ] Generic mod tool discovery — scan loaded mods for additional Defs and expose them as tools
- [ ] Mod conflict detection — warn when RimMind tools might conflict with other mod patches
