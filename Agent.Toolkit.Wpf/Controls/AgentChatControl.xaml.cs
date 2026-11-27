using System;
using System.Windows;
using System.Windows.Controls;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Models;
using Agent.Toolkit.Wpf.Theming;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Self-contained, extensible agent chat host. Wires the shared Chat/Settings tabs, models,
    /// store and agent host from defaults, so a WPF host can embed the full chat with a single
    /// <c>&lt;controls:AgentChatControl/&gt;</c>. Every seam can be overridden through the public
    /// configuration properties, and host-specific content is added through
    /// <see cref="AddHostTab"/> and <see cref="InsertSettingsSection"/>.
    /// </summary>
    public partial class AgentChatControl : UserControl
    {
        private bool _initialized;
        private ToolkitChatContext _context;
        private IChatHost _host;

        public AgentChatControl()
        {
            InitializeComponent();
            AgentThemeManager.EnsureInitialized();
            Loaded += OnLoaded;
        }

        // ---- configuration (set before the control is loaded) ----

        /// <summary>Host's app-data root; null selects the user's <c>%AppData%</c> root.</summary>
        public string AppDataDirectory { get; set; }

        /// <summary>Settings store override; null selects the SQLite default store.</summary>
        public ISettingsStore SettingsStore { get; set; }

        /// <summary>History store override; null selects the in-memory store.</summary>
        public IChatHistoryStore HistoryStore { get; set; }

        /// <summary>Optional per-session UI-state store.</summary>
        public IUiStateStore UiStateStore { get; set; }

        /// <summary>Optional workspace provider used to scope history/log settings.</summary>
        public IWorkspaceProvider WorkspaceProvider { get; set; }

        /// <summary>Agent runtime override; null selects the stub runtime.</summary>
        public IAgentRuntimeProvider RuntimeProvider { get; set; }

        /// <summary>Host-specific actions override; null selects the no-op actions.</summary>
        public IChatHostActions HostActions { get; set; }

        /// <summary>Full <see cref="IChatHost"/> override; null builds a standalone host.</summary>
        public IChatHost Host { get; set; }

        /// <summary>Optional logger; null selects the no-op logger.</summary>
        public IAgentLogger Logger { get; set; }

        // ---- accessors to the built graph ----

        public AgentTabControl TabControl => Tabs;
        public ChatControl ChatPanel => Tabs.ChatPanel;
        public SettingsControl SettingsPanel => Tabs.SettingsPanel;
        public ChatModel Model => _context?.Chat;
        public SettingsModel SettingsModel => _context?.Settings;

        // ---- extensions ----

        public TabItem AddHostTab(TabDefinition definition, object content) => Tabs.AddHostTab(definition, content);

        public void InsertSettingsSection(int index, SettingsControl.SettingsSection section) => SettingsPanel.InsertHostSection(index, section);

        public void InsertMicrophoneSection(int index) => SettingsPanel.InsertMicrophoneSection(index);

        public bool TrySelectTab(string key) => Tabs.TrySelectTab(key);

        public event EventHandler<string> VoiceError
        {
            add { ChatPanel.VoiceError += value; }
            remove { ChatPanel.VoiceError -= value; }
        }

        public event EventHandler<PromptCacheLossInfo> PromptCacheLost
        {
            add { ChatPanel.PromptCacheLost += value; }
            remove { ChatPanel.PromptCacheLost -= value; }
        }

        /// <summary>Builds the chat/settings graph. Idempotent; also runs automatically on Loaded.</summary>
        public void Run() => EnsureInitialized();

        private void OnLoaded(object sender, RoutedEventArgs e) => EnsureInitialized();

        private void OnSettingsSaved(object sender, EventArgs e)
        {
            // The standalone host reads the provider/protocol/permissions from SettingsModel; rebuild
            // the runner after a save so a newly configured provider is used on the next message.
            (_host as ChatHostBase)?.RebuildRuntime();
        }



        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            _context = ToolkitHostFactory.Build(new ToolkitChatConfiguration
            {
                AppDataDirectory = AppDataDirectory,
                SettingsStore = SettingsStore,
                HistoryStore = HistoryStore,
                UiStateStore = UiStateStore,
                WorkspaceProvider = WorkspaceProvider,
                RuntimeProvider = RuntimeProvider,
                HostActions = HostActions,
                Logger = Logger
            });

            _host = Host ?? new StandaloneChatHost(
                _context.Chat,
                Logger,
                _context.HostActions ?? NullHostActions.Instance,
                RuntimeProvider ?? new SettingsRuntimeProvider(_context.Settings));
            _context.Settings.SettingsSaved += OnSettingsSaved;


            ChatPanel.DataContext = _context.Chat;
            ChatPanel.Host = _host;
            ChatPanel.SettingsModel = _context.Settings;
            ChatPanel.AppDataDirectory = _context.AppDataDirectory;
            ChatPanel.Logger = Logger;

            SettingsPanel.DataContext = new SettingsViewModel(_context.Settings);

            ChatPanel.EnsureChatReadyToReceive();
            Tabs.TrySelectTab(CoreTabs.Chat.Key);
        }
    }
}
