using System;
using System.Collections.Generic;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Context;
using Agent.Core.Models;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatModelCacheLossTests
    {
        private FakeChatHistoryStore _store;

        [SetUp]
        public void SetUp() => _store = new FakeChatHistoryStore();

        private static ChatModel CreateModel(FakeChatHistoryStore store, IAgentLogger logger = null)
        {
            var model = new ChatModel(store, logger: logger);
            model.EnsureActiveConversation();
            return model;
        }

        [Test]
        public void WarmThenCold_RaisesPromptCacheLostOnce()
        {
            var logger = new CapturingAgentLogger();
            var model = CreateModel(_store, logger);
            PromptCacheLossInfo payload = null;
            var count = 0;
            model.PromptCacheLost += (s, e) => { count++; payload = e; };

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200, TotalTokens = 1000 });
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000, TotalTokens = 1000 });

            Assert.That(count, Is.EqualTo(1));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Kind, Is.EqualTo(PromptCacheLossKind.Full));
            Assert.That(payload.LostTokens, Is.EqualTo(800));
            Assert.That(payload.LostPercent, Is.EqualTo(100));
            Assert.That(logger.Warns, Has.Count.EqualTo(1));
            Assert.That(logger.Warns[0], Is.EqualTo(payload.Message));
        }

        [Test]
        public void RecordTokenUsage_Null_DoesNotRaise()
        {
            var logger = new CapturingAgentLogger();
            var model = CreateModel(_store, logger);
            var count = 0;
            model.PromptCacheLost += (s, e) => count++;

            model.RecordTokenUsage(null);

            Assert.That(count, Is.Zero);
            Assert.That(logger.Warns, Is.Empty);
        }

        [Test]
        public void NewChat_StartsWithZeroPrevious_NoFalseEvent()
        {
            var logger = new CapturingAgentLogger();
            var model = CreateModel(_store, logger);
            var count = 0;
            model.PromptCacheLost += (s, e) => count++;

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200 });

            model.StartNewChat();
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000 });

            Assert.That(count, Is.Zero);
            Assert.That(logger.Warns, Is.Empty);
        }

        [Test]
        public void CloseActiveChat_ThenEnsureActiveConversation_NoFalseEvent()
        {
            var logger = new CapturingAgentLogger();
            var model = CreateModel(_store, logger);
            var count = 0;
            model.PromptCacheLost += (s, e) => count++;

            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200 });

            model.CloseActiveChat();
            model.EnsureActiveConversation();
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000 });

            Assert.That(count, Is.Zero);
        }
    }

    public sealed class CapturingAgentLogger : IAgentLogger
    {
        public List<string> Warns { get; } = new List<string>();

        public void Info(string message) { }
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) { }
        public void Error(Exception exception, string message = null) { }
    }
}
