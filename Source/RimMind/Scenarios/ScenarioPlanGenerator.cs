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
                "You are also designing the player's starting state for a RimWorld colony. " +
                "Return a single JSON object with this schema:\n\n" +
                "{\n" +
                "  \"campaign\": {\n" +
                "    \"setting\": string, \"incitingIncident\": string, \"currentAct\": string,\n" +
                "    \"activeForces\": [string,...], \"pendingThreat\": string, \"opportunity\": string,\n" +
                "    \"plantedSeeds\": [{\"id\": string, \"description\": string, \"suggestedIncidentDefName\": string}, ...]\n" +
                "  },\n" +
                "  \"pawns\": [\n" +
                "    {\n" +
                "      \"firstName\": string, \"nickName\": string|null, \"lastName\": string,\n" +
                "      \"gender\": \"Male\"|\"Female\",\n" +
                "      \"childhoodBackstory\": string, \"adulthoodBackstory\": string,\n" +
                "      \"traits\": [string,...],   // vanilla TraitDef defNames: Industrious, Pessimist, Bloodlust, Pyromaniac, Kind, Brawler, etc.\n" +
                "      \"skills\": { \"Shooting\": 0-20, \"Melee\": 0-20, \"Construction\": 0-20, \"Mining\": 0-20, \"Cooking\": 0-20, \"Plants\": 0-20, \"Animals\": 0-20, \"Crafting\": 0-20, \"Artistic\": 0-20, \"Medicine\": 0-20, \"Social\": 0-20, \"Intellectual\": 0-20 },\n" +
                "      \"passions\": { \"<SkillDef>\": \"Minor\"|\"Major\" },\n" +
                "      \"narrative\": string\n" +
                "    }, ... typically 3 pawns\n" +
                "  ],\n" +
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
                "- Skill levels: 0-20 total budget ~50 per pawn (no all-20s). Show specialization.\n" +
                "- 2-3 traits per pawn, must be valid vanilla TraitDef defNames.\n" +
                "- Items: 8-15 entries appropriate to the story (food, medicine, raw materials, weapons, apparel).\n" +
                "- The starting state must match the user's pitch tonally. Tribal pitch -> low-tech items. Space-colonist pitch -> tech items.\n" +
                "- All defNames must exist in vanilla RimWorld. If unsure, omit rather than invent.\n";
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
                            ChildhoodBackstory = n["childhoodBackstory"]?.Value,
                            AdulthoodBackstory = n["adulthoodBackstory"]?.Value,
                            Traits = ParseStringArray(n["traits"]),
                            Narrative = n["narrative"]?.Value
                        };

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
