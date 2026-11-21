using Agent.Core.Contracts;
using Agent.Models;
using Agent.Toolkit.Wpf.Controls;

namespace Agent.ChatHost
{
    /// <summary>
    /// Standalone <see cref="IChatHost"/> for the smoke host. It delegates the entire agent chat
    /// loop to the shared <see cref="ChatHostBase"/> / <see cref="ChatAgentSession"/> stack and
    /// supplies only the smoke-host-specific runtime provider and host actions.
    /// </summary>
    public sealed class StandaloneChatHost : ChatHostBase
    {
        private readonly StandaloneHostActions _hostActions;

        public StandaloneChatHost(ChatModel model, IAgentRuntimeProvider runtimeProvider, IAgentLogger logger = null)
            : base(model, runtimeProvider, logger)
        {
            _hostActions = new StandaloneHostActions(model, logger);
        }

        protected override IChatHostActions HostActions => _hostActions;
    }
}
