using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class ChatMessageTests
    {
        [Test]
        public void Defaults_AreUserKindWithTextPayload()
        {
            var message = new ChatMessage();

            Assert.That(message.Kind, Is.EqualTo(ChatMessageKind.User));
            Assert.That(message.Payload, Is.TypeOf<TextPayload>());
            Assert.That(message.Text, Is.EqualTo(""));
        }

        [Test]
        public void Label_ByKind()
        {
            Assert.That(new ChatMessage { Kind = ChatMessageKind.User }.Label, Is.EqualTo(""));
            Assert.That(new ChatMessage { Kind = ChatMessageKind.AgentText, Mode = "" }.Label, Is.EqualTo("Agent: "));
            Assert.That(new ChatMessage { Kind = ChatMessageKind.AgentText, Mode = "Act" }.Label, Is.EqualTo("Agent [Act]: "));
            Assert.That(new ChatMessage { Kind = ChatMessageKind.Error, Text = "boom" }.Label, Is.EqualTo("⚠ Extension error: "));
        }

        [Test]
        public void DisplayText_ForToolCallAndBlocked()
        {
            var call = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = "read_file", Result = "content" }
            };
            Assert.That(call.DisplayText, Is.EqualTo("⚙ read_file → content"));

            var blocked = new ChatMessage
            {
                Kind = ChatMessageKind.ToolBlocked,
                Payload = new ToolCallPayload { ToolName = "write_file" }
            };
            Assert.That(blocked.DisplayText, Is.EqualTo("⛔ write_file blocked in Plan mode."));
        }

        [Test]
        public void DisplayText_ForQuestion_ReturnsQuestion()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.Question,
                Payload = new QuestionPayload { Question = "Pick?" }
            };

            Assert.That(message.DisplayText, Is.EqualTo("Pick?"));
        }

        [Test]
        public void DisplayText_ForApproval_UsesDecisionPrefix()
        {
            Assert.That(
                new ChatMessage
                {
                    Kind = ChatMessageKind.Approval,
                    Payload = new ApprovalPayload { ToolName = "editor", Decision = "approved" }
                }.DisplayText,
                Is.EqualTo("✓ editor"));

            Assert.That(
                new ChatMessage
                {
                    Kind = ChatMessageKind.Approval,
                    Payload = new ApprovalPayload { ToolName = "editor", Decision = "rejected" }
                }.DisplayText,
                Is.EqualTo("✗ editor"));
        }

        [Test]
        public void MessageTitle_ForToolCall_ReturnsToolName()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = "build" }
            };

            Assert.That(message.MessageTitle, Is.EqualTo("build"));
        }

        [Test]
        public void ReasoningDisplayText_StreamingShowsTail()
        {
            var message = new ChatMessage
            {
                IsThinking = true,
                Reasoning = new string('a', ChatMessage.ReasoningStreamTailLength + 10)
            };

            var text = message.ReasoningDisplayText;

            Assert.That(text, Does.StartWith("…"));
            Assert.That(text.Length, Is.EqualTo(ChatMessage.ReasoningStreamTailLength + 1));
            Assert.That(text, Does.EndWith(new string('a', ChatMessage.ReasoningStreamTailLength)));
        }

        [Test]
        public void ReasoningDisplayText_NotThinking_ReturnsFullReasoning()
        {
            var message = new ChatMessage { IsThinking = false, Reasoning = "reason" };

            Assert.That(message.ReasoningDisplayText, Is.EqualTo("reason"));
        }

        [Test]
        public void SingleLineDisplayText_CollapsesNewlines()
        {
            var message = new ChatMessage { Kind = ChatMessageKind.User, Text = "a\nb" };

            Assert.That(message.SingleLineDisplayText, Is.EqualTo("a b"));
        }
        [Test]
        public void IsExecuting_DefaultsFalse()
        {
            Assert.That(new ChatMessage().IsExecuting, Is.False);
        }

        [Test]
        public void IsExecuting_RaisesPropertyChanged_OnChange_NotOnSameValue()
        {
            var message = new ChatMessage();
            var names = new List<string>();
            message.PropertyChanged += (s, e) => names.Add(e.PropertyName);

            message.IsExecuting = true;
            message.IsExecuting = true;
            message.IsExecuting = false;

            Assert.That(names.FindAll(n => n == nameof(ChatMessage.IsExecuting)), Has.Count.EqualTo(2));
        }

        [Test]
        public void SetToolResult_UpdatesResultAndNotifiesToolBodyBlocks()
        {
            var message = new ChatMessage
            {
                Kind = ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = "build" }
            };
            var names = new List<string>();
            message.PropertyChanged += (s, e) => names.Add(e.PropertyName);

            message.SetToolResult("ok");

            Assert.That(((ToolCallPayload)message.Payload).Result, Is.EqualTo("ok"));
            Assert.That(message.ToolBodyText, Is.EqualTo("ok"));
            Assert.That(message.ToolBodyBlocks, Is.Not.Empty);
            Assert.That(names, Does.Contain(nameof(ChatMessage.ToolBodyText)));
            Assert.That(names, Does.Contain(nameof(ChatMessage.ToolBodyBlocks)));
        }

        [Test]
        public void SetToolResult_NoToolPayload_DoesNotThrow()
        {
            var message = new ChatMessage { Kind = ChatMessageKind.SystemNotice, Text = "x" };

            Assert.DoesNotThrow(() => message.SetToolResult("ok"));
            Assert.That(message.Text, Is.EqualTo("x"));
        }

    }

    [TestFixture]
    public class ChatPayloadTests
    {
        [Test]
        public void TextPayload_DefaultsAndConstructor()
        {
            Assert.That(new TextPayload().Text, Is.EqualTo(""));
            Assert.That(new TextPayload("hi").Text, Is.EqualTo("hi"));
        }

        [Test]
        public void ToolCallPayload_Defaults()
        {
            var payload = new ToolCallPayload();

            Assert.That(payload.ToolName, Is.EqualTo(""));
            Assert.That(payload.Result, Is.EqualTo(""));
            Assert.That(payload.Blocked, Is.False);
        }

        [Test]
        public void QuestionPayload_AnswerAndSkippedFlags()
        {
            var q = new QuestionPayload { Options = new List<string> { "a", "b" } };

            Assert.That(q.IsAnswered, Is.False);

            q.Answer = "a";
            Assert.That(q.IsAnswered, Is.True);
            Assert.That(q.IsFreeTextAnswer, Is.False);

            q.Answer = "custom";
            Assert.That(q.IsFreeTextAnswer, Is.True);

            q.IsSkipped = true;
            Assert.That(q.IsFreeTextAnswer, Is.False);
        }

        [Test]
        public void ApprovalPayload_DecisionStates()
        {
            var a = new ApprovalPayload();

            Assert.That(a.IsDecided, Is.False);
            Assert.That(a.ShowButtons, Is.True);
            Assert.That(a.DecisionText, Is.EqualTo(""));

            a.Decision = "approved";
            Assert.That(a.IsDecided, Is.True);
            Assert.That(a.ShowButtons, Is.False);
            Assert.That(a.DecisionText, Is.EqualTo("Approved"));

            a.Decision = "rejected";
            Assert.That(a.DecisionText, Is.EqualTo("Rejected"));
        }

        [Test]
        public void SubagentStatusItem_StatusTextAndOutput()
        {
            var item = new SubagentStatusItem();

            Assert.That(item.StatusText, Is.EqualTo("Pending…"));
            Assert.That(item.HasOutput, Is.False);

            item.Status = "running";
            item.ToolCalls = 2;
            Assert.That(item.StatusText, Is.EqualTo("Running · 2 tool call(s)…"));

            item.Status = "completed";
            Assert.That(item.StatusText, Is.EqualTo("Completed · 2 tool call(s)"));
            Assert.That(item.HasOutput, Is.True);
            Assert.That(item.HasError, Is.False);

            item.Status = "failed";
            item.Error = "boom";
            Assert.That(item.HasError, Is.True);
        }

        [Test]
        public void CheckpointPayload_ChangeFlags()
        {
            var payload = new CheckpointPayload();

            Assert.That(payload.HasChanges, Is.False);
            Assert.That(payload.HasDiff, Is.False);

            payload.HasChanges = true;
            payload.HasDiff = true;

            Assert.That(payload.HasChanges, Is.True);
            Assert.That(payload.HasDiff, Is.True);
        }
    }

    [TestFixture]
    public class PendingMessageTests
    {
        [Test]
        public void PreviewAndFlags()
        {
            var pending = new PendingMessage();

            Assert.That(pending.Id, Is.Not.Empty);
            Assert.That(pending.Preview, Is.EqualTo("(attachments only)"));
            Assert.That(pending.HasAttachments, Is.False);
            Assert.That(pending.CanSteer, Is.False);

            pending.Text = "  hi\nthere  ";
            Assert.That(pending.Preview, Is.EqualTo("hi there"));
            Assert.That(pending.CanSteer, Is.True);

            pending.Attachments = new List<ChatAttachment> { new ChatAttachment() };
            Assert.That(pending.HasAttachments, Is.True);
            Assert.That(pending.CanSteer, Is.False);
            Assert.That(pending.AttachmentSummary, Is.EqualTo("📎 1"));
        }

        [Test]
        public void DeliveryToolTip_ReflectsDelivery()
        {
            var pending = new PendingMessage { Delivery = PendingDelivery.Steer };

            Assert.That(pending.DeliveryToolTip, Does.Contain("Steer"));
            Assert.That(pending.IsSteered, Is.True);

            pending.Delivery = PendingDelivery.Queue;
            Assert.That(pending.DeliveryToolTip, Does.Contain("Queue"));
            Assert.That(pending.IsSteered, Is.False);
        }
    }

    [TestFixture]
    public class ChatAttachmentTests
    {
        [Test]
        public void DisplayText_ReflectsKind()
        {
            Assert.That(new ChatAttachment { FileName = "pic.png", IsImage = true }.DisplayText, Is.EqualTo("Image: pic.png"));
            Assert.That(new ChatAttachment { FileName = "doc.txt" }.DisplayText, Is.EqualTo("File: doc.txt"));
        }

        [Test]
        public void ToImagePart_CopiesFields()
        {
            var attachment = new ChatAttachment { MediaType = "image/png", DataBase64 = "abc" };

            var part = attachment.ToImagePart();

            Assert.That(part.MediaType, Is.EqualTo("image/png"));
            Assert.That(part.DataBase64, Is.EqualTo("abc"));
        }
    }

    [TestFixture]
    public class UiStateDataTests
    {
        [Test]
        public void RoundTripsEnumStrings()
        {
            var state = new UiStateData
            {
                ChatViewMode = ChatViewMode.History,
                LastConversationId = "conv-1",
                AgentMode = AgentMode.Act,
                ActiveTab = MainTab.KnowledgeBase
            };

            var json = JsonConvert.SerializeObject(state);
            var restored = JsonConvert.DeserializeObject<UiStateData>(json);

            Assert.That(restored.ChatViewMode, Is.EqualTo(ChatViewMode.History));
            Assert.That(restored.LastConversationId, Is.EqualTo("conv-1"));
            Assert.That(restored.AgentMode, Is.EqualTo(AgentMode.Act));
            Assert.That(restored.ActiveTab, Is.EqualTo(MainTab.KnowledgeBase));
        }

        [Test]
        public void MainTab_KnowledgeBase_UsesSpaceSeparatedEnumMember()
        {
            var json = JsonConvert.SerializeObject(
                MainTab.KnowledgeBase,
                new Newtonsoft.Json.Converters.StringEnumConverter());

            Assert.That(json, Is.EqualTo("\"Knowledge base\""));
        }
    }

    [TestFixture]
    public class AgentPhaseTests
    {
        [Test]
        public void Values()
        {
            Assert.That((int)AgentPhase.Idle, Is.EqualTo(0));
            Assert.That((int)AgentPhase.Streaming, Is.EqualTo(1));
            Assert.That((int)AgentPhase.Error, Is.EqualTo(2));
        }
    }
}
