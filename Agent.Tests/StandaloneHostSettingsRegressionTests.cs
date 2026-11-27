using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Agent.Core.Models;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    /// <summary>
    /// Regression lock for the standalone chat host bug: the host answered with the
    /// deterministic stub even though an OpenAI-compatible provider was configured on the
    /// Settings tab. These tests prove the settings persisted by the toolkit's own store flow
    /// into the runtime that the standalone host uses.
    /// </summary>
    [TestFixture]
    public class StandaloneHostSettingsRegressionTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "AgentToolkitStandaloneHostRegression", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort cleanup; SQLite may briefly keep the file handle open */ }
        }

        [Test]
        public async Task ConfiguredProviderInToolkitStore_IsUsedByRuntime()
        {
            // Build the same graph the standalone host builds (real SQLite settings store).
            var context = ToolkitHostFactory.Build(new ToolkitChatConfiguration { AppDataDirectory = _tempRoot });

            // Configure and persist the provider exactly like the Settings tab does.
            context.Settings.ProviderId = "openai";
            context.Settings.ApiKey = "sk-test";
            context.Settings.Model = "gpt-test";
            context.Settings.SaveSettingsCommand();

            var fake = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "configured reply" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "configured reply" });
                return Task.FromResult(new ChatResponse { Answer = "configured reply" });
            });

            // The standalone host's default runtime reads the provider from SettingsModel.
            var runtime = new SettingsRuntimeProvider(context.Settings, aiProviderFactory: _ => fake);
            var runner = runtime.CreateRunner();

            var result = await runner.RunAsync("hello", AgentMode.Act, CancellationToken.None, new Progress<ChatStreamDelta>(_ => { }));

            Assert.That(result.FinalText, Is.EqualTo("configured reply"),
                "The configured provider must be used; the stub answer means the settings were not wired into the runtime.");
            Assert.That(fake.StreamCalls, Is.EqualTo(1));
        }
    }
}
