using System;
using System.Collections.Generic;
using RimMind.API;
using Verse;

namespace RimMind.Tools
{
    public static class ToolExecutor
    {
        private static readonly Dictionary<string, Func<JSONNode, string>> handlers = new Dictionary<string, Func<JSONNode, string>>
        {
            // Colonist
            { "list_colonists", args => ColonistTools.ListColonists() },
            { "get_colonist_details", args => ColonistTools.GetColonistDetails(args?["name"]?.Value) },
            { "get_colonist_health", args => ColonistTools.GetColonistHealth(args?["name"]?.Value) },

            // Social
            { "get_relationships", args => SocialTools.GetRelationships(args?["name"]?.Value) },
            { "get_faction_relations", args => SocialTools.GetFactionRelations() },

            // Work
            { "get_work_priorities", args => WorkTools.GetWorkPriorities() },
            { "get_bills", args => WorkTools.GetBills(args?["workbench"]?.Value) },
            { "get_schedules", args => WorkTools.GetSchedules() },

            // Colony
            { "get_colony_overview", args => ColonyTools.GetColonyOverview() },
            { "get_resources", args => ColonyTools.GetResources(args?["category"]?.Value) },
            { "get_rooms", args => ColonyTools.GetRooms() },
            { "get_stockpiles", args => ColonyTools.GetStockpiles() },

            // Research
            { "get_research_status", args => ResearchTools.GetResearchStatus() },
            { "get_available_research", args => ResearchTools.GetAvailableResearch() },
            { "get_completed_research", args => ResearchTools.GetCompletedResearch() },

            // Military
            { "get_threats", args => MilitaryTools.GetThreats() },
            { "get_defenses", args => MilitaryTools.GetDefenses() },
            { "get_combat_readiness", args => MilitaryTools.GetCombatReadiness() },

            // Map
            { "get_weather_and_season", args => MapTools.GetWeatherAndSeason() },
            { "get_growing_zones", args => MapTools.GetGrowingZones() },
            { "get_power_status", args => MapTools.GetPowerStatus() },
            { "get_map_region", args => MapTools.GetMapRegion(args) },
            { "get_cell_details", args => MapTools.GetCellDetails(args) },
            { "search_map", args => MapTools.SearchMap(args) },

            // Animals
            { "list_animals", args => AnimalTools.ListAnimals() },
            { "get_animal_details", args => AnimalTools.GetAnimalDetails(args?["name"]?.Value) },

            // Events
            { "get_recent_events", args => EventTools.GetRecentEvents(args?["count"]?.AsInt ?? 5) },
            { "get_active_alerts", args => EventTools.GetActiveAlerts() },

            // Medical
            { "get_medical_overview", args => MedicalTools.GetMedicalOverview() },

            // Plan
            { "place_plans", args => PlanTools.PlacePlans(args) },
            { "remove_plans", args => PlanTools.RemovePlans(args) },
            { "get_plans", args => PlanTools.GetPlans() },

            // Zone
            { "list_zones", args => ZoneTools.ListZones() },
            { "create_zone", args => ZoneTools.CreateZone(args) },
            { "delete_zone", args => ZoneTools.DeleteZone(args) },

            // Area Restrictions
            { "list_areas", args => AreaTools.ListAreas() },
            { "get_area_restrictions", args => AreaTools.GetAreaRestrictions() },
            { "restrict_to_area", args => AreaTools.RestrictToArea(args?["colonist"]?.Value, args?["areaName"]?.Value) },
            { "unrestrict", args => AreaTools.Unrestrict(args?["colonist"]?.Value) },

            // Building
            { "list_buildable", args => BuildingTools.ListBuildable(args) },
            { "get_building_info", args => BuildingTools.GetBuildingInfo(args) },
            { "place_building", args => BuildingTools.PlaceBuilding(args) },
            { "place_structure", args => BuildingTools.PlaceStructure(args) },
            { "remove_building", args => BuildingTools.RemoveBuilding(args) },
            { "approve_buildings", args => BuildingTools.ApproveBuildings(args) },

            // Directives
            { "get_directives", args => DirectiveTools.GetDirectives() },
            { "add_directive", args => DirectiveTools.AddDirective(args?["text"]?.Value) },
            { "remove_directive", args => DirectiveTools.RemoveDirective(args?["search"]?.Value) },
        };

        public static string Execute(string toolName, string argumentsJson)
        {
            try
            {
                if (!handlers.ContainsKey(toolName))
                    return JsonError("Unknown tool: " + toolName);

                JSONNode args = null;
                if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson != "{}")
                {
                    try { args = JSONNode.Parse(argumentsJson); }
                    catch { args = null; }
                }

                return handlers[toolName](args);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Tool execution error for '" + toolName + "': " + ex);
                return JsonError("Tool execution failed: " + ex.Message);
            }
        }

        public static string JsonError(string message)
        {
            var obj = new JSONObject();
            obj["error"] = message;
            return obj.ToString();
        }
    }
}
