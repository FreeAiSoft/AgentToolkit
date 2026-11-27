using System;

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Models;
using Agent.Tests.Fakes;

namespace Agent.Tests
{
    /// <summary>
    /// Locks the settings-driven runtime used by the standalone chat host: a configured provider
    /// must be used instead of the deterministic stub, while an unconfigured provider still falls
    /// back to the stub answer.
    /// </summary>
    [TestFixture]
    public class SettingsRuntimeProviderTests
    {
        [Test]
        public async Task CreateRunner_WithConfiguredProvider_UsesRealProvider()
        {
            var settings = new SettingsModel(new CapturingSettingsStore(new SettingsData
            {
                ProviderId = "openai",
                ApiKey = "sk-test",
                Model = "gpt-test"
            }));
            settings.Run();

            var fake = new FakeStreamingAiProvider((call, progress, ct) =>
            {
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentBegin, SegmentId = "c1" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentDelta, Text = "real answer" });
                progress.Report(new ChatStreamDelta { Phase = ChatStreamPhase.ContentEnd, Text = "real answer" });
                return Task.FromResult(new ChatResponse { Answer = "real answer" });
            });

            var provider = new SettingsRuntimeProvider(settings, aiProviderFactory: _ => fake);
            var runner = provider.CreateRunner();

            var result = await runner.RunAsync("hello", AgentMode.Act, CancellationToken.None, new Progress<ChatStreamDelta>(_ => { }));

            Assert.That(result.FinalText, Is.EqualTo("real answer"));
            Assert.That(fake.StreamCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task CreateRunner_WithoutApiKey_FallsBackToStub()
        {
            var settings = new SettingsModel(new CapturingSettingsStore(new SettingsData
            {
                ProviderId = "openai",
                ApiKey = ""
            }));
            settings.Run();

            var provider = new SettingsRuntimeProvider(settings);
            var runner = provider.CreateRunner();

            var result = await runner.RunAsync("ты утл", AgentMode.Act, CancellationToken.None);

            Assert.That(result.FinalText, Does.Contain("Заглушка"));
        }

        private sealed class CapturingSettingsStore : ISettingsStore
        {
            private SettingsData _data;

            public CapturingSettingsStore(SettingsData data) => _data = data ?? new SettingsData();

            public SettingsData Load() => _data;

            public void Save(SettingsData settings) => _data = settings;
        }
    }
}
