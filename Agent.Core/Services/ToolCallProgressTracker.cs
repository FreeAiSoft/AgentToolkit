using System;
using System.Collections.ObjectModel;
using Agent.Core.AgentTools;
using Agent.Core.Models;

namespace Agent.Core.Services
{
    /// <summary>
    /// Owns the lifecycle of tool-call cards in the chat transcript: creates a card with the
    /// running indicator when a tool starts and updates (or creates) it when the tool finishes.
    /// Lives in Core (UI-free) so its logic is testable without WPF; the WPF layer supplies the
    /// message collection and a UI-thread marshaler.
    /// </summary>
    public sealed class ToolCallProgressTracker
    {
        private readonly Func<ObservableCollection<ChatMessage>> _messages;
        private readonly Action<Action> _marshal;
        private ChatMessage _activeToolMessage;
        private int _subagentDepth;

        public ToolCallProgressTracker(
            Func<ObservableCollection<ChatMessage>> messages,
            Action<Action> marshal)
        {
            _messages = messages ?? throw new ArgumentNullException(nameof(messages));
            _marshal = marshal ?? (a => a());
        }

        public void Attach()
        {
            ToolExecutionBus.Started += OnStarted;
            ToolExecutionBus.Executed += OnExecuted;
        }

        public void Detach()
        {
            ToolExecutionBus.Started -= OnStarted;
            ToolExecutionBus.Executed -= OnExecuted;
            _subagentDepth = 0;

            if (_activeToolMessage != null)
            {
                _activeToolMessage.IsExecuting = false;
                _activeToolMessage = null;
            }
        }

        private void OnStarted(object sender, AgentToolCall e)
        {
            if (IsSpawnAgent(e?.ToolName))
            {
                _subagentDepth++;
                return;
            }
            if (_subagentDepth > 0 || IsHandledElsewhere(e?.ToolName)) return;

            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                IsExecuting = true,
                Payload = new ToolCallPayload { ToolName = e.ToolName ?? "", Result = "", Blocked = false }
            };
            _activeToolMessage = message;
            _marshal(() => _messages().Add(message));
        }

        private void OnExecuted(object sender, AgentToolCall e)
        {
            if (IsSpawnAgent(e?.ToolName))
            {
                if (_subagentDepth > 0) _subagentDepth--;
                return;
            }
            if (_subagentDepth > 0 || IsHandledElsewhere(e?.ToolName)) return;

            var detail = string.IsNullOrWhiteSpace(e.DisplayResult) ? e.Result : e.DisplayResult;
            var active = _activeToolMessage;
            _activeToolMessage = null;

            if (active != null &&
                string.Equals((active.Payload as ToolCallPayload)?.ToolName, e.ToolName, StringComparison.Ordinal))
            {
                _marshal(() =>
                {
                    active.SetToolResult(detail);
                    active.IsExecuting = false;
                });
                return;
            }

            _marshal(() => _messages().Add(new ChatMessage
            {
                Kind = e.Blocked ? ChatMessageKind.ToolBlocked : ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = e.ToolName ?? "", Result = detail ?? "", Blocked = e.Blocked }
            }));
        }

        private static bool IsSpawnAgent(string toolName) =>
            string.Equals(toolName, "spawn_agent", StringComparison.OrdinalIgnoreCase);

        private static bool IsHandledElsewhere(string toolName) =>
            string.Equals(toolName, "ask_question", StringComparison.OrdinalIgnoreCase);
    }
}