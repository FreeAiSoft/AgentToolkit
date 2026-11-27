using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Models;
using Agent.Mvvm;
using Agent.Tests.Fakes;
using NUnit.Framework;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatModelDoItCommandTests
    {
        private FakeChatHistoryStore _store;

        [SetUp]
        public void SetUp() => _store = new FakeChatHistoryStore();

        private static ChatModel CreateModel(FakeChatHistoryStore store, IChatPromptSink sink = null)
        {
            var model = new ChatModel(store, chatSink: sink);
            model.EnsureActiveConversation();
            return model;
        }

        [Test]
        public async Task DoItCommand_WithText_SwitchesToActAndSendsTextPlusDoIt()
        {
            var sink = new RecordingPromptSink();
            var model = CreateModel(_store, sink);

            await model.DoItCommand("fix the bug");

            Assert.That(model.Mode, Is.EqualTo(AgentMode.Act));
            Assert.That(sink.Prompts, Has.Count.EqualTo(1));
            Assert.That(sink.Prompts[0], Is.EqualTo("fix the bug" + Environment.NewLine + "Do it!!!"));
        }

        [Test]
        public async Task DoItCommand_WithEmptyInput_SendsBareDoIt()
        {
            var sink = new RecordingPromptSink();
            var model = CreateModel(_store, sink);

            await model.DoItCommand("");

            Assert.That(model.Mode, Is.EqualTo(AgentMode.Act));
            Assert.That(sink.Prompts, Has.Count.EqualTo(1));
            Assert.That(sink.Prompts[0], Is.EqualTo("Do it!!!"));
        }

        [Test]
        public async Task DoItCommand_WithWhitespaceInput_SendsBareDoIt()
        {
            var sink = new RecordingPromptSink();
            var model = CreateModel(_store, sink);

            await model.DoItCommand("   \t ");

            Assert.That(model.Mode, Is.EqualTo(AgentMode.Act));
            Assert.That(sink.Prompts, Has.Count.EqualTo(1));
            Assert.That(sink.Prompts[0], Is.EqualTo("Do it!!!"));
        }

        [Test]
        public async Task DoItCommand_WithoutSink_OnlySwitchesMode()
        {
            var model = CreateModel(_store, sink: null);

            await model.DoItCommand("anything");

            Assert.That(model.Mode, Is.EqualTo(AgentMode.Act));
        }

        [Test]
        public async Task DoItCommand_SinkThrows_RaisesErrorNotification()
        {
            var model = CreateModel(_store, new ThrowingPromptSink());
            Notify received = null;
            model.Notify += (s, n) => received = n;

            await model.DoItCommand("anything");

            Assert.That(model.Mode, Is.EqualTo(AgentMode.Act));
            Assert.That(received, Is.Not.Null);
            Assert.That(received.NotifyLevel, Is.EqualTo(NotifyLevel.Error));
            Assert.That(received.Message, Is.EqualTo("boom"));
        }

        [Test]
        public async Task DoItCommand_RecordsModeSwitchInTranscript()
        {
            var model = CreateModel(_store, new RecordingPromptSink());

            await model.DoItCommand("go");

            Assert.That(model.Messages.Any(m => m.Kind == ChatMessageKind.ModeSwitch && m.Text == "Mode: Act"), Is.True);
        }

        private sealed class RecordingPromptSink : IChatPromptSink
        {
            public List<string> Prompts { get; } = new List<string>();

            public Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
            {
                Prompts.Add(prompt);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingPromptSink : IChatPromptSink
        {
            public Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
                => throw new Exception("boom");
        }
    }
}
