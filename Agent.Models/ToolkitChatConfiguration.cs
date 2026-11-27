using Agent.Core.Contracts;

namespace Agent.Models
{
    /// <summary>
    /// UI-free configuration for <see cref="ToolkitHostFactory"/>. Every property is optional: a
    /// null value selects the toolkit's self-contained default (in-memory history, SQLite settings
    /// under %AppData%\AgentToolkit, stub runtime, no-op host actions).
    /// </summary>
    public sealed class ToolkitChatConfiguration
    {
        /// <summary>Host's app-data root. When null, the user's <c>%AppData%</c> root is used.</summary>
        public string AppDataDirectory { get; set; }

        /// <summary>Settings store override; null selects <c>ToolkitSettingsStore</c>.</summary>
        public ISettingsStore SettingsStore { get; set; }

        /// <summary>History store override; null selects <c>InMemoryChatHistoryStore</c>.</summary>
        public IChatHistoryStore HistoryStore { get; set; }

        /// <summary>Optional per-session UI-state store.</summary>
        public IUiStateStore UiStateStore { get; set; }

        /// <summary>Optional workspace provider used to scope history/log settings.</summary>
        public IWorkspaceProvider WorkspaceProvider { get; set; }

        /// <summary>Agent runtime override; null selects the stub runtime.</summary>
        public IAgentRuntimeProvider RuntimeProvider { get; set; }

        /// <summary>Host-specific actions override; null selects <c>NullHostActions</c>.</summary>
        public IChatHostActions HostActions { get; set; }

        /// <summary>Optional logger; null selects <c>NoOpLogger</c>.</summary>
        public IAgentLogger Logger { get; set; }
    }
}
