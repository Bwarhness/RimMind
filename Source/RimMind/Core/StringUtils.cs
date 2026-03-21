using System;
using System.Text;

namespace RimMind.Core
{
    /// <summary>
    /// Utility methods for safe string handling.
    /// </summary>
    public static class StringUtils
    {
        /// <summary>
        /// Removes lone/invalid Unicode surrogates from a string so it can be safely
        /// encoded as UTF-8. RimWorld game data can contain such characters (e.g. from
        /// mods or localization), which cause Encoding.UTF8.GetBytes to throw
        /// "Illegal byte sequence encountered in the input. Parameter name: string".
        /// </summary>
        public static string SanitizeForUTF8(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsHighSurrogate(c))
                {
                    // Only include if followed by a valid low surrogate (valid surrogate pair)
                    if (i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    {
                        sb.Append(c);
                        sb.Append(input[++i]);
                    }
                    // else: lone high surrogate — drop it
                }
                else if (char.IsLowSurrogate(c))
                {
                    // Lone low surrogate — drop it
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
