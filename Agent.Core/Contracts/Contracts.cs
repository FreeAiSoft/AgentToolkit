using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Context;
using Agent.Core.Models;
using Agent.Core.AgentTools;

namespace Agent.Core.Contracts
{
    /// <summary>Generic chat agent. Backed by an OpenAI-compatible provider or a stub.</summary>
    public interface IAiProvider
    {
        /// <summary>Free-form chat with the agent (UI-facing). Returns the answer and, when the provider emits it, the model's "reasoning".</summary>
        Task<ChatResponse> ChatAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Multi-turn chat with the agent: carries the full message history (user/assistant/tool)
        /// and an explicit system prompt. Used by the agentic tool loop.
        /// </summary>
        Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Multi-turn chat that also passes the available tools. In native tool-calling mode the
        /// provider emits a <c>tools</c> array and returns <see cref="ChatResponse.ToolCalls"/>;
        /// in JSON-over-chat mode the tools are ignored and the model returns JSON text instead.
        /// </summary>
        Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional capability: streams the model's response (reasoning and content) as
    /// incremental deltas instead of returning only the completed response.
    /// Providers that cannot stream fall back to
    /// <see cref="IAiProvider.ChatAsync(IReadOnlyList{ChatMessageData}, string, CancellationToken)"/>.
    /// </summary>
    public interface IStreamingAiProvider
    {
        Task<ChatResponse> ChatStreamingAsync(
            IReadOnlyList<ChatMessageData> messages,
            string systemPrompt,
            IProgress<ChatStreamDelta> progress,
            CancellationToken cancellationToken = default);

