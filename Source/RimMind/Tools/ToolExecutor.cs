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
            { "draft_colonist", args => ColonistTools.DraftColonist(args?["name"]?.Value) },
            { "undraft_colonist", args => ColonistTools.UndraftColonist(args?["name"]?.Value) },
            { "draft_all", args => ColonistTools.DraftAll() },
            { "undraft_all", args => ColonistTools.UndraftAll() },

            // Social
            { "get_relationships", args => SocialTools.GetRelationships(args?["name"]?.Value) },
            { "get_faction_relations", args => SocialTools.GetFactionRelations() },

            // Work
            { "get_work_priorities", args => WorkTools.GetWorkPriorities() },
            { "set_work_priority", args => WorkTools.SetWorkPriority(args?["colonist"]?.Value, args?["workType"]?.Value, args?["priority"]?.AsInt ?? 0) },
            { "get_bills", args => WorkTools.GetBills(args?["workbench"]?.Value) },
            { "get_schedules", args => WorkTools.GetSchedules() },
            { "set_schedule", args => WorkTools.SetSchedule(args?["colonist"]?.Value, args?["hour"]?.AsInt ?? 0, args?["assignment"]?.Value) },
            { "copy_schedule", args => WorkTools.CopySchedule(args?["from"]?.Value, args?["to"]?.Value) },

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
            { "set_stockpile_priority", args => ZoneTools.SetStockpilePriority(args?["zoneName"]?.Value, args?["priority"]?.Value) },
            { "set_stockpile_filter", args => ZoneTools.SetStockpileFilter(args?["zoneName"]?.Value, args?["category"]?.Value, args?["allowed"]?.AsBool) },
            { "set_stockpile_item", args => ZoneTools.SetStockpileItem(args?["zoneName"]?.Value, args?["item"]?.Value, args?["allowed"]?.AsBool) },

            // Area Restrictions
            { "list_areas", args => AreaTools.ListAreas() },
            { "get_area_restrictions", args => AreaTools.GetAreaRestrictions() },
            { "restrict_to_area", args => AreaTools.RestrictToArea(args?["colonist"]?.Value, args?["areaName"]?.Value) },
            { "unrestrict", args => AreaTools.Unrestrict(args?["colonist"]?.Value) },

            // World & Diplomacy
            { "list_world_destinations", args => WorldTools.ListWorldDestinations() },
            { "get_caravan_info", args => WorldTools.GetCaravanInfo() },
            { "get_trade_status", args => WorldTools.GetTradeStatus() },
            { "list_trader_inventory", args => WorldTools.ListTraderInventory() },
            { "list_factions", args => WorldTools.ListFactions() },
            { "get_diplomatic_summary", args => WorldTools.GetDiplomaticSummary() },
            { "get_diplomacy_options", args => WorldTools.GetDiplomacyOptions(args?["factionName"]?.Value) },

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
