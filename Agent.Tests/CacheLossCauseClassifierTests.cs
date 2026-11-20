using NUnit.Framework;
using Agent.Core.Context;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class CacheLossCauseClassifierTests
    {
        [Test]
        public void Classify_ModeSwitchLoss_ReturnsModeSwitch()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Plan,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 0,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.ModeSwitch));
            Assert.That(info.Message, Does.Contain("mode switch"));
        }

        [Test]
        public void Classify_OrphanedUserLoss_ReturnsOrphanedUser()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Act,
                currentMode: AgentMode.Act,
                hasOrphanedUser: true,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 0,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.OrphanedUser));
            Assert.That(info.Message, Does.Contain("unanswered user message"));
        }

        [Test]
        public void Classify_UnchangedModeNoOrphan_ReturnsProviderColdCache()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Act,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 0,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.ProviderColdCache));
        }

        [Test]
        public void Classify_NoDrop_ReturnsNone()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Act,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 800,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.None));
        }

        [Test]
        public void Classify_FirstRequest_ReturnsNone()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Plan,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 0,
                currentCacheHitTokens: 0,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.None));
        }

        [Test]
        public void Classify_PartialDropWithModeSwitch_ReturnsModeSwitch()
        {
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Plan,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 300,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.ModeSwitch));
        }

        [Test]
        public void Classify_NoDropButModeSwitch_ReturnsNone()
        {
            // A mode switch that kept the cache warm is not a loss, so there is no cause to report.
            var info = CacheLossCauseClassifier.Classify(
                previousMode: AgentMode.Plan,
                currentMode: AgentMode.Act,
                hasOrphanedUser: false,
                previousCacheHitTokens: 800,
                currentCacheHitTokens: 900,
                currentPromptTokens: 1000);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(CacheLossCauseKind.None));
        }
    }
}
