using System;
using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Core.Services
{
    /// <summary>
    /// Default <see cref="IAgentRuntimeProvider"/> for smoke hosts and demos: assembles a real
    /// <see cref="AgentRunner"/> around <see cref="StubAiProvider"/> and an empty
    /// <see cref="AgentToolRegistry"/>. The full shared agent loop runs, but the model replies with
    /// the deterministic stub answer — no real provider call and no tools.
    /// </summary>
    public class StubRuntimeProvider : IAgentRuntimeProvider
    {
        private readonly Func<AgentMode, string> _systemPromptFactory;

        public StubRuntimeProvider(string systemPrompt = null)
        {
            var prompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? "You are a standalone toolkit smoke-test agent. You have no tools; reply briefly and in Russian."
                : systemPrompt;
            _systemPromptFactory = _ => prompt;
        }

        public AgentRunner CreateRunner()
        {
            return new AgentRunner(
                new StubAiProvider(),
                new AgentToolRegistry(),
                _systemPromptFactory,
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
