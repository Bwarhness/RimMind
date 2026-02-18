using System.Collections.Generic;
using RimMind.API;

namespace RimMind.Tools
{
    public static class ToolDefinitions
    {
        private static List<JSONNode> cachedTools;

        public static List<JSONNode> GetAllTools()
        {
            if (cachedTools != null) return cachedTools;
            var tools = new List<JSONNode>();

            // Colonist Tools
            tools.Add(MakeTool("list_colonists", "List all colonists with their name, current mood percentage, current job/activity, and mental state if any."));
            tools.Add(MakeTool("get_colonist_details", "Get full details about a specific colonist: backstory, traits, all skills with passion levels, current needs, mood, and active thoughts.",
                MakeParam("name", "string", "The colonist's name (first name or nickname)")));
            tools.Add(MakeTool("get_colonist_health", "Get health details for a specific colonist: injuries, diseases, immunity progress, bionics/implants, pain level, and consciousness.",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("draft_colonist", "Draft a colonist for combat. Drafted colonists will follow orders instead of performing normal work tasks.",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("undraft_colonist", "Undraft a colonist, returning them to normal work tasks.",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("draft_all", "Draft all colonists for combat at once."));
            tools.Add(MakeTool("undraft_all", "Undraft all colonists, returning them to normal work."));

            // Social Tools
            tools.Add(MakeTool("get_relationships", "Get a colonist's social relationships: opinions of and from other colonists, relationship types (lover, spouse, rival, friend, etc).",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("get_faction_relations", "Get relations with all known factions: faction name, goodwill value, and relationship status (ally, neutral, hostile)."));

            // Work Tools
            tools.Add(MakeTool("get_work_priorities", "Get the work priority grid for all colonists: each work type (Doctor, Cook, Hunt, Construct, Grow, Mine, Research, etc.) and its priority level (1=highest, 4=lowest, 0=disabled)."));
            tools.Add(MakeTool("set_work_priority", "Set the work priority for a specific colonist and work type. Priority values: 0=disabled, 1=highest, 2=high, 3=normal, 4=low.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("workType", "string", "Work type name (e.g., 'Doctor', 'Cook', 'Construction', 'Growing', 'Mining')"),
                MakeParam("priority", "integer", "Priority level (0-4, where 0=disabled, 1=highest, 4=lowest)")));
            tools.Add(MakeTool("get_bills", "Get active production bills at workbenches: recipe name, target count, ingredients needed, assigned worker, and whether suspended.",
                MakeOptionalParam("workbench", "string", "Filter by workbench name. If omitted, returns bills from all workbenches.")));
            tools.Add(MakeTool("list_recipes", "List all available recipes at a specific workbench with ingredients and products.",
                MakeParam("workbench", "string", "Workbench name (e.g., 'stove', 'smithy', 'tailor')")));
            tools.Add(MakeTool("create_bill", "Create a new production bill at a workbench. Supports setting target count, forever mode, ingredient radius, minimum skill, and pause state.",
                MakeParam("workbench", "string", "Workbench name or defName (e.g., 'Electric stove', 'FueledStove', 'Butcher table')"),
                MakeParam("recipe", "string", "Recipe name or defName (e.g., 'Cook simple meal', 'Make cloth', 'Smelt weapon')"),
                MakeOptionalParam("count", "integer", "Target count (default: 1). Ignored if 'forever' is true."),
                MakeOptionalParam("forever", "boolean", "Set to true for perpetual production (default: false)"),
                MakeOptionalParam("ingredientRadius", "integer", "Ingredient search radius in cells (default: 999)"),
                MakeOptionalParam("minSkill", "integer", "Minimum skill level required (0-20, default: 0)"),
                MakeOptionalParam("paused", "boolean", "Start the bill in suspended/paused state (default: false)")));
            tools.Add(MakeTool("modify_bill", "Modify an existing production bill. Can change target count, pause/resume, ingredient radius, and skill requirements. Identify bill by recipe name or index.",
                MakeParam("workbench", "string", "Workbench name or defName"),
                MakeOptionalParam("recipe", "string", "Recipe name to find the bill (alternative to 'index')"),
                MakeOptionalParam("index", "integer", "Bill index (0-based) at the workbench (alternative to 'recipe')"),
                MakeOptionalParam("count", "integer", "New target count"),
                MakeOptionalParam("forever", "boolean", "Set to true for perpetual production"),
                MakeOptionalParam("paused", "boolean", "Pause (true) or resume (false) the bill"),
                MakeOptionalParam("ingredientRadius", "integer", "New ingredient search radius"),
                MakeOptionalParam("minSkill", "integer", "New minimum skill requirement (0-20)")));
            tools.Add(MakeTool("delete_bill", "Delete/remove a production bill from a workbench. Identify bill by recipe name or index.",
                MakeParam("workbench", "string", "Workbench name or defName"),
                MakeOptionalParam("recipe", "string", "Recipe name to find the bill (alternative to 'index')"),
                MakeOptionalParam("index", "integer", "Bill index (0-based) at the workbench (alternative to 'recipe')")));
            tools.Add(MakeTool("get_schedules", "Get daily schedules for all colonists: hour-by-hour assignments (Sleep, Work, Anything, Joy/Recreation)."));
            tools.Add(MakeTool("set_schedule", "Set the schedule assignment for a specific colonist at a specific hour. Assignments: 'Work', 'Sleep', 'Anything', 'Joy'.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("hour", "integer", "Hour of day (0-23)"),
                MakeParam("assignment", "string", "Activity assignment ('Work', 'Sleep', 'Anything', or 'Joy')")));
            tools.Add(MakeTool("copy_schedule", "Copy one colonist's schedule to another colonist.",
                MakeParam("from", "string", "Source colonist name"),
                MakeParam("to", "string", "Target colonist name")));

            // Construction & Workflow Intelligence (Phase 2)
            tools.Add(MakeTool("get_work_queue", "Get pending work designations grouped by type (hauling, construction, mining, planting, repair). Shows total jobs, in-progress, blocked (unreachable or missing materials), and assigned colonists. Use this to diagnose work bottlenecks and understand why tasks aren't getting done."));
            tools.Add(MakeTool("get_construction_status", "Get status of all blueprints on the map: completion percentage, materials needed vs available, forbidden status (AI-placed awaiting approval), and current builders. Use this to track construction progress and diagnose why buildings aren't being built (missing materials, forbidden, no builders)."));

            // Job Prioritization Tools
            tools.Add(MakeTool("prioritize_rescue", "Force a colonist to immediately rescue a downed pawn.",
                MakeParam("colonist", "string", "The rescuer's name"),
                MakeParam("target", "string", "The downed pawn's name")));
            tools.Add(MakeTool("prioritize_tend", "Force a doctor to immediately tend to a patient.",
                MakeParam("doctor", "string", "The doctor's name"),
                MakeParam("patient", "string", "The patient's name")));
            tools.Add(MakeTool("prioritize_haul", "Force a colonist to haul a specific item at the given coordinates.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "X coordinate of the item"),
                MakeParam("z", "integer", "Z coordinate of the item")));
            tools.Add(MakeTool("prioritize_repair", "Force a colonist to repair a damaged building at the given coordinates.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "X coordinate of the building"),
                MakeParam("z", "integer", "Z coordinate of the building")));
            tools.Add(MakeTool("prioritize_clean", "Force a colonist to clean filth in an area.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "X coordinate of the center"),
                MakeParam("z", "integer", "Z coordinate of the center"),
                MakeParam("radius", "integer", "Radius to search for filth (1-20)")));

            // Colony Tools
            tools.Add(MakeTool("get_colony_overview", "Get a high-level colony overview: colonist count, total wealth, days survived, difficulty setting, storyteller, and map tile info."));
            tools.Add(MakeTool("get_resources", "Get resource counts in the colony's stockpiles.",
                MakeOptionalParam("category", "string", "Filter by category: 'food', 'materials', 'weapons', 'apparel', 'medicine', or 'all'. Defaults to 'all'.")));
            tools.Add(MakeTool("get_rooms", "Get info about all rooms: type/role, impressiveness, beauty, cleanliness, space, and owner if applicable."));
            tools.Add(MakeTool("get_stockpiles", "Get all stockpile zones: name, priority level, number of cells, and configured item filters."));

            // Research Tools
            tools.Add(MakeTool("get_research_status", "Get current research status: active project name and progress percentage, colony tech level, and available research benches."));
            tools.Add(MakeTool("get_available_research", "Get all currently available (unlocked but incomplete) research projects with their cost and prerequisites."));
            tools.Add(MakeTool("get_completed_research", "Get a list of all completed research projects."));

            // Military Tools
            tools.Add(MakeTool("get_threats", "Get active threats on the map with detailed combat analysis: hostile pawns with raid composition breakdown (melee/ranged/grenadiers/special units), weapon and armor analysis for each enemy, dangerous unit identification (centipedes, scythers, sappers, breachers), automatic raid strategy detection (assault/siege/sapper/breach/drop pod) with counter-tactics suggestions, manhunter animals, and active game conditions."));
            tools.Add(MakeTool("get_defenses", "Get defensive structures: turrets (type, status, ammo), traps, and sandbags/barricades with their locations."));
            tools.Add(MakeTool("get_combat_readiness", "Get combat readiness for each colonist: equipped weapon, armor pieces, shooting skill, melee skill, and any combat-relevant traits."));
            
            // Combat Intelligence Tools (Phase 5)
            tools.Add(MakeTool("get_weapon_stats", "Get detailed weapon statistics for any pawn (colonist or enemy). Returns weapon name, quality, damage type, base damage, DPS, armor penetration, range, accuracy curve (touch/short/medium/long), cooldown, warmup time, and burst shot count. Use this to analyze combat effectiveness and compare weapons.",
                MakeParam("pawnName", "string", "Name of the pawn (colonist or hostile) to analyze")));
            tools.Add(MakeTool("get_armor_stats", "Get detailed armor statistics for any pawn (colonist or enemy). Returns overall armor ratings (sharp/blunt/heat protection percentages), individual armor pieces with their quality, coverage (body parts protected), and hit points. Use this to assess defensive capabilities and vulnerability.",
                MakeParam("pawnName", "string", "Name of the pawn (colonist or hostile) to analyze")));
            tools.Add(MakeTool("get_enemy_morale", "Analyze enemy morale and predict when they will flee. Returns casualties breakdown (alive/downed/dead), morale percentage, flee threshold, and status prediction. Most raids flee around 40-50% casualties. Use this to determine if you're winning and when enemies will retreat."));
            tools.Add(MakeTool("get_friendly_fire_risk", "Calculate friendly fire risk for a specific shooter-target engagement. Identifies colonists in the line of fire, calculates friendly fire probability percentage, and provides tactical recommendations (clear/caution/danger). Use this before engaging enemies when colonists are nearby.",
                MakeParam("shooterName", "string", "Name of the colonist who will shoot"),
                MakeParam("targetName", "string", "Name of the target (enemy or location)")));
            tools.Add(MakeTool("get_cover_analysis", "Analyze cover positions in an area. Identifies full cover (75% protection), half cover (25-50% protection), and exposed positions. Returns optimal defensive positions and tactical recommendations. Use this for positioning colonists during combat.",
                MakeParam("x", "integer", "X coordinate of area center"),
                MakeParam("z", "integer", "Z coordinate of area center"),
                MakeOptionalParam("radius", "integer", "Search radius in cells (default: 10)")));
            tools.Add(MakeTool("get_tactical_pathfinding", "Get tactical combat intelligence and defensive positioning advice. Identifies enemy approach vectors, detects chokepoints (doors, narrow passages), analyzes defensive structures (turrets, sandbags), and provides actionable tactical recommendations. Includes specific advice for killbox design, drop pod defense, and counter-tactics for sappers/breachers. Use this to understand combat flow and optimize defensive positioning."));

            // Map Tools
            tools.Add(MakeTool("get_semantic_overview", "Get a compact text description of the colony layout including rooms, power, and buildable areas. This provides a high-level overview optimized for understanding base structure without loading full grid data. Use this instead of get_map_region when you need to understand colony layout quickly."));
            tools.Add(MakeTool("find_buildable_area", "Find buildable area candidates for construction. Returns scored candidates with exact positions, sizes, and notes. AI can ask 'where can I build a 5x4 room near the stockpile?' and get actionable results. Scores areas by distance to target, power availability, and terrain quality.",
                MakeParam("minWidth", "integer", "Minimum width in cells"),
                MakeParam("minHeight", "integer", "Minimum height in cells"),
                MakeOptionalParam("near", "string", "Thing or position to be near (e.g., 'stockpile', '50,60', or building name)"),
                MakeOptionalParam("maxDistance", "integer", "Maximum distance from 'near' target (default: 999)"),
                MakeOptionalParam("indoor", "boolean", "Must be roofed/indoors (default: false)"),
                MakeOptionalParam("requirePower", "boolean", "Must have power conduit nearby (default: false)")));
            tools.Add(MakeTool("get_weather_and_season", "Get current weather, outdoor/indoor temperature, season, and biome type."));
            tools.Add(MakeTool("get_growing_zones", "Get all growing zones: planted crop, average growth percentage, soil fertility, and zone size."));
            tools.Add(MakeTool("get_power_status", "Get power grid status: total generation, total consumption, battery storage levels, and any disconnected devices."));
            tools.Add(MakeTool("get_map_region", "Get a character-based grid view of the map showing buildings, pawns, zones, and terrain. Each cell is one character. Use this to understand the colony layout, analyze base design, and identify construction opportunities. Returns a legend of character codes used.",
                MakeOptionalParam("x", "integer", "Start X coordinate (default: 0)"),
                MakeOptionalParam("z", "integer", "Start Z coordinate (default: 0)"),
                MakeOptionalParam("width", "integer", "Width of region to scan (default: full map)"),
                MakeOptionalParam("height", "integer", "Height of region to scan (default: full map)")));
            tools.Add(MakeTool("get_cell_details", "Get detailed information about a specific map cell or range of cells: terrain, roof, temperature, fertility, room info, zone, and all things present. Use after get_map_region to drill down into specific locations.",
                MakeParam("x", "integer", "X coordinate (or start X for range)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for range)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for range query (max 15x15 = 225 cells)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for range query (max 15x15 = 225 cells)")));
            tools.Add(MakeTool("get_blueprints", "Get all placed blueprints on the map. Blueprints are unbuilt construction designations that colonists will build. Returns defName, label, position, material, size, and rotation for each blueprint. Use this to see what the player has planned for construction but hasn't built yet. Lowercase building codes in get_map_region grids (w=wall, d=door, b=bed, etc.) indicate blueprints.",
                MakeOptionalParam("filter", "string", "Optional text filter: matches against defName or label. Examples: 'wall', 'door', 'bed'. Case-insensitive."),
                MakeOptionalParam("x1", "integer", "Start X of search bounds (default: whole map)"),
                MakeOptionalParam("z1", "integer", "Start Z of search bounds"),
                MakeOptionalParam("x2", "integer", "End X of search bounds"),
                MakeOptionalParam("z2", "integer", "End Z of search bounds")));
            tools.Add(MakeTool("search_map", "Search the map for specific entities and get their exact coordinates. Much faster than scanning get_map_region grids. Use this to find colonists, items, ore deposits, plants, buildings, or enemies by type and optional name/defName filter. Returns coordinates you can use directly for building or navigation.",
                MakeParam("type", "string", "What to search for: 'colonists' (player pawns), 'hostiles' (enemies), 'animals' (all animals), 'items' (haul-able items), 'buildings' (player-built structures), 'minerals' (ore deposits), 'plants' (crops and wild plants)"),
                MakeOptionalParam("filter", "string", "Optional text filter: matches against defName or label. Examples: 'silver', 'steel', 'rice', 'wall'. Case-insensitive."),
                MakeOptionalParam("x1", "integer", "Start X of search bounds (default: whole map)"),
                MakeOptionalParam("z1", "integer", "Start Z of search bounds"),
                MakeOptionalParam("x2", "integer", "End X of search bounds"),
                MakeOptionalParam("z2", "integer", "End Z of search bounds")));

            // Environmental Visibility Tools
            tools.Add(MakeTool("get_light_levels", "Query per-cell light/glow values for illumination analysis. Light levels affect colonist mood and work speed. Returns glow values (0.0-1.0) and descriptions. Single cell or range query (max 15x15). Use to find dark rooms causing mood penalties, verify workspace lighting, or plan lamp placement.",
                MakeParam("x", "integer", "X coordinate (or start X for range query)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for range query)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for range query (max 15x15 area = 225 cells)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for range query")));
            tools.Add(MakeTool("get_light_sources", "List all light sources (lamps, torches) on the map with their position, glow radius, color, and powered status. Use to plan lighting coverage, find unpowered lights, or optimize lamp placement for full coverage.",
                MakeOptionalParam("x1", "integer", "Start X of bounding box filter (optional)"),
                MakeOptionalParam("z1", "integer", "Start Z of bounding box filter"),
                MakeOptionalParam("x2", "integer", "End X of bounding box filter"),
                MakeOptionalParam("z2", "integer", "End Z of bounding box filter"),
                MakeOptionalParam("filter", "string", "Filter by defName (e.g., 'StandingLamp', 'Torch')")));
            tools.Add(MakeTool("get_cell_beauty", "Query per-cell beauty values for environment quality analysis. Beauty affects colonist mood (ugly rooms cause debuffs, beautiful rooms boost mood). Returns beauty values and categories (Hideous/VeryUgly/Ugly/Neutral/Pretty/Beautiful/VeryBeautiful). Single cell or range query (max 15x15). Use to identify ugly areas needing art/plants, verify bedroom beauty for mood, or optimize room impressiveness.",
                MakeParam("x", "integer", "X coordinate (or start X for range query)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for range query)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for range query (max 15x15 area = 225 cells)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for range query")));
            tools.Add(MakeTool("get_pollution", "Query pollution grid for health and environment tracking. Requires Biotech DLC. Pollution affects colonist health and fertility. Returns per-cell pollution status and nearby pollution percentage within 10-cell radius. Single cell or range query (max 15x15). Use to identify polluted areas affecting colonist health, track pollution spread from wastepack atomizers, or verify clean zones for sensitive colonists.",
                MakeParam("x", "integer", "X coordinate (or start X for range query)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for range query)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for range query (max 15x15 area = 225 cells)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for range query")));
            tools.Add(MakeTool("get_roof_status", "Bulk roof analysis for a rectangular region. Returns roof type breakdown (constructed/natural thin/natural thick/none), total roofed vs unroofed cells, and optional breach detection (unroofed cells under overhead mountain). Use to find unroofed areas in bedrooms (temperature control), detect roof breaches in mountain bases (vacuum exposure risk), identify overhead mountain for infestation risk, verify constructed roof coverage, or plan roof construction.",
                MakeParam("x1", "integer", "Start X coordinate of region"),
                MakeParam("z1", "integer", "Start Z coordinate of region"),
                MakeParam("x2", "integer", "End X coordinate of region"),
                MakeParam("z2", "integer", "End Z coordinate of region"),
                MakeOptionalParam("roofType", "string", "Filter by roof type: 'none', 'thin', 'thick', 'constructed' (not yet implemented)"),
                MakeOptionalParam("detectBreaches", "boolean", "Enable breach detection: find unroofed cells adjacent to thick mountain roof (default: false)")));
            // Power Management Tools
            tools.Add(MakeTool("analyze_power_grid", "Comprehensive power network analysis. Returns all power networks with their generators, consumers, batteries, and power balance (surplus/deficit). Also identifies buildings that need power but are not connected. Use this to understand the colony's power infrastructure and identify connectivity issues."));
            tools.Add(MakeTool("check_power_connection", "Check if a specific building or area is connected to the power grid. Returns power status, which network it's connected to (if any), and nearest conduit location. Use this to diagnose why a specific building is not receiving power.",
                MakeOptionalParam("x", "integer", "X coordinate for single building check"),
                MakeOptionalParam("z", "integer", "Z coordinate for single building check"),
                MakeOptionalParam("x1", "integer", "Start X for area scan (alternative to single x/z)"),
                MakeOptionalParam("z1", "integer", "Start Z for area scan"),
                MakeOptionalParam("x2", "integer", "End X for area scan"),
                MakeOptionalParam("z2", "integer", "End Z for area scan")));
            tools.Add(MakeTool("suggest_power_route", "Suggest a conduit placement path between two points. Uses pathfinding to find an efficient route that avoids walls (optional) and minimizes cost. Returns list of cells where conduits should be placed and total steel cost estimate. Use this before auto_route_power to preview the path.",
                MakeParam("x1", "integer", "Start X coordinate (e.g., existing power network)"),
                MakeParam("z1", "integer", "Start Z coordinate"),
                MakeParam("x2", "integer", "End X coordinate (e.g., unpowered building)"),
                MakeParam("z2", "integer", "End Z coordinate"),
                MakeOptionalParam("avoidWalls", "boolean", "Try to route around walls (default: true)"),
                MakeOptionalParam("minimizeCost", "boolean", "Prefer cheaper terrain (default: true)")));
            tools.Add(MakeTool("auto_route_power", "Automatically place power conduits to connect a building to the nearest powered conduit. Finds the nearest active power network, calculates optimal path, and places conduit blueprints. Blueprints are placed as forbidden (require approval) unless autoApprove is true. Use this to quickly connect unpowered buildings to the grid.",
                MakeParam("targetX", "integer", "X coordinate of building to connect"),
                MakeParam("targetZ", "integer", "Z coordinate of building to connect"),
                MakeOptionalParam("autoApprove", "boolean", "Immediately approve blueprints for construction (default: false - places as forbidden, requires approval)")));


            // Animal Tools
            tools.Add(MakeTool("list_animals", "List all tamed/colony animals: species, name, assigned master, training completion status, and carrying capacity (for pack animals)."));
            tools.Add(MakeTool("get_animal_details", "Get detailed info about a specific animal: health, training progress for each skill, food requirements, bonded colonist, carrying capacity (pack animals), and production schedules (shearing, milking, eggs).",
                MakeParam("name", "string", "The animal's name")));
            // Note: get_animal_stats and get_wild_animals were never implemented (only defined). Removed to match tool registry.
            // Use list_animals and get_animal_details for current animal functionality.

            // Event Tools
            tools.Add(MakeTool("get_recent_events", "Get recent game events/letters: event type, severity, description, and when it occurred.",
                MakeOptionalParam("count", "integer", "Number of recent events to return. Defaults to 5.")));
            tools.Add(MakeTool("get_active_alerts", "Get all currently active game alerts (e.g. 'colonist needs rescue', 'starvation', 'tattered apparel', 'idle colonist')."));
            tools.Add(MakeTool("get_active_events", "Get all currently active weather events and disasters with detailed information: duration remaining, severity, temperature impacts, specific risks, and actionable recommendations. Covers cold snaps, heat waves, toxic fallout, solar flares, eclipses, volcanic winter, flashstorms, psychic drones, and active infestations. Returns event-specific advice like 'harvest crops before they freeze' or 'keep colonists indoors during toxic fallout'. Use this to understand ongoing environmental challenges and get context-specific survival strategies."));
            tools.Add(MakeTool("get_disaster_risks", "Assess colony-wide disaster risks and get prevention strategies. Analyzes: Infestation Risk (overhead mountain percentage, potential spawn locations, mitigation advice), Zzzt Risk (stored battery power, expected explosion damage, circuit breaker recommendations), and Raid Risk (based on colony wealth). Returns probability levels, specific vulnerabilities, and actionable prevention steps. Use this proactively to understand 'why did this happen' and 'how to prevent it in the future'."));

            // Medical Tools
            tools.Add(MakeTool("get_medical_overview", "Get medical overview: patients needing treatment, medicine supply by type, available medical beds, and doctors with their medical skill level."));

            // Health Check Tools
            tools.Add(MakeTool("colony_health_check", "Perform a comprehensive colony diagnostic check. This is the 'doctor's checkup' for your entire colony - a single tool that analyzes all critical systems and returns actionable insights. Use this when asked 'How is my colony doing?' or when you need a complete status overview. Analyzes: Food Security (days remaining, growing capacity), Power Grid (generation vs consumption, battery reserves), Defense Readiness (turrets, armed colonists, weapons), Colonist Wellbeing (injuries, mood risks, needs), Resource Bottlenecks (medicine, steel, components), Research Progress, Housing Quality (bedroom quality, bed assignments), and Production Issues (stalled bills, missing workers). Returns overall status (healthy/stable/warning/critical), per-system breakdowns with issues and recommendations, critical alerts requiring immediate action, and top 5 priority recommendations."));

            // Mood Tools
            tools.Add(MakeTool("get_mood_risks", "Analyze all colonists for mental break risk. Returns colonists at risk with their current mood level, break thresholds, negative thoughts, risk-affecting traits, and estimated time to mental break. Use this proactively to prevent mental breaks before they happen."));
            tools.Add(MakeTool("suggest_mood_interventions", "Get actionable mood improvement suggestions for a specific colonist. Analyzes their mood issues (recreation, bedroom quality, food, pain, social needs, etc.) and provides concrete steps to improve their mood and prevent mental breaks.",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("get_mood_trends", "Track colonist mood over the last 3 days with trend analysis. Calculates mood velocity (rising/falling/stable), flags colonists trending toward mental break, shows top negative thoughts, and predicts time-to-break (e.g., 'Mira will break in ~4 hours'). Requires 2-3 hours of gameplay data for accurate trends. Use this for early warning of mental break risks and proactive intervention."));

            // Social Tools
            tools.Add(MakeTool("get_social_risks", "Detect social conflicts between colonists. Finds colonist pairs with negative opinions (< -20), calculates mutual hostility, identifies risk-affecting traits (Abrasive, Volatile, Bloodlust, Psychopath), and provides intervention suggestions (separate work areas, avoid shared recreation, etc.). Use this to prevent social fights and optimize colonist interactions."));

            // Environment Tools
            tools.Add(MakeTool("get_environment_quality", "Score each room for beauty, cleanliness, space, and impressiveness. Flags rooms causing negative thoughts, identifies specific issues (poor lighting, extreme temperature, low beauty, cramped space), and suggests concrete improvements (add sculptures, install heaters, expand room, clean floors). Use this for root cause analysis of mood problems and to optimize room quality."));

            // Plan Tools
            tools.Add(MakeTool("place_plans", "Place plan designations on the map to mark where structures should be built. Plans are visual markers only — they don't consume resources or trigger construction. Use get_map_region first to understand the layout, then place plans at specific coordinates. Supports shapes: 'single' (one cell), 'rect' (rectangle outline for walls/rooms), 'filled_rect' (solid rectangle), 'line' (line between two points for corridors).",
                MakeParam("x", "integer", "X coordinate (or start X for shapes)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for shapes)"),
                MakeOptionalParam("x2", "integer", "End X coordinate (required for rect, filled_rect, line)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate (required for rect, filled_rect, line)"),
                MakeOptionalParam("shape", "string", "Shape to place: 'single' (default), 'rect' (outline only), 'filled_rect' (solid), 'line'"),
                MakeOptionalParam("name", "string", "Optional name/label for the plan group")));
            tools.Add(MakeTool("get_plans", "Get all plan designations currently on the map, including ones placed manually by the player. Returns total count, bounding box, and individual cell coordinates (if under 200 plans). Plans are visual markers showing where the player intends to build — use this to understand the player's building intentions."));
            tools.Add(MakeTool("remove_plans", "Remove plans from the map. Can remove by label (name), from a single cell, a rectangular area, or all plans on the map. Plans are the colored overlay markers players use to plan construction.",
                MakeOptionalParam("label", "string", "Remove a specific plan by its name/label"),
                MakeOptionalParam("x", "integer", "X coordinate (or start X for area removal)"),
                MakeOptionalParam("z", "integer", "Z coordinate (or start Z for area removal)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for area removal"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for area removal"),
                MakeOptionalParam("all", "boolean", "Set to true to remove ALL plans on the map")));

            // Zone Tools
            tools.Add(MakeTool("list_zones", "List all zones on the map: native RimWorld zones (growing, stockpile) with type, label, bounds, and cell count, plus custom AI-created planning zones with label, purpose, and bounds. Use this to understand the current zone layout before creating new planning zones."));
            tools.Add(MakeTool("create_zone", "Create a zone on the map. Supports three types: 'stockpile' (real RimWorld storage zone), 'growing' (real RimWorld farming zone with optional crop), or 'planning' (labeled area for AI planning purposes, drawn with plan designations). Stockpile and growing zones are real game zones that colonists will use. Use get_map_region first to choose coordinates. Use list_zones to see existing zones and avoid overlap.",
                MakeParam("type", "string", "Zone type: 'stockpile', 'growing', or 'planning'"),
                MakeParam("x1", "integer", "Start X coordinate"),
                MakeParam("z1", "integer", "Start Z coordinate"),
                MakeParam("x2", "integer", "End X coordinate"),
                MakeParam("z2", "integer", "End Z coordinate"),
                MakeOptionalParam("name", "string", "Custom name for the zone"),
                MakeOptionalParam("priority", "string", "Stockpile priority: 'Critical', 'Important', 'Normal', 'Low', 'Preferred' (stockpile only)"),
                MakeOptionalParam("crop", "string", "Crop to grow: 'rice', 'corn', 'potatoes', 'healroot', 'cotton', etc. (growing only)"),
                MakeOptionalParam("purpose", "string", "What the zone is for: housing, defense, prison, etc. (planning only)"),
                MakeOptionalParam("mark_on_map", "boolean", "Place plan designations on border (planning only, default: true)")));
            tools.Add(MakeTool("delete_zone", "Delete a zone by its label. Works on both real RimWorld zones (stockpile, growing) and custom planning zones. For native zones, frees the cells. Use list_zones to find zone names.",
                MakeParam("label", "string", "The label/name of the zone to delete"),
                MakeOptionalParam("remove_plans", "boolean", "Also remove plan designations in the area (planning zones only, default: false)")));
            tools.Add(MakeTool("set_crop", "Change which crop is planted in a growing zone.",
                MakeParam("zoneName", "string", "Growing zone name"),
                MakeParam("plantType", "string", "Crop to plant (e.g., 'rice', 'corn', 'potatoes', 'healroot', 'cotton')")));
            tools.Add(MakeTool("get_recommended_crops", "Get a list of recommended crops based on current season, growth time, yield, and purpose. Shows which crops can grow now and their characteristics."));
            tools.Add(MakeTool("set_stockpile_priority", "Set the storage priority of stockpile zones and/or storage buildings (shelves, dressers, tool cabinets). Works on blueprints too — configure storage IMMEDIATELY after placing, no need to wait for construction. Applies to ALL matching storage. Use 'room' to target storage in a specific room (e.g., 'kitchen', 'bedroom'). Use coordinate bounds to target a specific area. Higher priority storage is filled first.",
                MakeParam("zoneName", "string", "Name to match against stockpile zones and storage buildings (e.g., 'Stockpile', 'Shelf', 'Dresser'). Matches ALL storage containing this name."),
                MakeParam("priority", "string", "Priority: 'Critical', 'Important', 'Preferred', 'Normal', or 'Low'"),
                MakeOptionalParam("room", "string", "Filter by room role (e.g., 'kitchen', 'bedroom', 'hospital', 'dining room'). Only affects storage inside rooms with this role."),
                MakeOptionalParam("x1", "integer", "Start X of area bounds filter"),
                MakeOptionalParam("z1", "integer", "Start Z of area bounds filter"),
                MakeOptionalParam("x2", "integer", "End X of area bounds filter"),
                MakeOptionalParam("z2", "integer", "End Z of area bounds filter")));
            tools.Add(MakeTool("set_stockpile_filter", "Allow or disallow an entire category of items in stockpile zones and/or storage buildings (shelves, dressers, tool cabinets). Works on blueprints too — configure storage IMMEDIATELY after placing, no need to wait for construction. Use exclusive=true to ONLY allow that category (disallows everything else first). Applies to ALL matching storage. Use 'room' to target storage in a specific room (e.g., 'kitchen'). Use coordinate bounds to target a specific area.",
                MakeParam("zoneName", "string", "Name to match against stockpile zones and storage buildings (e.g., 'Stockpile', 'Shelf', 'Dresser'). Matches ALL storage containing this name."),
                MakeParam("category", "string", "Category name (e.g., 'Foods', 'ResourcesRaw', 'Items', 'Manufactured', 'Weapons', 'Apparel', 'Medicine', 'Drugs')"),
                MakeParam("allowed", "boolean", "True to allow, false to disallow"),
                MakeOptionalParam("exclusive", "boolean", "If true, disallow ALL categories first then allow ONLY this one. Use for 'only allow food' type requests. Ignores 'allowed' param."),
                MakeOptionalParam("room", "string", "Filter by room role (e.g., 'kitchen', 'bedroom', 'hospital', 'dining room'). Only affects storage inside rooms with this role."),
                MakeOptionalParam("x1", "integer", "Start X of area bounds filter"),
                MakeOptionalParam("z1", "integer", "Start Z of area bounds filter"),
                MakeOptionalParam("x2", "integer", "End X of area bounds filter"),
                MakeOptionalParam("z2", "integer", "End Z of area bounds filter")));
            tools.Add(MakeTool("set_stockpile_item", "Allow or disallow a specific item type in stockpile zones and/or storage buildings (shelves, dressers, tool cabinets). Works on blueprints too — configure storage IMMEDIATELY after placing, no need to wait for construction. Applies to ALL matching storage. Use 'room' to target storage in a specific room. Use coordinate bounds to target a specific area.",
                MakeParam("zoneName", "string", "Name to match against stockpile zones and storage buildings (e.g., 'Stockpile', 'Shelf', 'Dresser'). Matches ALL storage containing this name."),
                MakeParam("item", "string", "Item name or defName"),
                MakeParam("allowed", "boolean", "True to allow, false to disallow"),
                MakeOptionalParam("room", "string", "Filter by room role (e.g., 'kitchen', 'bedroom', 'hospital', 'dining room'). Only affects storage inside rooms with this role."),
                MakeOptionalParam("x1", "integer", "Start X of area bounds filter"),
                MakeOptionalParam("z1", "integer", "Start Z of area bounds filter"),
                MakeOptionalParam("x2", "integer", "End X of area bounds filter"),
                MakeOptionalParam("z2", "integer", "End Z of area bounds filter")));

            // Area Restriction Tools
            tools.Add(MakeTool("list_areas", "List all allowed areas that can be assigned to colonists."));
            tools.Add(MakeTool("get_area_restrictions", "Get current area restrictions for all colonists."));
            tools.Add(MakeTool("restrict_to_area", "Restrict a colonist to a specific allowed area (e.g., Home area, custom areas).",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("areaName", "string", "Area name to restrict to")));
            tools.Add(MakeTool("unrestrict", "Remove area restriction from a colonist, allowing them to go anywhere.",
                MakeParam("colonist", "string", "The colonist's name")));

            // World & Diplomacy Tools
            tools.Add(MakeTool("list_world_destinations", "List all world settlements that can be visited by caravan, with distances and faction relations."));
            tools.Add(MakeTool("get_caravan_info", "Get info about available colonists and animals for caravan formation. Note: Actual caravan formation requires manual player action."));
            tools.Add(MakeTool("get_trade_status", "Check if a trade session is currently active with a visiting trader."));
            tools.Add(MakeTool("list_trader_inventory", "List items available from current visiting trader. Requires active trade session."));
            tools.Add(MakeTool("list_factions", "List all known factions with their relation status and goodwill."));
            tools.Add(MakeTool("get_diplomatic_summary", "Get a summary count of allies, neutral factions, and hostile factions."));
            tools.Add(MakeTool("get_diplomacy_options", "Get available diplomatic actions with a specific faction.",
                MakeParam("factionName", "string", "Faction name")));

            // Bed Assignment Tools
            tools.Add(MakeTool("list_beds", "List all beds in the colony with owner assignments, locations, and room quality."));
            tools.Add(MakeTool("get_bed_assignments", "Get current bed assignments for all colonists."));
            tools.Add(MakeTool("assign_bed", "Assign a colonist to a specific bed. Use list_beds to find bed locations.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "Bed X coordinate"),
                MakeParam("z", "integer", "Bed Z coordinate")));
            tools.Add(MakeTool("unassign_bed", "Remove a colonist's bed assignment.",
                MakeParam("colonist", "string", "The colonist's name")));

            // Designation Tools (Hunting/Taming/Resource Gathering)
            tools.Add(MakeTool("designate_hunt", "Mark a wild animal for hunting. Search by name or species (e.g., 'Hare', 'Wild boar', 'Muffalo'). Use list_animals or search_map type='animals' to see available wild animals.",
                MakeParam("animal", "string", "Animal name or species to hunt (e.g., 'Hare', 'Muffalo', 'Wild boar')")));
            tools.Add(MakeTool("designate_tame", "Mark a wild animal for taming. Search by name or species. Animal must not be too wild (wildness < 98%).",
                MakeParam("animal", "string", "Animal name or species to tame (e.g., 'Husky', 'Muffalo', 'Alpaca')")));
            tools.Add(MakeTool("cancel_animal_designation", "Cancel hunt or tame designation on an animal. Search by name or species.",
                MakeParam("animal", "string", "Animal name or species to cancel designation for")));
            tools.Add(MakeTool("designate_mine", "Mark rocks for mining in an area.",
                MakeParam("x1", "integer", "Start X coordinate"),
                MakeParam("z1", "integer", "Start Z coordinate"),
                MakeParam("x2", "integer", "End X coordinate"),
                MakeParam("z2", "integer", "End Z coordinate")));
            tools.Add(MakeTool("designate_chop", "Mark trees for chopping in an area.",
                MakeParam("x1", "integer", "Start X coordinate"),
                MakeParam("z1", "integer", "Start Z coordinate"),
                MakeParam("x2", "integer", "End X coordinate"),
                MakeParam("z2", "integer", "End Z coordinate")));
            tools.Add(MakeTool("designate_harvest", "Mark plants for harvesting in an area.",
                MakeParam("x1", "integer", "Start X coordinate"),
                MakeParam("z1", "integer", "Start Z coordinate"),
                MakeParam("x2", "integer", "End X coordinate"),
                MakeParam("z2", "integer", "End Z coordinate")));

            // Equipment & Policy Tools
            tools.Add(MakeTool("list_equipment", "List current weapon and apparel for all colonists with armor ratings."));
            tools.Add(MakeTool("equip_weapon", "Force a colonist to equip a specific weapon at the given coordinates.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "Weapon X coordinate"),
                MakeParam("z", "integer", "Weapon Z coordinate")));
            tools.Add(MakeTool("wear_apparel", "Force a colonist to wear specific apparel at the given coordinates.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("x", "integer", "Apparel X coordinate"),
                MakeParam("z", "integer", "Apparel Z coordinate")));
            tools.Add(MakeTool("drop_equipment", "Make a colonist drop their current weapon.",
                MakeParam("colonist", "string", "The colonist's name")));
            tools.Add(MakeTool("assign_outfit", "Assign a clothing policy/outfit to a colonist.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("outfitName", "string", "Outfit name (e.g., 'Worker', 'Soldier', 'Nudist')")));
            tools.Add(MakeTool("assign_drug_policy", "Assign a drug policy to a colonist.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("policyName", "string", "Drug policy name")));
            tools.Add(MakeTool("assign_food_restriction", "Assign a food restriction to a colonist.",
                MakeParam("colonist", "string", "The colonist's name"),
                MakeParam("restrictionName", "string", "Food restriction name")));

            // Building Tools
            tools.Add(MakeTool("list_buildable", "List available buildings that can be constructed. Shows defName, label, size, material requirements, and research status. Use 'category' to filter (Structure, Furniture, Production, Power, Security, Temperature, Misc, Joy). Without filter, shows all buildings grouped by category.",
                MakeOptionalParam("category", "string", "Filter by building category (e.g., 'Structure', 'Furniture', 'Production', 'Power', 'Security')")));
            tools.Add(MakeTool("get_building_info", "Get detailed information about a specific building type: description, size, material requirements, available materials, costs, stats, research prerequisites, and passability.",
                MakeParam("defName", "string", "The building's defName (from list_buildable)")));
            tools.Add(MakeTool("get_requirements", "Get comprehensive placement requirements for a building. Returns size, power output/consumption, placement rules (special requirements like 'must be on steam geyser'), terrain requirements, resource costs, research prerequisites, and work to build. Use this when you need to know what's required to place a specific building type before attempting placement.",
                MakeParam("building", "string", "Building defName (e.g., 'ElectricStove', 'GeothermalGenerator', 'Bed')")));
            tools.Add(MakeTool("place_building", "Place building blueprints on the map. Use for individual buildings and furniture. For rooms/walls, prefer place_structure instead.\n\nSINGLE: {defName, x, z, stuff?, rotation?, auto_approve?}\nBATCH: {placements: [{defName, x, z, stuff?, rotation?}, ...], auto_approve?} (max 100)\n\nRotation: 0=North, 1=East, 2=South, 3=West. Stuff examples: WoodLog, BlocksGranite, Steel.\nIf auto_approve is true, colonists start building immediately.",
                MakeOptionalParam("defName", "string", "Building defName for single placement"),
                MakeOptionalParam("x", "integer", "X coordinate for single placement"),
                MakeOptionalParam("z", "integer", "Z coordinate for single placement"),
                MakeOptionalParam("stuff", "string", "Material defName if building requires stuff (e.g., 'WoodLog', 'BlocksGranite', 'Steel')"),
                MakeOptionalParam("rotation", "integer", "Rotation: 0=North (default), 1=East, 2=South, 3=West"),
                MakeOptionalParam("auto_approve", "boolean", "If true, blueprints are unforbidden immediately so colonists start building. Default: false (forbidden until approved)."),
                MakePlacementsArrayParam()));
            tools.Add(MakeTool("place_structure", "Build structures efficiently with one call. Use this instead of placing individual walls.\n\nShapes:\n- 'room': Walls + door. Builds a complete rectangular room.\n- 'wall_rect': Wall outline (no door).\n- 'wall_line': Line of walls between two points.\n\nExample room: {shape:'room', x1:10, z1:10, x2:16, z2:16, stuff:'BlocksGranite', door_side:'south'}\nA room (10,10)-(16,16) = 7x7 exterior, 5x5 interior.\n\ndoor_side: which wall gets the door (default: south).\ndoor_offset: 0-based position from left/bottom of wall, default: center.",
                MakeParam("shape", "string", "Shape: 'room', 'wall_rect', or 'wall_line'"),
                MakeParam("x1", "integer", "Start/corner X coordinate"),
                MakeParam("z1", "integer", "Start/corner Z coordinate"),
                MakeParam("x2", "integer", "End/corner X coordinate"),
                MakeParam("z2", "integer", "End/corner Z coordinate"),
                MakeParam("stuff", "string", "Material defName (e.g., 'WoodLog', 'BlocksGranite', 'Steel')"),
                MakeOptionalParam("door_side", "string", "Which wall for the door: 'north', 'south', 'east', 'west' (room only, default: 'south')"),
                MakeOptionalParam("door_offset", "integer", "Door position along wall, 0=leftmost/bottommost inner cell, default=center (room only)"),
                MakeOptionalParam("door_stuff", "string", "Material for the door, defaults to 'stuff' value (room only)"),
                MakeOptionalParam("auto_approve", "boolean", "If true, colonists start building immediately. Default: false")));
            tools.Add(MakeTool("remove_building", "Remove AI-proposed building blueprints from the map. Can target specific proposals by ID, an area, or all proposals at once.",
                MakeStringArrayParam("proposal_ids", "Array of proposal IDs to remove (e.g., ['rm_1', 'rm_2'])", false),
                MakeOptionalParam("x", "integer", "Start X for area removal"),
                MakeOptionalParam("z", "integer", "Start Z for area removal"),
                MakeOptionalParam("x2", "integer", "End X for area removal"),
                MakeOptionalParam("z2", "integer", "End Z for area removal"),
                MakeOptionalParam("all", "boolean", "Set true to remove ALL AI-proposed blueprints")));
            tools.Add(MakeTool("approve_buildings", "Approve AI-proposed building blueprints by unforbidding them so colonists will start construction. Can approve specific proposals by ID, an area, or all.",
                MakeStringArrayParam("proposal_ids", "Array of proposal IDs to approve (e.g., ['rm_1', 'rm_2'])", false),
                MakeOptionalParam("x", "integer", "Start X for area approval"),
                MakeOptionalParam("z", "integer", "Start Z for area approval"),
                MakeOptionalParam("x2", "integer", "End X for area approval"),
                MakeOptionalParam("z2", "integer", "End Z for area approval"),
                MakeOptionalParam("all", "boolean", "Set true to approve ALL AI-proposed blueprints")));

            // Directive Tools
            tools.Add(MakeTool("get_directives", "Get the current player-defined colony directives. These are standing rules, preferences, and playstyle instructions set by the player. Check this before adding new directives to avoid duplicates."));
            tools.Add(MakeTool("add_directive", "Add a new colony directive. Use this when the player confirms they want to save a preference or rule. Write concise, clear directive text.",
                MakeParam("text", "string", "The directive text to add (e.g., 'Melee weapons only - no ranged weapons or turrets')")));
            tools.Add(MakeTool("remove_directive", "Remove a colony directive by searching for matching text. Use when the player wants to remove or change a standing rule.",
                MakeParam("search", "string", "Text to search for in existing directives. The first directive line containing this text (case-insensitive) will be removed.")));

            // Trade Tools
            tools.Add(MakeTool("get_active_traders", "Get all currently available traders: orbital trade ships, visiting caravans, and allied settlements in comms range. For each trader, returns faction, items in stock with quantities and prices (buy/sell), silver available, and time until departure. Use this to discover trading opportunities."));
            tools.Add(MakeTool("analyze_trade_opportunity", "Analyze trade opportunities with current traders. Compares colony resources against trader inventory to suggest profitable trades: items to buy for urgent needs (medicine, components, food), items to sell for profit (surplus materials), and strategic purchases. Returns recommendations with priority scores and reasoning.",
                MakeOptionalParam("traderFilter", "string", "Optional filter to analyze specific trader by name, faction, or type (e.g., 'orbital', 'caravan'). If omitted, analyzes all traders.")));

            cachedTools = tools;
            return tools;
        }

        private static JSONObject MakeTool(string name, string description, params JSONObject[] parameters)
        {
            var tool = new JSONObject();
            tool["type"] = "function";

            var func = new JSONObject();
            func["name"] = name;
            func["description"] = description;

            var paramSchema = new JSONObject();
            paramSchema["type"] = "object";

            var properties = new JSONObject();
            var required = new JSONArray();

            foreach (var p in parameters)
            {
                string paramName = p["name"].Value;
                var prop = new JSONObject();
                prop["type"] = p["param_type"].Value;
                prop["description"] = p["description"].Value;
                if (p["items"] != null)
                    prop["items"] = p["items"];
                properties[paramName] = prop;

                if (p["required"].AsBool)
                    required.Add(paramName);
            }

            paramSchema["properties"] = properties;
            if (required.Count > 0)
                paramSchema["required"] = required;

            func["parameters"] = paramSchema;
            tool["function"] = func;
            return tool;
        }

        private static JSONObject MakeParam(string name, string type, string description)
        {
            var p = new JSONObject();
            p["name"] = name;
            p["param_type"] = type;
            p["description"] = description;
            p["required"] = true;
            return p;
        }

        private static JSONObject MakeOptionalParam(string name, string type, string description)
        {
            var p = new JSONObject();
            p["name"] = name;
            p["param_type"] = type;
            p["description"] = description;
            p["required"] = false;
            return p;
        }

        private static JSONObject MakePlacementsArrayParam()
        {
            var p = new JSONObject();
            p["name"] = "placements";
            p["param_type"] = "array";
            p["description"] = "Array of building placements for batch mode (max 100). Each element: {defName, x, z, stuff?, rotation?}";
            p["required"] = false;

            var itemSchema = new JSONObject();
            itemSchema["type"] = "object";
            var itemProps = new JSONObject();
            var defProp = new JSONObject(); defProp["type"] = "string"; defProp["description"] = "Building defName"; itemProps["defName"] = defProp;
            var xProp = new JSONObject(); xProp["type"] = "integer"; itemProps["x"] = xProp;
            var zProp = new JSONObject(); zProp["type"] = "integer"; itemProps["z"] = zProp;
            var stuffProp = new JSONObject(); stuffProp["type"] = "string"; stuffProp["description"] = "Material defName"; itemProps["stuff"] = stuffProp;
            var rotProp = new JSONObject(); rotProp["type"] = "integer"; rotProp["description"] = "0=North, 1=East, 2=South, 3=West"; itemProps["rotation"] = rotProp;
            itemSchema["properties"] = itemProps;
            var itemRequired = new JSONArray(); itemRequired.Add("defName"); itemRequired.Add("x"); itemRequired.Add("z");
            itemSchema["required"] = itemRequired;
            p["items"] = itemSchema;

            return p;
        }

        private static JSONObject MakeStringArrayParam(string name, string description, bool required)
        {
            var p = new JSONObject();
            p["name"] = name;
            p["param_type"] = "array";
            p["description"] = description;
            p["required"] = required;

            var itemSchema = new JSONObject();
            itemSchema["type"] = "string";
            p["items"] = itemSchema;

            return p;
        }
    }
}
