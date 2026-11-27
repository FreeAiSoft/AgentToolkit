using System;
using System.Collections.Generic;
using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Models
{
    /// <summary>
    /// Settings-driven <see cref="IAgentRuntimeProvider"/> for standalone/smoke hosts. It builds a
    /// real <see cref="AgentRunner"/> from the current <see cref="SettingsModel"/> — provider,
    /// tool-calling protocol, permissions and context budget — so a host that does not inject a
    /// custom runtime uses the configured OpenAI-compatible/Anthropic provider instead of the
    /// deterministic <see cref="StubAiProvider"/>. The tool registry is empty by default (the
    /// standalone host has no RPA/VS tools); hosts can pass their own registry.
    /// </summary>
    public sealed class SettingsRuntimeProvider : IAgentRuntimeProvider
    {
        private readonly SettingsModel _settings;
        private readonly AgentToolRegistry _registry;
        private readonly Func<AgentMode, string> _systemPromptFactory;
        private readonly string _compactSystemPrompt;
        private readonly Func<SettingsData, IAiProvider> _aiProviderFactory;

        public SettingsRuntimeProvider(
            SettingsModel settings,
            AgentToolRegistry registry = null,
            Func<AgentMode, string> systemPromptFactory = null,
            string compactSystemPrompt = null,
            Func<SettingsData, IAiProvider> aiProviderFactory = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _registry = registry ?? new AgentToolRegistry();
            _systemPromptFactory = systemPromptFactory ?? BuildSystemPrompt;
            _compactSystemPrompt = string.IsNullOrWhiteSpace(compactSystemPrompt)
                ? PromptDefaults.AgentCompactSystemPrompt
                : compactSystemPrompt;
            _aiProviderFactory = aiProviderFactory ?? AiProviderFactory.Create;
        }

        public AgentRunner CreateRunner()
        {
            var data = _settings.ToSettingsData();
            return new AgentRunner(
                _aiProviderFactory(data),
                _registry,
                _systemPromptFactory,
                BuildBudget(data),
                _compactSystemPrompt,
                ParseToolCallingMode(data.ToolCallingMode),
                BuildPermissions(data),
                approvalGate: null);
        }

        public ContextBudget Budget => BuildBudget(_settings.ToSettingsData());

        public string CompactSystemPrompt => _compactSystemPrompt;

        private string BuildSystemPrompt(AgentMode mode)
        {
            var data = _settings.ToSettingsData();
            return AgentPromptBuilder.Build(
                mode,
                _registry,
                ParseToolCallingMode(data.ToolCallingMode),
                BuildPermissions(data),
                data.UserName,
                data.AddressByName,
                ParseMemoryBankMode(data.MemoryBankMode),
                data.AdditionalReadPaths);
        }

        private static ContextBudget BuildBudget(SettingsData data)
        {
            return new ContextBudget
            {
                ContextWindowSize = data.ContextWindowSize > 0 ? data.ContextWindowSize : 409600,
                ReserveFraction = data.ReserveFraction,
                CompactThresholdFraction = data.CompactThreshold,
                KeepRecentMessages = data.KeepRecentMessages,
                ToolResultMaxChars = data.ToolResultMaxChars,
                UseToolRole = data.UseToolRole,
                MaxIterations = data.MaxIterations > 0 ? data.MaxIterations : 8,
                MaxStagnantRepeats = data.MaxStagnantRepeats,
                StagnantLoopNudge = data.StagnantLoopNudge,
                AutoCompact = data.AutoCompact
            };
        }

        private static AgentPermissions BuildPermissions(SettingsData data)
        {
            var permissions = default(AgentPermissions);
            if (data.AllowReadFiles) permissions |= AgentPermissions.ReadFiles;
            if (data.AllowEditFiles) permissions |= AgentPermissions.EditFiles;
            if (data.AllowExecuteCommand) permissions |= AgentPermissions.ExecuteCommand;
            if (data.AllowWebFetch) permissions |= AgentPermissions.WebFetch;
            if (data.AllowMcp) permissions |= AgentPermissions.Mcp;
            if (data.AllowSubagents) permissions |= AgentPermissions.Subagents;
            return permissions;
        }

        private static ToolCallingMode ParseToolCallingMode(string value)
            => string.Equals(value, "Native", StringComparison.OrdinalIgnoreCase)
                ? ToolCallingMode.Native
                : ToolCallingMode.JsonChat;

        private static MemoryBankMode ParseMemoryBankMode(string value)
        {
            if (string.Equals(value, "Tool", StringComparison.OrdinalIgnoreCase)) return MemoryBankMode.Tool;
            if (string.Equals(value, "Convention", StringComparison.OrdinalIgnoreCase)) return MemoryBankMode.Convention;
            return MemoryBankMode.Disabled;
        }
    }
}
