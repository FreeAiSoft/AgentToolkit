using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Models;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Base implementation of <see cref="IChatHost"/> that owns the shared agent chat mechanics
    /// through <see cref="ChatAgentSession"/>. Derived hosts supply only the host-specific pieces:
    /// <see cref="IAgentRuntimeProvider"/> (how the <see cref="Agent.Core.AgentTools.AgentRunner"/> is
    /// assembled) and <see cref="IChatHostActions"/> (DTE/Git/Roslyn/robot operations).
    /// </summary>
    /// <remarks>
    /// The two UI helpers <see cref="ShowDocsIndex"/> and <see cref="RequestMemoryUpdate"/> are now
    /// implemented by <see cref="ChatControl"/> itself; the base class keeps them as no-op virtual
    /// members only to satisfy the legacy <see cref="IChatHost"/> contract and preserve source
    /// compatibility for existing hosts.
    /// </remarks>
    public abstract class ChatHostBase : IChatHost
    {
        private readonly ChatModel _model;
        private readonly IAgentRuntimeProvider _runtimeProvider;
        private readonly IAgentLogger _logger;
        private readonly Lazy<ChatAgentSession> _session;

        /// <summary>
        /// Creates the base host. The session is created lazily so that a derived constructor can
        /// initialize <see cref="HostActions"/> before the agent runner is assembled.
        /// </summary>
        protected ChatHostBase(ChatModel model, IAgentRuntimeProvider runtimeProvider, IAgentLogger logger = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _logger = logger;
            _session = new Lazy<ChatAgentSession>(CreateSession);
        }

        /// <summary>The chat model backing the chat UI.</summary>
        protected ChatModel Model => _model;

        /// <summary>The shared, UI-free session that owns the agent chat loop.</summary>
        protected ChatAgentSession Session => _session.Value;

        /// <summary>
        /// Host-specific actions (commit control, build errors, create-robot, tool-registry rebuild,
        /// checkpoint/workspace scoping). Implemented by the derived host.
        /// </summary>
        protected abstract IChatHostActions HostActions { get; }

        private ChatAgentSession CreateSession()
            => new ChatAgentSession(_model, _runtimeProvider, HostActions, _logger);

        /// <inheritdoc />
        public bool IsAgentRunning => Session.IsRunning;

        /// <inheritdoc />
        public Task SendChatAsync(string text, IReadOnlyList<ChatAttachment> attachments)
            => Session.SendAsync(text, attachments);

        /// <inheritdoc />
        public void CancelAgentRun() => Session.Cancel();

        /// <inheritdoc />
        public Task ResumeAgentRunAsync() => Session.ResumeAsync();

        /// <inheritdoc />
        public void OpenCommitControl() => HostActions.OpenCommitControl();

        /// <inheritdoc />
        public void SendBuildErrors() => HostActions.SendBuildErrors();

        /// <inheritdoc />
        public void OpenCreateRobot() => HostActions.OpenCreateRobot();

        /// <inheritdoc />
        public void RebuildAgentTools() => HostActions.RebuildAgentTools();

        /// <summary>
        /// Rebuilds the agent runner from the runtime provider. Called after the settings are
        /// persisted so a changed provider/protocol/permission set takes effect on the next turn.
        /// </summary>
        public void RebuildRuntime() => Session.RebuildRuntime();

        /// <inheritdoc />
        public void StartNewChat() => Session.StartNewChat();

        /// <inheritdoc />
        public void OpenConversation(string id) => Session.OpenConversation(id);

        /// <inheritdoc />
        public void OnConversationRemoved() => Session.OnConversationRemoved();

        /// <inheritdoc />
        public Task CompactNowAsync() => Session.CompactNowAsync();

        /// <summary>
        /// Legacy backwards-compatibility hook. <see cref="ChatControl"/> renders the docs index
        /// itself and no longer calls this method; the default implementation does nothing.
        /// </summary>
        public virtual void ShowDocsIndex() { }

        /// <summary>
        /// Legacy backwards-compatibility hook. <see cref="ChatControl"/> inserts the memory-bank
        /// prompt itself and no longer calls this method; the default implementation does nothing.
        /// </summary>
        public virtual void RequestMemoryUpdate() { }
    }
}
