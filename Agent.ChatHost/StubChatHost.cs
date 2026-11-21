using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Models;
using Agent.Toolkit.Wpf.Controls;

namespace Agent.ChatHost
{
    /// <summary>
    /// Standalone <see cref="IChatHost"/>: every host seam is a no-op except sending, which appends
    /// the user message and echoes a fake agent reply so the chat UI can be exercised end to end.
    /// </summary>
    public sealed class StubChatHost : IChatHost
    {
        private readonly ChatModel _model;

        public StubChatHost(ChatModel model)
        {
            _model = model;
        }

        public bool IsAgentRunning => false;

        public async Task SendChatAsync(string text, IReadOnlyList<ChatAttachment> attachments)
        {
            _model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = text });

            // Simulate a short agent turn so the running state/footer is visible while testing.
            _model.Phase = AgentPhase.Streaming;
            await Task.Delay(300);
            _model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.AgentText, Text = "Echo: " + text });
            _model.Phase = AgentPhase.Idle;
        }

        public void CancelAgentRun() { }

        public Task ResumeAgentRunAsync() => Task.CompletedTask;

        public void OpenCommitControl() { }

        public void SendBuildErrors()
            => _model.AppendMessage(new ChatMessage
            {
                Kind = ChatMessageKind.SystemNotice,
                Text = "No build errors in the standalone host."
            });

        public void OpenCreateRobot() { }

        public void RebuildAgentTools() { }

        public void StartNewChat() => _model.StartNewChat();

        public void OpenConversation(string id) => _model.OpenConversation(id);

        public void OnConversationRemoved() { }

        public void ShowDocsIndex() { }

        public Task CompactNowAsync() => Task.CompletedTask;

        public void RequestMemoryUpdate() { }
    }
}
