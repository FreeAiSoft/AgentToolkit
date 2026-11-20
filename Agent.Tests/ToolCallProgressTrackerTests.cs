using System.Collections.ObjectModel;
using NUnit.Framework;
using Agent.Core.AgentTools;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class ToolCallProgressTrackerTests
    {
        private ObservableCollection<ChatMessage> _messages;
        private ToolCallProgressTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _messages = new ObservableCollection<ChatMessage>();
            _tracker = new ToolCallProgressTracker(() => _messages, action => action());
            _tracker.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _tracker.Detach();
        }

        [Test]
        public void Started_CreatesExecutingCard()
        {
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "read_file" });

            Assert.That(_messages, Has.Count.EqualTo(1));
            var message = _messages[0];
            Assert.That(message.Kind, Is.EqualTo(ChatMessageKind.ToolCall));
            Assert.That(message.IsExecuting, Is.True);
            Assert.That(((ToolCallPayload)message.Payload).Result, Is.EqualTo(""));
        }

        [Test]
        public void ExecutedAfterStarted_UpdatesSameCard()
        {
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "read_file" });
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "read_file", Result = "content" });

            Assert.That(_messages, Has.Count.EqualTo(1));
            var message = _messages[0];
            Assert.That(message.IsExecuting, Is.False);
            Assert.That(((ToolCallPayload)message.Payload).Result, Is.EqualTo("content"));
        }

        [Test]
        public void ExecutedWithoutStarted_CreatesFinalCard()
        {
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "write_file", Blocked = true, Result = "blocked in Plan mode." });

            Assert.That(_messages, Has.Count.EqualTo(1));
            var message = _messages[0];
            Assert.That(message.Kind, Is.EqualTo(ChatMessageKind.ToolBlocked));
            Assert.That(message.IsExecuting, Is.False);
            Assert.That(((ToolCallPayload)message.Payload).Blocked, Is.True);
        }

        [Test]
        public void SpecialTools_AreIgnored()
        {
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "ask_question" });
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "ask_question", Result = "a" });
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "spawn_agent" });
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "spawn_agent", Result = "r" });

            Assert.That(_messages, Is.Empty);
        }

        [Test]
        public void Detach_StopsTrackingAndClearsActiveCard()
        {
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "read_file" });
            Assert.That(_messages[0].IsExecuting, Is.True);

            _tracker.Detach();

            Assert.That(_messages[0].IsExecuting, Is.False);

            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "read_file", Result = "x" });
            Assert.That(_messages, Has.Count.EqualTo(1), "no new card after Detach");
        }
        [Test]
        public void SubagentTools_AreIgnored()
        {
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "spawn_agent" });
            ToolExecutionBus.RaiseStarted(new AgentToolCall { ToolName = "read_files" });
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "read_files", Result = "x" });
            ToolExecutionBus.RaiseExecuted(new AgentToolCall { ToolName = "spawn_agent", Result = "r" });

            Assert.That(_messages, Is.Empty);
        }

    }
}
