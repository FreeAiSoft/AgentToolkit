using System;

namespace Agent.Core.AgentTools
{
    /// <summary>
    /// Process-wide tool lifecycle events for the UI layer. The toolkit listens to this bus so
    /// the running-tool indicator and the tool-call cards do not depend on any single
    /// <see cref="AgentRunner"/> instance (and therefore do not depend on the toolkit consumer).
    /// </summary>
    public static class ToolExecutionBus
    {
        /// <summary>Raised right before a tool starts executing (after the approval gate).</summary>
        public static event EventHandler<AgentToolCall> Started;

        /// <summary>Raised after a tool finished, or was blocked/rejected.</summary>
        public static event EventHandler<AgentToolCall> Executed;

        public static void RaiseStarted(AgentToolCall e) => Started?.Invoke(null, e);
        public static void RaiseExecuted(AgentToolCall e) => Executed?.Invoke(null, e);
    }
}
