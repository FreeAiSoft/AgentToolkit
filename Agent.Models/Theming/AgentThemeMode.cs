namespace Agent.Models
{
    /// <summary>
    /// Controls who owns the shared toolkit theme.
    /// </summary>
    public enum AgentThemeMode
    {
        /// <summary>
        /// The toolkit owns the theme: only the "Theme" combo in <c>SettingsControl</c> applies it.
        /// Host calls to <c>AgentThemeManager.Apply(...)</c> are ignored.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// The host owns the theme: it applies its persisted theme through
        /// <c>AgentThemeManager.Apply(...)</c>, and <c>SettingsControl</c> only reflects it.
        /// </summary>
        Auto = 1
    }
}
