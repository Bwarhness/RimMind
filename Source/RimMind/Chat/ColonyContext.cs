using RimWorld;
using Verse;

namespace RimMind.Chat
{
    public static class ColonyContext
    {
        public static string GetLightweightContext()
        {
            var map = Find.CurrentMap;
            if (map == null) return "No active colony map.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Colony: " + map.mapPawns.FreeColonistsCount + " colonists");
            sb.AppendLine("Day " + GenDate.DaysPassed + ", " + GenLocalDate.Season(map).LabelCap().ToString());
            sb.AppendLine("Biome: " + map.Biome.LabelCap.ToString());
            sb.AppendLine("Weather: " + (map.weatherManager.curWeather?.LabelCap.ToString() ?? "Unknown"));

            return sb.ToString();
        }
    }
}
