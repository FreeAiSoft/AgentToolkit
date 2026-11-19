using System;

namespace Agent.Models
{
    /// <summary>
    /// UI-free state machine that backs the WPF theme manager. Kept in the model layer so the host
    /// contract (Manual/Auto modes and change detection) can be unit-tested without a WPF Application.
    /// </summary>
    public sealed class ThemeModeState
    {
        private AgentThemeMode _mode = AgentThemeMode.Manual;
        private string _current = "Dark";

        /// <summary>Currently applied base theme: "Dark" or "Light".</summary>
        public string Current => _current;

        /// <summary>Who owns the toolkit theme (Manual = toolkit, Auto = host).</summary>
        public AgentThemeMode Mode => _mode;

        /// <summary>Selects who owns the toolkit theme.</summary>
        public void SetMode(AgentThemeMode mode) => _mode = mode;

        /// <summary>True when a host-initiated Apply should change the palette (Auto mode).</summary>
        public bool AcceptsHostApply => _mode == AgentThemeMode.Auto;

        /// <summary>True when the toolkit (SettingsControl combo) may apply the theme (Manual mode).</summary>
        public bool AcceptsToolkitApply => _mode == AgentThemeMode.Manual;

        /// <summary>
        /// Records a palette swap and returns true when the theme actually changed (so callers can raise
        /// the changed event exactly once); returns false when <paramref name="name"/> is the current theme.
        /// Comparison is case-insensitive.
        /// </summary>
        public bool TryChange(string name)
        {
            if (string.Equals(_current, name, StringComparison.OrdinalIgnoreCase))
                return false;

            _current = name;
            return true;
        }

        /// <summary>Records the current theme without reporting a change (used when no Application exists).</summary>
        public void Record(string name) => _current = name;
    }
}
