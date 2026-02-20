using Verse;

namespace RimMind.Languages
{
    /// <summary>
    /// Helper class for accessing RimMind translations.
    /// Uses RimWorld's built-in translation system.
    /// </summary>
    public static class RimMindTranslations
    {
        /// <summary>
        /// Get a translated string by key. Returns the key wrapped in Translate() if not found.
        /// </summary>
        public static string Get(string key)
        {
            var translated = key.Translate();
            return translated.ToString();
        }

        /// <summary>
        /// Get a translated string with format args.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            var translated = key.Translate(args);
            return translated.ToString();
        }

        /// <summary>
        /// Get a translated string from the current language.
        /// </summary>
        public static TaggedString GetTagged(string key)
        {
            return key.Translate();
        }

        /// <summary>
        /// Get a translated TaggedString with format args.
        /// </summary>
        public static TaggedString GetTagged(string key, params object[] args)
        {
            return key.Translate(args);
        }
    }
}
