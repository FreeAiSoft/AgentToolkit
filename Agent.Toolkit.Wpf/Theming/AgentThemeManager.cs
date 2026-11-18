using System;
using System.Windows;

namespace Agent.Toolkit.Wpf.Theming
{
    /// <summary>
    /// Applies the shared dark/light palette at runtime.
    /// <para>
    /// The toolkit controls reference theme colors through <c>DynamicResource</c> keys such as
    /// <c>Theme.Text</c> / <c>Theme.Window</c>. Those keys are defined in
    /// <c>Styles/Theme.Dark.xaml</c> and <c>Styles/Theme.Light.xaml</c> and are registered on
    /// <see cref="Application.Current.Resources"/> here. Because the references are dynamic, writing
    /// new brush values re-colors the already-open UI without recreating windows or controls.
    /// </para>
    /// <para>Hosts should call <see cref="Apply"/> once at startup with the persisted base theme
    /// (e.g. <c>SettingsStore.Load().BaseTheme</c>).</para>
    /// </summary>
    public static class AgentThemeManager
    {
        private static readonly object Gate = new object();

        private static ResourceDictionary _darkPalette;
        private static ResourceDictionary _lightPalette;
        private static string _current = "Dark";

        /// <summary>Currently applied base theme: "Dark" or "Light".</summary>
        public static string Current => _current;

        /// <summary>Raised after the palette has been swapped.</summary>
        public static event EventHandler ThemeChanged;

        /// <summary>
        /// Seeds the default (dark) palette if it has not been registered yet. Called by the toolkit
        /// controls so they keep working even when the host forgets to apply a theme at startup.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (Application.Current == null)
                return;

            lock (Gate)
            {
                EnsurePalettes();
                if (!Application.Current.Resources.Contains(ThemeWindowKey))
                    WritePalette(_darkPalette, "Dark");
            }
        }

        /// <summary>Applies a base theme by name: "Dark" or "Light".</summary>
        public static void Apply(string baseTheme)
        {
            Apply(string.Equals(baseTheme, "Light", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Applies the light theme when <paramref name="light"/> is true, otherwise the dark theme.</summary>
        public static void Apply(bool light)
        {
            EnsurePalettes();
            WritePalette(light ? _lightPalette : _darkPalette, light ? "Light" : "Dark");
        }

        /// <summary>Applies the dark theme.</summary>
        public static void ApplyDark() => Apply(false);

        /// <summary>Applies the light theme.</summary>
        public static void ApplyLight() => Apply(true);

        private const string ThemeWindowKey = "Theme.Window";

        private static void EnsurePalettes()
        {
            if (_darkPalette == null)
                _darkPalette = LoadPalette("Theme.Dark.xaml");
            if (_lightPalette == null)
                _lightPalette = LoadPalette("Theme.Light.xaml");
        }

        private static ResourceDictionary LoadPalette(string fileName)
        {
            return new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Agent.Toolkit.Wpf;component/Styles/" + fileName,
                    UriKind.Absolute)
            };
        }

        private static void WritePalette(ResourceDictionary palette, string name)
        {
            var app = Application.Current;
            if (app == null)
            {
                _current = name;
                return;
            }

            var resources = app.Resources;
            foreach (var key in palette.Keys)
            {
                resources[key] = palette[key];
            }

            if (_current == name)
                return;

            _current = name;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
