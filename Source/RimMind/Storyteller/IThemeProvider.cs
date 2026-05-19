namespace RimMind.Storyteller
{
    /// <summary>
    /// Interface for narrative theme providers. Themes control voice, tone,
    /// naming conventions, and flavor text for the AI storyteller.
    /// </summary>
    public interface IThemeProvider
    {
        string ThemeId { get; }
        string ThemeName { get; }
        string SystemPrompt { get; }
        string PlannerPrompt { get; }
        string CampaignPrompt { get; }
        string EventFramePrompt { get; }
        string OutcomePrompt { get; }

        /// <summary>
        /// Transform a vanilla incident label into themed text.
        /// </summary>
        string FrameLetterLabel(string incidentDefName, string baseLabel);

        /// <summary>
        /// Transform vanilla incident text into themed narrative text.
        /// </summary>
        string FrameLetterText(string incidentDefName, string baseText, PlotBeat beat);

        /// <summary>
        /// Get a themed name for a generated story thread.
        /// </summary>
        string NameThread(string concept);
    }
}
