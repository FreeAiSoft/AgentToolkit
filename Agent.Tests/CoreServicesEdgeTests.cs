using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    // ------------------------------------------------------------------
    // ReasoningEffortMap
    // ------------------------------------------------------------------

    [TestFixture]
    public class ReasoningEffortMapTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("low")]
        [TestCase("LOW")]
        [TestCase("xhigh")]
        public void IsValid_AcceptsEmptyAndKnownValues(string value)
        {
            Assert.That(ReasoningEffortMap.IsValid(value), Is.True);
        }

        [TestCase("bogus")]
        [TestCase("loww")]
        public void IsValid_RejectsUnknownValues(string value)
        {
            Assert.That(ReasoningEffortMap.IsValid(value), Is.False);
        }

        [Test]
        public void Normalize_ReturnsCanonicalValue()
        {
            Assert.That(ReasoningEffortMap.Normalize(null), Is.EqualTo(""));
            Assert.That(ReasoningEffortMap.Normalize(""), Is.EqualTo(""));
            Assert.That(ReasoningEffortMap.Normalize("LOW"), Is.EqualTo("low"));
            Assert.That(ReasoningEffortMap.Normalize("xhigh"), Is.EqualTo("xhigh"));
            Assert.That(ReasoningEffortMap.Normalize("bogus"), Is.EqualTo(""));
        }

        [Test]
        public void IsDeepSeek_DetectsUrlAndModel()
        {
            Assert.That(ReasoningEffortMap.IsDeepSeek("https://api.deepseek.com", null), Is.True);
            Assert.That(ReasoningEffortMap.IsDeepSeek(null, "deepseek-chat"), Is.True);
            Assert.That(ReasoningEffortMap.IsDeepSeek(null, null), Is.False);
            Assert.That(ReasoningEffortMap.IsDeepSeek("https://api.openai.com", "gpt-4"), Is.False);
        }

        [Test]
        public void Apply_NullPayload_DoesNothing()
        {
            Assert.DoesNotThrow(() => ReasoningEffortMap.Apply(null, ReasoningEffortMap.High, null, null));
        }

        [Test]
        public void Apply_DefaultEffort_SendsNothing()
        {
            var payload = new JObject();
            ReasoningEffortMap.Apply(payload, "", null, null);
            ReasoningEffortMap.Apply(payload, null, null, null);

            Assert.That(payload["reasoning_effort"], Is.Null);
            Assert.That(payload["thinking"], Is.Null);
        }

        [TestCase(ReasoningEffortMap.None, "none")]
        [TestCase(ReasoningEffortMap.Minimal, "minimal")]
        [TestCase(ReasoningEffortMap.Low, "low")]
        [TestCase(ReasoningEffortMap.Medium, "medium")]
        [TestCase(ReasoningEffortMap.High, "high")]
        [TestCase(ReasoningEffortMap.ExtraHigh, "high")]
        [TestCase(ReasoningEffortMap.Max, "high")]
        public void Apply_OpenAi_MapsAllLevels(string effort, string expected)
        {
            var payload = new JObject();
            ReasoningEffortMap.Apply(payload, effort, "https://api.openai.com", "gpt-4");

            Assert.That(payload["reasoning_effort"].Value<string>(), Is.EqualTo(expected));
            Assert.That(payload["thinking"], Is.Null);
        }

        [Test]
        public void Apply_DeepSeekNone_SetsThinkingDisabled()
        {
            var payload = new JObject();
            ReasoningEffortMap.Apply(payload, ReasoningEffortMap.None, "https://api.deepseek.com", "deepseek-chat");

            Assert.That(payload["reasoning_effort"], Is.Null);
            Assert.That(payload["thinking"]["type"].Value<string>(), Is.EqualTo("disabled"));
        }

        [TestCase(ReasoningEffortMap.Minimal, "low")]
        [TestCase(ReasoningEffortMap.Low, "low")]
        [TestCase(ReasoningEffortMap.Medium, "high")]
        [TestCase(ReasoningEffortMap.High, "high")]
        [TestCase(ReasoningEffortMap.ExtraHigh, "high")]
        [TestCase(ReasoningEffortMap.Max, "max")]
        public void Apply_DeepSeek_MapsEffort(string effort, string expected)
        {
            var payload = new JObject();
            ReasoningEffortMap.Apply(payload, effort, "https://api.deepseek.com", "deepseek-chat");

            Assert.That(payload["reasoning_effort"].Value<string>(), Is.EqualTo(expected));
            Assert.That(payload["thinking"], Is.Null);
        }
    }

    // ------------------------------------------------------------------
    // ReadPathPolicy
    // ------------------------------------------------------------------

    [TestFixture]
    public class ReadPathPolicyTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "agent-readpolicy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string InRoot(params string[] parts)
            => Path.Combine(new[] { _root }.Concat(parts).ToArray());

        [Test]
        public void IsAllowed_NullEmptyWhitespace_ReturnsFalse()
        {
            Assert.That(ReadPathPolicy.IsAllowed(null, new[] { _root }), Is.False);
            Assert.That(ReadPathPolicy.IsAllowed("", new[] { _root }), Is.False);
            Assert.That(ReadPathPolicy.IsAllowed("   ", new[] { _root }), Is.False);
        }

        [Test]
        public void IsAllowed_NoRoots_ReturnsFalse()
        {
            Assert.That(ReadPathPolicy.IsAllowed(InRoot("file.txt"), null), Is.False);
            Assert.That(ReadPathPolicy.IsAllowed(InRoot("file.txt"), new string[0]), Is.False);
        }

        [Test]
        public void IsAllowed_InsideRoot_ReturnsTrue()
        {
            Assert.That(ReadPathPolicy.IsAllowed(InRoot("src", "file.txt"), new[] { _root }), Is.True);
        }

        [Test]
        public void IsAllowed_OutsideRoot_ReturnsFalse()
        {
            var outside = Path.Combine(Path.GetTempPath(), "agent-readpolicy-outside-" + Guid.NewGuid().ToString("N"), "file.txt");
            Assert.That(ReadPathPolicy.IsAllowed(outside, new[] { _root }), Is.False);
        }

        [Test]
        public void IsAllowed_ServiceDirectory_ReturnsFalse()
        {
            Assert.That(ReadPathPolicy.IsAllowed(InRoot("bin", "file.cs"), new[] { _root }), Is.False);
            Assert.That(ReadPathPolicy.IsAllowed(InRoot("obj", "file.cs"), new[] { _root }), Is.False);
            Assert.That(ReadPathPolicy.IsAllowed(InRoot(".git", "config"), new[] { _root }), Is.False);
        }

        [TestCase(".env")]
        [TestCase("web.config")]
        [TestCase("id_rsa")]
        [TestCase("appsettings.json")]
        [TestCase("secrets.json")]
        [TestCase("cert.pem")]
        [TestCase("my-credentials.txt")]
        public void IsAllowed_SensitiveFileName_ReturnsFalse(string fileName)
        {
            Assert.That(ReadPathPolicy.IsAllowed(InRoot(fileName), new[] { _root }), Is.False);
        }

        [Test]
        public void IsAllowed_WhenPoliciesDisabled_Allows()
        {
            Assert.That(
                ReadPathPolicy.IsAllowed(InRoot("bin", "file.cs"), new[] { _root }, denyServiceDirectories: false, denySensitiveFiles: false),
                Is.True);
            Assert.That(
                ReadPathPolicy.IsAllowed(InRoot(".env"), new[] { _root }, denyServiceDirectories: false, denySensitiveFiles: false),
                Is.True);
        }

        [Test]
        public void IsUnder_EqualRootAndChild_ReturnsTrue()
        {
            Assert.That(ReadPathPolicy.IsUnder(_root, _root), Is.True);
            Assert.That(ReadPathPolicy.IsUnder(_root, InRoot("file.txt")), Is.True);
        }

        [Test]
        public void IsUnder_NullEmptyOrOutside_ReturnsFalse()
        {
            Assert.That(ReadPathPolicy.IsUnder(null, "x"), Is.False);
            Assert.That(ReadPathPolicy.IsUnder(_root, null), Is.False);
            Assert.That(ReadPathPolicy.IsUnder(_root, Path.Combine(Path.GetTempPath(), "elsewhere", "file.txt")), Is.False);
        }

        [Test]
        public void IsUnder_SiblingWithSamePrefix_ReturnsFalse()
        {
            var sibling = _root + "suffix";
            Assert.That(ReadPathPolicy.IsUnder(_root, Path.Combine(sibling, "file.txt")), Is.False);
        }

        [Test]
        public void IsReadAllowed_ActMode_AllowsAnything()
        {
            Assert.That(ReadPathPolicy.IsReadAllowed(InRoot(".env"), new[] { _root }, AgentMode.Act), Is.True);
        }

        [Test]
        public void IsReadAllowed_PlanMode_EnforcesPolicy()
        {
            Assert.That(ReadPathPolicy.IsReadAllowed(InRoot(".env"), new[] { _root }, AgentMode.Plan), Is.False);
            Assert.That(ReadPathPolicy.IsReadAllowed(InRoot("src", "file.txt"), new[] { _root }, AgentMode.Plan), Is.True);
        }
    }

    // ------------------------------------------------------------------
    // ProviderTranscriptCodec
    // ------------------------------------------------------------------

    [TestFixture]
    public class ProviderTranscriptCodecTests
    {
        [Test]
        public void Serialize_NullOrEmpty_ReturnsEmptyString()
        {
            Assert.That(ProviderTranscriptCodec.Serialize(null), Is.EqualTo(""));
            Assert.That(ProviderTranscriptCodec.Serialize(new List<ChatMessageData>()), Is.EqualTo(""));
        }

        [Test]
        public void Deserialize_NullEmptyOrInvalid_ReturnsEmptyList()
        {
            Assert.That(ProviderTranscriptCodec.Deserialize(null), Is.Empty);
            Assert.That(ProviderTranscriptCodec.Deserialize(""), Is.Empty);
            Assert.That(ProviderTranscriptCodec.Deserialize("   "), Is.Empty);
            Assert.That(ProviderTranscriptCodec.Deserialize("not json"), Is.Empty);
            Assert.That(ProviderTranscriptCodec.Deserialize("null"), Is.Empty);
        }

        [Test]
        public void RoundTrip_PreservesToolCallsAndImages()
        {
            var messages = new List<ChatMessageData>
            {
                new ChatMessageData
                {
                    Role = "user",
                    Content = "hi",
                    Images = new List<ChatImagePart> { new ChatImagePart { MediaType = "image/png", DataBase64 = "abc" } }
                },
                new ChatMessageData
                {
                    Role = "assistant",
                    Content = "",
                    ToolCalls = new List<NativeToolCall>
                    {
                        new NativeToolCall { Id = "call_1", Name = "read_file", Arguments = JObject.Parse("{\"path\":\"a.txt\"}") }
                    }
                },
                new ChatMessageData { Role = "tool", Content = "result", ToolCallId = "call_1" }
            };

            var json = ProviderTranscriptCodec.Serialize(messages);
            var restored = ProviderTranscriptCodec.Deserialize(json);

            Assert.That(restored, Has.Count.EqualTo(3));
            Assert.That(restored[0].Role, Is.EqualTo("user"));
            Assert.That(restored[0].Images, Has.Count.EqualTo(1));
            Assert.That(restored[0].Images[0].MediaType, Is.EqualTo("image/png"));
            Assert.That(restored[0].Images[0].DataBase64, Is.EqualTo("abc"));
            Assert.That(restored[1].ToolCalls, Has.Count.EqualTo(1));
            Assert.That(restored[1].ToolCalls[0].Name, Is.EqualTo("read_file"));
            Assert.That(restored[1].ToolCalls[0].Arguments.Value<string>("path"), Is.EqualTo("a.txt"));
            Assert.That(restored[2].ToolCallId, Is.EqualTo("call_1"));
        }
    }

    // ------------------------------------------------------------------
    // ExceptionFormatter
    // ------------------------------------------------------------------

    [TestFixture]
    public class ExceptionFormatterTests
    {
        [Test]
        public void Format_Null_ReturnsEmpty()
        {
            Assert.That(ExceptionFormatter.Format(null), Is.EqualTo(""));
        }

        [Test]
        public void Format_SimpleException_ReturnsTypeAndMessage()
        {
            var ex = new InvalidOperationException("boom");
            Assert.That(ExceptionFormatter.Format(ex), Is.EqualTo("System.InvalidOperationException: boom"));
        }

        [Test]
        public void Format_InnerException_IncludesInnerError()
        {
            var ex = new Exception("outer", new InvalidOperationException("inner"));
            var text = ExceptionFormatter.Format(ex);

            Assert.That(text, Does.Contain("System.Exception: outer"));
            Assert.That(text, Does.Contain("Inner error: System.InvalidOperationException: inner"));
        }

        [Test]
        public void Format_AggregateException_IncludesEachInnerError()
        {
            var ex = new AggregateException("agg", new Exception("a"), new Exception("b"));
            var text = ExceptionFormatter.Format(ex);

            Assert.That(text, Does.Contain("System.AggregateException: agg"));
            Assert.That(text, Does.Contain("Inner error: System.Exception: a"));
            Assert.That(text, Does.Contain("Inner error: System.Exception: b"));
        }

        [Test]
        public void Format_WithStackTrace_WrapsInFence()
        {
            Exception ex;
            try { throw new Exception("thrown"); }
            catch (Exception caught) { ex = caught; }

            var text = ExceptionFormatter.Format(ex);

            Assert.That(text, Does.Contain("Stack trace:"));
            Assert.That(text, Does.Contain("```"));
        }
    }
}
