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

            // Production schedules (Phase 8 enhancement)
            var production = new JSONObject();
            
            // Shearing (wool producers)
            var wool = animal.def?.GetCompProperties<CompProperties_Wool>();
            if (wool != null)
            {
                var woolComp = animal.TryGetComp<CompWool>();
                if (woolComp != null)
                {
                    production["wool_ready"] = woolComp.Full;
                    production["wool_growth_days"] = wool.shearIntervalDays.ToString("F0");
                }
            }
            
            // Milkable
            var milkable = animal.def?.GetCompProperties<CompProperties_Milkable>();
            if (milkable != null)
            {
                var milkComp = animal.TryGetComp<CompMilkable>();
                if (milkComp != null)
                {
                    production["milk_ready"] = milkComp.Full;
                    production["milk_interval_days"] = milkComp.IntervalDays.ToString("F0");
                    production["next_milking"] = milkComp.TicksUntilReady.ToStringTicksToPeriod();
                }
            }
            
            // Egg layer
            var eggs = animal.def?.GetCompProperties<CompProperties_EggLayer>();
            if (eggs != null)
            {
                var eggComp = animal.TryGetComp<CompEggLayer>();
                if (eggComp != null)
                {
                    production["eggs_ready"] = eggComp.FullyFertilized;
                    production["egg_interval_days"] = eggComp.IntervalDays.ToString("F0");
                    production["next_egg"] = eggComp.TicksUntilReady.ToStringTicksToPeriod();
                }
            }

            if (production.Count > 0)
                obj["production"] = production;

            return obj.ToString();
        }

        public static string GetAnimalStats(string speciesName)
        {
            if (string.IsNullOrEmpty(speciesName)) return ToolExecutor.JsonError("Species name parameter required.");

            // Try to find the animal kind
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            // Search for matching species
            PawnKindDef animalKind = DefDatabase<PawnKindDef>.AllDefsListForReading
                .FirstOrDefault(k => k.race?.Animal == true && 
                    (k.defName.ToLower().Contains(speciesName.ToLower()) ||
                     k.label?.ToLower().Contains(speciesName.ToLower())));
            
            if (animalKind == null)
                return ToolExecutor.JsonError("Animal species '" + speciesName + "' not found.");

            var race = animalKind.race;
            if (race?.Animal != true)
                return ToolExecutor.JsonError("Species '" + speciesName + "' is not an animal.");

            var obj = new JSONObject();
            obj["species"] = animalKind.label ?? animalKind.defName;
            obj["defName"] = animalKind.defName;

            // Carrying capacity - calculate from body size instead of spawning pawn
            var packAnimal = race.GetCompProperties<CompProperties_PackAnimal>();
            obj["pack_animal"] = packAnimal != null;
            if (packAnimal != null)
            {
                // Estimate capacity from body size - no need to spawn a test pawn
                float estimatedCapacity = race.baseBodySize * 25f; // Rough multiplier for pack animal capacity
                obj["carrying_capacity"] = estimatedCapacity.ToString("F1") + " kg (estimated)";
            }

            // Movement speed - use body size as approximation
            // Note: Actual stat would need a pawn instance, but body size correlates with speed
            obj["move_speed"] = race.baseBodySize.ToString("F2");
            obj["leap_max_range"] = race.leashMaxRange.ToString("F1");

            // Combat stats
            var combat = new JSONObject();
            combat["melee_damage"] = race.baseMeleeDamagePPF.ToString("F1");
            combat["armor_natural"] = race.armorValue_Sharp.ToString("F2");
            combat["body_size"] = race.baseBodySize.ToString("F2");
            var dps = race.baseBodySize * race.baseMeleeDamagePPF;
            combat["dps_estimate"] = dps.ToString("F1");
            obj["combat"] = combat;

            // Abilities
            var abilities = new JSONObject();
            
            var wool = race.GetCompProperties<CompProperties_Wool>();
            if (wool != null)
            {
                abilities["wool_type"] = wool.woolDef?.label ?? "Unknown";
                abilities["shear_interval_days"] = wool.shearIntervalDays.ToString("F0");
            }
            
            var milkable = race.GetCompProperties<CompProperties_Milkable>();
            if (milkable != null)
            {
                abilities["milkable"] = true;
                abilities["milk_interval_days"] = milkable.IntervalDays.ToString("F0");
                abilities["milk_amount"] = milkable.milkAmount.ToString("F0");
            }
            
            var eggLayer = race.GetCompProperties<CompProperties_EggLayer>();
            if (eggLayer != null)
            {
                abilities["egg_layer"] = true;
                abilities["egg_interval_days"] = eggLayer.IntervalDays.ToString("F0");
                abilities["egg_count_min"] = eggLayer.eggCountMin;
                abilities["egg_count_max"] = eggLayer.eggCountMax;
            }

            if (abilities.Count > 0)
                obj["abilities"] = abilities;

            // Temperament
            obj["wildness"] = animalKind.wildness.ToString("P0");
            obj["trainability"] = animalKind.trainability?.label ?? "None";
            obj["manhunter_on_tame_fail"] = animalKind.manhunterOnTameFailChance.ToString("P0");
            obj["manhunter_on_damage"] = animalKind.manhunterOnDamageChance.ToString("P0");

            // Filth
            obj["filth_leave_some_chance"] = race.filthLeavingChance.ToString("P0");

            // Market value
            obj["market_value"] = animalKind.marketValue.ToString("F0");

            return obj.ToString();
        }

        public static string GetWildAnimals()
        {
            var map = Find.CurrentMap;
            if (map == null) return ToolExecutor.JsonError("No active map.");

            var animals = new List<JSONObject>();

            foreach (var animal in map.mapPawns.AllPawns.Where(p => 
                p.RaceProps.Animal && 
                p.Faction != Faction.OfPlayer &&
                p.Spawned))
            {
                var kind = animal.kindDef;
                if (kind == null) continue;

                var obj = new JSONObject();
                obj["species"] = kind.label ?? kind.defName;
                obj["defName"] = kind.defName;
                obj["location"] = animal.Position.ToString();
                obj["gender"] = animal.gender.ToString();
                obj["health_percent"] = animal.health.summaryHealth.SummaryHealthPercent.ToString("P0");
                obj["downed"] = animal.Downed;
                obj["wildness"] = kind.wildness.ToString("P0");
                obj["tameable"] = kind.wildness < 1.0f;
                obj["manhunter_on_fail"] = kind.manhunterOnTameFailChance.ToString("P0");
                obj["trainability"] = kind.trainability?.label ?? "None";
                
                var marketValue = kind.marketValue;
                if (marketValue > 1000) obj["value_rating"] = "extremely_high";
                else if (marketValue > 500) obj["value_rating"] = "high";
                else if (marketValue > 100) obj["value_rating"] = "medium";
                else obj["value_rating"] = "low";

                var recommendations = new JSONArray();
                if (kind.wildness < 0.5f) recommendations.Add("Easy to tame - good candidate");
                else if (kind.wildness > 0.9f) recommendations.Add("Very difficult to tame");
                if (marketValue > 1000) recommendations.Add("High value - worth attempting");
                if (kind.manhunterOnTameFailChance > 0.1f) recommendations.Add("Warning: High manhunter chance on fail");
                
                if (recommendations.Count > 0)
                    obj["recommendations"] = recommendations;

                animals.Add(obj);
            }

            // Group by species
            var grouped = new JSONObject();
            foreach (var animal in animals)
            {
                var species = animal["species"]?.Value ?? "Unknown";
                if (!grouped.ContainsKey(species))
                {
                    grouped[species] = new JSONObject();
                    grouped[species]["species"] = species;
                    grouped[species]["defName"] = animal["defName"]?.Value;
                    grouped[species]["count"] = 0;
                    grouped[species]["tameable"] = animal["tameable"]?.Value ?? "false";
                    grouped[species]["value_rating"] = animal["value_rating"]?.Value ?? "low";
                    grouped[species]["locations"] = new JSONArray();
                }
                grouped[species]["count"] = (grouped[species]["count"].AsInt) + 1;
                grouped[species]["locations"].Add(animal["location"]?.Value);
            }

            var result = new JSONObject();
            result["wild_animals"] = grouped;
            result["total_animals"] = animals.Count;
            result["species_count"] = grouped.Count;
            return result.ToString();
        }
    }
}
