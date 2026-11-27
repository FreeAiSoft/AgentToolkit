using System;
using System.IO;
using System.Windows;
using Agent.Toolkit.Wpf.Controls;

namespace Agent.ChatHost
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // The self-contained AgentChatControl builds the Chat/Settings tabs, the chat and
            // settings models, the SQLite settings store and the standalone agent host from
            // defaults. Here we only point it at this host's app-data root.
            Chat.AppDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgentChatHost");

            // Build eagerly so the chat is ready even before the Window is loaded (also idempotent).
            Chat.Run();
        }
    }
}
