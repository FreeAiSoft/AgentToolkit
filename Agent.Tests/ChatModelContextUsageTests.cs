using NUnit.Framework;
using Agent.Core.Context;
using Agent.Core.Models;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatModelContextUsageTests
    {
        private FakeChatHistoryStore _store;

        [SetUp]
        public void SetUp() => _store = new FakeChatHistoryStore();

        private static ChatModel CreateModel(FakeChatHistoryStore store)
        {
            var model = new ChatModel(store);
            model.EnsureActiveConversation();
            return model;
        }

        [Test]
        public void ContextUsage_NoWindow_IsIdlePlaceholder()
        {
            var model = CreateModel(_store);

            var info = model.ContextUsage;

            Assert.That(info, Is.Not.Null);
            Assert.That(info.IsAvailable, Is.False);
            Assert.That(info.Status, Is.EqualTo(ContextUsageStatus.Idle));
            Assert.That(info.FillText, Is.EqualTo("Context window: —"));
        }

        [Test]
        public void ContextUsage_ReflectsSetContextWindow()
        {
            var model = CreateModel(_store);

            model.SetContextWindow(409600, 348160, 360448);

            var info = model.ContextUsage;
            Assert.That(info.ContextWindowSize, Is.EqualTo(409600));
            Assert.That(info.IsAvailable, Is.True);
        }

        [Test]
        public void ContextUsage_ReflectsRecordTokenUsage()
        {
            var model = CreateModel(_store);
            model.SetContextWindow(100000, 0, 0);

            model.RecordTokenUsage(new TokenUsage
            {
                PromptTokens = 30000,
                CacheHitTokens = 20000,
                CacheMissTokens = 10000,
                TotalTokens = 30000
            });

            var info = model.ContextUsage;
            Assert.That(info.UsedTokens, Is.EqualTo(30000));
            Assert.That(info.RatioPercent, Is.EqualTo(30.0).Within(0.001));
            Assert.That(info.CacheHitRatioPercent, Is.EqualTo(20.0).Within(0.001));
            Assert.That(info.MissRatioPercent, Is.EqualTo(10.0).Within(0.001));
            Assert.That(info.RemainderRatioPercent, Is.EqualTo(70.0).Within(0.001));
            Assert.That(info.CacheText, Does.Contain("20,000").And.Contain("10,000"));
        }

        [Test]
        public void RecordTokenUsage_RaisesContextUsagePropertyChanged()
        {
            var model = CreateModel(_store);
            model.SetContextWindow(100000, 0, 0);
            var raised = false;
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatModel.ContextUsage))
                    raised = true;
            };

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1234 });

            Assert.That(raised, Is.True);
        }

        [Test]
        public void SetContextWindow_RaisesContextUsagePropertyChanged()
        {
            var model = CreateModel(_store);
            var raised = false;
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatModel.ContextUsage))
                    raised = true;
            };

            model.SetContextWindow(100000, 85000, 88000);

            Assert.That(raised, Is.True);
        }

        [Test]
        public void ConversationSwitch_RaisesContextUsageAndResetsUsage()
        {
            var model = CreateModel(_store);
            model.SetContextWindow(100000, 0, 0);
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 30000 });

            var raised = false;
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatModel.ContextUsage))
                    raised = true;
            };

            model.StartNewChat();

            Assert.That(raised, Is.True);
            Assert.That(model.ContextUsage.UsedTokens, Is.Zero);
        }

        [Test]
        public void ContextUsage_TracksNewConversationAfterSwitch()
        {
            var model = CreateModel(_store);
            model.SetContextWindow(100000, 0, 0);

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 30000 });
            model.StartNewChat();

            Assert.That(model.ContextUsage.UsedTokens, Is.Zero);

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 5000 });

            Assert.That(model.ContextUsage.UsedTokens, Is.EqualTo(5000));
        }
    }
}
