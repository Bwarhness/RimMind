using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMind.Tools
{
    public static class CaravanTools
    {
        /// <summary>
        /// Calculate caravan carrying capacity and current load
        /// Parameters: caravan_id (optional, defaults to selected or forming caravan)
        /// Returns: Total capacity, current mass, overload %, travel speed impact, recommendations
        /// </summary>
        public static string AnalyzeCaravanCapacity(string caravanId = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Find the caravan
            var caravan = Find.WorldObjects.PlayerCaravans
                .FirstOrDefault(c => c.IsPlayerOwned);

            if (caravan == null)
                return ToolExecutor.JsonError("No active caravan found. Form a caravan first.");

            var result = new JSONObject();

            // Calculate total carrying capacity
            float totalCapacity = 0;
            var packAnimals = new JSONArray();
            var colonists = new JSONArray();

            foreach (var pawn in caravan.PawnsListForReading)
            {
                if (pawn?.RaceProps?.packAnimal == true)
                {
                    var animal = new JSONObject();
                    animal["name"] = pawn.Name?.ToStringShort ?? pawn.def.label;
                    animal["carryCapacity"] = pawn.GetStatValue(StatDefOf.CarryingCapacity).ToString("0");
                    animal["currentInventory"] = pawn.inventory.innerContainer.Count.ToString();
                    totalCapacity += pawn.GetStatValue(StatDefOf.CarryingCapacity);
                    packAnimals.Add(animal);
                }
                else if (pawn?.IsColonist == true)
                {
                    var colonist = new JSONObject();
                    colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    colonist["carryCapacity"] = pawn.GetStatValue(StatDefOf.CarryingCapacity).ToString("0");
                    totalCapacity += pawn.GetStatValue(StatDefOf.CarryingCapacity);
                    colonists.Add(colonist);
                }
            }

            result["totalCapacity"] = totalCapacity.ToString("0.0");
            result["packAnimals"] = packAnimals;
            result["colonists"] = colonists;

            // Calculate current mass
            float currentMass = 0;
            var items = new JSONArray();

            foreach (var pawn in caravan.PawnsListForReading)
            {
                foreach (var thing in pawn.inventory.innerContainer)
                {
                    currentMass += thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
                    
                    var item = new JSONObject();
                    item["defName"] = thing.def.defName;
                    item["stackCount"] = thing.stackCount;
                    item["mass"] = thing.GetStatValue(StatDefOf.Mass).ToString("0.00");
                    item["totalMass"] = (thing.GetStatValue(StatDefOf.Mass) * thing.stackCount).ToString("0.00");
                    items.Add(item);
                }
            }

            result["currentMass"] = currentMass.ToString("0.0");
            result["items"] = items;

            // Calculate overload
            float overloadPercent = currentMass / totalCapacity;
            result["loadPercentage"] = overloadPercent.ToString("P0");
            result["isOverloaded"] = overloadPercent > 1.0f;

            // Travel speed impact
            float baseSpeed = 1.0f; // Base movement speed
            float speedMultiplier = 1.0f;
            if (overloadPercent > 1.0f)
            {
                // Overloaded: 50% speed reduction per 10% overload
                speedMultiplier = Math.Max(0.1f, 1.0f - ((overloadPercent - 1.0f) * 5.0f));
            }
            else if (overloadPercent > 0.8f)
            {
                // Near capacity: gradual slowdown
                speedMultiplier = 0.8f + (0.2f * (1.0f - overloadPercent));
            }

            result["travelSpeedMultiplier"] = speedMultiplier.ToString("P0");
            result["travelSpeedImpact"] = speedMultiplier < 0.8f ? "significant" : (speedMultiplier < 1.0f ? "moderate" : "none");

            // Recommendations
            var recommendations = new JSONArray();
            if (overloadPercent > 1.0f)
                recommendations.Add($"Remove {currentMass - totalCapacity:0} mass of items to avoid overload");
            if (packAnimals.Count == 0)
                recommendations.Add("Add pack animals to increase capacity");
            
            result["recommendations"] = recommendations;

            return result.ToString();
        }

        /// <summary>
        /// Predict travel time and encounter risks for a route
        /// Parameters: destination_tile, destination_name, caravan_id (optional)
        /// Returns: Est. travel time, speed, biomes, encounter probability, ambush risk
        /// </summary>
        public static string PredictCaravanTravel(string destinationTile = null, string destinationName = null, string caravanId = null)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Find destination
            int destTile = -1;
            if (destinationTile != null)
                int.TryParse(destinationTile, out destTile);
            else if (destinationName != null)
            {
                var settlement = Find.WorldObjects.Settlements
                    .FirstOrDefault(s => s.Name?.ToString().Contains(destinationName) == true);
                if (settlement != null)
                    destTile = settlement.Tile;
            }

            if (destTile < 0)
                return ToolExecutor.JsonError("Could not find destination. Provide tile ID or settlement name.");

            var currentTile = map.Tile;
            
            // Calculate distance
            var distance = Find.WorldGrid.ApproxDistanceInTiles(currentTile, destTile);
            
            var result = new JSONObject();
            result["startingTile"] = currentTile;
            result["destinationTile"] = destTile;
            result["distance"] = distance.ToString("0.0");

            // Biome analysis
            var path = RimWorld.WorldPathFinder.Get().FindPath(currentTile, destTile, null);
            
            var biomesCrossed = new JSONArray();
            var totalPathCost = 0f;
            
            if (path.Nodes.Count > 0)
            {
                foreach (var tile in path.Nodes)
                {
                    var biome = Find.WorldGrid[tile].biome;
                    var biomeInfo = new JSONObject();
                    biomeInfo["tile"] = tile;
                    biomeInfo["biome"] = biome.defName;
                    biomeInfo["walkCost"] = biome.pathCost_spring.ToString();
                    biomesCrossed.Add(biomeInfo);
                    
                    totalPathCost += biome.pathCost_spring;
                }
            }
            
            result["biomesCrossed"] = biomesCrossed;
            result["totalPathCost"] = totalPathCost.ToString("0");

            // Estimate travel time
            // Base: tiles / (movement speed * day length)
            float avgSpeed = 1.0f; // Base pawn speed
            float baseTimePerTile = 1.0f; // 1 hour per tile base
            float adjustedTime = baseTimePerTile * (totalPathCost / 100f); // Adjust for terrain
            
            float daysEstimate = (distance * adjustedTime) / 24f; // 24 hours per day
            result["estimatedDays"] = daysEstimate.ToString("0.0");
            result["estimatedHours"] = (daysEstimate * 24).ToString("0");

            // Encounter risk based on distance and terrain
            float encounterProbability = Math.Min(distance / 100f, 0.8f);
            result["encounterProbability"] = encounterProbability.ToString("P0");

            // Ambush risk (more likely on certain biomes)
            float ambushRisk = encounterProbability * 0.5f;
            result["ambushRisk"] = ambushRisk.ToString("P0");

            // Season/weather effects
            var season = GenLocalDate.Season(map);
            result["currentSeason"] = season?.defName ?? "Unknown";

            return result.ToString();
        }

        /// <summary>
        /// Suggest optimal caravan composition for a destination
        /// Parameters: destination, purpose (trade/raid/rescue), max_colonists (optional)
        /// Returns: Recommended colonists, pack animals, supplies, mass budget
        /// </summary>
        public static string OptimizeCaravanComposition(string destination, string purpose = "trade", int maxColonists = 0)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            result["purpose"] = purpose;
            result["destination"] = destination;

            // Find destination
            int destTile = -1;
            var settlement = Find.WorldObjects.Settlements
                .FirstOrDefault(s => s.Name?.ToString().Contains(destination) == true);
            if (settlement != null)
            {
                destTile = settlement.Tile;
                result["destinationTile"] = destTile;
            }

            // Calculate distance
            if (destTile >= 0)
            {
                var distance = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, destTile);
                result["distance"] = distance.ToString("0.0");

                // Calculate recommended mass based on purpose
                float recommendedMass;
                int recommendedColonists;
                int recommendedPackAnimals;

                switch (purpose.ToLower())
                {
                    case "trade":
                        recommendedMass = Math.Min(distance * 10f, 500f);
                        recommendedColonists = Math.Min(Math.Max(1, (int)(distance / 20f)), 6);
                        recommendedPackAnimals = Math.Min(Math.Max(0, (int)(recommendedMass / 80f)), 6);
                        break;
                    case "rescue":
                        recommendedMass = 50f; // Light for speed
                        recommendedColonists = Math.Min(Math.Max(2, (int)(distance / 15f)), 4);
                        recommendedPackAnimals = 0;
                        break;
                    case "raid":
                        recommendedMass = 30f;
                        recommendedColonists = Math.Min(Math.Max(3, (int)(distance / 10f)), 8);
                        recommendedPackAnimals = 0;
                        break;
                    default:
                        recommendedMass = 200f;
                        recommendedColonists = 2;
                        recommendedPackAnimals = 2;
                        break;
                }

                if (maxColonists > 0)
                    recommendedColonists = Math.Min(recommendedColonists, maxColonists);

                result["recommendedColonists"] = recommendedColonists;
                result["recommendedPackAnimals"] = recommendedPackAnimals;
                result["recommendedMass"] = recommendedMass.ToString("0.0");

                // Supplies recommendation
                var supplies = new JSONObject();
                int daysEstimate = (int)(distance / 5f) + 5; // Est. days + buffer
                
                // Food: ~2 meals per day per person
                int foodPerDay = recommendedColonists * 2 + recommendedPackAnimals;
                supplies["meals"] = (foodPerDay * daysEstimate).ToString();
                supplies["estimatedDays"] = daysEstimate;

                // Medicine for rescue/raid
                if (purpose == "rescue" || purpose == "raid")
                {
                    supplies["medicine"] = (Math.Max(1, recommendedColonists / 2)).ToString();
                }

                // Weapons
                if (purpose == "raid")
                {
                    supplies["weapons"] = recommendedColonists.ToString();
                }

                result["supplies"] = supplies;
            }

            // Get available colonists
            var availableColonists = new JSONArray();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (!pawn.Drafted && !pawn.Downed && pawn.health?.summaryHealth > 0.5f)
                {
                    var colonist = new JSONObject();
                    colonist["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                    colonist["skills"] = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level.ToString() ?? "N/A";
                    colonist["combat"] = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level.ToString() ?? "N/A";
                    availableColonists.Add(colonist);
                }
            }
            result["availableColonists"] = availableColonists;

            // Get available pack animals
            var availablePackAnimals = new JSONArray();
            foreach (var pawn in map.mapPawns.AllPawns)
            {
                if (pawn?.RaceProps?.packAnimal == true && pawn.Faction == Faction.OfPlayer && !pawn.Dead)
                {
                    var animal = new JSONObject();
                    animal["name"] = pawn.Name?.ToStringShort ?? pawn.def.label;
                    animal["carryCapacity"] = pawn.GetStatValue(StatDefOf.CarryingCapacity).ToString("0");
                    availablePackAnimals.Add(animal);
                }
            }
            result["availablePackAnimals"] = availablePackAnimals;

            return result.ToString();
        }

        /// <summary>
        /// Get detailed info on nearby tradeable settlements
        /// Parameters: max_distance_tiles (optional, default 20)
        /// Returns: Settlement name, faction, tile, distance, trade inventory, relations
        /// </summary>
        public static string GetTradeSettlementInfo(int maxDistanceTiles = 20)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            result["searchRadius"] = maxDistanceTiles;

            var currentTile = map.Tile;
            var settlements = new JSONArray();

            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction == null || settlement.Faction == Faction.OfPlayer)
                    continue;

                var distance = Find.WorldGrid.ApproxDistanceInTiles(currentTile, settlement.Tile);
                if (distance > maxDistanceTiles)
                    continue;

                var settlementInfo = new JSONObject();
                settlementInfo["name"] = settlement.Name?.ToString() ?? "Unknown";
                settlementInfo["faction"] = settlement.Faction.Name?.ToString() ?? "Unknown";
                settlementInfo["tile"] = settlement.Tile;
                settlementInfo["distance"] = distance.ToString("0.0");

                // Get faction relations
                var playerFaction = Faction.OfPlayer;
                if (playerFaction != null && settlement.Faction != null)
                {
                    var relations = settlement.Faction.GetRelation(playerFaction);
                    settlementInfo["goodwill"] = relations?.Goodwill.ToString() ?? "N/A";
                    settlementInfo["hostile"] = relations?.Hostile ?? false;
                }

                // Trade options (simplified)
                var tradeOptions = new JSONArray();
                if (settlement.Faction.def?.baseGoodwill != null)
                {
                    // Add potential trade types
                    tradeOptions.Add("buyer"); // Can sell to them
                    tradeOptions.Add("seller"); // Can buy from them
                }
                settlementInfo["tradeOptions"] = tradeOptions;

                settlements.Add(settlementInfo);
            }

            // Sort by distance
            var sortedSettlements = settlements.OrderBy(s => float.Parse(s["distance"].Value)).ToList();
            result["settlements"] = sortedSettlements;
            result["count"] = sortedSettlements.Count;

            return result.ToString();
        }
    }
}
