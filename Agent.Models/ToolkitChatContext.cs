using Agent.Core.Contracts;

namespace Agent.Models
{
    /// <summary>
    /// Result of <see cref="ToolkitHostFactory.Build"/>: the initialized chat/settings models and
    /// the resolved seams. The WPF facade consumes this to wire its controls; tests consume it to
    /// assert the default graph without touching WPF.
    /// </summary>
    public sealed class ToolkitChatContext
    {
        public ChatModel Chat { get; set; }
        public SettingsModel Settings { get; set; }
        public ISettingsStore SettingsStore { get; set; }
        public IChatHistoryStore HistoryStore { get; set; }
        public IUiStateStore UiStateStore { get; set; }
        public IAgentRuntimeProvider RuntimeProvider { get; set; }
        public IChatHostActions HostActions { get; set; }
        public string AppDataDirectory { get; set; }
    }
}
