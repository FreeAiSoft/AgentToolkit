using System.Collections.Generic;
using System.Linq;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Core.Stores
{
    /// <summary>
    /// In-memory <see cref="IChatHistoryStore"/> implementation. Conversations live only for the
    /// process lifetime: useful for smoke hosts, demos and unit tests. Kept in the core so every
    /// host shares one implementation instead of each declaring its own fake.
    /// </summary>
    public class InMemoryChatHistoryStore : IChatHistoryStore
    {
        private readonly Dictionary<string, ChatConversation> _stored = new Dictionary<string, ChatConversation>();

        public string WorkspaceId { get; set; } = "";

        public void Initialize() { }

        public IReadOnlyList<ChatConversation> ListConversations(int limit = 50)
            => _stored.Values
                .OrderByDescending(c => c.UpdatedAtUtc)
                .Take(limit)
                .ToList();

        public ChatConversation LoadConversation(string id)
            => id != null && _stored.TryGetValue(id, out var conversation) ? conversation : null;

        public void SaveConversation(ChatConversation conversation)
        {
            if (conversation != null)
                _stored[conversation.Id] = conversation;
        }

        public void SetConversationImportant(string id, bool isImportant)
        {
            if (_stored.TryGetValue(id, out var conversation))
                conversation.IsImportant = isImportant;
        }

        public void DeleteConversation(string id) => _stored.Remove(id);

        public void DeleteAllConversations() => _stored.Clear();
    }
}
