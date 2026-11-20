using System;
using System.Collections.Generic;
using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    /// <summary>
    /// Round-trip tests for <see cref="ChatMessageMapper"/>: live <see cref="ChatMessage"/>
    /// entries mapped to persisted <see cref="ChatHistoryMessage"/> entries and back without
    /// data loss, plus title generation.
    /// </summary>
    [TestFixture]
    public class ChatMessageMapperTests
    {
        private static readonly DateTime Stamp = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ToHistoryMessage_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ChatMessageMapper.ToHistoryMessage(null));
        }

        [Test]
        public void FromHistoryMessage_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ChatMessageMapper.FromHistoryMessage(null));
        }

        [Test]
        public void ToHistoryMessages_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ChatMessageMapper.ToHistoryMessages(null));
        }

        [Test]
        public void ToHistoryMessages_DropsNullEntriesAndPreservesOrder()
        {
            var a = new ChatMessage { Kind = ChatMessageKind.User, Text = "a", TimestampUtc = Stamp };
            var b = new ChatMessage { Kind = ChatMessageKind.User, Text = "b", TimestampUtc = Stamp };

            var result = ChatMessageMapper.ToHistoryMessages(new ChatMessage[] { a, null, b });

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Text, Is.EqualTo("a"));
            Assert.That(result[1].Text, Is.EqualTo("b"));
        }

        [Test]
        public void AgentText_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.AgentText,
                Text = "hello",
                Reasoning = "thinking…",
                Mode = "Act",
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Kind, Is.EqualTo(ChatMessageKind.AgentText));
            Assert.That(stored.Text, Is.EqualTo("hello"));
            Assert.That(stored.ToolName, Is.EqualTo(""));
            Assert.That(restored.Text, Is.EqualTo("hello"));
            Assert.That(restored.Reasoning, Is.EqualTo("thinking…"));
            Assert.That(restored.Mode, Is.EqualTo("Act"));
            Assert.That(restored.TimestampUtc, Is.EqualTo(Stamp));
        }

        [Test]
        public void ToolCall_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = "read_file", Result = "content", Blocked = false },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Text, Is.EqualTo("content"));
            Assert.That(stored.ToolName, Is.EqualTo("read_file"));
            Assert.That(restored.Kind, Is.EqualTo(ChatMessageKind.ToolCall));
            var payload = (ToolCallPayload)restored.Payload;
            Assert.That(payload.ToolName, Is.EqualTo("read_file"));
            Assert.That(payload.Result, Is.EqualTo("content"));
            Assert.That(payload.Blocked, Is.False);
        }

        [Test]
        public void ToolBlocked_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolBlocked,
                Payload = new ToolCallPayload { ToolName = "write_file", Result = "", Blocked = true },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Kind, Is.EqualTo(ChatMessageKind.ToolBlocked));
            Assert.That(stored.ToolName, Is.EqualTo("write_file"));
            Assert.That(restored.Kind, Is.EqualTo(ChatMessageKind.ToolBlocked));
            var payload = (ToolCallPayload)restored.Payload;
            Assert.That(payload.Blocked, Is.True);
        }

        [Test]
        public void Checkpoint_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.Checkpoint,
                Payload = new CheckpointPayload { CommitHash = "abc123", HasChanges = true, HasDiff = true },
                Mode = "Act",
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Kind, Is.EqualTo(ChatMessageKind.Checkpoint));
            Assert.That(stored.CheckpointHash, Is.EqualTo("abc123"));
            var payload = (CheckpointPayload)restored.Payload;
            Assert.That(payload.CommitHash, Is.EqualTo("abc123"));
            Assert.That(restored.Mode, Is.EqualTo("Act"));
        }

        [Test]
        public void Question_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.Question,
                Payload = new QuestionPayload
                {
                    Question = "Pick one",
                    Answer = "b",
                    Options = new List<string> { "a", "b" }
                },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Text, Is.EqualTo("Pick one"));
            Assert.That(stored.ToolName, Is.EqualTo("b"));
            Assert.That(stored.Options, Is.EqualTo(new List<string> { "a", "b" }));
            var payload = (QuestionPayload)restored.Payload;
            Assert.That(payload.Question, Is.EqualTo("Pick one"));
            Assert.That(payload.Answer, Is.EqualTo("b"));
            Assert.That(payload.Options, Is.EqualTo(new List<string> { "a", "b" }));
            Assert.That(payload.IsSkipped, Is.False);
        }

        [Test]
        public void SkippedQuestion_RestoresIsSkippedFlag()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.Question,
                Payload = new QuestionPayload
                {
                    Question = "Pick one",
                    Answer = ChatMessageMapper.SkipAnswerText,
                    IsSkipped = true,
                    Options = new List<string> { "a", "b" }
                },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            var payload = (QuestionPayload)restored.Payload;
            Assert.That(payload.Answer, Is.EqualTo(ChatMessageMapper.SkipAnswerText));
            Assert.That(payload.IsSkipped, Is.True);
        }

        [Test]
        public void Approval_RoundTrips()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.Approval,
                Payload = new ApprovalPayload { ToolName = "run_commands", Decision = "approved" },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(stored.Text, Is.EqualTo("run_commands"));
            Assert.That(stored.ToolName, Is.EqualTo("approved"));
            var payload = (ApprovalPayload)restored.Payload;
            Assert.That(payload.ToolName, Is.EqualTo("run_commands"));
            Assert.That(payload.Decision, Is.EqualTo("approved"));
        }

        [Test]
        public void LegacyTimestamp_FallsBackToNow()
        {
            var stored = new ChatHistoryMessage { Kind = ChatMessageKind.AgentText, Text = "legacy" };

            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(restored.TimestampUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
        }

        [Test]
        public void BuildTitle_EmptyOrWhitespace_ReturnsNewChat()
        {
            Assert.That(ChatMessageMapper.BuildTitle(null), Is.EqualTo("New chat"));
            Assert.That(ChatMessageMapper.BuildTitle(""), Is.EqualTo("New chat"));
            Assert.That(ChatMessageMapper.BuildTitle("   "), Is.EqualTo("New chat"));
        }

        [Test]
        public void BuildTitle_TrimsAndCollapsesNewlines()
        {
            Assert.That(ChatMessageMapper.BuildTitle("  hello world  "), Is.EqualTo("hello world"));
            Assert.That(ChatMessageMapper.BuildTitle("a\nb"), Is.EqualTo("a b"));
            Assert.That(ChatMessageMapper.BuildTitle("a\r\nb"), Is.EqualTo("a  b")); // CRLF collapses to two spaces
        }

        [Test]
        public void BuildTitle_TruncatesLongText()
        {
            var longText = new string('x', ChatMessageMapper.TitleMaxLength + 5);
            var title = ChatMessageMapper.BuildTitle(longText);

            Assert.That(title.Length, Is.EqualTo(ChatMessageMapper.TitleMaxLength + 1));
            Assert.That(title, Does.StartWith(new string('x', ChatMessageMapper.TitleMaxLength)));
            Assert.That(title, Does.EndWith("…"));
        }
        [Test]
        public void ToolCall_RuntimeIsExecuting_IsNotPersisted()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                IsExecuting = true,
                Payload = new ToolCallPayload { ToolName = "read_file", Result = "content", Blocked = false },
                TimestampUtc = Stamp
            };

            var stored = ChatMessageMapper.ToHistoryMessage(message);
            var restored = ChatMessageMapper.FromHistoryMessage(stored);

            Assert.That(restored.IsExecuting, Is.False);
        }

    }
}
