using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Models
{
    /// <summary>
    /// UI-free owner of the shared agent chat mechanics: drains the pending queue, converts the
    /// visible transcript to the provider format, runs <see cref="AgentRunner"/>, maps the
    /// streamed segments back to <see cref="ChatMessage"/> entries, and manages cancel/resume,
    /// new chat/open/remove and manual compaction. Host-specific operations are delegated to
    /// <see cref="IChatHostActions"/>; the runner is supplied by <see cref="IAgentRuntimeProvider"/>.
    /// </summary>
    public sealed class ChatAgentSession
    {
        private readonly ChatModel _model;
        private readonly IAgentRuntimeProvider _runtimeProvider;
        private readonly IChatHostActions _hostActions;
        private readonly IAgentLogger _logger;

        private AgentRunner _runner;
        private CancellationTokenSource _cts;
        private AgentResumeSnapshot _resumeSnapshot;
        private AgentMode _resumeMode;
        private bool _isRunning;
        private bool _isDraining;
        private int _runVersion;

        public ChatAgentSession(
            ChatModel model,
            IAgentRuntimeProvider runtimeProvider,
            IChatHostActions hostActions,
            IAgentLogger logger = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _hostActions = hostActions ?? throw new ArgumentNullException(nameof(hostActions));
            _logger = logger ?? NoOpLogger.Instance;
            _runner = _runtimeProvider.CreateRunner();
        }

        /// <summary>True while a single agent turn is executing.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Queues a message and starts draining the pending queue when no turn is already running.
        /// A message enqueued while a turn is running is picked up automatically after it finishes.
        /// </summary>
        public Task SendAsync(string text, IReadOnlyList<ChatAttachment> attachments)
        {
            if (string.IsNullOrWhiteSpace(text) && (attachments == null || attachments.Count == 0))
                return Task.CompletedTask;

            _model.Enqueue(text, attachments);

            if (_isDraining)
                return Task.CompletedTask;

            return DrainQueueAsync();
        }

        /// <summary>Cancels the running turn and remembers that it can be resumed (when a breakpoint exists).</summary>
        public void Cancel()
        {
            if (!_isRunning)
                return;

            _model.HasResumableRun = _resumeSnapshot != null;
            _cts?.Cancel();
        }

        /// <summary>Continues the last interrupted turn from the stored resume snapshot.</summary>
        public async Task ResumeAsync()
        {
            if (_isDraining || _isRunning || _resumeSnapshot == null)
                return;

            var snapshot = _resumeSnapshot;
            var mode = _resumeMode;
            _resumeSnapshot = null;
            _model.HasResumableRun = false;

            _isDraining = true;
            try
            {
                await RunResumeAsync(snapshot, mode).ConfigureAwait(false);
                await ProcessQueueAsync().ConfigureAwait(false);
            }
            finally
            {
                _isDraining = false;
            }
        }

        /// <summary>Rebuilds the agent runner from the host runtime provider (e.g. after settings changed).</summary>
        public void RebuildRuntime()
        {
            _runner = _runtimeProvider.CreateRunner();
        }

        /// <summary>Starts a new chat: cancels any running turn, resets the model and checkpoint state.</summary>
        public void StartNewChat()
        {
            Cancel();
            _runVersion++;
            _resumeSnapshot = null;
            _model.HasResumableRun = false;
            _model.StartNewChat();
            _model.StopStatus();
            _hostActions.ResetCheckpointState();
        }

        /// <summary>Opens a saved conversation, cancelling any running turn first.</summary>
        public bool OpenConversation(string id)
        {
            Cancel();

            if (!_model.OpenConversation(id))
                return false;

            _runVersion++;
            _resumeSnapshot = null;
            _model.HasResumableRun = false;
            _model.StopStatus();
            _ = RehydrateAsync(id);
            return true;
        }

        /// <summary>Drops host-side state and the resume snapshot after a conversation was removed.</summary>
        public void OnConversationRemoved()
        {
            Cancel();
            _runVersion++;
            _resumeSnapshot = null;
            _model.HasResumableRun = false;
            _model.StopStatus();
            _hostActions.OnConversationRemoved();
        }

        /// <summary>Runs manual context compaction and applies the condensed transcript to the model.</summary>
        public async Task CompactNowAsync()
        {
            var transcript = BuildTranscript(_model.Messages);
            if (transcript.Count == 0)
                return;

            var condensed = await _runner.CompactAsync(transcript, _runtimeProvider.Budget, CancellationToken.None).ConfigureAwait(false);
            if (condensed == null)
                return;

            _model.ApplyCompactedTranscript(condensed.Select(ToChatMessage));
        }

        private async Task DrainQueueAsync()
        {
            if (_isDraining)
                return;

            _isDraining = true;
            try
            {
                await ProcessQueueAsync().ConfigureAwait(false);
            }
            finally
            {
                _isDraining = false;
            }
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                var pending = _model.DequeueNext();
                if (pending == null)
                    return;

                await RunTurnAsync(pending).ConfigureAwait(false);
            }
        }

        private Task RunTurnAsync(PendingMessage pending)
        {
            _model.EnsureActiveConversation();

            var history = BuildTranscript(_model.Messages);

            var userMessage = new ChatMessage { Kind = ChatMessageKind.User, Text = pending.Text ?? "" };
            _model.AppendMessage(userMessage);

            var images = BuildImages(pending.Attachments);
            var text = BuildEffectiveText(pending);

            return RunNewTurnAsync(history, text, images, _model.Mode);
        }

        private Task RunNewTurnAsync(IReadOnlyList<ChatMessageData> history, string text, IReadOnlyList<ChatImagePart> images, AgentMode mode)
        {
            return RunStreamedAsync(
                (token, progress, resumeProgress) => _runner.RunAsync(history, text, images, mode, _runtimeProvider.Budget, token, progress, resumeProgress),
                mode);
        }

        private Task RunResumeAsync(AgentResumeSnapshot snapshot, AgentMode mode)
        {
            return RunStreamedAsync(
                (token, progress, resumeProgress) => _runner.ResumeAsync(snapshot, mode, _runtimeProvider.Budget, token, progress, resumeProgress),
                mode);
        }

        private async Task RunStreamedAsync(
            Func<CancellationToken, IProgress<ChatStreamDelta>, IProgress<AgentResumeSnapshot>, Task<AgentRunResult>> invoker,
            AgentMode mode)
        {
            var version = ++_runVersion;
            var cts = new CancellationTokenSource();
            _cts = cts;
            _isRunning = true;
            _resumeSnapshot = null;
            _resumeMode = mode;
            _model.HasResumableRun = false;
            _model.StartStatus();

            var assembler = new ChatStreamAssembler();
            var segments = new Dictionary<string, ChatMessage>();
            assembler.SegmentChanged += (s, e) => ApplySegment(e, segments, version, mode);

            var progress = new ContextProgress<ChatStreamDelta>(delta =>
            {
                if (version == _runVersion)
                {
                    _logger.Info("[delta] recv phase=" + delta.Phase +
                        " seg=" + (delta.SegmentId ?? "") +
                        " textLen=" + (delta.Text?.Length ?? 0) +
                        " rDelta=" + (delta.ReasoningDelta?.Length ?? 0) +
                        " cDelta=" + (delta.ContentDelta?.Length ?? 0));
                    assembler.OnDelta(delta);
                }
            }, _logger);

            var resumeProgress = new ContextProgress<AgentResumeSnapshot>(snapshot =>
            {
                if (version == _runVersion)
                    _resumeSnapshot = snapshot;
            }, _logger);

            try
            {
                await invoker(cts.Token, progress, resumeProgress).ConfigureAwait(false);

                if (version == _runVersion)
                {
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.TurnEnd });
                    _resumeSnapshot = null;
                    _model.HasResumableRun = false;
                    _model.StopStatus();
                    _logger.Info("[stream] turnEnd messages=" + _model.Messages.Count + " kinds=" + string.Join(",", _model.Messages.Select(m => m.Kind)));

                }
            }
            catch (OperationCanceledException)
            {
                if (version == _runVersion)
                {
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.TurnEnd });
                    _model.HasResumableRun = _resumeSnapshot != null;
                    _model.StopStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                if (version == _runVersion)
                {
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.TurnEnd });
                    _resumeSnapshot = null;
                    _model.ShowStatusError(ex.Message);
                    _model.HasResumableRun = false;
                }
            }
            finally
            {
                if (ReferenceEquals(_cts, cts))
                    _cts = null;
                cts.Dispose();
                _isRunning = false;
            }
        }

        private void ApplySegment(ChatStreamSegmentEventArgs e, IDictionary<string, ChatMessage> segments, int version, AgentMode mode)
        {
            if (version != _runVersion)
                return;

            var segmentId = e.SegmentId ?? "";
            _logger.Info("[seg] " + e.Event + " " + e.Kind + " id=" + (e.SegmentId ?? "") + " textLen=" + (e.Text?.Length ?? 0));

            switch (e.Event)
            {
                case ChatStreamSegmentEventKind.Opened:
                {
                    // Each streamed segment (reasoning or final content) is its own chat
                    // message. Keeping them separate means the answer is never folded into
                    // the reasoning block: reasoning renders as a collapsed "Reasoning"
                    // expander, and the answer renders as its own mode-styled card
                    // (ResultAnswerPlan / ResultAnswerAct, or plain AgentText for Help).
                    var isReasoning = e.Kind == ChatStreamSegmentKind.Reasoning;
                    var message = new ChatMessage
                    {
                        Kind = isReasoning ? ChatMessageKind.AgentText : KindForMode(mode),
                        Mode = isReasoning ? "" : AgentModeNames.ToValue(mode),
                        IsThinking = isReasoning,
                        IsPartial = e.Kind == ChatStreamSegmentKind.Content
                    };

                    _model.AppendMessage(message);
                    segments[segmentId] = message;
                    break;
                }

                case ChatStreamSegmentEventKind.Text:
                {
                    if (segments.TryGetValue(segmentId, out var message))
                    {
                        if (e.Kind == ChatStreamSegmentKind.Reasoning)
                            message.Reasoning = e.Text ?? "";
                        else
                            message.Text = e.Text ?? "";
                    }
                    break;
                }

                case ChatStreamSegmentEventKind.Closed:
                {
                    if (segments.TryGetValue(segmentId, out var message))
                    {
                        if (e.Kind == ChatStreamSegmentKind.Reasoning)
                        {
                            message.Reasoning = e.Text ?? "";
                            message.IsThinking = false;
                            _logger.Info("[seg] closed reasoning text='" + Preview(e.Text) + "'");
                        }
                        else
                        {
                            message.Text = e.Text ?? "";
                            message.IsPartial = false;
                            _logger.Info("[seg] closed content text='" + Preview(e.Text) + "'");
                        }
                    }
                    break;
                }
            }
        }

        private static ChatMessageKind KindForMode(AgentMode mode)
        {
            switch (mode)
            {
                case AgentMode.Act:
                    return ChatMessageKind.ResultAnswerAct;
                case AgentMode.Help:
                    return ChatMessageKind.AgentText;
                default:
                    return ChatMessageKind.ResultAnswerPlan;
            }
        }

        private static string Preview(string text, int max = 200)
        {
            if (string.IsNullOrEmpty(text))
                return "<empty>";

            var single = text.Replace("\r", "\\r").Replace("\n", "\\n");
            return single.Length <= max ? single : single.Substring(0, max) + "…";
        }

        private static List<ChatMessageData> BuildTranscript(IEnumerable<ChatMessage> messages)
        {
            var transcript = new List<ChatMessageData>();
            if (messages == null)
                return transcript;

            foreach (var message in messages)
            {
                if (message == null)
                    continue;

                var role = GetRole(message.Kind);
                if (role == null)
                    continue;

                transcript.Add(new ChatMessageData
                {
                    Role = role,
                    Content = message.Text ?? ""
                });
            }

            return transcript;
        }

        private static string GetRole(ChatMessageKind kind)
        {
            switch (kind)
            {
                case ChatMessageKind.User:
                    return "user";
                case ChatMessageKind.AgentText:
                case ChatMessageKind.ResultAnswerPlan:
                case ChatMessageKind.ResultAnswerAct:
                    return "assistant";
                default:
                    return null;
            }
        }

        private static string BuildEffectiveText(PendingMessage pending)
        {
            if (pending == null)
                return "";

            var text = pending.Text ?? "";
            var attachments = pending.Attachments;
            if (attachments == null || attachments.Count == 0)
                return text;

            var parts = attachments
                .Where(a => a != null && !a.IsImage && !string.IsNullOrWhiteSpace(a.TextContent))
                .Select(a => a.TextContent)
                .ToList();

            if (parts.Count == 0)
                return text;

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
                builder.AppendLine();
            }

            foreach (var part in parts)
                builder.AppendLine(part);

            return builder.ToString().Trim();
        }

        private static IReadOnlyList<ChatImagePart> BuildImages(IReadOnlyList<ChatAttachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
                return new List<ChatImagePart>();

            return attachments
                .Where(a => a != null && a.IsImage)
                .Select(a => a.ToImagePart())
                .ToList();
        }

        private static ChatMessage ToChatMessage(ChatMessageData data)
        {
            if (data == null)
                return new ChatMessage { Kind = ChatMessageKind.SystemNotice, Text = "" };

            switch ((data.Role ?? "").ToLowerInvariant())
            {
                case "user":
                    return new ChatMessage { Kind = ChatMessageKind.User, Text = data.Content ?? "" };
                case "assistant":
                    return new ChatMessage { Kind = ChatMessageKind.AgentText, Text = data.Content ?? "" };
                case "tool":
                    return new ChatMessage
                    {
                        Kind = ChatMessageKind.ToolCall,
                        Payload = new ToolCallPayload { ToolName = "", Result = data.Content ?? "", Blocked = false }
                    };
                default:
                    return new ChatMessage { Kind = ChatMessageKind.SystemNotice, Text = data.Content ?? "" };
            }
        }

        private async Task RehydrateAsync(string conversationId)
        {
            try
            {
                await _hostActions.RehydrateCheckpointAsync(conversationId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private sealed class ContextProgress<T> : IProgress<T>
        {
            private readonly SynchronizationContext _context;
            private readonly Action<T> _handler;
            private readonly IAgentLogger _logger;
            private readonly Queue<T> _pending = new Queue<T>();
            private long _seq;
            private bool _draining;

            public ContextProgress(Action<T> handler, IAgentLogger logger = null)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
                _logger = logger ?? NoOpLogger.Instance;
                _context = SynchronizationContext.Current;
            }

            public void Report(T value)
            {
                if (_context == null)
                {
                    _logger.Info("[ctx] sync phase=" + Describe(value) + " thread=" + Environment.CurrentManagedThreadId);
                    _handler(value);
                    return;
                }

                lock (_pending)
                {
                    var seq = System.Threading.Interlocked.Increment(ref _seq);
                    _logger.Info("[ctx] enqueue seq=" + seq + " phase=" + Describe(value) + " thread=" + Environment.CurrentManagedThreadId + " draining=" + _draining + " count=" + (_pending.Count + 1));
                    _pending.Enqueue(value);
                    if (_draining)
                        return;

                    _draining = true;
                    _context.Post(_ => Drain(), null);
                }
            }

            private void Drain()
            {
                while (true)
                {
                    T value;
                    lock (_pending)
                    {
                        if (_pending.Count == 0)
                        {
                            _draining = false;
                            return;
                        }

                        value = _pending.Dequeue();
                        _logger.Info("[ctx] dequeue phase=" + Describe(value) + " thread=" + Environment.CurrentManagedThreadId + " count=" + _pending.Count);
                    }

                    _handler(value);
                }
            }

            private static string Describe(T value)
            {
                if (value is ChatStreamDelta d)
                    return d.Phase + " seg=" + (d.SegmentId ?? "") + " textLen=" + (d.Text?.Length ?? 0);
                if (value is AgentResumeSnapshot)
                    return "ResumeSnapshot";
                return typeof(T).Name;
            }
        }
        private sealed class NoOpLogger : IAgentLogger
        {
            public static readonly NoOpLogger Instance = new NoOpLogger();

            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string message = null) { }
        }
    }
}
