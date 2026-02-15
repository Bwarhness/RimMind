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

            // Social Tools
            tools.Add(MakeTool("get_relationships", "Get a colonist's social relationships: opinions of and from other colonists, relationship types (lover, spouse, rival, friend, etc).",
                MakeParam("name", "string", "The colonist's name")));
            tools.Add(MakeTool("get_faction_relations", "Get relations with all known factions: faction name, goodwill value, and relationship status (ally, neutral, hostile)."));

            // Work Tools
            tools.Add(MakeTool("get_work_priorities", "Get the work priority grid for all colonists: each work type (Doctor, Cook, Hunt, Construct, Grow, Mine, Research, etc.) and its priority level (1=highest, 4=lowest, 0=disabled)."));
            tools.Add(MakeTool("get_bills", "Get active production bills at workbenches: recipe name, target count, ingredients needed, assigned worker, and whether suspended.",
                MakeOptionalParam("workbench", "string", "Filter by workbench name. If omitted, returns bills from all workbenches.")));
            tools.Add(MakeTool("get_schedules", "Get daily schedules for all colonists: hour-by-hour assignments (Sleep, Work, Anything, Joy/Recreation)."));

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

            // Plan Tools
            tools.Add(MakeTool("place_plans", "Place plan designations on the map to mark where structures should be built. Plans are visual markers only — they don't consume resources or trigger construction. Use get_map_region first to understand the layout, then place plans at specific coordinates. Supports shapes: 'single' (one cell), 'rect' (rectangle outline for walls/rooms), 'filled_rect' (solid rectangle), 'line' (line between two points for corridors).",
                MakeParam("x", "integer", "X coordinate (or start X for shapes)"),
                MakeParam("z", "integer", "Z coordinate (or start Z for shapes)"),
                MakeOptionalParam("x2", "integer", "End X coordinate (required for rect, filled_rect, line)"),
                MakeOptionalParam("z2", "integer", "End Z coordinate (required for rect, filled_rect, line)"),
                MakeOptionalParam("shape", "string", "Shape to place: 'single' (default), 'rect' (outline only), 'filled_rect' (solid), 'line'")));
            tools.Add(MakeTool("remove_plans", "Remove plan designations from the map. Can remove from a single cell, a rectangular area, or all plans on the map.",
                MakeOptionalParam("x", "integer", "X coordinate (or start X for area removal)"),
                MakeOptionalParam("z", "integer", "Z coordinate (or start Z for area removal)"),
                MakeOptionalParam("x2", "integer", "End X coordinate for area removal"),
                MakeOptionalParam("z2", "integer", "End Z coordinate for area removal"),
                MakeOptionalParam("all", "boolean", "Set to true to remove ALL plan designations on the map")));

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
    }
}
