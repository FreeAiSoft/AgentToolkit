using System;
using System.IO;
using System.Windows;
using Agent.ChatHost.Extended.Logging;
using Agent.Core.Stores;
using Agent.Toolkit.Wpf.Controls;

namespace Agent.ChatHost.Extended
{
    public partial class MainWindow : Window
    {
        private readonly NLogAgentLogger _logger = new NLogAgentLogger();

        public MainWindow()
        {
            InitializeComponent();

            var appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgentChatHost.Extended");

            // Point the toolkit at this host's app-data root (settings/history).
            Chat.AppDataDirectory = appDataDirectory;

            // The default history store is in-memory and is lost on restart. Use the SQLite store
            // so saved chats persist under <appData>\AgentToolkit\toolkit.db (same DB as settings).
            Chat.HistoryStore = new SqliteChatHistoryStore(appDataDirectory, _logger);

            // The extended host injects a real NLog-backed logger. ChatModel, SettingsModel and
            // every store then use it instead of the silent no-op fallback.
            Chat.Logger = _logger;

            _logger.Info("MainWindow: appDataDirectory=" + appDataDirectory + " historyStore=SqliteChatHistoryStore");

            // Build eagerly so the chat is ready even before the Window is loaded (also idempotent).
            Chat.Run();
        }
    }
}
