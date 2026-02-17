using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMind.Tools
{
    public static class TradeTools
    {
        public static string GetActiveTraders()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var traders = new JSONArray();

            // 1. Orbital trade ships (via comms console)
            if (map.passingShipManager != null)
            {
                foreach (var ship in map.passingShipManager.passingShips)
                {
                    var traderObj = new JSONObject();
                    traderObj["traderKind"] = "orbital_ship";
                    traderObj["name"] = ship.name ?? "Unknown Ship";
                    traderObj["faction"] = ship.Faction?.Name ?? "Independent";
                    traderObj["factionRelation"] = ship.Faction?.PlayerRelationKind.ToString() ?? "Neutral";
                    
                    // Time until departure
                    int ticksLeft = ship.ticksUntilDeparture;
                    traderObj["departureInHours"] = (ticksLeft / 2500.0f).ToString("F1");
                    traderObj["departureInTicks"] = ticksLeft;

                    // Get items in stock
                    var items = GetTraderItems(ship);
                    traderObj["items"] = items["items"];
                    traderObj["itemCount"] = items["count"];
                    traderObj["silverAvailable"] = GetTraderSilver(ship);

                    traders.Add(traderObj);
                }
            }

            // 2. Visiting caravans on map
            var caravans = map.mapPawns.AllPawnsSpawned
                .Where(p => p.trader != null && p.Faction != Faction.OfPlayer)
                .Select(p => p.trader)
                .Distinct()
                .ToList();

            foreach (var trader in caravans)
            {
                var traderObj = new JSONObject();
                traderObj["traderKind"] = "caravan";
                traderObj["name"] = trader.TraderName;
                traderObj["faction"] = trader.Faction?.Name ?? "Unknown";
                traderObj["factionRelation"] = trader.Faction?.PlayerRelationKind.ToString() ?? "Neutral";

                // Estimate departure time (caravans typically stay 1-2 days)
                var pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.trader == trader);
                if (pawn?.mindState?.duty != null)
                {
                    var lord = pawn.GetLord();
                    if (lord != null && lord.CurLordToil != null)
                    {
                        traderObj["status"] = lord.CurLordToil.ToString();
                    }
                }
                traderObj["departureNote"] = "Caravans typically stay 1-2 days before leaving";

                // Get items
                var items = GetTraderItems(trader);
                traderObj["items"] = items["items"];
                traderObj["itemCount"] = items["count"];
                traderObj["silverAvailable"] = GetTraderSilver(trader);

                traders.Add(traderObj);
            }

            // 3. Allied settlements in comms range (can call for trade caravans)
            if (map.listerBuildings != null)
            {
                var commsConsoles = map.listerBuildings.AllBuildingsColonistOfClass<Building_CommsConsole>()
                    .Where(c => c.PowerOn)
                    .ToList();

                if (commsConsoles.Any())
                {
                    var settlements = Find.WorldObjects.Settlements
                        .Where(s => s.Faction != null && 
                                    s.Faction != Faction.OfPlayer &&
                                    s.Faction.PlayerRelationKind == FactionRelationKind.Ally)
                        .ToList();

                    foreach (var settlement in settlements.Take(10)) // Limit to avoid spam
                    {
                        var settObj = new JSONObject();
                        settObj["traderKind"] = "allied_settlement";
                        settObj["name"] = settlement.LabelCap.ToString();
                        settObj["faction"] = settlement.Faction.Name;
                        settObj["factionRelation"] = "Ally";
                        settObj["canCallTrader"] = true;
                        
                        var playerHome = Find.AnyPlayerHomeMap?.Tile ?? -1;
                        if (playerHome >= 0)
                        {
                            int distance = Find.WorldGrid.TraversalDistanceBetween(playerHome, settlement.Tile);
                            settObj["distance"] = distance;
                            settObj["travelDays"] = (distance / 5.0f).ToString("F1");
                        }

                        settObj["note"] = "Can request trade caravan via comms console (requires player action)";
                        settObj["items"] = "Unknown - send caravan request to see inventory";
                        
                        traders.Add(settObj);
                    }
                }
            }

            var result = new JSONObject();
            result["traders"] = traders;
            result["count"] = traders.Count;
            
            if (traders.Count == 0)
            {
                result["note"] = "No traders currently available. Orbital ships and caravans visit randomly. Allied settlements can send caravans if called via comms console.";
            }

            return result.ToString();
        }

        public static string AnalyzeTradeOpportunity(string traderFilter)
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Get active traders
            var tradersResult = JSONNode.Parse(GetActiveTraders());
            var traders = tradersResult["traders"].AsArray;

            if (traders == null || traders.Count == 0)
            {
                return ToolExecutor.JsonError("No traders currently available.");
            }

            // Filter traders if specified
            List<JSONNode> targetTraders = new List<JSONNode>();
            if (!string.IsNullOrEmpty(traderFilter))
            {
                string filterLower = traderFilter.ToLower();
                foreach (JSONNode trader in traders)
                {
                    string name = trader["name"]?.Value ?? "";
                    string faction = trader["faction"]?.Value ?? "";
                    string kind = trader["traderKind"]?.Value ?? "";
                    
                    if (name.ToLower().Contains(filterLower) || 
                        faction.ToLower().Contains(filterLower) ||
                        kind.ToLower().Contains(filterLower))
                    {
                        targetTraders.Add(trader);
                    }
                }
                
                if (targetTraders.Count == 0)
                {
                    return ToolExecutor.JsonError("No traders match filter: " + traderFilter);
                }
            }
            else
            {
                foreach (JSONNode trader in traders)
                {
                    targetTraders.Add(trader);
                }
            }

            // Get colony resources
            var resourcesResult = JSONNode.Parse(ColonyTools.GetResources("all"));
            
            // Analyze colony needs
            var needs = AnalyzeColonyNeeds(map, resourcesResult);
            
            // Generate trade suggestions for each trader
            var suggestions = new JSONArray();
            
            foreach (var trader in targetTraders)
            {
                var suggestion = new JSONObject();
                suggestion["trader"] = trader["name"];
                suggestion["traderKind"] = trader["traderKind"];
                suggestion["faction"] = trader["faction"];
                
                var recommendations = new JSONArray();
                
                // Skip allied settlements (can't analyze without inventory)
                if (trader["traderKind"]?.Value == "allied_settlement")
                {
                    suggestion["note"] = "Call for trade caravan to analyze inventory";
                    suggestions.Add(suggestion);
                    continue;
                }
                
                var items = trader["items"]?.AsArray;
                if (items == null || items.Count == 0)
                {
                    suggestion["note"] = "No items in trader inventory";
                    suggestions.Add(suggestion);
                    continue;
                }

                // Check for urgent needs
                foreach (var need in needs["urgent"].AsArray)
                {
                    string itemType = need["itemType"]?.Value;
                    string reason = need["reason"]?.Value;
                    
                    // Find matching items in trader inventory
                    foreach (JSONNode item in items)
                    {
                        string itemDef = item["defName"]?.Value ?? "";
                        string category = item["category"]?.Value ?? "";
                        
                        if (MatchesNeed(itemDef, category, itemType))
                        {
                            var rec = new JSONObject();
                            rec["action"] = "BUY";
                            rec["item"] = item["label"];
                            rec["defName"] = itemDef;
                            rec["quantity"] = item["quantity"];
                            rec["buyPrice"] = item["buyPrice"];
                            rec["priority"] = "URGENT";
                            rec["reason"] = reason;
                            rec["utilityScore"] = 100; // Urgent needs = max score
                            recommendations.Add(rec);
                        }
                    }
                }
                
                // Check for profitable sales (colony surplus)
                foreach (var surplus in needs["surplus"].AsArray)
                {
                    string itemDef = surplus["defName"]?.Value;
                    int amount = surplus["amount"]?.AsInt ?? 0;
                    
                    // Find if trader will buy this
                    foreach (JSONNode item in items)
                    {
                        if (item["defName"]?.Value == itemDef && item["sellPrice"] != null)
                        {
                            int sellPrice = item["sellPrice"].AsInt;
                            if (sellPrice > 0)
                            {
                                var rec = new JSONObject();
                                rec["action"] = "SELL";
                                rec["item"] = item["label"];
                                rec["defName"] = itemDef;
                                rec["suggestedQuantity"] = Math.Min(amount / 2, 50); // Sell half, keep reserve
                                rec["sellPrice"] = sellPrice;
                                rec["priority"] = "PROFIT";
                                rec["reason"] = "Colony has surplus (" + amount + "), generate silver";
                                rec["utilityScore"] = CalculateProfitScore(sellPrice, amount);
                                recommendations.Add(rec);
                            }
                        }
                    }
                }
                
                // Check for good deals (items trader wants to sell at reasonable price)
                foreach (JSONNode item in items)
                {
                    string itemDef = item["defName"]?.Value ?? "";
                    int buyPrice = item["buyPrice"]?.AsInt ?? 0;
                    
                    // High-value strategic items
                    if (IsStrategicItem(itemDef) && buyPrice > 0)
                    {
                        var rec = new JSONObject();
                        rec["action"] = "BUY";
                        rec["item"] = item["label"];
                        rec["defName"] = itemDef;
                        rec["quantity"] = item["quantity"];
                        rec["buyPrice"] = buyPrice;
                        rec["priority"] = "STRATEGIC";
                        rec["reason"] = "Valuable long-term investment";
                        rec["utilityScore"] = 70;
                        recommendations.Add(rec);
                    }
                }
                
                suggestion["recommendations"] = recommendations;
                suggestion["totalOpportunities"] = recommendations.Count;
                suggestions.Add(suggestion);
            }
            
            var result = new JSONObject();
            result["tradeAnalysis"] = suggestions;
            result["colonyNeeds"] = needs;
            result["silverAvailable"] = resourcesResult["materials"]?["silver"]?.AsInt ?? 0;
            
            return result.ToString();
        }

        // Helper methods
        private static JSONObject GetTraderItems(ITrader trader)
        {
            var items = new JSONArray();
            var result = new JSONObject();
            
            try
            {
                var tradeables = trader.Goods;
                if (tradeables == null)
                {
                    result["items"] = items;
                    result["count"] = 0;
                    return result;
                }

                foreach (var tradeable in tradeables.Take(100)) // Limit to avoid spam
                {
                    if (tradeable.CountHeldBy(Transactor.Trader) <= 0) continue;
                    
                    var itemObj = new JSONObject();
                    
                    // Get the first thing to determine details
                    var firstThing = tradeable.AnyThing;
                    if (firstThing != null)
                    {
                        itemObj["label"] = firstThing.LabelCap.ToString();
                        itemObj["defName"] = firstThing.def.defName;
                        itemObj["category"] = firstThing.def.category.ToString();
                        itemObj["quality"] = TryGetQuality(firstThing);
                        
                        if (firstThing.def.IsWeapon)
                            itemObj["type"] = "weapon";
                        else if (firstThing.def.IsApparel)
                            itemObj["type"] = "apparel";
                        else if (firstThing.def.IsIngestible)
                            itemObj["type"] = "food";
                        else if (firstThing.def.IsMedicine)
                            itemObj["type"] = "medicine";
                    }
                    else
                    {
                        itemObj["label"] = tradeable.Label;
                        itemObj["defName"] = "unknown";
                    }
                    
                    itemObj["quantity"] = tradeable.CountHeldBy(Transactor.Trader);
                    
                    // Get prices
                    itemObj["buyPrice"] = (int)tradeable.GetPriceFor(TradeAction.PlayerBuys);
                    itemObj["sellPrice"] = (int)tradeable.GetPriceFor(TradeAction.PlayerSells);
                    
                    items.Add(itemObj);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Error getting trader items: " + ex.Message);
            }
            
            result["items"] = items;
            result["count"] = items.Count;
            return result;
        }

        private static int GetTraderSilver(ITrader trader)
        {
            try
            {
                var silverTradeable = trader.Goods?.FirstOrDefault(t => 
                    t.AnyThing?.def == ThingDefOf.Silver);
                
                if (silverTradeable != null)
                {
                    return silverTradeable.CountHeldBy(Transactor.Trader);
                }
            }
            catch { }
            
            return 0;
        }

        private static string TryGetQuality(Thing thing)
        {
            if (thing.TryGetQuality(out QualityCategory qc))
            {
                return qc.ToString();
            }
            return "Normal";
        }

        private static JSONObject AnalyzeColonyNeeds(Map map, JSONNode resources)
        {
            var needs = new JSONObject();
            var urgent = new JSONArray();
            var surplus = new JSONArray();
            
            // Check medicine
            int totalMedicine = resources["medicine"]?["medicineTotalCount"]?.AsInt ?? 0;
            int colonistCount = map.mapPawns.FreeColonistsCount;
            
            if (totalMedicine < colonistCount * 5)
            {
                var need = new JSONObject();
                need["itemType"] = "medicine";
                need["reason"] = "Low medicine stocks (" + totalMedicine + " for " + colonistCount + " colonists)";
                need["severity"] = totalMedicine < colonistCount * 2 ? "CRITICAL" : "HIGH";
                urgent.Add(need);
            }
            
            // Check components
            int components = resources["materials"]?["components"]?.AsInt ?? 0;
            if (components < 10)
            {
                var need = new JSONObject();
                need["itemType"] = "components";
                need["reason"] = "Low component stocks (" + components + " available)";
                need["severity"] = components < 5 ? "CRITICAL" : "HIGH";
                urgent.Add(need);
            }
            
            // Check food (nutrition)
            float totalFood = float.Parse(resources["food"]?["totalNutrition"]?.Value ?? "0");
            float daysOfFood = totalFood / (colonistCount * 2); // ~2 nutrition per colonist per day
            
            if (daysOfFood < 5)
            {
                var need = new JSONObject();
                need["itemType"] = "food";
                need["reason"] = "Low food stocks (" + daysOfFood.ToString("F1") + " days remaining)";
                need["severity"] = daysOfFood < 2 ? "CRITICAL" : "HIGH";
                urgent.Add(need);
            }
            
            // Check for surplus materials to sell
            int steel = resources["materials"]?["steel"]?.AsInt ?? 0;
            if (steel > 1000)
            {
                var surp = new JSONObject();
                surp["defName"] = "Steel";
                surp["amount"] = steel;
                surp["reason"] = "Excess steel reserves";
                surplus.Add(surp);
            }
            
            int silver = resources["materials"]?["silver"]?.AsInt ?? 0;
            if (silver > 5000)
            {
                needs["silverNote"] = "Colony has good silver reserves (" + silver + ") for purchasing";
            }
            else if (silver < 500)
            {
                needs["silverNote"] = "Low silver reserves (" + silver + ") - selling items recommended";
            }
            
            needs["urgent"] = urgent;
            needs["surplus"] = surplus;
            return needs;
        }

        private static bool MatchesNeed(string defName, string category, string needType)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            
            string defLower = defName.ToLower();
            string catLower = category.ToLower();
            string needLower = needType.ToLower();
            
            if (needLower == "medicine")
            {
                return defLower.Contains("medicine") || catLower == "medicine";
            }
            if (needLower == "components")
            {
                return defLower.Contains("component");
            }
            if (needLower == "food")
            {
                return defLower.Contains("meal") || defLower.Contains("rice") || 
                       defLower.Contains("corn") || defLower.Contains("potato") ||
                       catLower == "food" || defLower.Contains("pemmican");
            }
            
            return false;
        }

        private static bool IsStrategicItem(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            
            string lower = defName.ToLower();
            
            // Advanced materials
            if (lower.Contains("plasteel") || lower.Contains("uranium") || 
                lower.Contains("advanced") || lower.Contains("spacer"))
                return true;
            
            // High-value items
            if (lower.Contains("gold") || lower.Contains("jade"))
                return true;
            
            // Important consumables
            if (lower.Contains("medicineultratech") || lower.Contains("component"))
                return true;
            
            return false;
        }

        private static int CalculateProfitScore(int sellPrice, int amount)
        {
            // Score based on total value that could be generated
            int totalValue = sellPrice * Math.Min(amount / 2, 50);
            
            if (totalValue > 1000) return 90;
            if (totalValue > 500) return 80;
            if (totalValue > 200) return 70;
            if (totalValue > 50) return 60;
            return 50;
        }
    }
}
