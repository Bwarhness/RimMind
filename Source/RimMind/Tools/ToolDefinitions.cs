using System.Collections.Generic;
using RimMind.API;

namespace RimMind.Tools
{
    public static class ToolDefinitions
    {
        public static List<JSONNode> GetAllTools()
        {
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
            tools.Add(MakeTool("get_threats", "Get active threats on the map: hostile pawns/factions, sieges, infestations, manhunter animals, and mechanoid clusters."));
            tools.Add(MakeTool("get_defenses", "Get defensive structures: turrets (type, status, ammo), traps, and sandbags/barricades with their locations."));
            tools.Add(MakeTool("get_combat_readiness", "Get combat readiness for each colonist: equipped weapon, armor pieces, shooting skill, melee skill, and any combat-relevant traits."));

            // Map Tools
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

            // Animal Tools
            tools.Add(MakeTool("list_animals", "List all tamed/colony animals: species, name, assigned master, and training completion status."));
            tools.Add(MakeTool("get_animal_details", "Get detailed info about a specific animal: health, training progress for each skill, food requirements, and bonded colonist.",
                MakeParam("name", "string", "The animal's name")));

            // Event Tools
            tools.Add(MakeTool("get_recent_events", "Get recent game events/letters: event type, severity, description, and when it occurred.",
                MakeOptionalParam("count", "integer", "Number of recent events to return. Defaults to 5.")));
            tools.Add(MakeTool("get_active_alerts", "Get all currently active game alerts (e.g. 'colonist needs rescue', 'starvation', 'tattered apparel', 'idle colonist')."));

            // Medical Tools
            tools.Add(MakeTool("get_medical_overview", "Get medical overview: patients needing treatment, medicine supply by type, available medical beds, and doctors with their medical skill level."));

            // Health Check Tools
            tools.Add(MakeTool("colony_health_check", "Perform a comprehensive colony diagnostic check. This is the 'doctor's checkup' for your entire colony - a single tool that analyzes all critical systems and returns actionable insights. Use this when asked 'How is my colony doing?' or when you need a complete status overview. Analyzes: Food Security (days remaining, growing capacity), Power Grid (generation vs consumption, battery reserves), Defense Readiness (turrets, armed colonists, weapons), Colonist Wellbeing (injuries, mood risks, needs), Resource Bottlenecks (medicine, steel, components), Research Progress, Housing Quality (bedroom quality, bed assignments), and Production Issues (stalled bills, missing workers). Returns overall status (healthy/stable/warning/critical), per-system breakdowns with issues and recommendations, critical alerts requiring immediate action, and top 5 priority recommendations."));

            // Mood Tools
            tools.Add(MakeTool("get_mood_risks", "Analyze all colonists for mental break risk. Returns colonists at risk with their current mood level, break thresholds, negative thoughts, risk-affecting traits, and estimated time to mental break. Use this proactively to prevent mental breaks before they happen."));
            tools.Add(MakeTool("suggest_mood_interventions", "Get actionable mood improvement suggestions for a specific colonist. Analyzes their mood issues (recreation, bedroom quality, food, pain, social needs, etc.) and provides concrete steps to improve their mood and prevent mental breaks.",
                MakeParam("name", "string", "The colonist's name")));

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
            tools.Add(MakeTool("designate_hunt", "Mark a wild animal for hunting. Use get_map_region to find animals.",
                MakeParam("x", "integer", "Animal X coordinate"),
                MakeParam("z", "integer", "Animal Z coordinate")));
            tools.Add(MakeTool("designate_tame", "Mark a wild animal for taming. Animal must not be too wild.",
                MakeParam("x", "integer", "Animal X coordinate"),
                MakeParam("z", "integer", "Animal Z coordinate")));
            tools.Add(MakeTool("cancel_animal_designation", "Cancel hunt or tame designation on an animal.",
                MakeParam("x", "integer", "Animal X coordinate"),
                MakeParam("z", "integer", "Animal Z coordinate")));
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

        private static JSONObject MakeArrayParam(string name, string description, bool required)
        {
            var p = new JSONObject();
            p["name"] = name;
            p["param_type"] = "array";
            p["description"] = description;
            p["required"] = required;

            var itemSchema = new JSONObject();
            itemSchema["type"] = "object";
            var itemProps = new JSONObject();
            var xProp = new JSONObject(); xProp["type"] = "integer"; itemProps["x"] = xProp;
            var zProp = new JSONObject(); zProp["type"] = "integer"; itemProps["z"] = zProp;
            itemSchema["properties"] = itemProps;
            var itemRequired = new JSONArray(); itemRequired.Add("x"); itemRequired.Add("z");
            itemSchema["required"] = itemRequired;
            p["items"] = itemSchema;

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
