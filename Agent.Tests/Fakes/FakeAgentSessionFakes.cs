using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Tests.Fakes
{
    /// <summary>
    /// Test <see cref="IAgentRuntimeProvider"/> that returns a pre-built <see cref="AgentRunner"/>
    /// and exposes a controllable budget + compact system prompt.
    /// </summary>
    public sealed class FakeAgentRuntimeProvider : IAgentRuntimeProvider
    {
        private readonly Func<AgentRunner> _runnerFactory;

        public FakeAgentRuntimeProvider(Func<AgentRunner> runnerFactory, ContextBudget budget = null, string compactSystemPrompt = null)
        {
            _runnerFactory = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
            Budget = budget ?? new ContextBudget();
            CompactSystemPrompt = compactSystemPrompt ?? "Summarize the conversation.";
        }

        public AgentRunner CreateRunner() => _runnerFactory();

        public ContextBudget Budget { get; }

        public string CompactSystemPrompt { get; }
    }

    /// <summary>Records host-action invocations so session scenarios can assert delegation.</summary>
    public sealed class FakeChatHostActions : IChatHostActions
    {
        public int OpenCommitControlCount { get; private set; }
        public int SendBuildErrorsCount { get; private set; }
        public int OpenCreateRobotCount { get; private set; }
        public int RebuildAgentToolsCount { get; private set; }
        public int ResetCheckpointStateCount { get; private set; }
        public int OnConversationRemovedCount { get; private set; }
        public string RehydratedConversationId { get; private set; }

        public void OpenCommitControl() => OpenCommitControlCount++;
        public void SendBuildErrors() => SendBuildErrorsCount++;
        public void OpenCreateRobot() => OpenCreateRobotCount++;
        public void RebuildAgentTools() => RebuildAgentToolsCount++;
        public void ResetCheckpointState() => ResetCheckpointStateCount++;

        public Task RehydrateCheckpointAsync(string conversationId)
        {
            RehydratedConversationId = conversationId;
            return Task.CompletedTask;
        }

        public void OnConversationRemoved() => OnConversationRemovedCount++;
    }

    /// <summary>
    /// Controllable provider implementing both <see cref="IAiProvider"/> and
    /// <see cref="IStreamingAiProvider"/>, so a real <see cref="AgentRunner"/> can be driven
    /// deterministically in headless session tests.
    /// </summary>
    public sealed class FakeStreamingAiProvider : IAiProvider, IStreamingAiProvider
    {
        private readonly Func<int, IProgress<ChatStreamDelta>, CancellationToken, Task<ChatResponse>> _streaming;
        private readonly Func<IReadOnlyList<ChatMessageData>, string, CancellationToken, Task<ChatResponse>> _chat;
        private int _streamCalls;

        public FakeStreamingAiProvider(
            Func<int, IProgress<ChatStreamDelta>, CancellationToken, Task<ChatResponse>> streaming,
            Func<IReadOnlyList<ChatMessageData>, string, CancellationToken, Task<ChatResponse>> chat = null)
        {
            _streaming = streaming ?? throw new ArgumentNullException(nameof(streaming));
            _chat = chat;
        }

        public int StreamCalls => _streamCalls;

        public Task<ChatResponse> ChatAsync(string message, CancellationToken cancellationToken = default)
            => ChatAsync(new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = message } }, "", cancellationToken);

        public Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, CancellationToken cancellationToken = default)
        {
            if (_chat != null)
                return _chat(messages, systemPrompt, cancellationToken);

            return Task.FromResult(new ChatResponse { Answer = "" });
        }

        public Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
            => ChatAsync(messages, systemPrompt, cancellationToken);

        public Task<ChatResponse> ChatStreamingAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IProgress<ChatStreamDelta> progress, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _streamCalls);
            return _streaming(call, progress, cancellationToken);
        }

        public Task<ChatResponse> ChatStreamingAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IReadOnlyList<ToolDefinition> tools, IProgress<ChatStreamDelta> progress, CancellationToken cancellationToken = default)
            => ChatStreamingAsync(messages, systemPrompt, progress, cancellationToken);
    }
}
