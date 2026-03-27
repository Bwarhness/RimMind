using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class OutfitTools
    {
        /// <summary>
        /// List all outfit (apparel policy) definitions and which colonists use them.
        /// </summary>
        public static string ListOutfits()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var outfitsArray = new JSONArray();

            // Get all outfits from the database
            var allOutfits = Current.Game.outfitDatabase.AllOutfits.ToList();

            // Build outfit info by checking each pawn's current outfit
            var outfitData = new Dictionary<string, JSONArray>();
            var outfitNames = new List<string>();

            foreach (var outfit in allOutfits)
            {
                outfitNames.Add(outfit.label);
                outfitData[outfit.label] = new JSONArray();
            }

            // Get colonists and group by their current outfit
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.outfits == null) continue;

                var outfit = pawn.outfits.CurrentApparelPolicy;
                if (outfit == null) continue;

                var outfitName = outfit.label;
                if (!outfitData.ContainsKey(outfitName))
                {
                    outfitData[outfitName] = new JSONArray();
                }
                outfitData[outfitName].Add(pawn.Name?.ToStringShort ?? "Unknown");
            }

            // Build result
            foreach (var outfit in allOutfits)
            {
                var outfitObj = new JSONObject();
                outfitObj["name"] = outfit.label;
                outfitObj["assignedColonists"] = outfitData[outfit.label];
                outfitObj["colonistCount"] = outfitData[outfit.label].Count;
                outfitsArray.Add(outfitObj);
            }

            result["outfits"] = outfitsArray;
            result["totalOutfits"] = outfitsArray.Count;
            result["totalColonists"] = map.mapPawns.FreeColonists.Count();

            return result.ToString();
        }
    }
}
