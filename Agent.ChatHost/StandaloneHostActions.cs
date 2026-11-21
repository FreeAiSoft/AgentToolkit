using System.Threading.Tasks;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Models;

namespace Agent.ChatHost
{
    /// <summary>
    /// Host-specific actions for the standalone smoke host. Nothing here touches VS/DTE, Git,
    /// Roslyn or robot services; every seam is a no-op (logged only), except
    /// <see cref="SendBuildErrors"/>, which posts a system notice so the build-errors button
    /// stays observable in the standalone UI.
    /// </summary>
    public sealed class StandaloneHostActions : IChatHostActions
    {
        private readonly ChatModel _model;
        private readonly IAgentLogger _logger;

        public StandaloneHostActions(ChatModel model, IAgentLogger logger = null)
        {
            _model = model;
            _logger = logger;
        }

        public void OpenCommitControl() => Log("OpenCommitControl");

        public void SendBuildErrors()
        {
            Log("SendBuildErrors");
            _model?.AppendMessage(new ChatMessage
            {
                Kind = ChatMessageKind.SystemNotice,
                Text = "No build errors in the standalone host."
            });
        }

        public void OpenCreateRobot() => Log("OpenCreateRobot");

        public void RebuildAgentTools() => Log("RebuildAgentTools");

        public void ResetCheckpointState() => Log("ResetCheckpointState");

        public Task RehydrateCheckpointAsync(string conversationId)
        {
            Log("RehydrateCheckpointAsync: " + (conversationId ?? ""));
            return Task.CompletedTask;
        }

        public void OnConversationRemoved() => Log("OnConversationRemoved");

        private void Log(string action) => _logger?.Info("StandaloneHostActions." + action);
    }
}
