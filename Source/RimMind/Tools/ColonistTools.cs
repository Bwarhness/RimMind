using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class ColonistTools
    {
        public static string ListColonists()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            var arr = new JSONArray();

            foreach (var pawn in colonists)
            {
                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                obj["mood"] = pawn.needs?.mood?.CurLevelPercentage.ToString("P0") ?? "N/A";
                obj["currentJob"] = pawn.CurJobDef?.reportString ?? pawn.CurJobDef?.defName ?? "Idle";

                if (pawn.MentalStateDef != null)
                    obj["mentalState"] = pawn.MentalStateDef.label;

                if (pawn.Downed)
                    obj["status"] = "Downed";
                else if (pawn.InMentalState)
                    obj["status"] = "Mental break";

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["colonists"] = arr;
            result["count"] = colonists.Count();
            return result.ToString();
        }

        public static string GetColonistDetails(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringFull ?? "Unknown";
            obj["nickname"] = pawn.Name?.ToStringShort ?? "Unknown";
            obj["age"] = pawn.ageTracker.AgeBiologicalYears;
            obj["gender"] = pawn.gender.ToString();

            // Backstory
            if (pawn.story != null)
            {
                obj["childhood"] = pawn.story.Childhood?.TitleFor(pawn.gender).ToString() ?? "Unknown";
                obj["adulthood"] = pawn.story.Adulthood?.TitleFor(pawn.gender).ToString() ?? "None";
            }

            // Traits
            if (pawn.story?.traits != null)
            {
                var traits = new JSONArray();
                foreach (var trait in pawn.story.traits.allTraits)
                    traits.Add(trait.LabelCap.ToString());
                obj["traits"] = traits;
            }

            // Skills
            if (pawn.skills != null)
            {
                var skills = new JSONObject();
                foreach (var skill in pawn.skills.skills)
                {
                    string passion = skill.passion == Passion.None ? "" : skill.passion == Passion.Minor ? " (interested)" : " (passionate)";
                    skills[skill.def.defName] = skill.Level + passion;
                }
                obj["skills"] = skills;
            }

            // Mood & Needs
            if (pawn.needs != null)
            {
                var needs = new JSONObject();
                if (pawn.needs.mood != null)
                    needs["mood"] = pawn.needs.mood.CurLevelPercentage.ToString("P0");
                if (pawn.needs.food != null)
                    needs["food"] = pawn.needs.food.CurLevelPercentage.ToString("P0");
                if (pawn.needs.rest != null)
                    needs["rest"] = pawn.needs.rest.CurLevelPercentage.ToString("P0");
                if (pawn.needs.joy != null)
                    needs["joy"] = pawn.needs.joy.CurLevelPercentage.ToString("P0");
                obj["needs"] = needs;
            }

            // Active thoughts
            if (pawn.needs?.mood?.thoughts?.memories != null)
            {
                var thoughts = new JSONArray();
                foreach (var thought in pawn.needs.mood.thoughts.memories.Memories.Take(10))
                {
                    thoughts.Add(thought.LabelCap.ToString() + " (" + thought.MoodOffset().ToString("+0.#;-0.#") + ")");
                }
                obj["thoughts"] = thoughts;
            }

            obj["currentJob"] = pawn.CurJobDef?.reportString ?? "Idle";
            return obj.ToString();
        }

        public static string GetColonistHealth(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";

            if (pawn.health != null)
            {
                obj["overallCondition"] = pawn.health.State.ToString();

                // Hediffs (injuries, diseases, bionics)
                var injuries = new JSONArray();
                var diseases = new JSONArray();
                var bionics = new JSONArray();

                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    string entry = hediff.LabelCap.ToString();
                    if (hediff.Part != null)
                        entry += " (" + hediff.Part.Label + ")";

                    if (hediff is Hediff_Injury)
                        injuries.Add(entry);
                    else if (hediff is Hediff_AddedPart || hediff is Hediff_Implant)
                        bionics.Add(entry);
                    else if (hediff.def.makesSickThought || hediff.def.lethalSeverity > 0)
                        diseases.Add(entry);
                }

                if (injuries.Count > 0) obj["injuries"] = injuries;
                if (diseases.Count > 0) obj["diseases"] = diseases;
                if (bionics.Count > 0) obj["bionics"] = bionics;

                // Pain & consciousness
                obj["painLevel"] = pawn.health.hediffSet.PainTotal.ToString("P0");

                var consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                obj["consciousness"] = consciousness.ToString("P0");

                var moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
                obj["movementCapacity"] = moving.ToString("P0");

                var manipulation = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
                obj["manipulation"] = manipulation.ToString("P0");
            }

            return obj.ToString();
        }

        public static string DraftColonist(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.drafter == null) return ToolExecutor.JsonError("Colonist cannot be drafted.");
            if (pawn.Downed) return ToolExecutor.JsonError("Colonist is downed and cannot be drafted.");

            pawn.drafter.Drafted = true;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["status"] = "drafted";
            return result.ToString();
        }

        public static string UndraftColonist(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var pawn = FindPawnByName(name);
            if (pawn == null) return ToolExecutor.JsonError("Colonist '" + name + "' not found.");

            if (pawn.drafter == null) return ToolExecutor.JsonError("Colonist cannot be drafted.");

            pawn.drafter.Drafted = false;

            var result = new JSONObject();
            result["success"] = true;
            result["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
            result["status"] = "undrafted";
            return result.ToString();
        }

        public static string DraftAll()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            int drafted = 0;
            int failed = 0;

            foreach (var pawn in colonists)
            {
                if (pawn.drafter != null && !pawn.Downed)
                {
                    pawn.drafter.Drafted = true;
                    drafted++;
                }
                else
                {
                    failed++;
                }
            }

            var result = new JSONObject();
            result["success"] = true;
            result["drafted"] = drafted;
            result["failed"] = failed;
            result["total"] = colonists.Count();
            return result.ToString();
        }

        public static string UndraftAll()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            int undrafted = 0;

            foreach (var pawn in colonists)
            {
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = false;
                    undrafted++;
                }
            }

            var result = new JSONObject();
            result["success"] = true;
            result["undrafted"] = undrafted;
            result["total"] = colonists.Count();
            return result.ToString();
        }

        public static string GetColonistLocations()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            var arr = new JSONArray();

            foreach (var pawn in colonists)
            {
                var obj = new JSONObject();
                obj["name"] = pawn.Name?.ToStringShort ?? "Unknown";
                obj["x"] = pawn.Position.x;
                obj["z"] = pawn.Position.z;

                // Status flags
                obj["drafted"] = pawn.drafter?.Drafted == true;
                obj["downed"] = pawn.Downed;
                obj["mentalState"] = pawn.InMentalState;

                if (pawn.CurJobDef != null)
                    obj["currentJob"] = pawn.CurJobDef.reportString ?? pawn.CurJobDef.defName;
                else
                    obj["currentJob"] = "Idle";

                // Distance from home area center
                var homeArea = map.areaManager.Home;
                if (homeArea != null && homeArea.TrueCount > 0)
                {
                    var homeCell = homeArea.ActiveCells.FirstOrDefault();
                    float distance = pawn.Position.DistanceTo(homeCell);
                    obj["distanceFromHome"] = distance.ToString("F0");
                    
                    if (distance > 100)
                        obj["farFromHome"] = true;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["colonists"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetTemperatureRisks()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var colonists = map.mapPawns.FreeColonists;
            var atRisk = new JSONArray();
            int totalChecked = 0;
            int risksFound = 0;

            foreach (var pawn in colonists)
            {
                totalChecked++;
                
                // Get current cell temperature
                float temp = pawn.Position.GetTemperature(map);
                
                // Get colonist's comfortable temperature range
                FloatRange comfortRange = pawn.ComfortableTemperatureRange();
                
                string risk = null;
                string action = null;
                
                // Check for temperature risks
                if (temp < comfortRange.min - 10)
                {
                    risk = "freezing";
                    risksFound++;
                }
                else if (temp < comfortRange.min)
                {
                    risk = "cold";
                    risksFound++;
                }
                else if (temp > comfortRange.max + 10)
                {
                    risk = "overheating";
                    risksFound++;
                }
                else if (temp > comfortRange.max)
                {
                    risk = "hot";
                    risksFound++;
                }
                
                // Only include colonists with temperature risks
                if (risk != null)
                {
                    var obj = new JSONObject();
                    obj["colonist"] = pawn.Name?.ToStringShort ?? "Unknown";
                    obj["currentTemp"] = temp.ToString("F1") + "°C";
                    obj["comfortableRange"] = string.Format("{0:F0}°C - {1:F0}°C", comfortRange.min, comfortRange.max);
                    obj["risk"] = risk;
                    obj["currentPosition"] = string.Format("({0},{1})", pawn.Position.x, pawn.Position.z);
                    
                    // Find safe locations
                    var safeRoom = FindSafeRoom(map, comfortRange);
                    if (safeRoom != null)
                    {
                        obj["suggestedAction"] = string.Format("Move to {0} at ({1},{2}) - {3:F0}°C", 
                            safeRoom.Item1, 
                            safeRoom.Item2.x, 
                            safeRoom.Item2.z,
                            safeRoom.Item3);
                        action = obj["suggestedAction"].Value;
                    }
                    else
                    {
                        if (risk == "freezing" || risk == "cold")
                            obj["suggestedAction"] = "Seek indoor heated area or equip warm clothing";
                        else
                            obj["suggestedAction"] = "Seek cooled indoor area or remove layers";
                    }
                    
                    // Add severity assessment
                    if (risk == "freezing" || risk == "overheating")
                    {
                        obj["severity"] = "critical";
                        obj["warning"] = "⚠️ Immediate health risk - colonist may develop hypothermia/heatstroke!";
                    }
                    else
                    {
                        obj["severity"] = "moderate";
                    }
                    
                    atRisk.Add(obj);
                }
            }

            var result = new JSONObject();
            result["atRisk"] = atRisk;
            result["riskCount"] = risksFound;
            result["totalColonists"] = totalChecked;
            result["outdoorTemp"] = map.mapTemperature.OutdoorTemp.ToString("F1") + "°C";
            
            if (risksFound == 0)
            {
                result["status"] = "all_safe";
                result["message"] = "All colonists are in comfortable temperature conditions.";
            }
            else if (atRisk.Count > 0 && atRisk[0].AsObject["severity"]?.Value == "critical")
            {
                result["status"] = "critical";
            }
            else
            {
                result["status"] = "warning";
            }

            return result.ToString();
        }

        private static System.Tuple<string, IntVec3, float> FindSafeRoom(Map map, FloatRange comfortRange)
        {
            // Find a room within comfortable temperature range
            foreach (var room in map.regionGrid.AllRooms)
            {
                // RimWorld 1.6 API change: PsychoActive property removed, using TouchesMapEdge check
                if (!room.TouchesMapEdge && room.ProperRoom)
                {
                    float roomTemp = room.Temperature;
                    if (roomTemp >= comfortRange.min && roomTemp <= comfortRange.max)
                    {
                        // Get a cell in the room
                        var cell = room.Cells.FirstOrDefault();
                        if (cell.IsValid)
                        {
                            string roomRole = room.Role?.LabelCap ?? "Room";
                            return new System.Tuple<string, IntVec3, float>(roomRole, cell, roomTemp);
                        }
                    }
                }
            }
            return null;
        }

        public static Pawn FindPawnByName(string name)
        {
            var map = Find.CurrentMap;
            if (map == null) return null;

            string lower = name.ToLower();

            // Search free colonists first
            var pawn = map.mapPawns.FreeColonists
                .FirstOrDefault(p =>
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.Name?.ToStringFull?.ToLower().Contains(lower) == true ||
                    p.LabelShort?.ToLower() == lower);
            if (pawn != null) return pawn;

            // Also search prisoners and slaves
            pawn = map.mapPawns.PrisonersOfColony
                .FirstOrDefault(p =>
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.Name?.ToStringFull?.ToLower().Contains(lower) == true ||
                    p.LabelShort?.ToLower() == lower);
            if (pawn != null) return pawn;

            // Search slaves (they may not be in PrisonersOfColony)
            pawn = map.mapPawns.SlavesOfColonySpawned
                .FirstOrDefault(p =>
                    p.Name?.ToStringShort?.ToLower() == lower ||
                    p.Name?.ToStringFull?.ToLower().Contains(lower) == true ||
                    p.LabelShort?.ToLower() == lower);
            return pawn;
        }
    }
}
