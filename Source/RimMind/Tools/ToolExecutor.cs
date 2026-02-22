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
            { "get_colonist_locations", args => ColonistTools.GetColonistLocations() },

            // Social
            { "get_relationships", args => SocialTools.GetRelationships(args?["name"]?.Value) },
            { "get_faction_relations", args => SocialTools.GetFactionRelations() },

            // Work
            { "get_work_priorities", args => WorkTools.GetWorkPriorities() },
            { "set_work_priority", args => WorkTools.SetWorkPriority(args?["colonist"]?.Value, args?["workType"]?.Value, args?["priority"]?.AsInt ?? 0) },
            { "get_bills", args => WorkTools.GetBills(args?["workbench"]?.Value) },
            { "list_recipes", args => WorkTools.ListRecipes(args?["workbench"]?.Value) },
            { "create_bill", args => WorkTools.CreateBill(args) },
            { "modify_bill", args => WorkTools.ModifyBill(args) },
            { "delete_bill", args => WorkTools.DeleteBill(args) },
            { "get_schedules", args => WorkTools.GetSchedules() },
            { "set_schedule", args => WorkTools.SetSchedule(args?["colonist"]?.Value, args?["hour"]?.AsInt ?? -1, args?["assignment"]?.Value) },
            { "copy_schedule", args => WorkTools.CopySchedule(args?["from"]?.Value, args?["to"]?.Value) },
            { "get_work_queue", args => WorkTools.GetWorkQueue() },
            { "get_construction_status", args => WorkTools.GetConstructionStatus() },

            // Job Prioritization
            { "prioritize_rescue", args => JobTools.PrioritizeRescue(args?["colonist"]?.Value, args?["target"]?.Value) },
            { "prioritize_tend", args => JobTools.PrioritizeTend(args?["doctor"]?.Value, args?["patient"]?.Value) },
            { "prioritize_haul", args => JobTools.PrioritizeHaul(args?["colonist"]?.Value, args?["x"]?.AsInt ?? -1, args?["z"]?.AsInt ?? -1) },
            { "prioritize_repair", args => JobTools.PrioritizeRepair(args?["colonist"]?.Value, args?["x"]?.AsInt ?? -1, args?["z"]?.AsInt ?? -1) },
            { "prioritize_clean", args => JobTools.PrioritizeClean(args?["colonist"]?.Value, args?["x"]?.AsInt ?? -1, args?["z"]?.AsInt ?? -1, args?["radius"]?.AsInt ?? 5) },

            // Colony
            { "get_colony_overview", args => ColonyTools.GetColonyOverview() },
            { "get_resources", args => ColonyTools.GetResources(args?["category"]?.Value) },
            { "get_rooms", args => ColonyTools.GetRooms() },
            { "get_stockpiles", args => ColonyTools.GetStockpiles() },
            { "get_resource_trends", args => ColonyTools.GetResourceTrends() },

            // Research
            { "get_research_status", args => ResearchTools.GetResearchStatus() },
            { "get_available_research", args => ResearchTools.GetAvailableResearch() },
            { "get_completed_research", args => ResearchTools.GetCompletedResearch() },

            // Military
            { "get_threats", args => MilitaryTools.GetThreats() },
            { "get_fire_support", args => MilitaryTools.GetFireSupport() },
            { "get_casualties", args => MilitaryTools.GetCasualties() },
            { "get_defenses", args => MilitaryTools.GetDefenses() },
            { "get_combat_readiness", args => MilitaryTools.GetCombatReadiness() },
            
            // Combat Intelligence (Phase 5)
            { "get_weapon_stats", args => CombatTools.GetWeaponStats(args?["pawnName"]?.Value) },
            { "get_armor_stats", args => CombatTools.GetArmorStats(args?["pawnName"]?.Value) },
            { "get_enemy_morale", args => CombatTools.GetEnemyMorale() },
            { "get_friendly_fire_risk", args => CombatTools.GetFriendlyFireRisk(args?["shooterName"]?.Value, args?["targetName"]?.Value) },
            { "get_cover_analysis", args => CombatTools.GetCoverAnalysis(args) },
            { "get_tactical_pathfinding", args => CombatTools.GetTacticalPathfinding(args) },

            // DLC Combat (Royalty & Biotech)
            { "get_psycasts", args => DLCTools.GetPsycasts(args?["name"]?.Value) },
            { "get_genes", args => DLCTools.GetGenes(args?["name"]?.Value) },
            { "get_mechanitor_info", args => DLCTools.GetMechanitorInfo(args?["name"]?.Value) },

            // Map
            { "get_semantic_overview", args => SemanticTools.GetSemanticOverview() },
            { "find_buildable_area", args => SemanticTools.FindBuildableArea(args) },
            { "get_weather_and_season", args => MapTools.GetWeatherAndSeason() },
            { "get_growing_zones", args => MapTools.GetGrowingZones() },
            { "get_power_status", args => MapTools.GetPowerStatus() },
            { "get_temperature_risks", args => MapTools.GetTemperatureRisks() },
            { "get_map_region", args => MapTools.GetMapRegion(args) },
            { "get_cell_details", args => MapTools.GetCellDetails(args) },
            { "get_blueprints", args => MapTools.GetBlueprints(args) },
            { "search_map", args => MapTools.SearchMap(args) },
            { "get_light_levels", args => MapTools.GetLightLevels(args) },
            { "get_light_sources", args => MapTools.GetLightSources(args) },
            { "get_cell_beauty", args => MapTools.GetCellBeauty(args) },
            { "get_pollution", args => MapTools.GetPollution(args) },
            { "get_roof_status", args => MapTools.GetRoofStatus(args) },

            // Power Management
            { "analyze_power_grid", args => PowerTools.AnalyzePowerGrid() },
            { "check_power_connection", args => PowerTools.CheckPowerConnection(args) },
            { "suggest_power_route", args => PowerTools.SuggestPowerRoute(args) },
            { "auto_route_power", args => PowerTools.AutoRoutePower(args) },

            // Animals
            { "list_animals", args => AnimalTools.ListAnimals() },
            { "get_animal_details", args => AnimalTools.GetAnimalDetails(args?["name"]?.Value) },
            { "get_animal_stats", args => AnimalTools.GetAnimalStats(args?["speciesName"]?.Value) },
            { "get_wild_animals", args => AnimalTools.GetWildAnimals() },

            // Events
            { "get_recent_events", args => EventTools.GetRecentEvents(args?["count"]?.AsInt ?? 5) },
            { "get_active_alerts", args => EventTools.GetActiveAlerts() },
            { "get_active_events", args => EventTools.GetActiveEvents() },
            { "get_disaster_risks", args => EventTools.GetDisasterRisks() },

            // Medical
            { "get_medical_overview", args => MedicalTools.GetMedicalOverview() },

            // Health Check
            { "colony_health_check", args => HealthCheckTools.ColonyHealthCheck() },

            // Mood
            { "get_mood_risks", args => MoodTools.GetMoodRisks() },
            { "suggest_mood_interventions", args => MoodTools.SuggestMoodInterventions(args?["name"]?.Value) },
            { "get_mood_trends", args => MoodTools.GetMoodTrends() },
            { "get_environment_quality", args => EnvironmentTools.GetEnvironmentQuality() },

            // Social
            { "get_social_risks", args => SocialTools.GetSocialRisks() },

            // Plan
            { "place_plans", args => PlanTools.PlacePlans(args) },
            { "remove_plans", args => PlanTools.RemovePlans(args) },
            { "get_plans", args => PlanTools.GetPlans() },

            // Zone
            { "list_zones", args => ZoneTools.ListZones() },
            { "create_zone", args => ZoneTools.CreateZone(args) },
            { "delete_zone", args => ZoneTools.DeleteZone(args) },
            { "set_crop", args => ZoneTools.SetCrop(args?["zoneName"]?.Value, args?["plantType"]?.Value) },
            { "get_recommended_crops", args => ZoneTools.GetRecommendedCrops() },
            { "set_stockpile_priority", args => ZoneTools.SetStockpilePriority(args) },
            { "set_stockpile_filter", args => ZoneTools.SetStockpileFilter(args) },
            { "set_stockpile_item", args => ZoneTools.SetStockpileItem(args) },

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

            // Bed Assignments
            { "list_beds", args => BedTools.ListBeds() },
            { "get_bed_assignments", args => BedTools.GetBedAssignments() },
            { "assign_bed", args => args?["x"] == null || args?["z"] == null ? JsonError("'x' and 'z' coordinates are required.") : BedTools.AssignBed(args["colonist"]?.Value, args["x"].AsInt, args["z"].AsInt) },
            { "unassign_bed", args => BedTools.UnassignBed(args?["colonist"]?.Value) },

            // Designations
            { "designate_hunt", args => DesignationTools.DesignateHunt(args?["animal"]?.Value) },
            { "designate_tame", args => DesignationTools.DesignateTame(args?["animal"]?.Value) },
            { "cancel_animal_designation", args => DesignationTools.CancelAnimalDesignation(args?["animal"]?.Value) },
            { "designate_mine", args => args?["x1"] == null || args?["z1"] == null || args?["x2"] == null || args?["z2"] == null ? JsonError("'x1', 'z1', 'x2', 'z2' coordinates are required.") : DesignationTools.DesignateMine(args["x1"].AsInt, args["z1"].AsInt, args["x2"].AsInt, args["z2"].AsInt) },
            { "designate_chop", args => args?["x1"] == null || args?["z1"] == null || args?["x2"] == null || args?["z2"] == null ? JsonError("'x1', 'z1', 'x2', 'z2' coordinates are required.") : DesignationTools.DesignateChop(args["x1"].AsInt, args["z1"].AsInt, args["x2"].AsInt, args["z2"].AsInt) },
            { "designate_harvest", args => args?["x1"] == null || args?["z1"] == null || args?["x2"] == null || args?["z2"] == null ? JsonError("'x1', 'z1', 'x2', 'z2' coordinates are required.") : DesignationTools.DesignateHarvest(args["x1"].AsInt, args["z1"].AsInt, args["x2"].AsInt, args["z2"].AsInt) },

            // Equipment & Policies
            { "list_equipment", args => EquipmentTools.ListEquipment() },
            { "equip_weapon", args => args?["x"] == null || args?["z"] == null ? JsonError("'x' and 'z' coordinates are required.") : EquipmentTools.EquipWeapon(args["colonist"]?.Value, args["x"].AsInt, args["z"].AsInt) },
            { "wear_apparel", args => args?["x"] == null || args?["z"] == null ? JsonError("'x' and 'z' coordinates are required.") : EquipmentTools.WearApparel(args["colonist"]?.Value, args["x"].AsInt, args["z"].AsInt) },
            { "drop_equipment", args => EquipmentTools.DropEquipment(args?["colonist"]?.Value) },
            { "assign_outfit", args => EquipmentTools.AssignOutfit(args?["colonist"]?.Value, args?["outfitName"]?.Value) },
            { "assign_drug_policy", args => EquipmentTools.AssignDrugPolicy(args?["colonist"]?.Value, args?["policyName"]?.Value) },
            { "assign_food_restriction", args => EquipmentTools.AssignFoodRestriction(args?["colonist"]?.Value, args?["restrictionName"]?.Value) },

            // Building
            { "list_buildable", args => BuildingTools.ListBuildable(args) },
            { "get_building_info", args => BuildingTools.GetBuildingInfo(args) },
            { "get_requirements", args => BuildingTools.GetRequirements(args) },
            { "place_building", args => BuildingTools.PlaceBuilding(args) },
            { "place_structure", args => BuildingTools.PlaceStructure(args) },
            { "check_placement", args => BuildingTools.CheckPlacement(args) },
            { "remove_building", args => BuildingTools.RemoveBuilding(args) },
            { "approve_buildings", args => BuildingTools.ApproveBuildings(args) },

            // Directives
            { "get_directives", args => DirectiveTools.GetDirectives() },
            { "add_directive", args => DirectiveTools.AddDirective(args?["text"]?.Value) },
            { "remove_directive", args => DirectiveTools.RemoveDirective(args?["search"]?.Value) },

            // Trade
            { "get_active_traders", args => TradeTools.GetActiveTraders() },
            { "analyze_trade_opportunity", args => TradeTools.AnalyzeTradeOpportunity(args?["traderFilter"]?.Value) },
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
