using System;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class EnvironmentTools
    {
        public static string GetEnvironmentQuality()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var rooms = new JSONArray();

            var allRooms = map.regionGrid.allRooms
                .Where(r => !r.PsychologicallyOutdoors && !r.TouchesMapEdge)
                .ToList();

            foreach (var room in allRooms)
            {
                var roomObj = new JSONObject();

                // Basic info
                roomObj["cellCount"] = room.CellCount;

                // Get room role
                var roomRole = room.Role;
                if (roomRole != null)
                    roomObj["role"] = roomRole.label;

                // Get room stats
                float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
                float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
                float beauty = room.GetStat(RoomStatDefOf.Beauty);
                float wealth = room.GetStat(RoomStatDefOf.Wealth);
                float space = room.GetStat(RoomStatDefOf.Space);

                roomObj["impressiveness"] = impressiveness.ToString("0.0");
                roomObj["cleanliness"] = cleanliness.ToString("0.0");
                roomObj["beauty"] = beauty.ToString("0.0");
                roomObj["wealth"] = wealth.ToString("0.0");
                roomObj["space"] = space.ToString("0.0");

                // Score each dimension (0-100)
                int impressivenessScore = ScoreImpressiveness(impressiveness);
                int cleanlinessScore = ScoreCleanliness(cleanliness);
                int beautyScore = ScoreBeauty(beauty);
                int spaceScore = ScoreSpace(space);

                roomObj["impressivenessScore"] = impressivenessScore;
                roomObj["cleanlinessScore"] = cleanlinessScore;
                roomObj["beautyScore"] = beautyScore;
                roomObj["spaceScore"] = spaceScore;

                // Overall score (weighted average)
                int overallScore = (int)((impressivenessScore * 0.4) + 
                                        (cleanlinessScore * 0.2) + 
                                        (beautyScore * 0.2) + 
                                        (spaceScore * 0.2));
                roomObj["overallScore"] = overallScore;

                // Quality grade
                string grade = "Poor";
                if (overallScore >= 80) grade = "Excellent";
                else if (overallScore >= 60) grade = "Good";
                else if (overallScore >= 40) grade = "Mediocre";
                else if (overallScore >= 20) grade = "Poor";
                else grade = "Awful";

                roomObj["grade"] = grade;

                // Identify issues and suggestions
                var issues = new JSONArray();
                var suggestions = new JSONArray();

                if (impressivenessScore < 40)
                {
                    issues.Add("Low impressiveness (" + impressiveness.ToString("0.0") + ")");
                    suggestions.Add("Improve overall room quality (better furniture, decorations, flooring)");
                }

                if (cleanlinessScore < 40)
                {
                    issues.Add("Poor cleanliness (" + cleanliness.ToString("0.0") + ")");
                    suggestions.Add("Assign colonists to cleaning duty");
                    suggestions.Add("Install sterile tiles for clinical environments");
                    suggestions.Add("Remove dirt and debris");
                }

                if (beautyScore < 40)
                {
                    issues.Add("Low beauty (" + beauty.ToString("0.0") + ")");
                    suggestions.Add("Add sculptures or art");
                    suggestions.Add("Plant flowers or decorative plants");
                    suggestions.Add("Replace rough stone floors with tiles");
                    suggestions.Add("Use better quality furniture");
                }

                if (spaceScore < 40)
                {
                    issues.Add("Cramped space (" + space.ToString("0.0") + ")");
                    suggestions.Add("Expand the room if possible");
                    suggestions.Add("Remove unnecessary furniture");
                    suggestions.Add("For bedrooms, aim for 20+ cells");
                }

                // Check temperature
                float temp = room.Temperature;
                bool tempComfortable = temp >= 16f && temp <= 26f;
                roomObj["temperature"] = temp.ToString("0.0") + "°C";
                roomObj["temperatureComfortable"] = tempComfortable;

                if (!tempComfortable)
                {
                    if (temp < 16f)
                    {
                        issues.Add("Too cold (" + temp.ToString("0.0") + "°C)");
                        suggestions.Add("Install heaters");
                        suggestions.Add("Seal gaps and close doors");
                    }
                    else
                    {
                        issues.Add("Too hot (" + temp.ToString("0.0") + "°C)");
                        suggestions.Add("Install coolers");
                        suggestions.Add("Ensure coolers are powered");
                    }
                }

                // Check lighting
                bool wellLit = false;
                int litCells = 0;
                foreach (var cell in room.Cells)
                {
                    float light = map.glowGrid.GameGlowAt(cell, false);
                    if (light >= 0.3f) litCells++;
                }
                float litPercentage = (float)litCells / room.CellCount;
                wellLit = litPercentage >= 0.8f;

                roomObj["wellLit"] = wellLit;

                if (!wellLit)
                {
                    issues.Add("Poor lighting (" + (litPercentage * 100).ToString("0") + "% lit)");
                    suggestions.Add("Add standing lamps or ceiling lights");
                    suggestions.Add("Ensure lights are powered");
                }

                if (issues.Count > 0)
                    roomObj["issues"] = issues;
                if (suggestions.Count > 0)
                    roomObj["suggestions"] = suggestions;

                // Find assigned pawns (for bedrooms)
                var assignedPawns = new JSONArray();
                foreach (var bed in room.ContainedBeds)
                {
                    foreach (var owner in bed.OwnersForReading)
                    {
                        assignedPawns.Add(owner.Name?.ToStringShort ?? "Unknown");
                    }
                }
                if (assignedPawns.Count > 0)
                    roomObj["assignedTo"] = assignedPawns;

                rooms.Add(roomObj);
            }

            result["rooms"] = rooms;
            result["totalRooms"] = rooms.Count;

            // Summary of problem rooms
            var problemRooms = new JSONArray();
            foreach (JSONObject room in rooms)
            {
                int score = room["overallScore"].AsInt;
                if (score < 40 && room["assignedTo"] != null)
                {
                    var summary = new JSONObject();
                    summary["assignedTo"] = room["assignedTo"];
                    summary["score"] = score;
                    summary["grade"] = room["grade"].Value;
                    summary["mainIssue"] = room["issues"]?[0]?.Value ?? "Unknown";
                    problemRooms.Add(summary);
                }
            }

            if (problemRooms.Count > 0)
                result["problemRooms"] = problemRooms;

            return result.ToString();
        }

        private static int ScoreImpressiveness(float impressiveness)
        {
            // Impressiveness ranges: awful<0, dull<20, mediocre<40, interesting<65, impressive<85, very impressive<120, unbelievably impressive 120+
            if (impressiveness < 0) return 0;
            if (impressiveness < 20) return 20;
            if (impressiveness < 40) return 40;
            if (impressiveness < 65) return 60;
            if (impressiveness < 85) return 75;
            if (impressiveness < 120) return 90;
            return 100;
        }

        private static int ScoreCleanliness(float cleanliness)
        {
            // Cleanliness: very dirty<-2, dirty<-0.4, slightly dirty<0, clean<0.4, very clean 0.4+
            if (cleanliness < -2) return 0;
            if (cleanliness < -0.4) return 25;
            if (cleanliness < 0) return 50;
            if (cleanliness < 0.4) return 75;
            return 100;
        }

        private static int ScoreBeauty(float beauty)
        {
            // Beauty: ugly<-5, not nice<0, pleasant<5, beautiful<15, very beautiful 15+
            if (beauty < -5) return 0;
            if (beauty < 0) return 30;
            if (beauty < 5) return 60;
            if (beauty < 15) return 80;
            return 100;
        }

        private static int ScoreSpace(float space)
        {
            // Space: cramped<12, rather cramped<29, average<55, rather spacious<70, very spacious 70+
            if (space < 12) return 20;
            if (space < 29) return 40;
            if (space < 55) return 60;
            if (space < 70) return 80;
            return 100;
        }
    }
}