        /// <summary>Streaming variant that also passes the available tools (native tool calling).</summary>
        Task<ChatResponse> ChatStreamingAsync(
            IReadOnlyList<ChatMessageData> messages,
            string systemPrompt,
            IReadOnlyList<ToolDefinition> tools,
            IProgress<ChatStreamDelta> progress,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional capability for AI providers that can report token usage
    /// relative to the model's context window.
    /// </summary>
    public interface ITokenUsageProvider
    {
        /// <summary>Cumulative usage across every request handled by this provider instance.</summary>
        TokenUsage TotalTokenUsage { get; }
        int ContextWindowSize { get; }
        /// <summary>Raised after each request with the usage of that single request (not cumulative).</summary>
        event EventHandler<TokenUsage> TokenUsageUpdated;
    }

    /// <summary>
    /// Optional capability for AI providers that report each outgoing message
    /// right before it is sent to the model.
    /// </summary>
    public interface IMessageNotifier
    {
        event EventHandler<AiMessageEventArgs> MessageSent;
    }

    /// <summary>
    /// Optional capability for AI providers that report the full assembled request
    /// (system prompt + all messages) right before it is sent to the model.
    /// </summary>
    public interface IPromptNotifier
    {
        event EventHandler<PromptAssembledEventArgs> PromptAssembled;
    }

    /// <summary>
    /// Optional capability for AI providers that report every retry attempt,
    /// including the provider's status code and response body.
    /// </summary>
    public interface IRetryNotifier
    {
        event EventHandler<RetryAttemptEventArgs> RetryAttempt;
    }

    /// <summary>
    /// Optional capability for AI providers that load prompt templates from disk
    /// and can re-read them on demand (e.g. after the files were edited).
    /// </summary>
    public interface IPromptReloadable
    {
        void ReloadPromptsFromDisk();
    }

    /// <summary>
    /// One-click build using a given solution configuration, plus reading compiler diagnostics and the VS output pane.
    /// </summary>
    public interface IBuildFeedbackService
    {
        Task BuildAsync(string configuration, CancellationToken cancellationToken = default);
        Task BuildProjectAsync(string configuration, string projectName, CancellationToken cancellationToken = default);
        Task<bool> HasSolutionConfigurationAsync(string configuration, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BuildError>> GetCompilationErrorsAsync(CancellationToken cancellationToken = default);
        Task<string> GetOutputWindowTextAsync(CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// Raised by providers instead of a bare transport exception, so the agent
    /// loop can react to the failure class (e.g. compact and retry on context overflow).
    /// </summary>
    public class ProviderErrorException : Exception
    {
        public ProviderErrorException(ProviderErrorKind kind, string message, int? statusCode = null, string body = null, Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>Classified failure kind.</summary>
        public ProviderErrorKind Kind { get; }

        /// <summary>HTTP status code returned by the provider, or null when no response was received.</summary>
        public int? StatusCode { get; }

        /// <summary>Response body returned by the provider (may be truncated), or null.</summary>
        public string Body { get; }

        /// <summary>
        /// Short, user-facing description of the failure, derived from <see cref="Kind"/> and
        /// <see cref="StatusCode"/>. Used by the host for the status line instead of the raw
        /// transport message; full detail stays in the exception <see cref="Exception.Message"/>.
        /// </summary>
        public string FriendlyMessage
        {
            get
            {
                switch (Kind)
                {
                    case ProviderErrorKind.Auth:
                        return StatusCode == 403
                            ? "The AI provider denied access (403). Check the API key and account permissions in Settings → API."
                            : "The AI provider rejected the API key (401). Check the API key in Settings → API.";
                    case ProviderErrorKind.RateLimit:
                        return "The AI provider rate-limited the request (429). Wait a moment and try again.";
                    case ProviderErrorKind.ContextWindowExceeded:
                        return "The request is too large for the model's context window. Enable Auto-compact or start a new chat.";
                    case ProviderErrorKind.ContentFilter:
                        return "The AI provider blocked the request (content filter). Rephrase the message and try again.";
                    case ProviderErrorKind.Transient:
                        return "The AI provider is temporarily unavailable. Try again in a moment.";
                    case ProviderErrorKind.Other:
                        if (StatusCode == 400 && !string.IsNullOrEmpty(Body) &&
                            Body.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0)
                            return "The AI provider rejected the request (400): a tool result is out of sync with its tool call. Press Cancel, then Resume task, or reduce Max iterations.";
                        return "The AI provider rejected the request" + (StatusCode.HasValue ? " (" + StatusCode.Value + ")" : string.Empty) + ".";
                    default:
                        return "The AI provider request failed. Check Base URL, API key and Model in Settings → API.";
                }
            }
        }
    }

    /// <summary>Estimates the number of tokens an outgoing request will consume.</summary>
    public interface ITokenEstimator
    {
        int Estimate(string systemPrompt, IReadOnlyList<ChatMessageData> messages);
    }

    /// <summary>
    /// Condenses a conversation transcript so it fits the context budget: summarization
    /// of older messages with a fallback to rule-based truncation.
    /// </summary>
    public interface IContextCondenser
    {
        /// <summary>Raised when condensation actually begins (a real summarize/truncate pass).</summary>
        event EventHandler CompactionStarted;

        /// <summary>Raised when condensation finishes, regardless of success or failure.</summary>
        event EventHandler CompactionFinished;

        Task<IReadOnlyList<ChatMessageData>> CondenseAsync(
            IReadOnlyList<ChatMessageData> transcript,
            ContextBudget budget,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Factory for the agent runtime supplied by the host. The shared chat session does not know
    /// how an <see cref="AgentRunner"/> is assembled (AI provider, tool registry, permissions,
    /// budget, prompt factory, approval gate); it only asks the host for a ready-to-run runner.
    /// </summary>
    public interface IAgentRuntimeProvider
    {
        /// <summary>Creates a ready-to-run <see cref="AgentRunner"/>.</summary>
        AgentRunner CreateRunner();

        /// <summary>Context budget used by <c>RunAsync</c>/<c>ResumeAsync</c>/<c>CompactAsync</c>.</summary>
        ContextBudget Budget { get; }

        /// <summary>System prompt used when compacting the transcript.</summary>
        string CompactSystemPrompt { get; }
    }

    /// <summary>
    /// Host-specific actions that the shared chat session cannot implement because they depend on
    /// VS/DTE, Git, Roslyn or robot services. Generic chat mechanics do not belong here.
    /// </summary>
    public interface IChatHostActions
    {
        /// <summary>Opens the commit control (Git + VS document save).</summary>
        void OpenCommitControl();

        /// <summary>Feeds the current VS build/compilation errors into the chat as agent context.</summary>
        void SendBuildErrors();

        /// <summary>Opens the create-robot dialog.</summary>
        void OpenCreateRobot();

        /// <summary>Rebuilds the agent tool registry from the current settings and permissions.</summary>
        void RebuildAgentTools();

        /// <summary>Resets checkpoint/workspace-scoping state when a new chat starts.</summary>
        void ResetCheckpointState();

        /// <summary>Rehydrates checkpoint/diff metadata after opening a conversation.</summary>
        Task RehydrateCheckpointAsync(string conversationId);

        /// <summary>Drops host-side state when a conversation is closed, deleted or cleared.</summary>
        void OnConversationRemoved();
    }
}
