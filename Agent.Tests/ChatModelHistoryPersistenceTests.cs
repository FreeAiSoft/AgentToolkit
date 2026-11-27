using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Stores;
using Agent.Models;

namespace Agent.Tests
{
    /// <summary>
    /// Regression coverage for history persistence: an empty conversation (created by
    /// <see cref="ChatModel.EnsureActiveConversation"/> before the user typed anything) must not be
    /// written into the history store, and a non-empty conversation must be persisted with a title
    /// derived from its first message even when that first message is not a user message.
    /// </summary>
    [TestFixture]
    public class ChatModelHistoryPersistenceTests
    {
        private InMemoryChatHistoryStore _store;

        [SetUp]
        public void SetUp() => _store = new InMemoryChatHistoryStore();

        private static ChatModel CreateModel(InMemoryChatHistoryStore store)
        {
            var model = new ChatModel(store);
            model.EnsureActiveConversation();
            return model;
        }

        [Test]
        public void SaveHistoryNow_EmptyConversation_IsNotPersisted()
        {
            var model = CreateModel(_store);

            model.SaveHistoryNow();

            Assert.That(_store.ListConversations(), Is.Empty,
                "A fresh, empty conversation must not produce an empty history card.");
            Assert.That(model.History, Is.Empty);
        }

        [Test]
        public void SaveHistoryNow_ConversationWithUserMessage_PersistsWithDerivedTitle()
        {
            var model = CreateModel(_store);
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = "hello world" });

            model.SaveHistoryNow();

            var conversations = _store.ListConversations();
            Assert.That(conversations, Has.Count.EqualTo(1));
            Assert.That(conversations[0].Title, Is.EqualTo("hello world"));
            Assert.That(conversations[0].Messages, Has.Count.EqualTo(1));
            Assert.That(conversations[0].Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
        }

        [Test]
        public void SaveHistoryNow_ConversationWithoutUserMessage_DerivesTitleFromFirstMessage()
        {
            var model = CreateModel(_store);
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.SystemNotice, Text = "system note" });

            model.SaveHistoryNow();

            var conversations = _store.ListConversations();
            Assert.That(conversations, Has.Count.EqualTo(1));
            Assert.That(conversations[0].Title, Is.EqualTo("system note"));
        }

        [Test]
        public void SaveHistoryNow_ExistingTitle_IsPreserved()
        {
            var model = CreateModel(_store);
            model.ActiveChatConversation.Title = "Pinned title";
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = "ignored" });

            model.SaveHistoryNow();

            Assert.That(_store.ListConversations()[0].Title, Is.EqualTo("Pinned title"));
        }
    }
}
