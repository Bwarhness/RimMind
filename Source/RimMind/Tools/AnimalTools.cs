using System.Linq;
using System.Collections.Generic;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class AnimalTools
    {
        public static string ListAnimals()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var arr = new JSONArray();

            foreach (var animal in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (!animal.RaceProps.Animal) continue;

                var obj = new JSONObject();
                obj["name"] = animal.Name?.ToStringShort ?? animal.LabelShort;
                obj["species"] = animal.kindDef?.label ?? "Unknown";
                obj["gender"] = animal.gender.ToString();

                // Master
                var master = animal.playerSettings?.Master;
                if (master != null)
                    obj["master"] = master.Name?.ToStringShort ?? "Unknown";

                // Carrying capacity (Phase 8 enhancement)
                if (animal.RaceProps.packAnimal)
                {
                    var massUtil = MassUtility.Capacity(animal);
                    var massCurrent = MassUtility.GearAndInventoryMass(animal);
                    obj["carrying"] = massCurrent.ToString("F1") + "/" + massUtil.ToString("F1") + " kg";
                    obj["load_percentage"] = ((massCurrent / massUtil) * 100f).ToString("F0") + "%";
                }

                // Training
                if (animal.training != null)
                {
                    var training = new JSONObject();
                    foreach (var trainable in DefDatabase<TrainableDef>.AllDefsListForReading)
                    {
                        if (animal.training.HasLearned(trainable))
                            training[trainable.LabelCap.ToString()] = "Learned";
                        else if (animal.training.CanAssignToTrain(trainable).Accepted)
                            training[trainable.LabelCap.ToString()] = "Available";
                    }
                    if (training.Count > 0)
                        obj["training"] = training;
                }

                arr.Add(obj);
            }

            var result = new JSONObject();
            result["animals"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetAnimalDetails(string name)
        {
            if (string.IsNullOrEmpty(name)) return ToolExecutor.JsonError("Name parameter required.");

            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            string lower = name.ToLower();
            var animal = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .FirstOrDefault(p => p.RaceProps.Animal &&
                    (p.Name?.ToStringShort?.ToLower() == lower ||
                     p.LabelShort?.ToLower() == lower));

            if (animal == null) return ToolExecutor.JsonError("Animal '" + name + "' not found.");

            var obj = new JSONObject();
            obj["name"] = animal.Name?.ToStringShort ?? animal.LabelShort;
            obj["species"] = animal.kindDef?.label ?? "Unknown";
            obj["age"] = animal.ageTracker.AgeBiologicalYears;
            obj["gender"] = animal.gender.ToString();

            // Health
            obj["healthState"] = animal.health.State.ToString();
            if (animal.health.hediffSet.hediffs.Count > 0)
            {
                var conditions = new JSONArray();
                foreach (var h in animal.health.hediffSet.hediffs)
                    conditions.Add(h.LabelCap.ToString() + (h.Part != null ? " (" + h.Part.Label + ")" : ""));
                obj["healthConditions"] = conditions;
            }

            // Master and bonding
            var master = animal.playerSettings?.Master;
            if (master != null)
                obj["master"] = master.Name?.ToStringShort ?? "Unknown";

            var bondedPawn = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedPawn != null)
                obj["bondedTo"] = bondedPawn.Name?.ToStringShort ?? "Unknown";

            // Food
            obj["hunger"] = animal.needs?.food?.CurLevelPercentage.ToString("P0") ?? "N/A";
            obj["diet"] = animal.RaceProps.foodType.ToString();

            return obj.ToString();
        }
    }
}
