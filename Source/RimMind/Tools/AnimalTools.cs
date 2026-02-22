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
            
            // Shearable animals
            var shearable = animal.TryGetComp<CompShearable>();
            if (shearable != null)
            {
                production["shearable"] = true;
            }
            
            // Milkable
            var milkable = animal.TryGetComp<CompMilkable>();
            if (milkable != null)
            {
                production["milkable"] = true;
                var props = milkable.Props;
                if (props != null)
                {
                    production["milk_interval_days"] = props.milkIntervalDays.ToString("F0");
                }
            }
            
            // Egg layer
            var eggLayer = animal.TryGetComp<CompEggLayer>();
            if (eggLayer != null)
            {
                production["egg_layer"] = true;
                production["fertilized"] = eggLayer.FullyFertilized;
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
                .FirstOrDefault(k => k.RaceProps?.Animal == true && 
                    (k.defName.ToLower().Contains(speciesName.ToLower()) ||
                     (k.label != null && k.label.ToLower().Contains(speciesName.ToLower()))));
            
            if (animalKind == null)
                return ToolExecutor.JsonError("Animal species '" + speciesName + "' not found.");

            var race = animalKind.race;
            if (race?.race?.Animal != true)
                return ToolExecutor.JsonError("Species '" + speciesName + "' is not an animal.");

            var obj = new JSONObject();
            obj["species"] = animalKind.label ?? animalKind.defName;
            obj["defName"] = animalKind.defName;

            var raceProps = animalKind.RaceProps;
            var bodySize = raceProps.baseBodySize;
            obj["body_size"] = bodySize.ToString("F2");

            // Pack animal & carrying capacity
            obj["pack_animal"] = raceProps.packAnimal;
            if (raceProps.packAnimal)
            {
                float estimatedCapacity = bodySize * 25f;
                obj["carrying_capacity"] = estimatedCapacity.ToString("F1") + " kg (estimated)";
            }

            // Movement speed (from ThingDef stat base)
            var moveSpeed = race.GetStatValueAbstract(StatDefOf.MoveSpeed);
            obj["move_speed"] = moveSpeed.ToString("F2");

            // Wildness & trainability (available from ThingDef/RaceProps)
            var wildness = race.GetStatValueAbstract(StatDefOf.Wildness);
            obj["wildness"] = (wildness * 100f).ToString("F0") + "%";
            obj["trainability"] = raceProps.trainability?.LabelCap.ToString() ?? "None";

            // Market value
            obj["market_value"] = race.BaseMarketValue.ToString("F0");

            // Manhunter chances
            obj["manhunter_on_tame_fail"] = (raceProps.manhunterOnTameFailChance * 100f).ToString("F0") + "%";
            obj["manhunter_on_damage"] = (raceProps.manhunterOnDamageChance * 100f).ToString("F0") + "%";

            // Filth rate (from stat bases to avoid pawn-required warning)
            if (race.statBases != null)
            {
                var filthStat = race.statBases.FirstOrDefault(s => s.stat == StatDefOf.FilthRate);
                if (filthStat != null)
                    obj["filth_rate"] = filthStat.value.ToString("F2");
            }

            // Combat stats — calculated from ThingDef tools (melee verbs)
            var combat = new JSONObject();
            combat["body_size"] = bodySize.ToString("F2");
            // Calculate melee DPS from the animal's attack tools
            if (race.tools != null && race.tools.Count > 0)
            {
                float totalDPS = 0f;
                var attacks = new JSONArray();
                foreach (var tool in race.tools)
                {
                    float dps = tool.power / (tool.cooldownTime > 0 ? tool.cooldownTime : 1f);
                    totalDPS += dps;
                    attacks.Add(tool.label + " (" + tool.power.ToString("F0") + " dmg, " + tool.cooldownTime.ToString("F1") + "s)");
                }
                combat["attacks"] = attacks;
                combat["estimated_dps"] = (totalDPS / race.tools.Count).ToString("F1");
            }
            // Armor from stat bases (safe on ThingDef)
            if (race.statBases != null)
            {
                var armorSharpStat = race.statBases.FirstOrDefault(s => s.stat == StatDefOf.ArmorRating_Sharp);
                var armorBluntStat = race.statBases.FirstOrDefault(s => s.stat == StatDefOf.ArmorRating_Blunt);
                if (armorSharpStat != null) combat["armor_sharp"] = (armorSharpStat.value * 100f).ToString("F0") + "%";
                if (armorBluntStat != null) combat["armor_blunt"] = (armorBluntStat.value * 100f).ToString("F0") + "%";
            }
            obj["combat"] = combat;

            // Production abilities
            var abilities = new JSONObject();

            var shearable = race.GetCompProperties<CompProperties_Shearable>();
            if (shearable != null)
            {
                abilities["shearable"] = true;
                abilities["wool_type"] = shearable.woolDef?.label ?? "wool";
                abilities["wool_amount"] = shearable.woolAmount;
                abilities["shear_interval_days"] = shearable.shearIntervalDays.ToString("F0");
            }

            var milkable = race.GetCompProperties<CompProperties_Milkable>();
            if (milkable != null)
            {
                abilities["milkable"] = true;
                abilities["milk_type"] = milkable.milkDef?.label ?? "milk";
                abilities["milk_amount"] = milkable.milkAmount;
                abilities["milk_interval_days"] = milkable.milkIntervalDays.ToString("F0");
            }

            var eggLayer = race.GetCompProperties<CompProperties_EggLayer>();
            if (eggLayer != null)
            {
                abilities["egg_layer"] = true;
                abilities["egg_interval_days"] = eggLayer.eggLayIntervalDays.ToString("F0");
                if (eggLayer.eggUnfertilizedDef != null)
                    abilities["egg_type"] = eggLayer.eggUnfertilizedDef.label;
                else if (eggLayer.eggFertilizedDef != null)
                    abilities["egg_type"] = eggLayer.eggFertilizedDef.label;
                abilities["egg_count_range"] = eggLayer.eggCountRange.min + "-" + eggLayer.eggCountRange.max;
            }

            if (abilities.Count > 0)
                obj["abilities"] = abilities;

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
                obj["id"] = animal.thingIDNumber.ToString();
                obj["species"] = kind.label ?? kind.defName;
                obj["defName"] = kind.defName;
                obj["location"] = animal.Position.ToString();
                obj["gender"] = animal.gender.ToString();
                obj["health_percent"] = animal.health.summaryHealth.SummaryHealthPercent.ToString("P0");
                obj["downed"] = animal.Downed;
                
                // Get wildness from stat (requires pawn instance)
                float wildness = animal.GetStatValue(StatDefOf.Wildness);
                obj["wildness"] = wildness.ToString("P0");
                obj["tameable"] = wildness < 1.0f;
                obj["trainability"] = animal.training?.CanAssignToTrain(TrainableDefOf.Obedience) == true ? "Trainable" : "None";

                var recommendations = new JSONArray();
                if (wildness < 0.35f) recommendations.Add("Easy to tame - good candidate");
                else if (wildness < 0.65f) recommendations.Add("Moderate taming difficulty");
                else if (wildness > 0.9f) recommendations.Add("Very difficult to tame");
                else if (wildness > 0.65f) recommendations.Add("Hard to tame");

                // Check for production value
                var race = animal.def;
                if (race.GetCompProperties<CompProperties_Shearable>() != null)
                    recommendations.Add("Produces wool - valuable for textiles");
                if (race.GetCompProperties<CompProperties_Milkable>() != null)
                    recommendations.Add("Produces milk - food source");
                if (race.GetCompProperties<CompProperties_EggLayer>() != null)
                    recommendations.Add("Lays eggs - food source");
                if (animal.RaceProps.packAnimal)
                    recommendations.Add("Pack animal - useful for caravans");
                if (animal.RaceProps.baseBodySize >= 2.0f)
                    recommendations.Add("Large animal - good meat yield if hunted");
                if (animal.kindDef.defName == "Thrumbo")
                    recommendations.Add("Rare! Extremely valuable - consider taming");

                obj["recommendations"] = recommendations;

                animals.Add(obj);
            }

            // Group by species, with individual animal details (id, gender, location)
            var grouped = new JSONObject();
            foreach (var animal in animals)
            {
                var species = animal["species"]?.Value ?? "Unknown";
                if (!grouped.HasKey(species))
                {
                    grouped[species] = new JSONObject();
                    grouped[species]["species"] = species;
                    grouped[species]["defName"] = animal["defName"]?.Value;
                    grouped[species]["count"] = 0;
                    grouped[species]["tameable"] = animal["tameable"]?.Value ?? "false";
                    grouped[species]["wildness"] = animal["wildness"]?.Value ?? "Unknown";
                    grouped[species]["trainability"] = animal["trainability"]?.Value ?? "None";
                    grouped[species]["individuals"] = new JSONArray();
                    // Taming difficulty rating based on wildness
                    float w = 0f;
                    float.TryParse(animal["wildness"]?.Value?.Replace("%", ""), out w);
                    w = w / 100f;
                    if (w < 0.35f) grouped[species]["taming_difficulty"] = "Easy";
                    else if (w < 0.65f) grouped[species]["taming_difficulty"] = "Moderate";
                    else if (w < 0.85f) grouped[species]["taming_difficulty"] = "Hard";
                    else grouped[species]["taming_difficulty"] = "Very Hard";
                    // Recommendations
                    var recs = new JSONArray();
                    if (animal["recommendations"] != null)
                    {
                        for (int i = 0; i < animal["recommendations"].Count; i++)
                            recs.Add(animal["recommendations"][i].Value);
                    }
                    grouped[species]["recommendations"] = recs;
                }
                grouped[species]["count"] = (grouped[species]["count"].AsInt) + 1;
                // Add individual animal with unique ID for precise targeting
                var individual = new JSONObject();
                individual["id"] = animal["id"]?.Value;
                individual["gender"] = animal["gender"]?.Value;
                individual["location"] = animal["location"]?.Value;
                individual["health_percent"] = animal["health_percent"]?.Value;
                if (animal["downed"]?.AsBool == true) individual["downed"] = true;
                grouped[species]["individuals"].Add(individual);
            }

            var result = new JSONObject();
            result["wild_animals"] = grouped;
            result["total_animals"] = animals.Count;
            result["species_count"] = grouped.Count;
            return result.ToString();
        }
    }
}
