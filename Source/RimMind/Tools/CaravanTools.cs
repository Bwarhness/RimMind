using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using RimWorld.Planet;
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
            var caravan = Find.WorldObjects.Caravans
                .FirstOrDefault(c => c.Faction == Faction.OfPlayer);

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
            float overloadPercent = totalCapacity > 0 ? currentMass / totalCapacity : 0f;
            result["loadPercentage"] = overloadPercent.ToString("P0");
            result["isOverloaded"] = overloadPercent > 1.0f;

            // Travel speed impact
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
                    destTile = (int)settlement.Tile;
            }

            if (destTile < 0)
                return ToolExecutor.JsonError("Could not find destination. Provide tile ID or settlement name.");

            int currentTile = (int)map.Tile;
            
            // Calculate distance
            int distance = Find.WorldGrid.TraversalDistanceBetween(currentTile, destTile);
            
            var result = new JSONObject();
            result["startingTile"] = currentTile;
            result["destinationTile"] = destTile;
            result["distance"] = distance.ToString();

            // Biome info not available in RimWorld 1.6 via WorldGrid indexer
            var biomesCrossed = new JSONArray();
            result["biomesCrossed"] = biomesCrossed;

            // Estimate travel time (in days): roughly distance / 5 tiles per day
            float daysEstimate = distance / 5f;
            result["estimatedDays"] = daysEstimate.ToString("0.0");
            result["estimatedHours"] = (daysEstimate * 24f).ToString("0");

            // Encounter risk based on distance
            float encounterProbability = Math.Min(distance / 100f, 0.8f);
            result["encounterProbability"] = encounterProbability.ToString("P0");

            // Ambush risk
            float ambushRisk = encounterProbability * 0.5f;
            result["ambushRisk"] = ambushRisk.ToString("P0");

            // Season/weather effects
            var season = GenLocalDate.Season(map);
            result["currentSeason"] = season.LabelCap().ToString();

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
                destTile = (int)settlement.Tile;
                result["destinationTile"] = destTile;
            }

            // Calculate distance
            if (destTile >= 0)
            {
                int distance = Find.WorldGrid.TraversalDistanceBetween((int)map.Tile, destTile);
                result["distance"] = distance.ToString();

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
                if (!pawn.Drafted && !pawn.Downed && pawn.health != null)
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

            int currentTile = (int)map.Tile;
            
            // Collect settlements with their distances for sorting
            var settlementList = new List<KeyValuePair<int, JSONObject>>();

            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction == null || settlement.Faction == Faction.OfPlayer)
                    continue;

                int distance = Find.WorldGrid.TraversalDistanceBetween(currentTile, (int)settlement.Tile);
                if (distance > maxDistanceTiles)
                    continue;

                var settlementInfo = new JSONObject();
                settlementInfo["name"] = settlement.Name?.ToString() ?? "Unknown";
                settlementInfo["faction"] = settlement.Faction.Name?.ToString() ?? "Unknown";
                settlementInfo["tile"] = (int)settlement.Tile;
                settlementInfo["distance"] = distance.ToString();

                // Get faction relations using the working RimWorld 1.6 API
                settlementInfo["goodwill"] = settlement.Faction.PlayerGoodwill.ToString();
                settlementInfo["relationKind"] = settlement.Faction.PlayerRelationKind.ToString();
                settlementInfo["hostile"] = settlement.Faction.PlayerRelationKind == FactionRelationKind.Hostile;

                // Trade options
                var tradeOptions = new JSONArray();
                tradeOptions.Add("buyer"); // Can sell to them
                tradeOptions.Add("seller"); // Can buy from them
                settlementInfo["tradeOptions"] = tradeOptions;

                settlementList.Add(new KeyValuePair<int, JSONObject>(distance, settlementInfo));
            }

            // Sort by distance
            settlementList.Sort((a, b) => a.Key.CompareTo(b.Key));

            var settlements = new JSONArray();
            foreach (var kvp in settlementList)
                settlements.Add(kvp.Value);

            result["settlements"] = settlements;
            result["count"] = settlements.Count;

            return result.ToString();
        }
    }
}
