using System.Collections.Generic;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Tests.Fakes
{
    /// <summary>
    /// Minimal in-memory <see cref="IChatHistoryStore"/> for the headless model tests. It only
    /// stores/loads conversations; the model scenarios exercised here do not need the SQLite
    /// fidelity (deep-copy, workspace scoping, failure injection) of the host test fakes.
    /// </summary>
    public sealed class FakeChatHistoryStore : IChatHistoryStore
    {
        private readonly Dictionary<string, ChatConversation> _stored = new Dictionary<string, ChatConversation>();

        public string WorkspaceId { get; set; } = "";

        public void Initialize() { }

        public IReadOnlyList<ChatConversation> ListConversations(int limit = 50) => new List<ChatConversation>();

        public ChatConversation LoadConversation(string id)
            => id != null && _stored.TryGetValue(id, out var conversation) ? conversation : null;

        public void SaveConversation(ChatConversation conversation) => _stored[conversation.Id] = conversation;

        public void SetConversationImportant(string id, bool isImportant) { }

        public void DeleteConversation(string id) => _stored.Remove(id);

        public void DeleteAllConversations() => _stored.Clear();
    }
}
