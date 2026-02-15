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

            listing.GapLine();

            listing.Label("Temperature: " + Settings.temperature.ToString("F2"));
            Settings.temperature = listing.Slider(Settings.temperature, 0f, 2f);

            listing.Label("Max Tokens: " + Settings.maxTokens);
            Settings.maxTokens = (int)listing.Slider(Settings.maxTokens, 128, 4096);

            listing.GapLine();

            listing.CheckboxLabeled("Enable Chat Companion", ref Settings.enableChatCompanion);

            listing.End();
        }
    }
}
