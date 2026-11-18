using NUnit.Framework;
using Agent.Core.Context;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class PromptCacheLossDetectorTests
    {
        private static TokenUsage Usage(int prompt, int cacheHit, int cacheMiss = 0)
            => new TokenUsage { PromptTokens = prompt, CacheHitTokens = cacheHit, CacheMissTokens = cacheMiss };

        [Test]
        public void Detect_NullUsage_ReturnsNull()
        {
            Assert.That(PromptCacheLossDetector.Detect(null, 800, 1000, 0), Is.Null);
        }

        [Test]
        public void Detect_FirstRequest_ReturnsNull()
        {
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 500, 500), 0, 0, 0), Is.Null);
        }

        [Test]
        public void Detect_FullLoss_ReturnsFullKind()
        {
            var info = PromptCacheLossDetector.Detect(Usage(1000, 0, 1000), 800, 1000, 0);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(PromptCacheLossKind.Full));
            Assert.That(info.PreviousCacheHitTokens, Is.EqualTo(800));
            Assert.That(info.CurrentCacheHitTokens, Is.EqualTo(0));
            Assert.That(info.CurrentPromptTokens, Is.EqualTo(1000));
            Assert.That(info.LostTokens, Is.EqualTo(800));
            Assert.That(info.LostPercent, Is.EqualTo(100));
            Assert.That(info.Message, Does.Contain("Prompt cache lost"));
            Assert.That(info.Message, Does.Contain("100%"));
        }

        [Test]
        public void Detect_DropRatioZero_DetectsAnyDrop()
        {
            var info = PromptCacheLossDetector.Detect(Usage(1000, 999, 1), 1000, 1000, 0);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(PromptCacheLossKind.Partial));
        }

        [Test]
        public void Detect_DropRatioHundred_NeverDetects()
        {
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 0, 1000), 800, 1000, 100), Is.Null);
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 400, 600), 800, 1000, 100), Is.Null);
        }

        [Test]
        public void Detect_WarmCacheNoDrop_ReturnsNull()
        {
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 800, 200), 800, 1000, 0), Is.Null);
        }

        [Test]
        public void Detect_ProviderWithoutCache_ReturnsNull()
        {
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 0, 1000), 0, 1000, 0), Is.Null);
        }

        [Test]
        public void Detect_PartialLoss_ReturnsPartialKindAndPercent()
        {
            var info = PromptCacheLossDetector.Detect(Usage(1000, 300, 700), 800, 1000, 0);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.Kind, Is.EqualTo(PromptCacheLossKind.Partial));
            Assert.That(info.LostTokens, Is.EqualTo(500));
            Assert.That(info.LostPercent, Is.EqualTo(63));
            Assert.That(info.Message, Does.Contain("Prompt cache degraded"));
            Assert.That(info.Message, Does.Contain("63%"));
        }

        [Test]
        public void Detect_PartialLoss_BelowRatio_ReturnsNull()
        {
            Assert.That(PromptCacheLossDetector.Detect(Usage(1000, 300, 700), 800, 1000, 70), Is.Null);
        }

        [Test]
        public void Detect_LostPercentRounding()
        {
            var info = PromptCacheLossDetector.Detect(Usage(1000, 199, 1), 200, 1000, 0);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.LostPercent, Is.EqualTo(1));
        }
    }
}
