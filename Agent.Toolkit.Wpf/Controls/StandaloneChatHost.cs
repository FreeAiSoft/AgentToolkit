using Agent.Core.Contracts;
using Agent.Core.Services;
using Agent.Models;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Drop-in <see cref="IChatHost"/> for standalone/smoke hosts. It wires the shared
    /// <see cref="ChatHostBase"/>/<see cref="ChatAgentSession"/> loop to a
    /// <see cref="StubRuntimeProvider"/> and no-op host actions, so a host can run the full chat UI
    /// without implementing any DTE/Git/Roslyn/robot seams.
    /// </summary>
    public class StandaloneChatHost : ChatHostBase
    {
        private readonly IChatHostActions _hostActions;

        public StandaloneChatHost(ChatModel model, IAgentLogger logger = null, IChatHostActions hostActions = null, IAgentRuntimeProvider runtimeProvider = null)
            : base(model, runtimeProvider ?? new StubRuntimeProvider(), logger)
        {
            _hostActions = hostActions ?? NullHostActions.Instance;
        }

        protected override IChatHostActions HostActions => _hostActions;
    }
}
