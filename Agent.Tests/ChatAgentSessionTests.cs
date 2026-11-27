using System;
using System.Collections.Generic;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Agent.Core.AgentTools;
using Agent.Core.Context;
using Agent.Core.Models;
using Agent.Core.Contracts;

using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatAgentSessionTests
    {
        private static AgentRunner CreateRunner(FakeStreamingAiProvider provider, ContextBudget budget, string compactPrompt = null)
        {
            return new AgentRunner(
                provider,
                new AgentToolRegistry(),
                _ => "system prompt",
                budget ?? new ContextBudget { MaxIterations = 4 },
                compactPrompt ?? "Summarize the conversation.",
                ToolCallingMode.JsonChat,
                AgentPermissions.All,
                null);
        }

        private static ChatAgentSession CreateSession(
            ChatModel model,
            FakeStreamingAiProvider provider,
            FakeChatHostActions actions,
            ContextBudget budget = null,
            string compactPrompt = null)
        {
            var effectiveBudget = budget ?? new ContextBudget { MaxIterations = 4 };
            var runtime = new FakeAgentRuntimeProvider(
                () => CreateRunner(provider, effectiveBudget, compactPrompt),
                effectiveBudget,
                compactPrompt);

            return new ChatAgentSession(model, runtime, actions);
        }

        [Test]
        public async Task SendAsync_Success_SetsIdlePhaseAndNoResumableRun()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            var provider = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "hello" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "hello" });
                return Task.FromResult(new ChatResponse { Answer = "hello" });
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            await session.SendAsync("hi", null);

            Assert.That(session.IsRunning, Is.False);
            Assert.That(model.Phase, Is.EqualTo(AgentPhase.Idle));
            Assert.That(model.HasResumableRun, Is.False);

            Assert.That(model.Messages.Count, Is.EqualTo(2));
            Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
            Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.ResultAnswerPlan));
            Assert.That(model.Messages[1].Text, Is.EqualTo("hello"));
            Assert.That(model.Messages[1].IsPartial, Is.False);
        }

        [Test]
        public async Task SendAsync_ReasoningThenContent_KeepsAnswerInSeparateMessage()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            var provider = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningBegin, SegmentId = "r1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningDelta, Text = "think" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ReasoningEnd, Text = "think" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "answer" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "answer" });
                return Task.FromResult(new ChatResponse { Answer = "answer", Reasoning = "think" });
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            await session.SendAsync("hi", null);

            Assert.That(model.Messages.Count, Is.EqualTo(3));
            Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));

            // Reasoning is its own message, never folded into the answer.
            Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.AgentText));
            Assert.That(model.Messages[1].Reasoning, Is.EqualTo("think"));
            Assert.That(model.Messages[1].Text, Is.EqualTo(""));
            Assert.That(model.Messages[1].IsThinking, Is.False);

            // The answer is a separate message with no reasoning.
            Assert.That(model.Messages[2].Kind, Is.EqualTo(ChatMessageKind.ResultAnswerPlan));
            Assert.That(model.Messages[2].Reasoning, Is.EqualTo(""));
            Assert.That(model.Messages[2].Text, Is.EqualTo("answer"));
            Assert.That(model.Messages[2].IsPartial, Is.False);
        }

        [Test]
        public async Task SendAsync_InActMode_UsesResultAnswerAct()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            model.RestoreMode(AgentMode.Act);

            var provider = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "done" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "done" });
                return Task.FromResult(new ChatResponse { Answer = "done" });
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            await session.SendAsync("hi", null);

            Assert.That(model.Messages.Count, Is.EqualTo(2));
            Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
            Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.ResultAnswerAct));
            Assert.That(model.Messages[1].Mode, Is.EqualTo("Act"));
            Assert.That(model.Messages[1].Text, Is.EqualTo("done"));
            Assert.That(model.Messages[1].IsPartial, Is.False);
        }



        [Test]
        public async Task SendAsync_WithDispatcherContext_DoesNotCloseContentBeforeDeltasAreApplied()
        {
            var model = new ChatModel(new FakeChatHistoryStore());

            var firstPosted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowMore = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var provider = new FakeStreamingAiProvider(async (call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "a" });
                firstPosted.TrySetResult(true);
                await allowMore.Task.ConfigureAwait(false);
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "bc" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd });
                return new ChatResponse { Answer = "abc" };
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            var dispatcher = new ManualDispatcherSynchronizationContext();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(dispatcher);
            try
            {
                var turnTask = session.SendAsync("hi", null);

                await firstPosted.Task.ConfigureAwait(false);
                dispatcher.ProcessOne(); // ContentBegin -> opens the AgentText message (IsPartial = true)
                dispatcher.ProcessOne(); // ContentDelta "a"

                allowMore.TrySetResult(true);
                await turnTask.ConfigureAwait(false);

                // The turn has completed. Because every stream mutation, including the closing
                // TurnEnd, is marshalled through the dispatcher queue, the message is still open
                // here: its remaining deltas are queued, not applied early on another thread.
                Assert.That(model.Messages.Count, Is.EqualTo(2));
                Assert.That(model.Messages[1].IsPartial, Is.True);

                dispatcher.Drain();

                Assert.That(model.Messages.Count, Is.EqualTo(2));
                Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
                Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.ResultAnswerPlan));
                Assert.That(model.Messages[1].Text, Is.EqualTo("abc"));
                Assert.That(model.Messages[1].IsPartial, Is.False);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        [Test]
        public async Task SendAsync_WithReorderingContext_PreservesReportOrder()
        {
            var model = new ChatModel(new FakeChatHistoryStore());

            var provider = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "a" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "bc" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd });
                return Task.FromResult(new ChatResponse { Answer = "abc" });
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            var reordering = new ReorderingSynchronizationContext();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(reordering);
            try
            {
                var turnTask = session.SendAsync("hi", null);
                await turnTask.ConfigureAwait(false);
                reordering.Drain();

                Assert.That(model.Messages.Count, Is.EqualTo(2));
                Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
                Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.ResultAnswerPlan));
                Assert.That(model.Messages[1].Text, Is.EqualTo("abc"));
                Assert.That(model.Messages[1].IsPartial, Is.False);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }


        [Test]
        public async Task SendAsync_WhileRunning_EnqueuesAndDrainsAfterTurn()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var provider = new FakeStreamingAiProvider(async (call, progress, ct) =>
            {
                if (call == 1)
                {
                    firstStarted.TrySetResult(true);
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "first" });
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "first" });
                    await releaseFirst.Task.ConfigureAwait(false);
                    return new ChatResponse { Answer = "first" };
                }

                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c2" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "second" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "second" });
                return new ChatResponse { Answer = "second" };
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            var firstSend = session.SendAsync("first", null);
            await firstStarted.Task.ConfigureAwait(false);

            var secondSend = session.SendAsync("second", null);
            Assert.That(secondSend.IsCompleted, Is.True);

            releaseFirst.TrySetResult(true);
            await firstSend.ConfigureAwait(false);

            Assert.That(model.Messages.Count, Is.EqualTo(4));
            Assert.That(model.Messages[0].Text, Is.EqualTo("first"));
            Assert.That(model.Messages[1].Text, Is.EqualTo("first"));
            Assert.That(model.Messages[2].Text, Is.EqualTo("second"));
            Assert.That(model.Messages[3].Text, Is.EqualTo("second"));
            Assert.That(model.Phase, Is.EqualTo(AgentPhase.Idle));
            Assert.That(model.HasResumableRun, Is.False);
        }

        [Test]
        public async Task CancelThenResume_SetsResumableAndContinues()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var provider = new FakeStreamingAiProvider(async (call, progress, ct) =>
            {
                if (call == 1)
                {
                    firstStarted.TrySetResult(true);
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                    progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "partial" });
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return new ChatResponse { Answer = "unreachable" };
                }

                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c2" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "resumed" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "resumed" });
                return new ChatResponse { Answer = "resumed" };
            });

            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions);

            var sendTask = session.SendAsync("hi", null);
            await firstStarted.Task.ConfigureAwait(false);

            session.Cancel();
            await sendTask.ConfigureAwait(false);

            Assert.That(session.IsRunning, Is.False);
            Assert.That(model.Phase, Is.EqualTo(AgentPhase.Idle));
            Assert.That(model.HasResumableRun, Is.True);

            await session.ResumeAsync().ConfigureAwait(false);

            Assert.That(session.IsRunning, Is.False);
            Assert.That(model.Phase, Is.EqualTo(AgentPhase.Idle));
            Assert.That(model.HasResumableRun, Is.False);
            Assert.That(model.Messages.Last().Text, Is.EqualTo("resumed"));
        }

        [Test]
        public async Task CompactNowAsync_AppliesCondensedTranscript()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            model.EnsureActiveConversation();
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = "u1" });
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.AgentText, Text = "a1" });
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = "u2" });
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.AgentText, Text = "a2" });
            model.AppendMessage(new ChatMessage { Kind = ChatMessageKind.User, Text = "u3" });

            var provider = new FakeStreamingAiProvider(
                (call, progress, ct) => Task.FromResult(new ChatResponse { Answer = "" }),
                (messages, systemPrompt, ct) => Task.FromResult(new ChatResponse { Answer = "short summary" }));

            var budget = new ContextBudget { MaxIterations = 2, KeepRecentMessages = 2 };
            var actions = new FakeChatHostActions();
            var session = CreateSession(model, provider, actions, budget);

            await session.CompactNowAsync().ConfigureAwait(false);

            Assert.That(model.Messages.Count, Is.EqualTo(3));
            Assert.That(model.Messages[0].Kind, Is.EqualTo(ChatMessageKind.User));
            Assert.That(model.Messages[0].Text, Does.Contain("short summary"));
            Assert.That(model.Messages[1].Kind, Is.EqualTo(ChatMessageKind.AgentText));
            Assert.That(model.Messages[1].Text, Is.EqualTo("a2"));
            Assert.That(model.Messages[2].Kind, Is.EqualTo(ChatMessageKind.User));
            Assert.That(model.Messages[2].Text, Is.EqualTo("u3"));
        }

        [Test]
        public void RebuildRuntime_RecreatesRunnerFromProvider()
        {
            var model = new ChatModel(new FakeChatHistoryStore());
            var provider = new FakeStreamingAiProvider(
                (call, progress, ct) => Task.FromResult(new ChatResponse { Answer = "ok" }));
            var createCount = 0;

            var runtime = new CountingRuntimeProvider(() =>
            {
                createCount++;
                return CreateRunner(provider, new ContextBudget { MaxIterations = 4 });
            });

            var session = new ChatAgentSession(model, runtime, new FakeChatHostActions());
            Assert.That(createCount, Is.EqualTo(1), "The session must create the initial runner.");

            session.RebuildRuntime();
            Assert.That(createCount, Is.EqualTo(2), "RebuildRuntime must recreate the runner so newly saved settings take effect.");
        }

        private sealed class CountingRuntimeProvider : IAgentRuntimeProvider
        {
            private readonly Func<AgentRunner> _factory;

            public CountingRuntimeProvider(Func<AgentRunner> factory) => _factory = factory;

            public AgentRunner CreateRunner() => _factory();

            public ContextBudget Budget { get; } = new ContextBudget();

            public string CompactSystemPrompt { get; } = "Summarize the conversation.";
        }

        private sealed class ManualDispatcherSynchronizationContext : SynchronizationContext
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object State)> _queue =
                new System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object State)>();

            public override void Post(SendOrPostCallback d, object state) => _queue.Enqueue((d, state));

            public override void Send(SendOrPostCallback d, object state) => d(state);

            public void ProcessOne()
            {
                if (_queue.TryDequeue(out var item))
                    item.Callback(item.State);
            }

            public void Drain()
            {
                while (_queue.TryDequeue(out var item))
                    item.Callback(item.State);
            }
        }

        private sealed class ReorderingSynchronizationContext : SynchronizationContext
        {
            private readonly Stack<(SendOrPostCallback Callback, object State)> _stack =
                new Stack<(SendOrPostCallback Callback, object State)>();

            public override void Post(SendOrPostCallback d, object state) => _stack.Push((d, state));

            public override void Send(SendOrPostCallback d, object state) => d(state);

            public void Drain()
            {
                while (_stack.Count > 0)
                {
                    var item = _stack.Pop();
                    item.Callback(item.State);
                }
            }
        }


    }
}
