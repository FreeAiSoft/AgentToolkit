using System;
using System.Text;
using System.Threading;
using Agent.Core.Models;

namespace Agent.Core.Services
{
    /// <summary>Kind of a streamed segment: model reasoning or final answer content.</summary>
    public enum ChatStreamSegmentKind
    {
        Reasoning,
        Content
    }

    /// <summary>Operation the assembler asks the host to apply to a chat message.</summary>
    public enum ChatStreamSegmentEventKind
    {
        Opened,
        Text,
        Closed
    }

    /// <summary>An idempotent segment operation emitted by <see cref="ChatStreamAssembler"/>.</summary>
    public sealed class ChatStreamSegmentEventArgs : EventArgs
    {
        public string SegmentId { get; set; }
        public ChatStreamSegmentKind Kind { get; set; }
        public ChatStreamSegmentEventKind Event { get; set; }

        /// <summary>Accumulated text (Event == Text) or final text (Event == Closed).</summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// Turns a raw <see cref="ChatStreamDelta"/> stream (explicit phases or legacy deltas) into
    /// stable, idempotent segment operations. Each reasoning/content segment gets its own stable
    /// id, so a host can create a separate chat message per segment instead of appending every
    /// delta to the first one (the Cline "streaming reasoning" behaviour).
    /// </summary>
    public sealed class ChatStreamAssembler
    {
        private int _nextId;

        private string _reasoningId;
        private StringBuilder _reasoningText;
        private bool _reasoningOpen;

        private string _contentId;
        private StringBuilder _contentText;
        private bool _contentOpen;

        /// <summary>Raised synchronously for every segment operation (<see cref="ChatStreamSegmentEventKind.Opened"/>/<see cref="ChatStreamSegmentEventKind.Text"/>/<see cref="ChatStreamSegmentEventKind.Closed"/>).</summary>
        public event EventHandler<ChatStreamSegmentEventArgs> SegmentChanged;

        /// <summary>Applies one stream event (an explicit phase or a legacy delta).</summary>
        public void OnDelta(ChatStreamDelta delta)
        {
            if (delta == null) return;

            switch (delta.Phase)
            {
                case ChatStreamPhase.ReasoningBegin:
                    BeginReasoning(delta.SegmentId);
                    break;

                case ChatStreamPhase.ReasoningDelta:
                    EnsureReasoning(delta.SegmentId);
                    AppendReasoning(delta.Text ?? delta.ReasoningDelta);
                    break;

                case ChatStreamPhase.ReasoningEnd:
                    EnsureReasoning(delta.SegmentId);
                    CloseReasoning(delta.Text);
                    break;

                case ChatStreamPhase.ContentBegin:
                    BeginContent(delta.SegmentId);
                    break;

                case ChatStreamPhase.ContentDelta:
                    EnsureContent(delta.SegmentId);
                    AppendContent(delta.Text ?? delta.ContentDelta);
                    break;

                case ChatStreamPhase.ContentEnd:
                    EnsureContent(delta.SegmentId);
                    CloseContent(delta.Text);
                    break;

                case ChatStreamPhase.TurnEnd:
                    CloseReasoningIfOpen();
                    CloseContentIfOpen();
                    break;

                default:
                    // Legacy Delta phase: graceful degradation for old senders that only fill
                    // ReasoningDelta/ContentDelta. Reasoning auto-opens; the first content delta
                    // closes any open reasoning and opens content.
                    if (!string.IsNullOrEmpty(delta.ReasoningDelta))
                    {
                        EnsureReasoning(delta.SegmentId);
                        AppendReasoning(delta.ReasoningDelta);
                    }

                    if (!string.IsNullOrEmpty(delta.ContentDelta))
                    {
                        CloseReasoningIfOpen();
                        EnsureContent(delta.SegmentId);
                        AppendContent(delta.ContentDelta);
                    }
                    break;
            }
        }

        private void BeginReasoning(string segmentId)
        {
            CloseContentIfOpen();
            CloseReasoningIfOpen();
            OpenReasoning(segmentId);
        }

        private void EnsureReasoning(string segmentId)
        {
            if (_reasoningOpen) return;

            CloseContentIfOpen();
            OpenReasoning(segmentId);
        }

        private void OpenReasoning(string segmentId)
        {
            _reasoningOpen = true;
            _reasoningId = string.IsNullOrEmpty(segmentId) ? MintId(ChatStreamSegmentKind.Reasoning) : segmentId;
            _reasoningText = new StringBuilder();
            Raise(ChatStreamSegmentKind.Reasoning, ChatStreamSegmentEventKind.Opened, _reasoningId, "");
        }

        private void AppendReasoning(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _reasoningText.Append(text);
            Raise(ChatStreamSegmentKind.Reasoning, ChatStreamSegmentEventKind.Text, _reasoningId, _reasoningText.ToString());
        }

        private void CloseReasoning(string finalText)
        {
            if (!_reasoningOpen) return;

            if (!string.IsNullOrEmpty(finalText))
                _reasoningText = new StringBuilder(finalText);

            CloseReasoningIfOpen();
        }

        private void CloseReasoningIfOpen()
        {
            if (!_reasoningOpen) return;

            var id = _reasoningId;
            var text = _reasoningText?.ToString() ?? "";
            _reasoningOpen = false;
            _reasoningId = null;
            _reasoningText = null;
            Raise(ChatStreamSegmentKind.Reasoning, ChatStreamSegmentEventKind.Closed, id, text);
        }

        private void BeginContent(string segmentId)
        {
            CloseReasoningIfOpen();
            CloseContentIfOpen();
            OpenContent(segmentId);
        }

        private void EnsureContent(string segmentId)
        {
            if (_contentOpen) return;

            CloseReasoningIfOpen();
            OpenContent(segmentId);
        }

        private void OpenContent(string segmentId)
        {
            _contentOpen = true;
            _contentId = string.IsNullOrEmpty(segmentId) ? MintId(ChatStreamSegmentKind.Content) : segmentId;
            _contentText = new StringBuilder();
            Raise(ChatStreamSegmentKind.Content, ChatStreamSegmentEventKind.Opened, _contentId, "");
        }

        private void AppendContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _contentText.Append(text);
            Raise(ChatStreamSegmentKind.Content, ChatStreamSegmentEventKind.Text, _contentId, _contentText.ToString());
        }

        private void CloseContent(string finalText)
        {
            if (!_contentOpen) return;

            if (!string.IsNullOrEmpty(finalText))
                _contentText = new StringBuilder(finalText);

            CloseContentIfOpen();
        }

        private void CloseContentIfOpen()
        {
            if (!_contentOpen) return;

            var id = _contentId;
            var text = _contentText?.ToString() ?? "";
            _contentOpen = false;
            _contentId = null;
            _contentText = null;
            Raise(ChatStreamSegmentKind.Content, ChatStreamSegmentEventKind.Closed, id, text);
        }

        private string MintId(ChatStreamSegmentKind kind)
        {
            var n = Interlocked.Increment(ref _nextId);
            return (kind == ChatStreamSegmentKind.Reasoning ? "reasoning-" : "content-") + n;
        }

        private void Raise(ChatStreamSegmentKind kind, ChatStreamSegmentEventKind @event, string segmentId, string text)
        {
            SegmentChanged?.Invoke(this, new ChatStreamSegmentEventArgs
            {
                Kind = kind,
                Event = @event,
                SegmentId = segmentId,
                Text = text
            });
        }
    }
}
