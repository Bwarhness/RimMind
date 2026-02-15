using UnityEngine;
using Verse;

namespace RimMind.Core
{
    public class RimMindMod : Mod
    {
        public static RimMindSettings Settings { get; private set; }

        public RimMindMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindSettings>();
            Log.Message("[RimMind] Mod loaded successfully.");
            DebugLogger.Init();
        }

        public override string SettingsCategory() => "RimMind AI";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Provider selection
            listing.Label("AI Provider:");
            listing.Gap(4f);

            bool isOpenRouter = Settings.apiProvider == "openrouter";
            bool isAnthropic = Settings.apiProvider == "anthropic";
            bool isClaudeCode = Settings.apiProvider == "claudecode";

            if (listing.RadioButton("OpenRouter", isOpenRouter))
            {
                Settings.apiProvider = "openrouter";
            }
            if (listing.RadioButton("Anthropic (Direct API Key)", isAnthropic))
            {
                Settings.apiProvider = "anthropic";
            }
            if (listing.RadioButton("Claude Code (Max/Pro Subscription)", isClaudeCode))
            {
                Settings.apiProvider = "claudecode";
            }

            listing.GapLine();

            if (Settings.IsClaudeCode)
            {
                bool available = RimMind.API.ClaudeCodeAuth.IsAvailable();
                if (available)
                {
                    listing.Label("<color=#88ff88>Claude Code credentials detected. Your subscription will be used automatically.</color>");
                }
                else
                {
                    listing.Label("<color=#ff8888>Claude Code not logged in. Run 'claude login' in a terminal.</color>");
                }

                listing.GapLine();

                listing.Label("Model ID (e.g. claude-sonnet-4-5-20250929, claude-opus-4-6):");
                Settings.claudeCodeModelId = listing.TextEntry(Settings.claudeCodeModelId);
            }
            else if (Settings.IsAnthropic)
            {
                listing.Label("Anthropic API Key:");
                Settings.anthropicToken = listing.TextEntry(Settings.anthropicToken);
                listing.Gap(4f);
                if (listing.ButtonText("Get an API key at console.anthropic.com"))
                {
                    Application.OpenURL("https://console.anthropic.com/settings/keys");
                }

                listing.GapLine();

                listing.Label("Model ID (e.g. claude-sonnet-4-5-20250929, claude-opus-4-6):");
                Settings.anthropicModelId = listing.TextEntry(Settings.anthropicModelId);
            }
            else
            {
                listing.Label("OpenRouter API Key:");
                Settings.apiKey = listing.TextEntry(Settings.apiKey);
                listing.Gap(4f);
                if (listing.ButtonText("Get an API key at openrouter.ai"))
                {
                    Application.OpenURL("https://openrouter.ai/keys");
                }

                listing.GapLine();

                listing.Label("Model ID (e.g. anthropic/claude-sonnet-4-5, openai/gpt-4o):");
                Settings.modelId = listing.TextEntry(Settings.modelId);
            }

            listing.GapLine();

            listing.Label("Temperature: " + Settings.temperature.ToString("F2"));
            Settings.temperature = listing.Slider(Settings.temperature, 0f, 2f);

            listing.Label("Max Tokens: " + Settings.maxTokens);
            Settings.maxTokens = (int)listing.Slider(Settings.maxTokens, 128, 4096);

            listing.GapLine();

            listing.CheckboxLabeled("Enable Chat Companion", ref Settings.enableChatCompanion);
            listing.CheckboxLabeled("Auto-detect Directives", ref Settings.autoDetectDirectives, "When enabled, the AI will detect playstyle preferences during conversation and offer to save them as colony directives.");

            listing.End();
        }
    }
}
