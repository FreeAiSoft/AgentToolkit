using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatModelCacheLossDiagnosticsTests
    {
        private FakeChatHistoryStore _store;
        private InfoCapturingLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeChatHistoryStore();
            _logger = new InfoCapturingLogger();
        }

        private ChatModel CreateModel()
        {
            var model = new ChatModel(_store, logger: _logger);
            model.EnsureActiveConversation();
            return model;
        }

        [Test]
        public void RecordTokenUsage_AfterModeSwitch_LogsModeSwitchCause()
        {
            var model = CreateModel();
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200 });
            model.SwitchMode(AgentMode.Act);
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000 });

            Assert.That(_logger.Infos.Any(m =>
                m.Contains("[cache-diag] usage") && m.Contains("cause=ModeSwitch")), Is.True);
        }

        [Test]
        public void RecordTokenUsage_OrphanedUser_LogsOrphanedUserCause()
        {
            var model = CreateModel();
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200 });
            model.HasOrphanedUserInTranscript = true;
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000 });

            Assert.That(_logger.Infos.Any(m =>
                m.Contains("[cache-diag] usage") && m.Contains("cause=OrphanedUser")), Is.True);
        }

        [Test]
        public void RecordTokenUsage_UnchangedModeNoOrphan_LogsProviderColdCache()
        {
            var model = CreateModel();
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 800, CacheMissTokens = 200 });
            model.RecordTokenUsage(new TokenUsage { PromptTokens = 1000, CacheHitTokens = 0, CacheMissTokens = 1000 });

            Assert.That(_logger.Infos.Any(m =>
                m.Contains("[cache-diag] usage") && m.Contains("cause=ProviderColdCache")), Is.True);
        }
    }

    public sealed class InfoCapturingLogger : IAgentLogger
    {
        public List<string> Infos { get; } = new List<string>();

        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(Exception exception, string message = null) { }
    }
}
