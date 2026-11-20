using System.Collections.Generic;
using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatStreamAssemblerTests
    {
        private static List<ChatStreamSegmentEventArgs> Run(ChatStreamAssembler assembler, params ChatStreamDelta[] deltas)
        {
            var events = new List<ChatStreamSegmentEventArgs>();
            assembler.SegmentChanged += (s, e) => events.Add(e);
            foreach (var delta in deltas)
                assembler.OnDelta(delta);
            return events;
        }

        [Test]
        public void ReasoningBegin_Delta_End_ProducesOneClosedSegment()
        {
            var events = Run(new ChatStreamAssembler(),
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin, SegmentId = "r1" },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "think " },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "more" },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningEnd, Text = "think more" });

            Assert.That(events.Count, Is.EqualTo(4));
            Assert.That(events[0].Kind, Is.EqualTo(ChatStreamSegmentKind.Reasoning));
            Assert.That(events[0].Event, Is.EqualTo(ChatStreamSegmentEventKind.Opened));
            Assert.That(events[0].SegmentId, Is.EqualTo("r1"));

            Assert.That(events[1].Event, Is.EqualTo(ChatStreamSegmentEventKind.Text));
            Assert.That(events[1].Text, Is.EqualTo("think "));
            Assert.That(events[2].Text, Is.EqualTo("think more"));

            Assert.That(events[3].Event, Is.EqualTo(ChatStreamSegmentEventKind.Closed));
            Assert.That(events[3].SegmentId, Is.EqualTo("r1"));
            Assert.That(events[3].Text, Is.EqualTo("think more"));
        }

        [Test]
        public void TwoReasoningSegments_ProduceDifferentSegmentIds()
        {
            var events = Run(new ChatStreamAssembler(),
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "a" },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningEnd },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "b" },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningEnd });

            var opened = new List<string>();
            foreach (var e in events)
                if (e.Kind == ChatStreamSegmentKind.Reasoning && e.Event == ChatStreamSegmentEventKind.Opened)
                    opened.Add(e.SegmentId);

            Assert.That(opened.Count, Is.EqualTo(2));
            Assert.That(opened[0], Is.Not.EqualTo(opened[1]));
        }

        [Test]
        public void ContentBegin_AfterReasoning_ClosesReasoningFirst()
        {
            var events = Run(new ChatStreamAssembler(),
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "think" },
                new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin });

            Assert.That(events[0].Event, Is.EqualTo(ChatStreamSegmentEventKind.Opened));
            Assert.That(events[0].Kind, Is.EqualTo(ChatStreamSegmentKind.Reasoning));
            Assert.That(events[1].Event, Is.EqualTo(ChatStreamSegmentEventKind.Text));
            Assert.That(events[2].Event, Is.EqualTo(ChatStreamSegmentEventKind.Closed));
            Assert.That(events[2].Kind, Is.EqualTo(ChatStreamSegmentKind.Reasoning));
            Assert.That(events[2].Text, Is.EqualTo("think"));
            Assert.That(events[3].Event, Is.EqualTo(ChatStreamSegmentEventKind.Opened));
            Assert.That(events[3].Kind, Is.EqualTo(ChatStreamSegmentKind.Content));
            Assert.That(events[2].SegmentId, Is.Not.EqualTo(events[3].SegmentId));
        }

        [Test]
        public void TurnEnd_ClosesDanglingSegments()
        {
            var events = Run(new ChatStreamAssembler(),
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin },
                new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "think" },
                new ChatStreamDelta { Phase = ChatStreamPhase.TurnEnd });

            var closed = events.FindLast(e => e.Kind == ChatStreamSegmentKind.Reasoning && e.Event == ChatStreamSegmentEventKind.Closed);
            Assert.That(closed, Is.Not.Null);
            Assert.That(closed.Text, Is.EqualTo("think"));
        }

        [Test]
        public void LegacyDelta_StillAccumulates()
        {
            var events = Run(new ChatStreamAssembler(),
                new ChatStreamDelta { ReasoningDelta = "r1" },
                new ChatStreamDelta { ReasoningDelta = "r2" },
                new ChatStreamDelta { ContentDelta = "c" });

            var reasoningTexts = new List<string>();
            var contentTexts = new List<string>();
            foreach (var e in events)
            {
                if (e.Kind == ChatStreamSegmentKind.Reasoning && e.Event == ChatStreamSegmentEventKind.Text)
                    reasoningTexts.Add(e.Text);
                if (e.Kind == ChatStreamSegmentKind.Content && e.Event == ChatStreamSegmentEventKind.Text)
                    contentTexts.Add(e.Text);
            }

            Assert.That(reasoningTexts, Does.Contain("r1"));
            Assert.That(reasoningTexts, Does.Contain("r1r2"));
            Assert.That(contentTexts, Does.Contain("c"));
        }
    }
}
