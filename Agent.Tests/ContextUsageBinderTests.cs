using System;
using NUnit.Framework;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    [TestFixture]
    public class ContextUsageBinderTests
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
        public void Attach_PushesContextWindowIntoModel()
        {
            var model = CreateModel(_store);
            var provider = new FakeTokenUsageProvider { ContextWindowSize = 409600 };
            var budget = new ContextBudget { ContextWindowSize = 409600, CompactThresholdFraction = 0.85, ReserveFraction = 0.12 };

            new ContextUsageBinder(model).Attach(provider, budget);

            Assert.That(model.ContextWindowSize, Is.EqualTo(409600));
            Assert.That(model.CompactThresholdTokens, Is.EqualTo(348160));
            Assert.That(model.MaxInputTokens, Is.EqualTo(360448));
        }

        [Test]
        public void Attach_ForwardsUsageEventsToModel()
        {
            var model = CreateModel(_store);
            var provider = new FakeTokenUsageProvider { ContextWindowSize = 409600 };
            var binder = new ContextUsageBinder(model);
            binder.Attach(provider, new ContextBudget());

            provider.Raise(new TokenUsage
            {
                PromptTokens = 1234,
                CacheHitTokens = 400,
                CacheMissTokens = 834,
                TotalTokens = 2000
            });

            Assert.That(model.ActiveChatConversation.UsedTokens, Is.EqualTo(1234));
            Assert.That(model.ActiveChatConversation.LastCacheHitTokens, Is.EqualTo(400));
            Assert.That(model.ActiveChatConversation.LastCacheMissTokens, Is.EqualTo(834));
            Assert.That(model.ActiveChatConversation.Usage.TotalTokens, Is.EqualTo(2000));
        }

        [Test]
        public void Detach_StopsForwarding()
        {
            var model = CreateModel(_store);
            var provider = new FakeTokenUsageProvider { ContextWindowSize = 409600 };
            var binder = new ContextUsageBinder(model);
            binder.Attach(provider, new ContextBudget());

            binder.Detach();
            provider.Raise(new TokenUsage { TotalTokens = 5 });

            Assert.That(model.ActiveChatConversation.Usage.TotalTokens, Is.Zero);
        }

        [Test]
        public void Attach_NoProvider_ZerosThresholds()
        {
            var model = CreateModel(_store);
            var binder = new ContextUsageBinder(model);

            binder.Attach(null, new ContextBudget());

            Assert.That(model.ContextWindowSize, Is.Zero);
            Assert.That(model.CompactThresholdTokens, Is.Zero);
            Assert.That(model.MaxInputTokens, Is.Zero);
        }
    }

    public sealed class FakeTokenUsageProvider : ITokenUsageProvider
    {
        public TokenUsage TotalTokenUsage => throw new NotImplementedException();
        public int ContextWindowSize { get; set; }
        public event EventHandler<TokenUsage> TokenUsageUpdated;

        public void Raise(TokenUsage usage) => TokenUsageUpdated?.Invoke(this, usage);
    }
}
