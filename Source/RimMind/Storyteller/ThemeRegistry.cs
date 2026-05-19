using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Registry for narrative theme providers. New themes can be added here.
    /// </summary>
    public static class ThemeRegistry
    {
        private static readonly Dictionary<string, IThemeProvider> themes = new Dictionary<string, IThemeProvider>();
        private static bool initialized = false;

        public static void Init()
        {
            if (initialized) return;
            initialized = true;

            Register(new ChronicleThemeProvider());
            Register(new LotrThemeProvider());
        }

        public static void Register(IThemeProvider theme)
        {
            themes[theme.ThemeId] = theme;
            Log.Message($"[RimMind] Registered storyteller theme: {theme.ThemeName} ({theme.ThemeId})");
        }

        public static IThemeProvider Get(string themeId)
        {
            if (!initialized) Init();
            if (themes.TryGetValue(themeId, out var theme))
                return theme;
            return themes.ContainsKey("chronicle") ? themes["chronicle"] : null;
        }

        public static List<IThemeProvider> AllThemes
        {
            get
            {
                if (!initialized) Init();
                return themes.Values.ToList();
            }
        }

        public static List<string> AllThemeIds
        {
            get
            {
                if (!initialized) Init();
                return themes.Keys.ToList();
            }
        }
    }
}
