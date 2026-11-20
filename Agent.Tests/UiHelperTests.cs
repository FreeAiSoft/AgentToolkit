using System;
using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatMessageCopyFormatterTests
    {
        [Test]
        public void Format_Null_ReturnsEmpty()
        {
            Assert.That(ChatMessageCopyFormatter.Format(null), Is.EqualTo(""));
        }

        [Test]
        public void Format_WithoutReasoning_ReturnsDisplayTextOnly()
        {
            var message = new ChatMessage { Kind = ChatMessageKind.User, Text = "hi" };

            Assert.That(ChatMessageCopyFormatter.Format(message), Is.EqualTo("hi"));
        }

        [Test]
        public void Format_WithReasoning_AppendsReasoningBlock()
        {
            var message = new ChatMessage { Kind = ChatMessageKind.User, Text = "hi", Reasoning = "think" };

            var text = ChatMessageCopyFormatter.Format(message);

            Assert.That(
                text,
                Is.EqualTo("hi" + Environment.NewLine + "[Reasoning]" + Environment.NewLine + "think"));
        }
    }

    [TestFixture]
    public class AccentBackgroundCalculatorTests
    {
        [Test]
        public void FromColor_AppliesAlphaAndKeepsRgb()
        {
            var background = AccentBackgroundCalculator.FromColor(0x10, 0x20, 0x30);

            Assert.That(background.A, Is.EqualTo(AccentBackgroundCalculator.Alpha));
            Assert.That(background.R, Is.EqualTo(0x10));
            Assert.That(background.G, Is.EqualTo(0x20));
            Assert.That(background.B, Is.EqualTo(0x30));
        }

        [Test]
        public void Fallback_UsesInfoBlueWithAlpha()
        {
            var background = AccentBackgroundCalculator.Fallback();

            Assert.That(background.A, Is.EqualTo(AccentBackgroundCalculator.Alpha));
            Assert.That(background.R, Is.EqualTo(AccentBackgroundCalculator.FallbackR));
            Assert.That(background.G, Is.EqualTo(AccentBackgroundCalculator.FallbackG));
            Assert.That(background.B, Is.EqualTo(AccentBackgroundCalculator.FallbackB));
        }

        [Test]
        public void Alpha_IsSemiTransparentTint()
        {
            Assert.That(AccentBackgroundCalculator.Alpha, Is.EqualTo(0x33));
        }
    }
}
