using System.Threading.Tasks;
using Agent.Core.Contracts;

namespace Agent.Core.Services
{
    /// <summary>
    /// No-op <see cref="IChatHostActions"/> for hosts that do not wire VS/DTE, Git, Roslyn or robot
    /// services (smoke hosts, demos, headless tests). Every action is a safe no-op.
    /// </summary>
    public class NullHostActions : IChatHostActions
    {
        public static readonly NullHostActions Instance = new NullHostActions();

        public NullHostActions() { }

        public virtual void OpenCommitControl() { }

        public virtual void SendBuildErrors() { }

        public virtual void OpenCreateRobot() { }

        public virtual void RebuildAgentTools() { }

        public virtual void ResetCheckpointState() { }

        public virtual Task RehydrateCheckpointAsync(string conversationId) => Task.CompletedTask;

        public virtual void OnConversationRemoved() { }
    }
}
