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
            var model = new ChatModel(historyStore, logger: logger);
            var host = new StubChatHost(model);

            Tabs.ChatPanel.DataContext = model;
            Tabs.ChatPanel.Host = host;
            Tabs.ChatPanel.Settings = new SettingsData();

            // --- Settings tab ---
            var settingsModel = new SettingsModel(new InMemorySettingsStore());
            var settingsViewModel = new SettingsHostViewModel(settingsModel);
            Tabs.SettingsPanel.DataContext = settingsViewModel;

            // Initialize both models and make sure the chat is ready to receive input.
            model.Run();
            settingsModel.Run();
            Tabs.ChatPanel.EnsureChatReadyToReceive();

            Tabs.TrySelectTab(CoreTabs.Chat.Key);
        }
    }
}
