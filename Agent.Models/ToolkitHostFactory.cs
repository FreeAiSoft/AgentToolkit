using System;
using System.IO;
using Agent.Core.Contracts;
using Agent.Core.Services;
using Agent.Core.Stores;

namespace Agent.Models
{
    /// <summary>
    /// UI-free assembler for the toolkit chat graph. Resolves defaults, creates and runs the
    /// <see cref="ChatModel"/> and <see cref="SettingsModel"/>, and returns the resolved seams so
    /// the WPF facade (and headless tests) can wire everything from one place.
    /// </summary>
    public static class ToolkitHostFactory
    {
        /// <summary>
        /// Default host app-data root used when the host passes none. It is the user's roaming
        /// application-data root; <see cref="ToolkitSettingsStore"/> then appends its own
        /// <c>AgentToolkit</c> folder, so settings end up in <c>%AppData%\AgentToolkit\toolkit.db</c>.
        /// </summary>
        public static string DefaultAppDataDirectory =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static ToolkitChatContext Build(ToolkitChatConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var logger = config.Logger ?? NoOpLogger.Instance;
            var appDataDirectory = string.IsNullOrWhiteSpace(config.AppDataDirectory)
                ? DefaultAppDataDirectory
                : config.AppDataDirectory;

            var settingsStore = config.SettingsStore
                ?? new ToolkitSettingsStore(appDataDirectory, config.WorkspaceProvider, logger: logger);
            var historyStore = config.HistoryStore ?? new InMemoryChatHistoryStore();

            var settings = new SettingsModel(settingsStore, workspaceProvider: config.WorkspaceProvider, logger: logger);
            var chat = new ChatModel(historyStore, config.UiStateStore, workspaceProvider: config.WorkspaceProvider, logger: logger);

            settings.Run();
            chat.Run();

            return new ToolkitChatContext
            {
                Chat = chat,
                Settings = settings,
                SettingsStore = settingsStore,
                HistoryStore = historyStore,
                UiStateStore = config.UiStateStore,
                RuntimeProvider = config.RuntimeProvider,
                HostActions = config.HostActions,
                AppDataDirectory = appDataDirectory
            };
        }
    }
}
