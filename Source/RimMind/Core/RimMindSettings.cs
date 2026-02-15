using Verse;

namespace RimMind.Core
{
    public class RimMindSettings : ModSettings
    {
        // Provider: "openrouter" or "anthropic"
        public string apiProvider = "openrouter";

        // OpenRouter settings
        public string apiKey = "";
        public string modelId = "anthropic/claude-sonnet-4-5";

        // Anthropic Direct API settings
        public string anthropicToken = "";
        public string anthropicModelId = "claude-sonnet-4-5-20250929";

        // Claude Code subscription settings
        public string claudeCodeModelId = "claude-sonnet-4-5-20250929";

        // Shared settings
        public float temperature = 0.7f;
        public int maxTokens = 4096;
        public bool enableChatCompanion = true;
        public bool autoDetectDirectives = true;

        public bool IsAnthropic => apiProvider == "anthropic";
        public bool IsClaudeCode => apiProvider == "claudecode";

        public string ActiveModelId
        {
            get
            {
                if (IsClaudeCode) return claudeCodeModelId;
                if (IsAnthropic) return anthropicModelId;
                return modelId;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiProvider, "apiProvider", "openrouter");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref modelId, "modelId", "anthropic/claude-sonnet-4-5");
            Scribe_Values.Look(ref anthropicToken, "anthropicToken", "");
            Scribe_Values.Look(ref anthropicModelId, "anthropicModelId", "claude-sonnet-4-5-20250929");
            Scribe_Values.Look(ref claudeCodeModelId, "claudeCodeModelId", "claude-sonnet-4-5-20250929");
            Scribe_Values.Look(ref temperature, "temperature", 0.7f);
            Scribe_Values.Look(ref maxTokens, "maxTokens", 4096);
            Scribe_Values.Look(ref enableChatCompanion, "enableChatCompanion", true);
            Scribe_Values.Look(ref autoDetectDirectives, "autoDetectDirectives", true);
            base.ExposeData();
        }
    }
}
