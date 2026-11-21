using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using Agent.Core.Providers;
using Agent.Models;
using Agent.Mvvm;
using Agent.Toolkit.Wpf.Converters;
using Agent.Toolkit.Wpf.Theming;
using Material.Icons;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Generic settings panel shared by the agent hosts: the save button, the section navigation and
    /// the generic sections (API, Native tools, General, Sound).
    /// <para>
    /// DataContext contract: a view model exposing <c>Settings</c> (<c>Agent.Models.SettingsModel</c>)
    /// and the <c>Cmd</c> command dispatcher (an <c>Agent.Mvvm.BaseViewModel</c>). Host-specific
    /// sections are injected with <see cref="InsertHostSection"/>.
    /// </para>
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private const int SettingsToastAutoHideSeconds = 6;
        private SettingsModel _settingsModel;

        public SettingsControl()
        {
            InitializeComponent();
            ToastBanner.CloseRequested += OnToastClose;
            AgentThemeManager.EnsureInitialized();
            ProviderCombo.ItemsSource = AiProviderCatalog.All;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachSettingsEvents();
            _settingsModel = GetSettingsModel();
            AttachSettingsEvents();

            // Apply the persisted ownership mode first, then honor it.
            var mode = GetThemeMode();
            AgentThemeManager.SetMode(mode);
            ThemeCombo.IsEnabled = mode == AgentThemeMode.Manual;

            // In Auto mode the host owns the theme and must not be overridden here.
            if (mode != AgentThemeMode.Manual)
                return;

            AgentThemeManager.ApplyFromSettings(GetBaseTheme());
        }

        private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The combo only drives the theme in Manual mode (the toolkit owns it).
            if (AgentThemeManager.Mode != AgentThemeMode.Manual)
                return;

            AgentThemeManager.ApplyFromSettings(ThemeCombo.SelectedIndex == 1);
        }

        private void OnThemeModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeModeCombo == null || ThemeModeCombo.SelectedIndex < 0)
                return;

            var mode = ThemeModeCombo.SelectedIndex == 1 ? AgentThemeMode.Auto : AgentThemeMode.Manual;
            AgentThemeManager.SetMode(mode);
            ThemeCombo.IsEnabled = mode == AgentThemeMode.Manual;

            if (mode == AgentThemeMode.Manual)
                AgentThemeManager.ApplyFromSettings(GetBaseTheme());
        }

        private string GetBaseTheme() => GetSettingsModel()?.BaseTheme ?? "Dark";

        private AgentThemeMode GetThemeMode()
        {
            var settings = GetSettingsModel();
            return string.Equals(settings?.ThemeMode, "Auto", StringComparison.OrdinalIgnoreCase)
                ? AgentThemeMode.Auto
                : AgentThemeMode.Manual;
        }

        private SettingsModel GetSettingsModel()
        {
            var dataContext = DataContext;
            if (dataContext == null)
                return null;

            var settingsProperty = dataContext.GetType().GetProperty("Settings");
            return settingsProperty?.GetValue(dataContext) as SettingsModel;
        }

        private void AttachSettingsEvents()
        {
            if (_settingsModel == null)
                return;

            _settingsModel.SettingsSaved += OnSettingsSaved;
            _settingsModel.Notify += OnSettingsNotify;
        }

        private void DetachSettingsEvents()
        {
            if (_settingsModel == null)
                return;

            _settingsModel.SettingsSaved -= OnSettingsSaved;
            _settingsModel.Notify -= OnSettingsNotify;
            _settingsModel = null;
        }

        private void OnSettingsSaved(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnSettingsSaved(sender, e)));
                return;
            }

            ShowToast(NotificationKind.Success, "Settings saved.");
        }

        private void OnSettingsNotify(object sender, Notify notify)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnSettingsNotify(sender, notify)));
                return;
            }

            if (notify == null)
                return;

            ShowToast((NotificationKind)notify.NotifyLevel, notify.Message ?? "");
        }

        private void ShowToast(NotificationKind kind, string message)
        {
            ToastBanner.Kind = kind;
            ToastBanner.Message = message;
            ToastBanner.AutoHideSeconds = SettingsToastAutoHideSeconds;
            ToastBanner.Visibility = Visibility.Visible;
        }

        private void OnToastClose(object sender, RoutedEventArgs e)
        {
            ToastBanner.Visibility = Visibility.Collapsed;
        }

        /// <summary>Describes a host-provided settings section.</summary>
        public sealed class SettingsSection
        {
            public string Title { get; set; }
            public MaterialIconKind OutlineIcon { get; set; }
            public MaterialIconKind FilledIcon { get; set; }
            public FrameworkElement Content { get; set; }
        }

        /// <summary>Inserts a host-provided section into both the navigation and the content tabs at <paramref name="index"/>.</summary>
        public void InsertHostSection(int index, SettingsSection section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            SettingsNav.Items.Insert(index, new ListBoxItem
            {
                ToolTip = section.Title,
                Content = BuildNavIcon(section.OutlineIcon, section.FilledIcon),
            });

            SettingsSubTabs.Items.Insert(index, new TabItem { Content = section.Content });
        }

        /// <summary>Inserts the toolkit's microphone device settings section at <paramref name="index"/>.</summary>
        public void InsertMicrophoneSection(int index)
        {
            InsertHostSection(index, new SettingsSection
            {
                Title = "Microphone",
                OutlineIcon = MaterialIconKind.MicrophoneOutline,
                FilledIcon = MaterialIconKind.Microphone,
                Content = new MicrophoneSettingsView()
            });
        }

        private Grid BuildNavIcon(MaterialIconKind outlineIcon, MaterialIconKind filledIcon)
        {
            var grid = new Grid();
            grid.Children.Add(BuildIconPath(outlineIcon, "SettingsNavIconOutlineStyle"));
            grid.Children.Add(BuildIconPath(filledIcon, "SettingsNavIconFilledStyle"));
            return grid;
        }

        private Path BuildIconPath(MaterialIconKind icon, string styleKey)
        {
            var path = new Path { Style = (Style)FindResource(styleKey) };
            var binding = new Binding { Source = icon, Converter = new MaterialIconToGeometryConverter() };
            path.SetBinding(Path.DataProperty, binding);
            return path;
        }

        private void OnBrowseMcpRegistry(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://registry.modelcontextprotocol.io");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open MCP registry: " + ex.Message);
            }
        }
    }
}
