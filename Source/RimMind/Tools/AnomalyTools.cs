using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class AnomalyTools
    {
        /// <summary>
        /// Get all anomaly entities in the colony
        /// Parameters: entity_type (optional filter)
        /// Returns: List of entities with location, type, containment status, threat level
        /// </summary>
        public static string GetAnomalyEntities(string entityType = null)
        {
            // Check if Anomaly DLC is active
            if (!ModsConfig.AnomalyActive)
                return ToolExecutor.JsonError("Anomaly DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();
            var entities = new JSONArray();

            // Find all anomaly-related things
            var allThings = map.listerThings.AllThings;

            foreach (var thing in allThings)
            {
                // Skip things without valid positions
                if (thing.Position == IntVec3.Zero) continue;

                // Check for Anomaly DLC entities
                bool isAnomalyEntity = IsAnomalyEntity(thing);
                if (!isAnomalyEntity) continue;

                if (entityType != null && !thing.def.defName.Contains(entityType, StringComparison.OrdinalIgnoreCase))
                    continue;

                var entity = new JSONObject();
                entity["defName"] = thing.def.defName;
                entity["label"] = thing.LabelCap ?? thing.def.label;
                entity["position"] = $"{thing.Position.x}, {thing.Position.z}";
                entity["tile"] = map.WorldGrid[thing.Position].index;

                // Entity type classification
                entity["entityType"] = ClassifyEntityType(thing.def.defName);

                // Containment status
                var containment = GetContainmentStatus(thing);
                entity["containmentLevel"] = containment.level;
                entity["containmentStatus"] = containment.status;

                // Threat level
                entity["threatLevel"] = AssessThreatLevel(thing);

                entities.Add(entity);
            }

            result["entities"] = entities;
            result["count"] = entities.Count;

            return result.ToString();
        }

        /// <summary>
        /// Get containment facility status
        /// Parameters: None
        /// Returns: Containment cells, containment Pawns, entity breakdown, threat assessment
        /// </summary>
        public static string GetContainmentStatus()
        {
            // Check if Anomaly DLC is active
            if (!ModsConfig.AnomalyActive)
                return ToolExecutor.JsonError("Anomaly DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Find containment buildings
            var containmentBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.defName.Contains("Containment") || 
                           b.def.HasComp(typeof(CompContainment)))
                .ToList();

            var buildings = new JSONArray();
            int totalCapacity = 0;
            int currentOccupancy = 0;

            foreach (var building in containmentBuildings)
            {
                var b = new JSONObject();
                b["defName"] = building.def.defName;
                b["position"] = $"{building.Position.x}, {building.Position.z}";
                
                // Get containment-related properties
                if (building is Building buildingWithComp)
                {
                    var comp = buildingWithComp.GetComp<CompContainment>();
                    if (comp != null)
                    {
                        b["containmentStrength"] = comp.ContainmentStrength.ToString("0");
                        b["occupied"] = comp.Occupied;
                    }
                }

                // Try to get capacity from def
                b["capacity"] = "1"; // Each containment building holds 1
                totalCapacity++;

                // Check if occupied
                if (!building.HasAnyDynamicPawn())
                {
                    b["occupied"] = false;
                }
                else
                {
                    b["occupied"] = true;
                    currentOccupancy++;
                }

                buildings.Add(b);
            }

            result["containmentBuildings"] = buildings;
            result["totalCapacity"] = totalCapacity;
            result["currentOccupancy"] = currentOccupancy;
            result["availableSpace"] = totalCapacity - currentOccupancy;

            // Find contained entities
            var containedEntities = new JSONArray();
            foreach (var pawn in map.mapPawns.PawnsInSpawnedContainers)
            {
                var entity = new JSONObject();
                entity["name"] = pawn.Name?.ToStringShort ?? pawn.def.label;
                entity["type"] = pawn.def.defName;
                entity["contained"] = true;
                containedEntities.Add(entity);
            }
            result["containedEntities"] = containedEntities;

            // Find uncontained entities (potential threats)
            var uncontainedThreats = new JSONArray();
            foreach (var thing in map.listerThings.AllThings)
            {
                if (!IsAnomalyEntity(thing)) continue;
                
                // Check if this entity is contained
                bool isContained = IsEntityContained(thing);
                if (!isContained)
                {
                    var threat = new JSONObject();
                    threat["defName"] = thing.def.defName;
                    threat["position"] = $"{thing.Position.x}, {thing.Position.z}";
                    threat["threatLevel"] = AssessThreatLevel(thing);
                    uncontainedThreats.Add(threat);
                }
            }
            result["uncontainedThreats"] = uncontainedThreats;

            return result.ToString();
        }

        /// <summary>
        /// Analyze entity interactions and risks
        /// Returns: Entity groups, interaction risks, recommended containment actions
        /// </summary>
        public static string AnalyzeEntityInteractions()
        {
            // Check if Anomaly DLC is active
            if (!ModsConfig.AnomalyActive)
                return ToolExecutor.JsonError("Anomaly DLC is not active.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var result = new JSONObject();

            // Group entities by type
            var entityGroups = new JSONObject();
            var allEntities = map.listerThings.AllThings.Where(IsAnomalyEntity).ToList();

            foreach (var entity in allEntities)
            {
                string type = ClassifyEntityType(entity.def.defName);
                if (!entityGroups.ContainsKey(type))
                    entityGroups[type] = new JSONArray();
                
                ((JSONArray)entityGroups[type]).Add(entity.def.defName);
            }
            result["entityGroups"] = entityGroups;

            // Analyze proximity risks
            var proximityRisks = new JSONArray();
            for (int i = 0; i < allEntities.Count; i++)
            {
                for (int j = i + 1; j < allEntities.Count; j++)
                {
                    var e1 = allEntities[i];
                    var e2 = allEntities[j];
                    
                    float dist = (e1.Position - e2.Position).LengthHorizontal;
                    if (dist < 10) // Too close
                    {
                        var risk = new JSONObject();
                        risk["entity1"] = e1.def.defName;
                        risk["entity2"] = e2.def.defName;
                        risk["distance"] = dist.ToString("0.0");
                        risk["recommendation"] = "Separate entities to prevent interaction";
                        proximityRisks.Add(risk);
                    }
                }
            }
            result["proximityRisks"] = proximityRisks;

            // Recommendations
            var recommendations = new JSONArray();
            var uncontained = allEntities.Where(e => !IsEntityContained(e)).ToList();
            if (uncontained.Count > 0)
            {
                recommendations.Add($"Contain {uncontained.Count} uncontained entities");
            }

            var criticalThreats = uncontained.Where(e => AssessThreatLevel(e) == "critical").ToList();
            if (criticalThreats.Count > 0)
            {
                recommendations.Add("CRITICAL: Immediately contain critical threat entities");
            }

            result["recommendations"] = recommendations;

            return result.ToString();
        }

        // Helper methods
        private static bool IsAnomalyEntity(Thing thing)
        {
            if (thing == null) return false;
            string defName = thing.def.defName;

            // Anomaly DLC entity types
            return defName.Contains("GlowingBody") ||
                   defName.Contains("Shrine") ||
                   defName.Contains("Monolith") ||
                   defName.Contains("Obelisk") ||
                   defName.Contains("Pylon") ||
                   defName.Contains("Corpse") && thing.def.defName.Contains("Human") && 
                   thing.def.defName.Contains("Anima") ||
                   defName.Contains("Anomaly") ||
                   thing.def?.thingCategories?.Any(c => c.defName.Contains("Anomaly")) == true;
        }

        private static string ClassifyEntityType(string defName)
        {
            if (defName.Contains("GlowingBody")) return "Glowing Body";
            if (defName.Contains("Shrine")) return "Shrine";
            if (defName.Contains("Monolith") || defName.Contains("Obelisk")) return "Monolith";
            if (defName.Contains("Pylon")) return "Pylon";
            if (defName.Contains("Anima")) return "Anima Entity";
            return "Unknown Anomaly";
        }

        private static (string level, string status) GetContainmentStatus(Thing thing)
        {
            // Check if thing is inside a container
            if (thing?.ParentHolder is Building_Casket casket)
            {
                if (casket.HasComp<CompContainment>())
                {
                    return ("high", "Contained");
                }
            }

            // Check if in a containment building
            var map = Find.CurrentMap;
            if (map != null)
            {
                var buildings = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("Containment"))
                    .ToList();

                foreach (var b in buildings)
                {
                    if (b.Position == thing.Position)
                    {
                        return ("high", "Contained");
                    }
                }
            }

            // Check distance to containment
            if (thing != null)
            {
                var containmentBuildings = map?.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("Containment"))
                    .ToList() ?? new List<Building>();

                foreach (var cb in containmentBuildings)
                {
                    float dist = (thing.Position - cb.Position).LengthHorizontal;
                    if (dist < 3)
                    {
                        return ("medium", "Near Containment");
                    }
                }
            }

            return ("none", "Uncontained");
        }

        private static bool IsEntityContained(Thing thing)
        {
            var status = GetContainmentStatus(thing);
            return status.status == "Contained";
        }

        private static string AssessThreatLevel(Thing thing)
        {
            string defName = thing.def.defName;

            // Critical threats
            if (defName.Contains("Monolith") || defName.Contains("Obelisk"))
                return "critical";

            // High threats
            if (defName.Contains("GlowingBody") && defName.Contains("Tier"))
                return "high";

            // Medium threats
            if (defName.Contains("Shrine") || defName.Contains("Pylon"))
                return "medium";

            // Low threats
            if (defName.Contains("GlowingBody"))
                return "low";

            return "unknown";
        }
    }
}
