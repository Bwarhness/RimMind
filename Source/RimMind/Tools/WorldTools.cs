using System.Linq;
using RimMind.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMind.Tools
{
    public static class WorldTools
    {
        // Issue #35: Caravans
        public static string ListWorldDestinations()
        {
            if (Find.WorldObjects == null) return ToolExecutor.JsonError("World not accessible.");

            var arr = new JSONArray();
            var homeMap = Find.AnyPlayerHomeMap;
            int playerHome = homeMap != null ? (int)homeMap.Tile : -1;

            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction == null || settlement.Faction == Faction.OfPlayer)
                    continue;

                var obj = new JSONObject();
                obj["name"] = settlement.LabelCap.ToString();
                obj["faction"] = settlement.Faction.Name;
                obj["factionRelation"] = settlement.Faction.PlayerRelationKind.ToString();
                int tileId = (int)settlement.Tile;
                obj["tile"] = tileId;

                if (playerHome >= 0 && tileId >= 0)
                {
                    int distance = Find.WorldGrid.TraversalDistanceBetween(playerHome, tileId);
                    obj["distance"] = distance;
                    obj["travelDays"] = (distance / 5.0f).ToString("F1");
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["destinations"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetCaravanInfo()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();
            int availableColonists = map.mapPawns.FreeColonistsSpawned.Count();
            int availableAnimals = map.mapPawns.SpawnedColonyAnimals.Count();

            var obj = new JSONObject();
            obj["availableColonists"] = availableColonists;
            obj["availableAnimals"] = availableAnimals;
            obj["note"] = "Caravan formation requires manual player approval. AI can recommend but not execute.";

            var result = new JSONObject();
            result["caravanInfo"] = obj;
            return result.ToString();
        }

        // Issue #36: Trade
        public static string ListTraderInventory()
        {
            if (!TradeSession.Active)
                return ToolExecutor.JsonError("No active trade session. Wait for a trader to visit.");

            var trader = TradeSession.trader;
            if (trader == null) return ToolExecutor.JsonError("No trader in session.");

            var arr = new JSONArray();
            var negotiator = TradeSession.playerNegotiator;
            if (negotiator == null) return ToolExecutor.JsonError("No negotiator found for trade session.");

            var tradeableItems = trader.ColonyThingsWillingToBuy(negotiator);

            foreach (var thing in tradeableItems.Take(50))
            {
                var obj = new JSONObject();
                obj["name"] = thing.LabelCap.ToString();
                obj["category"] = thing.def.category.ToString();
                obj["stackCount"] = thing.stackCount;
                obj["note"] = "Use colony trade UI to execute trades.";

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["trader"] = trader.TraderName;
            result["faction"] = trader.Faction?.Name ?? "Unknown";
            result["inventory"] = arr;
            result["count"] = arr.Count;
            result["note"] = "Trading requires manual player execution via trade UI.";
            return result.ToString();
        }

        public static string GetTradeStatus()
        {
            if (TradeSession.Active)
            {
                var trader = TradeSession.trader;
                var result = new JSONObject();
                result["tradeActive"] = true;
                result["trader"] = trader?.TraderName ?? "Unknown";
                result["faction"] = trader?.Faction?.Name ?? "Unknown";
                result["note"] = "Trade session is open. Use list_trader_inventory to see items.";
                return result.ToString();
            }

            var result2 = new JSONObject();
            result2["tradeActive"] = false;
            result2["note"] = "No active trade session. Wait for a trader caravan to visit.";
            return result2.ToString();
        }

        // Issue #37: Diplomacy
        public static string GetDiplomacyOptions(string factionName)
        {
            if (string.IsNullOrEmpty(factionName))
                return ToolExecutor.JsonError("factionName parameter required.");

            string factionLower = factionName.ToLower();
            var faction = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f.Name.ToLower().Contains(factionLower) && !f.IsPlayer);

            if (faction == null)
            {
                var available = Find.FactionManager.AllFactionsListForReading
                    .Where(f => !f.IsPlayer && !f.Hidden)
                    .Select(f => f.Name)
                    .Take(10);
                return ToolExecutor.JsonError("Faction '" + factionName + "' not found. Available: " + string.Join(", ", available));
            }

            var obj = new JSONObject();
            obj["faction"] = faction.Name;
            obj["relationKind"] = faction.PlayerRelationKind.ToString();
            obj["goodwill"] = faction.PlayerGoodwill;

            var options = new JSONArray();
            
            if (faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                options.Add("Request military aid (requires comms console)");
                options.Add("Request trade caravan (requires comms console)");
            }
            
            if (faction.PlayerRelationKind != FactionRelationKind.Hostile)
            {
                options.Add("Send gift to improve relations");
            }

            obj["availableActions"] = options;
            obj["note"] = "Diplomatic actions require comms console and manual player execution.";

            var result = new JSONObject();
            result["diplomacy"] = obj;
            return result.ToString();
        }

        public static string ListFactions()
        {
            var arr = new JSONArray();

            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.IsPlayer || faction.Hidden) continue;

                var obj = new JSONObject();
                obj["name"] = faction.Name;
                obj["relationKind"] = faction.PlayerRelationKind.ToString();
                obj["goodwill"] = faction.PlayerGoodwill;
                obj["defeated"] = faction.defeated;

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["factions"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetDiplomaticSummary()
        {
            var allies = 0;
            var neutral = 0;
            var hostile = 0;

            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.IsPlayer || faction.Hidden) continue;

                switch (faction.PlayerRelationKind)
                {
                    case FactionRelationKind.Ally: allies++; break;
                    case FactionRelationKind.Neutral: neutral++; break;
                    case FactionRelationKind.Hostile: hostile++; break;
                }
            }

            var result = new JSONObject();
            result["allies"] = allies;
            result["neutral"] = neutral;
            result["hostile"] = hostile;
            result["total"] = allies + neutral + hostile;
            return result.ToString();
        }
    }
}
