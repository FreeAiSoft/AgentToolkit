using System;
using System.IO;
using System.Windows;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Models;
using Agent.Toolkit.Wpf.Controls;

namespace Agent.ChatHost
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // --- Chat tab ---
            var historyStore = new InMemoryChatHistoryStore();
            var logger = new NLogAgentLogger();

            // The standalone host persists the toolkit's own settings (the chat permissions
            // overlay) to a SQLite database under its app-data root. The toolkit appends its
            // "AgentToolkit" folder, so the final file is
            // %AppData%\AgentChatHost\AgentToolkit\toolkit.db (see toolkit-settings-persistence-plan.md).
            var appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgentChatHost");
            var settingsStore = new ToolkitSettingsStore(appDataDirectory, logger: logger);
            var settings = settingsStore.Load();
            logger.Info("MainWindow: standalone host wiring — ChatHost=StandaloneChatHost (shared ChatAgentSession + ChatHostBase). The configured AI provider is NOT used; StubAiProvider runs through the full agent loop.");
            logger.Info("MainWindow: loaded provider settings " + DescribeProvider(settings));
            var model = new ChatModel(historyStore, logger: logger);
            var runtimeProvider = new StandaloneRuntimeProvider();
            var host = new StandaloneChatHost(model, runtimeProvider, logger);

            Tabs.ChatPanel.DataContext = model;
            Tabs.ChatPanel.Host = host;
            Tabs.ChatPanel.Settings = settings;
            Tabs.ChatPanel.AppDataDirectory = appDataDirectory;
            Tabs.ChatPanel.Logger = logger;

            // --- Settings tab ---
            // Uses the same SQLite store as the chat permission overlay: both sides write a full
            // SettingsData snapshot to the single 'settings' row. A real host keeps the two views
            // in sync around one shared source of truth; this smoke host loads both at startup and
            // accepts last-writer-wins for the rare case where both are edited in one session.
            var settingsModel = new SettingsModel(settingsStore, logger: logger);
            var settingsViewModel = new SettingsHostViewModel(settingsModel);
            Tabs.SettingsPanel.DataContext = settingsViewModel;

            // Initialize both models and make sure the chat is ready to receive input.
            model.Run();
            settingsModel.Run();
            Tabs.ChatPanel.EnsureChatReadyToReceive();

            Tabs.TrySelectTab(CoreTabs.Chat.Key);
        }

        private static string DescribeProvider(SettingsData settings)
        {
            if (settings == null)
                return "<null>";

            return "ProviderId=" + (settings.ProviderId ?? "") +
                   ", BaseUrl=" + (settings.BaseUrl ?? "") +
                   ", Model=" + (settings.Model ?? "") +
                   ", HasApiKey=" + !string.IsNullOrEmpty(settings.ApiKey);
        }
    }
}
