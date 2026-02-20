using System.Collections.Generic;
using RimMind.Automation;
using Verse;

namespace RimMind.Core
{
    public class RimMindSettings : ModSettings
    {
        // Provider: "openrouter", "anthropic", "claudecode", or "custom"
        public string apiProvider = "openrouter";

        // OpenRouter settings
        public string apiKey = "";
        public string modelId = "anthropic/claude-sonnet-4-5";

        // Anthropic Direct API settings
        public string anthropicToken = "";
        public string anthropicModelId = "claude-haiku-4-5";

        // Claude Code subscription settings
        public string claudeCodeModelId = "claude-haiku-4-5";

        // Custom provider settings (OpenAI-compatible)
        public string customEndpointUrl = "";
        public string customApiKey = "";
        public string customModelId = "";

        // Shared settings
        public float temperature = 0.7f;
        public int maxTokens = 4096;
        public bool enableChatCompanion = true;
        public bool autoDetectDirectives = true;

        // Event Automation settings
        public bool enableEventAutomation = false;
        public Dictionary<string, AutomationRule> automationRules = new Dictionary<string, AutomationRule>();

        public bool IsAnthropic => apiProvider == "anthropic";
        public bool IsClaudeCode => apiProvider == "claudecode";
        public bool IsCustom => apiProvider == "custom";

        public string ActiveModelId
        {
            get
            {
                if (IsClaudeCode) return claudeCodeModelId;
                if (IsAnthropic) return anthropicModelId;
                if (IsCustom) return customModelId;
                return modelId;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiProvider, "apiProvider", "openrouter");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref modelId, "modelId", "anthropic/claude-sonnet-4-5");
            Scribe_Values.Look(ref anthropicToken, "anthropicToken", "");
            Scribe_Values.Look(ref anthropicModelId, "anthropicModelId", "claude-haiku-4-5");
            Scribe_Values.Look(ref claudeCodeModelId, "claudeCodeModelId", "claude-haiku-4-5");
            Scribe_Values.Look(ref customEndpointUrl, "customEndpointUrl", "");
            Scribe_Values.Look(ref customApiKey, "customApiKey", "");
            Scribe_Values.Look(ref customModelId, "customModelId", "");
            Scribe_Values.Look(ref temperature, "temperature", 0.7f);
            Scribe_Values.Look(ref maxTokens, "maxTokens", 4096);
            Scribe_Values.Look(ref enableChatCompanion, "enableChatCompanion", true);
            Scribe_Values.Look(ref autoDetectDirectives, "autoDetectDirectives", true);
            Scribe_Values.Look(ref enableEventAutomation, "enableEventAutomation", false);
            Scribe_Collections.Look(ref automationRules, "automationRules", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars && automationRules == null)
            {
                automationRules = new Dictionary<string, AutomationRule>();
            }

            base.ExposeData();
        }
    }
}
