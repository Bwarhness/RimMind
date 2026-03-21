using HarmonyLib;
using UnityEngine;
using Verse;
using RimMind.Languages;

namespace RimMind.Core
{
    public class RimMindMod : Mod
    {
        public static RimMindSettings Settings { get; private set; }

        public RimMindMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindSettings>();
            DebugLogger.Init(content.RootDir);

            // Apply Harmony patches
            try
            {
                var harmony = new Harmony("com.rimmind.mod");
                harmony.PatchAll();

                // Manual patch for LetterStack.ReceiveLetter (3 overloads in RimWorld 1.6,
                // attribute-based patching causes AmbiguousMatchException)
                Automation.LetterAutomationPatch.Apply(harmony);

                var patched = harmony.GetPatchedMethods();
                int count = 0;
                foreach (var m in patched) count++;
                Log.Message("[RimMind] Harmony patches applied: " + count + " method(s) patched.");
            }
            catch (System.Exception ex)
            {
                Log.Error("[RimMind] Harmony patching failed: " + ex);
            }

            Log.Message("[RimMind] Mod loaded successfully.");
        }

        public override string SettingsCategory() => RimMindTranslations.Get("RimMind_SettingsCategory");

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Provider selection
            listing.Label(RimMindTranslations.Get("RimMind_AIProvider"));
            listing.Gap(4f);

            bool isOpenRouter = Settings.apiProvider == "openrouter";
            bool isAnthropic = Settings.apiProvider == "anthropic";
            bool isClaudeCode = Settings.apiProvider == "claudecode";
            bool isCustom = Settings.apiProvider == "custom";

            if (listing.RadioButton(RimMindTranslations.Get("RimMind_OpenRouter"), isOpenRouter))
            {
                Settings.apiProvider = "openrouter";
            }
            if (listing.RadioButton(RimMindTranslations.Get("RimMind_AnthropicDirect"), isAnthropic))
            {
                Settings.apiProvider = "anthropic";
            }
            if (listing.RadioButton(RimMindTranslations.Get("RimMind_ClaudeCode"), isClaudeCode))
            {
                Settings.apiProvider = "claudecode";
            }
            if (listing.RadioButton(RimMindTranslations.Get("RimMind_CustomProvider"), isCustom))
            {
                Settings.apiProvider = "custom";
            }

            listing.GapLine();

            if (Settings.IsCustom)
            {
                listing.Label("<b>" + RimMindTranslations.Get("RimMind_CustomProviderSettings") + "</b>");
                listing.Label(RimMindTranslations.Get("RimMind_CustomProviderDesc"));
                listing.Gap(8f);

                listing.Label(RimMindTranslations.Get("RimMind_EndpointURL"));
                Settings.customEndpointUrl = listing.TextEntry(Settings.customEndpointUrl);
                listing.Label(RimMindTranslations.Get("RimMind_EndpointURLExamples"));
                listing.Gap(4f);

                listing.Label(RimMindTranslations.Get("RimMind_APIKeyOptional"));
                Settings.customApiKey = listing.TextEntry(Settings.customApiKey);
                listing.Gap(4f);

                listing.Label(RimMindTranslations.Get("RimMind_ModelName"));
                Settings.customModelId = listing.TextEntry(Settings.customModelId);
                listing.Label(RimMindTranslations.Get("RimMind_ModelNameExamples"));
                listing.Gap(8f);

                listing.CheckboxLabeled(RimMindTranslations.Get("RimMind_SupportsToolCalling"), ref Settings.customProviderSupportsTools, RimMindTranslations.Get("RimMind_SupportsToolCallingDesc"));
                listing.GapLine();
            }
            else if (Settings.IsClaudeCode)
            {
                bool available = RimMind.API.ClaudeCodeAuth.IsAvailable();
                if (available)
                {
                    listing.Label("<color=#88ff88>" + RimMindTranslations.Get("RimMind_ClaudeCodeAvailable") + "</color>");
                }
                else
                {
                    listing.Label("<color=#ff8888>" + RimMindTranslations.Get("RimMind_ClaudeCodeNotAvailable") + "</color>");
                }

                listing.GapLine();

                listing.Label(RimMindTranslations.Get("RimMind_ModelIDAnthropic"));
                Settings.claudeCodeModelId = listing.TextEntry(Settings.claudeCodeModelId);
            }
            else if (Settings.IsAnthropic)
            {
                listing.Label(RimMindTranslations.Get("RimMind_AnthropicAPIKey"));
                Settings.anthropicToken = listing.TextEntry(Settings.anthropicToken);
                listing.Gap(4f);
                if (listing.ButtonText(RimMindTranslations.Get("RimMind_GetAPIKeyAnthropic")))
                {
                    Application.OpenURL("https://console.anthropic.com/settings/keys");
                }

                listing.GapLine();

                listing.Label(RimMindTranslations.Get("RimMind_ModelIDAnthropic"));
                Settings.anthropicModelId = listing.TextEntry(Settings.anthropicModelId);
            }
            else
            {
                listing.Label(RimMindTranslations.Get("RimMind_OpenRouterAPIKey"));
                Settings.apiKey = listing.TextEntry(Settings.apiKey);
                listing.Gap(4f);
                if (listing.ButtonText(RimMindTranslations.Get("RimMind_GetAPIKeyOpenRouter")))
                {
                    Application.OpenURL("https://openrouter.ai/keys");
                }

                listing.GapLine();

                listing.Label(RimMindTranslations.Get("RimMind_ModelID"));
                Settings.modelId = listing.TextEntry(Settings.modelId);
            }

            listing.GapLine();

            listing.Label(RimMindTranslations.Get("RimMind_Temperature", Settings.temperature.ToString("F2")));
            Settings.temperature = listing.Slider(Settings.temperature, 0f, 2f);

            listing.Label(RimMindTranslations.Get("RimMind_MaxTokens", Settings.maxTokens.ToString()));
            Settings.maxTokens = (int)listing.Slider(Settings.maxTokens, 128, 4096);

            listing.GapLine();

            listing.CheckboxLabeled(RimMindTranslations.Get("RimMind_EnableChatCompanion"), ref Settings.enableChatCompanion);
            listing.CheckboxLabeled(RimMindTranslations.Get("RimMind_AutoDetectDirectives"), ref Settings.autoDetectDirectives, RimMindTranslations.Get("RimMind_AutoDetectDirectivesDesc"));

            listing.GapLine();

            // Event Automation
            listing.Label("<b>" + RimMindTranslations.Get("RimMind_EventAutomation") + "</b>");
            listing.CheckboxLabeled(RimMindTranslations.Get("RimMind_EnableEventAutomation"), ref Settings.enableEventAutomation, RimMindTranslations.Get("RimMind_EnableEventAutomationDesc"));
            
            if (listing.ButtonText(RimMindTranslations.Get("RimMind_ConfigureAutomationRules")))
            {
                Find.WindowStack.Add(new RimMind.Automation.AutomationSettingsWindow());
            }

            listing.End();
        }
    }
}
