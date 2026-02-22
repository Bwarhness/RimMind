using System.Collections.Generic;
using RimMind.Languages;

namespace RimMind.Automation
{
    /// <summary>
    /// Provides default automation prompts for common event types.
    /// </summary>
    public static class DefaultAutomationPrompts
    {
        private static readonly Dictionary<string, string> defaults = new Dictionary<string, string>
        {
            // Combat & Threats
            ["RaidEnemy"] = "Draft all combat-capable colonists. Equip best available weapons (rifles to shooters, melee to brawlers). Position behind defensive structures. Close all exterior doors.",
            ["RaidFriendly"] = "Analyze friendly raid composition. Suggest if we should support them in combat.",
            ["PredatorsDeadlyHunt"] = "Draft all colonists. Move everyone indoors. Close and lock all exterior doors. Wait for manhunters to leave or die.",
            ["Siege"] = "Draft combat colonists. Prepare counter-attack force. Check if we have mortar shells for counter-battery fire.",
            ["MechanoidCluster"] = "Scout mechanoid cluster composition. Suggest attack strategy. Check for EMP weapons in stockpile.",
            
            // Emergencies
            ["FireStarted"] = "Assign 3 colonists to firefighting immediately. Forbid flammable items near fire area. Check for chemfuel storage nearby. Open vents if temperature is rising.",
            ["Infestation"] = "Close all interior doors to contain infestation. Draft combat-capable colonists. Equip fire-based weapons if available. Consider temperature-based elimination.",
            ["ToxicFallout"] = "Restrict all colonists to indoor areas. Suspend all outdoor jobs (mining, construction, planting). Check food stockpile - can we last the duration?",
            ["SolarFlare"] = "Disable non-essential power consumers. Prepare for darkness and cold. Check battery reserves.",
            ["Eclipse"] = "Activate backup power sources. Prepare lighting in critical areas. Monitor temperature - heaters may need activation.",
            ["ColdSnap"] = "Activate all heaters. Check colonist clothing - assign parkas if needed. Monitor for hypothermia.",
            ["HeatWave"] = "Activate coolers. Check for heat stroke risks. Move vulnerable colonists to cooled rooms.",
            
            // Medical & Mental
            ["MentalBreakExtreme"] = "Arrest the colonist immediately if violent. Move other colonists away from area. Lock doors if safe to do so.",
            ["MentalBreakMajor"] = "Monitor colonist behavior. Prepare arrest if becomes dangerous. Check what triggered break - can we fix it?",
            ["MentalBreakMinor"] = "Assign recreation time. Check colonist needs - joy, comfort, rest. Suggest mood improvements.",
            ["DiseaseContracted"] = "Assign best doctor to patient. Check medicine stockpile. Prepare hospital bed if needed.",
            ["DiseaseOutbreak"] = "All doctors focus on medical work. Check medicine production. Consider quarantine area for plague.",
            ["Death"] = "Manage colony mood - expect negative thoughts. Plan burial or cremation. Check if anyone had close relationships.",
            
            // Social & Events
            ["WandererJoin"] = "Assign work priorities based on new colonist's skills. Find available bed. Set appropriate outfit and drug policy.",
            ["RefugeeChased"] = "Analyze refugee's skills and traits. If accepting, prepare for immediate raid. If declining, explain why.",
            ["StrangerInBlackJoin"] = "Review stranger's skills and traits. Assign work priorities and equipment.",
            ["Marriage"] = "Assign double bed for married couple. Congratulate them (colonist morale boost if we celebrate).",
            
            // Trade & Diplomacy
            ["TraderArrival"] = "Analyze trade opportunity. Check what we need: medicine, components, advanced components, food, silver. Suggest purchases.",
            ["CaravanRequest"] = "Analyze caravan request - is the reward worth the risk and resources?",
            
            // Resources
            ["ResearchProjectFinished"] = "Suggest next research project based on colony needs. Check if new research unlocks important buildings or items.",
        };

        public static string Get(string eventType)
        {
            if (defaults.TryGetValue(eventType, out string prompt))
            {
                return prompt;
            }
            return $"Analyze the event: {eventType}. Suggest appropriate responses based on colony status.";
        }

        public static bool HasDefault(string eventType)
        {
            return defaults.ContainsKey(eventType);
        }

        /// <summary>
        /// Returns all event types that have default prompts.
        /// </summary>
        public static IEnumerable<string> GetKnownEventTypes()
        {
            return defaults.Keys;
        }

        /// <summary>
        /// Returns a translated display name for an event type.
        /// Falls back to the raw eventType if no translation key exists.
        /// </summary>
        public static string GetDisplayName(string eventType)
        {
            string key = "RimMind_Event_" + eventType;
            var translated = RimMindTranslations.Get(key);
            // If translation system returns the key itself, it wasn't found
            if (translated == key)
                return eventType;
            return translated;
        }

        /// <summary>
        /// Categorizes event types for UI organization.
        /// </summary>
        public static string GetCategory(string eventType)
        {
            if (eventType.Contains("Raid") || eventType.Contains("Siege") || eventType.Contains("Mechanoid"))
                return RimMindTranslations.Get("RimMind_CategoryCombat");
            if (eventType.Contains("Fire") || eventType.Contains("Infestation") || eventType.Contains("Toxic") || 
                eventType.Contains("Solar") || eventType.Contains("Eclipse") || eventType.Contains("Cold") || 
                eventType.Contains("Heat"))
                return RimMindTranslations.Get("RimMind_CategoryEmergencies");
            if (eventType.Contains("Mental") || eventType.Contains("Disease") || eventType.Contains("Death"))
                return RimMindTranslations.Get("RimMind_CategoryMedical");
            if (eventType.Contains("Wanderer") || eventType.Contains("Refugee") || eventType.Contains("Stranger") || 
                eventType.Contains("Marriage"))
                return RimMindTranslations.Get("RimMind_CategorySocial");
            if (eventType.Contains("Trader") || eventType.Contains("Caravan"))
                return RimMindTranslations.Get("RimMind_CategoryTrade");
            if (eventType.Contains("Research"))
                return RimMindTranslations.Get("RimMind_CategoryResources");
            return RimMindTranslations.Get("RimMind_CategoryOther");
        }
    }
}
