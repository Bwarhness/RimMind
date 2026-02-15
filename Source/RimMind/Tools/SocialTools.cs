using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class SocialTools
    {
        public static string GetRelationships(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = ColonistTools.FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

            var relations = new JSONArray();
            var map = Find.CurrentMap;
            if (map != null && pawn.relations != null)
            {
                foreach (var other in map.mapPawns.FreeColonists)
                {
                    if (other == pawn) continue;

                    var rel = new JSONObject();
                    rel["name"] = other.Name?.ToStringShort ?? "Unknown";
                    rel["opinionOfThem"] = pawn.relations.OpinionOf(other);
                    rel["theirOpinionOfMe"] = other.relations.OpinionOf(pawn);

                    // Get direct relations
                    var directRelations = pawn.relations.DirectRelations
                        .Where(r => r.otherPawn == other)
                        .Select(r => r.def.label)
                        .ToList();

                    if (directRelations.Count > 0)
                    {
                        var relTypes = new JSONArray();
                        foreach (var r in directRelations) relTypes.Add(r);
                        rel["relationTypes"] = relTypes;
                    }

                    relations.Add(rel);
                }
            }

            obj["relationships"] = relations;
            return obj.ToString();
        }

        public static string GetFactionRelations()
        {
            var arr = new JSONArray();

            foreach (var faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (faction.IsPlayer) continue;

                var obj = new JSONObject();
                obj["name"] = faction.Name;
                obj["type"] = faction.def.label;
                obj["goodwill"] = faction.PlayerGoodwill;

                if (faction.HostileTo(Faction.OfPlayer))
                    obj["status"] = "Hostile";
                else if (faction.PlayerGoodwill >= 75)
                    obj["status"] = "Ally";
                else
                    obj["status"] = "Neutral";

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["factions"] = arr;
            return result.ToString();
        }
    }
}
