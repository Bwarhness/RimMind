using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RimMind.API;
using RimMind.Core;
using RimMind.Storyteller;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// Generates a RimMindScenarioPlan from a user prompt by calling the configured LLM.
    /// Static so it can run pre-game when Current.Game is null. The callback fires on
    /// the HTTP completion thread — consumers must handle main-thread marshalling.
    /// </summary>
    public static class ScenarioPlanGenerator
    {
        private static readonly Regex MarkdownCodeBlock = new Regex(@"```(?:json)?\s*\n(.*?)\n```", RegexOptions.Singleline);

        public static void Generate(string userPrompt, string themeId, Action<RimMindScenarioPlan> callback)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                callback?.Invoke(null);
                return;
            }

            ThemeRegistry.Init();
            var theme = ThemeRegistry.Get(themeId ?? "chronicle") ?? new ChronicleThemeProvider();

            var systemPrompt = BuildSystemPrompt(theme);
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(systemPrompt),
                ChatMessage.User("Player pitch:\n\n" + userPrompt + "\n\nRespond with a single JSON object matching the schema. Do NOT wrap it in markdown.")
            };

            var request = new ChatRequest
            {
                model = RimMindMod.Settings.ActiveModelId,
                messages = messages,
                temperature = 0.9f,
                // Effectively uncapped. Anthropic requires the field, so we send the
                // current ceiling (Sonnet 4.6 = 64K output). Smaller-output models will
                // error here; switch to a model that supports the budget you need.
                max_tokens = 64000,
                // The schema is fixed and structured — we don't need the model to reason
                // before answering. Skips Qwen / DeepSeek-R1 thinking passes (huge speedup).
                // Other providers ignore this field.
                enable_thinking = false
            };

            Action<ChatResponse> onResponse = response =>
            {
                if (!response.success)
                {
                    Log.Warning("[RimMind] Scenario plan generation failed: " + response.error);
                    callback?.Invoke(null);
                    return;
                }
                var plan = Parse(response.message?.content ?? "", userPrompt);
                callback?.Invoke(plan);
            };

            try
            {
                if (RimMindMod.Settings.IsClaudeCode)
                    ClaudeCodeClient.SendAsync(request, onResponse);
                else if (RimMindMod.Settings.IsAnthropic)
                    AnthropicClient.SendAsync(request, onResponse);
                else if (RimMindMod.Settings.IsCustom)
                    CustomProviderClient.SendAsync(request, onResponse);
                else
                    OpenRouterClient.SendAsync(request, onResponse);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Scenario plan dispatch failed: " + ex.Message);
                callback?.Invoke(null);
            }
        }

        private static string BuildSystemPrompt(IThemeProvider theme)
        {
            return theme.CampaignPrompt + "\n\n" +
                "You are running a D&D-style session zero for a RimWorld colony. The player gave you a one-line pitch; " +
                "your job is to build out the world and the characters with the depth of a published campaign setting. " +
                "Future story beats will reference what you write here, so be COMPREHENSIVE and SPECIFIC. " +
                "Prose fields should be 2-4 paragraphs of vivid, grounded writing — not one-liners.\n\n" +
                "Return a single JSON object with this schema:\n\n" +
                "{\n" +
                "  \"campaign\": {\n" +
                "    // THE WORLD\n" +
                "    \"setting\": string,            // one-paragraph vibe / atmosphere\n" +
                "    \"worldLore\": string,          // 2-3 PARAGRAPHS: geography, politics, history of the region\n" +
                "    \"ideologyName\": string,       // short evocative name for the colony's belief system. Becomes the in-game Ideology name.\n" +
                "    \"ideologyDescription\": string,// 1-2 paragraphs: tenets, taboos, rituals, what they revere. Becomes the in-game Ideology description.\n" +
                "    \"recentEvents\": string,       // 1-2 paragraphs: what shook the world in the years before the colony begins\n" +
                "    \"techLevel\": string,          // tribal | medieval | industrial | spacer | glittertech | mixed (be specific)\n" +
                "    \"themes\": [string, ...],      // 3-6 thematic motifs the storyteller should honor\n\n" +
                "    // THE PARTY\n" +
                "    \"colonyOrigin\": string,       // why are these particular people HERE on this map\n" +
                "    \"howTheyMet\": string,         // the actual circumstances of the party forming (1-2 paragraphs)\n" +
                "    \"sharedGoal\": string,         // what binds them together\n" +
                "    \"internalTension\": string,    // what divides them despite the alliance\n\n" +
                "    // STORY ENGINE\n" +
                "    \"incitingIncident\": string,   // 1 paragraph: what kicks the story off\n" +
                "    \"currentAct\": string,         // narrative phase label\n" +
                "    \"activeForces\": [string, ...],// 3-5 factions/powers in play, each described in 1-2 sentences\n" +
                "    \"pendingThreat\": string,      // an overarching danger\n" +
                "    \"opportunity\": string,        // something the colony could gain\n" +
                "    \"plantedSeeds\": [{\"id\": string, \"description\": string, \"suggestedIncidentDefName\": string}, ...]\n" +
                "  },\n\n" +
                "  \"pawns\": [\n" +
                "    {\n" +
                "      \"firstName\": string, \"nickName\": string|null, \"lastName\": string,\n" +
                "      \"gender\": \"Male\"|\"Female\",\n" +
                "      \"age\": int,\n" +
                "      \"appearance\": string,                // 1-2 sentences\n" +
                "      \"xenotype\": string,                  // Biotech DLC. Either a vanilla XenotypeDef defName OR an EVOCATIVE custom name (e.g. \"Sandblood\", \"Vat-spawn\", \"Twilight Vampire\"). NEVER use the literal string \"Custom\". When defining a custom xenotype (via xenotypeGenes), the name MUST NOT be one of the vanilla names. Vanilla options (use as-is, no xenotypeGenes): Baseliner, Hussar, Genie, Pigskin, Yttakin, Sanguophage, Impid, Waster, Highmate, Neanderthal, Dirtmole. Use Baseliner if no xenotype fits.\n" +
                "      \"xenotypeGenes\": [string, ...],      // OPTIONAL list of GeneDef defNames (5-12 typical) ONLY when defining a custom xenotype. Leave empty for vanilla. MUST be alphanumeric defNames — NEVER numbers, NEVER labels with spaces. Valid examples by category:\n" +
                "         //   skin: Skin_SheerWhite, Skin_LightGray, Skin_SlateGray, Skin_InkBlack, Skin_PaleYellow, Skin_DeepYellow, Skin_PaleRed, Skin_DeepRed, Skin_Orange, Skin_Green, Skin_Blue, Skin_Purple\n" +
                "         //   body: Body_Standard, Body_Hulk, Body_Thin, Body_Fat\n" +
                "         //   vision: DarkVision, Nearsighted\n" +
                "         //   health: Robust, Delicate, WoundHealing_Fast, WoundHealing_SuperFast, WoundHealing_Slow, Immunity_Strong, Immunity_SuperStrong, Immunity_Weak, ToxicEnvironmentResistance_Total, ToxicEnvironmentResistance_Partial, Superclotting, Sterile, Fertile, DiseaseFree, PerfectImmunity\n" +
                "         //   temperature: FireResistant, FireWeakness, MinTemp_LargeDecrease, MinTemp_SmallDecrease, MaxTemp_SmallIncrease, MaxTemp_LargeIncrease\n" +
                "         //   aggression: Aggression_DeadCalm, Aggression_Aggressive, Aggression_HyperAggressive, KindInstinct, KillThirst\n" +
                "         //   speed: MoveSpeed_VeryQuick, MoveSpeed_Quick, MoveSpeed_Slow, NakedSpeed, LongjumpLegs\n" +
                "         //   mood: Mood_Sanguine, Mood_Optimist, Mood_Pessimist, Mood_Depressive, Pain_Reduced, Pain_Extra\n" +
                "         //   sleep: Sleepy, LowSleep, Neversleep\n" +
                "         //   learning: Learning_Fast, Learning_Slow\n" +
                "         //   psychic: PsychicAbility_Enhanced, PsychicAbility_Extreme, PsychicAbility_Dull, PsychicAbility_Deaf, PsychicBonding\n" +
                "         //   sanguophage: Bloodfeeder, Hemogenic, Deathrest, Deathless, Coagulate, Ageless, ArchiteMetabolism\n" +
                "         //   combat: MeleeDamage_Strong, MeleeDamage_Weak, FireSpew, AcidSpray, FoamSpray, PiercingSpine, AnimalWarcall\n" +
                "         //   misc: CaveDweller, Furskin, Inbred, Nearsighted, Resurrect, Pyrophobia, FireTerror, ElongatedFingers\n" +
                "         //   STRICT RULE: ONLY use names from the lists above. Do NOT invent variations like Skin_PaleGreen, Hearing_Enhanced, Social_Fast, CarryingCapacity_Large — those do not exist. If a concept isn't covered above, leave it out.\n" +
                "      \"childhoodBackstory\": string,        // 1-2 paragraphs of grounded prose\n" +
                "      \"adulthoodBackstory\": string,        // 1-2 paragraphs that tie into the campaign frame\n" +
                "      \"definingMoment\": string,            // ONE pivotal event, 1-2 sentences\n" +
                "      \"narrative\": string,                 // 1-sentence tagline summary\n" +
                "      \"traits\": [string, ...],             // 2-3 vanilla TraitDef defNames\n" +
                "      \"skills\": { \"Shooting\": 0-20, \"Melee\": 0-20, \"Construction\": 0-20, \"Mining\": 0-20, \"Cooking\": 0-20, \"Plants\": 0-20, \"Animals\": 0-20, \"Crafting\": 0-20, \"Artistic\": 0-20, \"Medicine\": 0-20, \"Social\": 0-20, \"Intellectual\": 0-20 },\n" +
                "      \"passions\": { \"<SkillDef>\": \"Minor\"|\"Major\" }\n" +
                "    }, ... typically 3 pawns. Keep pawn descriptions tight — depth belongs in the campaign frame.\n" +
                "  ],\n\n" +
                "  \"items\": [\n" +
                "    {\"defName\": string, \"count\": int, \"stuff\": string|null}\n" +
                "    // ThingDef defNames: Silver, MealSurvivalPack, MedicineIndustrial, ComponentIndustrial,\n" +
                "    // Steel, WoodLog, Gun_BoltActionRifle, Gun_Revolver, MeleeWeapon_Knife, Apparel_Parka, etc.\n" +
                "  ],\n" +
                "  \"biomeHint\": string,    // BiomeDef: TemperateForest, BorealForest, AridShrubland, Desert, IceSheet, Tundra, TropicalRainforest\n" +
                "  \"seasonHint\": string,   // Spring|Summer|Fall|Winter\n" +
                "  \"locationHint\": string  // freeform description of map placement\n" +
                "}\n\n" +
                "Constraints:\n" +
                "- Prose fields are PROSE. Write paragraphs with specific names, places, dates, sensory detail. No bullet lists, no summaries.\n" +
                "- Pawns must feel like people with histories that intersect. Use each other's names in relationshipsToOthers.\n" +
                "- The world must feel lived-in: invent named cities, factions, leaders, dates, events.\n" +
                "- Skill levels: 0-20, total budget ~50 per pawn (no all-20s). Show specialization.\n" +
                "- 2-3 traits per pawn, must be valid vanilla TraitDef defNames.\n" +
                "- Items: 8-15 entries appropriate to the story.\n" +
                "- The starting state must match the user's pitch tonally.\n" +
                "- All RimWorld defNames (traits, items, biomes) must exist in vanilla. If unsure, omit rather than invent.\n";
        }

        public static RimMindScenarioPlan Parse(string content, string userPrompt)
        {
            var plan = new RimMindScenarioPlan { UserPrompt = userPrompt };
            try
            {
                content = ExtractJsonFromMarkdown(content);
                var root = JSONNode.Parse(content);
                if (root == null) return plan;

                var campNode = root["campaign"];
                if (campNode != null && !campNode.IsNull)
                {
                    plan.Campaign = new CampaignFrame
                    {
                        UserPrompt = userPrompt,
                        Setting = campNode["setting"]?.Value ?? "An untamed rim world",
                        WorldLore = campNode["worldLore"]?.Value ?? "",
                        IdeologyName = campNode["ideologyName"]?.Value ?? "",
                        IdeologyDescription = campNode["ideologyDescription"]?.Value ?? "",
                        RecentEvents = campNode["recentEvents"]?.Value ?? "",
                        TechLevel = campNode["techLevel"]?.Value ?? "",
                        Themes = ParseStringArray(campNode["themes"]),
                        ColonyOrigin = campNode["colonyOrigin"]?.Value ?? "",
                        HowTheyMet = campNode["howTheyMet"]?.Value ?? "",
                        SharedGoal = campNode["sharedGoal"]?.Value ?? "",
                        InternalTension = campNode["internalTension"]?.Value ?? "",
                        IncitingIncident = campNode["incitingIncident"]?.Value ?? "Arrival",
                        CurrentAct = campNode["currentAct"]?.Value ?? "Act I",
                        PendingThreat = campNode["pendingThreat"]?.Value ?? "",
                        Opportunity = campNode["opportunity"]?.Value ?? "",
                        ActiveForces = ParseStringArray(campNode["activeForces"]),
                        PlantedSeeds = ParseSeedsArray(campNode["plantedSeeds"])
                    };
                }

                var pawnsNode = root["pawns"];
                if (pawnsNode != null && pawnsNode.IsArray)
                {
                    foreach (JSONNode n in pawnsNode.AsArray)
                    {
                        if (n == null || n.IsNull) continue;
                        var spec = new PawnSpec
                        {
                            FirstName = n["firstName"]?.Value,
                            NickName = n["nickName"]?.Value,
                            LastName = n["lastName"]?.Value,
                            Gender = n["gender"]?.Value,
                            Appearance = n["appearance"]?.Value,
                            Xenotype = SanitizeXenotypeName(n["xenotype"]?.Value),
                            XenotypeGenes = ParseGeneDefArray(n["xenotypeGenes"]),
                            ChildhoodBackstory = n["childhoodBackstory"]?.Value,
                            AdulthoodBackstory = n["adulthoodBackstory"]?.Value,
                            DefiningMoment = n["definingMoment"]?.Value,
                            Traits = ParseStringArray(n["traits"]),
                            Narrative = n["narrative"]?.Value
                        };
                        if (n["age"] != null && int.TryParse(n["age"].Value, out int age))
                            spec.Age = age;

                        var skillsNode = n["skills"];
                        if (skillsNode != null && skillsNode.IsObject)
                        {
                            foreach (var kv in skillsNode.AsObject.Pairs)
                            {
                                if (int.TryParse(kv.Value?.Value ?? "", out int lvl))
                                    spec.Skills[kv.Key] = Mathf(lvl, 0, 20);
                            }
                        }

                        var passionsNode = n["passions"];
                        if (passionsNode != null && passionsNode.IsObject)
                        {
                            foreach (var kv in passionsNode.AsObject.Pairs)
                            {
                                if (!string.IsNullOrEmpty(kv.Value?.Value))
                                    spec.Passions[kv.Key] = kv.Value.Value;
                            }
                        }

                        plan.Pawns.Add(spec);
                    }
                }

                var itemsNode = root["items"];
                if (itemsNode != null && itemsNode.IsArray)
                {
                    foreach (JSONNode n in itemsNode.AsArray)
                    {
                        if (n == null || n.IsNull) continue;
                        var defName = n["defName"]?.Value;
                        if (string.IsNullOrWhiteSpace(defName)) continue;
                        int count = 1;
                        if (n["count"] != null && int.TryParse(n["count"].Value, out int c)) count = c < 1 ? 1 : c;
                        plan.Items.Add(new ItemSpec { DefName = defName, Count = count, Stuff = n["stuff"]?.Value });
                    }
                }

                plan.BiomeHint = root["biomeHint"]?.Value;
                plan.SeasonHint = root["seasonHint"]?.Value;
                plan.LocationHint = root["locationHint"]?.Value;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Failed to parse scenario plan: " + ex.Message);
            }
            return plan;
        }

        // Gene defNames are alphanumeric + underscore, must start with a letter.
        // Filter out garbage like "20", "Custom", or labels with spaces that the model
        // sometimes emits in this array.
        private static readonly Regex DefNameLike = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

        private static List<string> ParseGeneDefArray(JSONNode node)
        {
            var list = new List<string>();
            if (node == null || !node.IsArray) return list;
            foreach (JSONNode n in node.AsArray)
            {
                if (n == null || n.IsNull) continue;
                var raw = n.Value?.Trim();
                if (string.IsNullOrEmpty(raw)) continue;
                if (!DefNameLike.IsMatch(raw))
                {
                    Log.Warning($"[RimMind] Discarding non-defName entry in xenotypeGenes: \"{raw}\"");
                    continue;
                }
                list.Add(raw);
            }
            return list;
        }

        // Trim whitespace. Generic "Custom" is replaced with a pawn-specific name later
        // (in SaveCustomXenotypes) so the genes still apply — we don't null it out here
        // anymore, that was killing the xenotype entirely.
        private static string SanitizeXenotypeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Trim();
        }

        private static List<string> ParseStringArray(JSONNode node)
        {
            var list = new List<string>();
            if (node == null || !node.IsArray) return list;
            foreach (JSONNode n in node.AsArray)
                if (n != null && !n.IsNull) list.Add(n.Value);
            return list;
        }

        private static List<NarrativeSeed> ParseSeedsArray(JSONNode node)
        {
            var list = new List<NarrativeSeed>();
            if (node == null || !node.IsArray) return list;
            int idx = 0;
            foreach (JSONNode n in node.AsArray)
            {
                if (n == null || n.IsNull) continue;
                list.Add(new NarrativeSeed(
                    n["id"]?.Value ?? $"seed_{idx}",
                    n["description"]?.Value ?? "A mystery yet to unfold",
                    n["suggestedIncidentDefName"]?.Value ?? "",
                    0));
                idx++;
            }
            return list;
        }

        private static string ExtractJsonFromMarkdown(string content)
        {
            var match = MarkdownCodeBlock.Match(content ?? "");
            if (match.Success) return match.Groups[1].Value.Trim();
            return content?.Trim() ?? "";
        }

        private static int Mathf(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
