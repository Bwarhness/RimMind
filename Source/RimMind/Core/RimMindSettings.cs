using Verse;

namespace RimMind.Core
{
    public class RimMindSettings : ModSettings
    {
        public string apiKey = "";
        public string modelId = "anthropic/claude-sonnet-4-5";
        public float temperature = 0.7f;
        public int maxTokens = 1024;
        public bool enableChatCompanion = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref modelId, "modelId", "anthropic/claude-sonnet-4-5");
            Scribe_Values.Look(ref temperature, "temperature", 0.7f);
            Scribe_Values.Look(ref maxTokens, "maxTokens", 1024);
            Scribe_Values.Look(ref enableChatCompanion, "enableChatCompanion", true);
            base.ExposeData();
        }
    }
}
