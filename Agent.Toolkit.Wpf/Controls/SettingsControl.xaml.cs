using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using Agent.Models;
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
        public SettingsControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AgentThemeManager.Apply(GetBaseTheme());
        }

        private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedIndex == 1)
                AgentThemeManager.ApplyLight();
            else
                AgentThemeManager.ApplyDark();
        }

        private string GetBaseTheme()
        {
            var dataContext = DataContext;
            if (dataContext == null)
                return "Dark";

            var settingsProperty = dataContext.GetType().GetProperty("Settings");
            var settings = settingsProperty?.GetValue(dataContext) as SettingsModel;
            return settings?.BaseTheme ?? "Dark";
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
