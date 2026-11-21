using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.ChatHost
{
    /// <summary>
    /// Supplies the agent runtime for the standalone smoke host. It assembles a real
    /// <see cref="AgentRunner"/> around the toolkit's <see cref="StubAiProvider"/> and an empty
    /// <see cref="AgentToolRegistry"/>, so the chat goes through the full shared agent loop
    /// (<see cref="Agent.Models.ChatAgentSession"/>) while still replying with the deterministic
    /// stub answer — no real AI provider call and no tools.
    /// </summary>
    public sealed class StandaloneRuntimeProvider : IAgentRuntimeProvider
    {
        public AgentRunner CreateRunner()
        {
            return new AgentRunner(
                new StubAiProvider(),
                new AgentToolRegistry(),
                _ => "You are a standalone ChatHost smoke-test agent. You have no tools; reply briefly and in Russian.",
                Budget,
                CompactSystemPrompt,
                ToolCallingMode.JsonChat,
                AgentPermissions.All,
                approvalGate: null);
        }

        public ContextBudget Budget { get; } = new ContextBudget();

        public string CompactSystemPrompt { get; } = PromptDefaults.AgentCompactSystemPrompt;
    }
}
