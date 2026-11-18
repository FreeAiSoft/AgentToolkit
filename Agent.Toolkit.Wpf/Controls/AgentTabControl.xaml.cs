using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using Agent.Core.Models;
using Agent.Toolkit.Wpf.Converters;
using Agent.Toolkit.Wpf.Theming;
using Material.Icons;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Top-level tab control owned by the core toolkit. It renders the tab chrome, owns the
    /// <c>Chat</c> and <c>Settings</c> tabs (built from <see cref="CoreTabs"/>, the single source
    /// of truth for their keys/headers/icons), and hosts domain-specific tabs that the host
    /// registers at load time via <see cref="AddHostTab"/>.
    /// </summary>
    public partial class AgentTabControl : TabControl
    {
        /// <summary>
        /// Creates the control, applies the shared tab chrome and builds the core Chat/Settings tabs.
        /// </summary>
        public AgentTabControl()
        {
            InitializeComponent();
            AgentThemeManager.EnsureInitialized();

            // An implicit TabControl style is not picked up by a subclass, so apply the named
            // chrome style explicitly (otherwise the TabPanel template is lost).
            Style = (Style)FindResource("MainTabControlStyle");

            ChatPanel = new ChatControl();
            SettingsPanel = new SettingsControl();

            AddTab(CoreTabs.Chat, ChatPanel);
            AddTab(CoreTabs.Settings, SettingsPanel);
        }

        /// <summary>The chat tab content, exposed so the host can wire <see cref="ChatControl.Host"/> and friends.</summary>
        public ChatControl ChatPanel { get; }

        /// <summary>The settings tab content, exposed so the host can attach a view model.</summary>
        public SettingsControl SettingsPanel { get; }

        /// <summary>Key of the currently selected tab, or <c>null</c> when no tab is selected.</summary>
        public string SelectedTabKey => (SelectedItem as TabItem)?.Tag as string;

        /// <summary>
        /// Registers a host-owned tab. Validates <paramref name="definition"/> (non-null, unique key),
        /// builds the icon header, applies the tab item style and appends the tab to the control.
        /// </summary>
        /// <param name="definition">UI-free tab descriptor carrying key, header, tooltip and icon names.</param>
        /// <param name="content">The tab body (typically a host <see cref="UserControl"/>).</param>
        /// <returns>The created <see cref="TabItem"/>.</returns>
        public TabItem AddHostTab(TabDefinition definition, object content)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (FindTab(definition.Key) != null)
                throw new ArgumentException($"A tab with key '{definition.Key}' is already registered.", nameof(definition));

            return AddTab(definition, content);
        }

        /// <summary>Returns the tab registered under <paramref name="key"/>, or <c>null</c> when absent.</summary>
        public TabItem FindTab(string key)
        {
            if (key == null)
                return null;

            foreach (var item in Items)
            {
                if (item is TabItem tabItem && string.Equals(tabItem.Tag as string, key, StringComparison.Ordinal))
                    return tabItem;
            }

            return null;
        }

        /// <summary>Selects the tab registered under <paramref name="key"/>. Returns <c>false</c> when absent.</summary>
        public bool TrySelectTab(string key)
        {
            var tab = FindTab(key);
            if (tab == null)
                return false;

            SelectedItem = tab;
            return true;
        }

        private TabItem AddTab(TabDefinition definition, object content)
        {
            var tabItem = BuildTabItem(definition, content);
            Items.Add(tabItem);
            return tabItem;
        }

        private TabItem BuildTabItem(TabDefinition definition, object content)
        {
            return new TabItem
            {
                Tag = definition.Key,
                ToolTip = string.IsNullOrEmpty(definition.ToolTip) ? null : definition.ToolTip,
                Header = BuildHeader(definition),
                Content = content,
                Style = (Style)FindResource("MainTabItemStyle"),
            };
        }

        /// <summary>
        /// Builds the tab header: a <see cref="Grid"/> stacking the outline and filled icon paths
        /// (mirrors <c>SettingsControl.BuildNavIcon</c>). The filled icon becomes visible when the
        /// parent <see cref="TabItem"/> is selected.
        /// </summary>
        private Grid BuildHeader(TabDefinition definition)
        {
            var grid = new Grid();
            if (!string.IsNullOrEmpty(definition.OutlineIconKey))
                grid.Children.Add(BuildIconPath(definition.OutlineIconKey, "MainTabIconOutlineStyle"));
            if (!string.IsNullOrEmpty(definition.FilledIconKey))
                grid.Children.Add(BuildIconPath(definition.FilledIconKey, "MainTabIconFilledStyle"));
            return grid;
        }

        private Path BuildIconPath(string iconKey, string styleKey)
        {
            var icon = (MaterialIconKind)Enum.Parse(typeof(MaterialIconKind), iconKey);
            var path = new Path { Style = (Style)FindResource(styleKey) };
            var binding = new Binding { Source = icon, Converter = new MaterialIconToGeometryConverter() };
            path.SetBinding(Path.DataProperty, binding);
            return path;
        }
    }
}
