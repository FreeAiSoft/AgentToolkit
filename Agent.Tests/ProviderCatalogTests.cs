using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Providers;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class ProviderCatalogTests
    {
        private static string Message(string content)
        {
            return "{\"id\":\"msg_1\",\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"" + content + "\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":10,\"output_tokens\":5}}";
        }

        [Test]
        public void AiProviderId_Normalize_MapsAliasesAndDefaults()
        {
            Assert.That(AiProviderId.Normalize(null), Is.EqualTo(AiProviderId.OpenAi));
            Assert.That(AiProviderId.Normalize(""), Is.EqualTo(AiProviderId.OpenAi));
            Assert.That(AiProviderId.Normalize("claude"), Is.EqualTo(AiProviderId.Anthropic));
            Assert.That(AiProviderId.Normalize("azure"), Is.EqualTo(AiProviderId.AzureOpenAi));
            Assert.That(AiProviderId.Normalize("openai-compatible"), Is.EqualTo(AiProviderId.OpenAiCompatible));
        }

        [Test]
        public void AiProviderCatalog_Find_ResolvesSpecs()
        {
            Assert.That(AiProviderCatalog.Find("anthropic").Protocol, Is.EqualTo(AiProviderProtocol.Anthropic));
            Assert.That(AiProviderCatalog.Find("ollama").RequiresApiKey, Is.False);
            Assert.That(AiProviderCatalog.Find("ollama").DefaultBaseUrl, Is.EqualTo("http://localhost:11434/v1"));
            Assert.That(AiProviderCatalog.Find("openai").Protocol, Is.EqualTo(AiProviderProtocol.OpenAiCompatible));
            Assert.That(AiProviderCatalog.Find("nope"), Is.Null);
        }

        [Test]
        public void AiProviderFactory_NoKey_ReturnsStub()
        {
            var provider = AiProviderFactory.Create(new SettingsData { ApiKey = "" });
            Assert.That(provider, Is.InstanceOf<StubAiProvider>());
        }

        [Test]
        public void AiProviderFactory_Ollama_NoKey_ReturnsOpenAiCompatible()
        {
            var provider = AiProviderFactory.Create(new SettingsData { ProviderId = AiProviderId.Ollama, ApiKey = "" });
            Assert.That(provider, Is.InstanceOf<OpenAiCompatibleProvider>());
        }

        [Test]
        public void AiProviderFactory_Anthropic_ReturnsAnthropicProvider()
        {
            var provider = AiProviderFactory.Create(new SettingsData { ProviderId = AiProviderId.Anthropic, ApiKey = "sk-ant" });
            Assert.That(provider, Is.InstanceOf<AnthropicProvider>());
        }

        [Test]
        public async Task AnthropicProvider_ChatAsync_BuildsMessagesRequest()
        {
            var handler = new FakeHttpHandler { Responder = req => Message("Hi") };
            var provider = new AnthropicProvider(new AnthropicOptions { ApiKey = "sk-ant", Model = "claude-test" }, new HttpClient(handler));

            await provider.ChatAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "hello" } },
                "sys-prompt");

            Assert.That(handler.LastRequest.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.LastRequest.RequestUri.ToString(), Is.EqualTo("https://api.anthropic.com/v1/messages"));

            handler.LastRequest.Headers.TryGetValues("x-api-key", out var keys);
            Assert.That(keys.FirstOrDefault(), Is.EqualTo("sk-ant"));
            handler.LastRequest.Headers.TryGetValues("anthropic-version", out var versions);
            Assert.That(versions.FirstOrDefault(), Is.EqualTo("2023-06-01"));

            var body = JObject.Parse(handler.LastBody);
            Assert.That(body["model"].Value<string>(), Is.EqualTo("claude-test"));
            Assert.That(body["max_tokens"].Value<int>(), Is.EqualTo(8192));
            Assert.That(body["system"][0]["cache_control"]["type"].Value<string>(), Is.EqualTo("ephemeral"));
            Assert.That(body["messages"][0]["role"].Value<string>(), Is.EqualTo("user"));
            Assert.That(body["messages"][0]["content"].Value<string>(), Is.EqualTo("hello"));
        }

        [Test]
        public async Task AnthropicProvider_ChatAsync_ParsesTextThinkingAndToolUse()
        {
            var handler = new FakeHttpHandler
            {
                Responder = req =>
                    "{\"content\":[" +
                    "{\"type\":\"thinking\",\"thinking\":\"let me think\"}," +
                    "{\"type\":\"text\",\"text\":\"Hello\"}," +
                    "{\"type\":\"tool_use\",\"id\":\"toolu_1\",\"name\":\"read_files\",\"input\":{\"path\":\"a.cs\"}}]," +
                    "\"stop_reason\":\"tool_use\"," +
                    "\"usage\":{\"input_tokens\":10,\"output_tokens\":5,\"cache_read_input_tokens\":3}}"
            };
            var provider = new AnthropicProvider(new AnthropicOptions { ApiKey = "sk-ant", Model = "claude-test" }, new HttpClient(handler));

            var reply = await provider.ChatAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "read a.cs" } },
                "system");

            Assert.That(reply.Answer, Is.EqualTo("Hello"));
            Assert.That(reply.Reasoning, Is.EqualTo("let me think"));
            Assert.That(reply.FinishReason, Is.EqualTo("tool_use"));
            Assert.That(reply.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(reply.ToolCalls[0].Name, Is.EqualTo("read_files"));
            Assert.That(reply.ToolCalls[0].Arguments["path"].Value<string>(), Is.EqualTo("a.cs"));

            Assert.That(provider.TotalTokenUsage.PromptTokens, Is.EqualTo(10));
            Assert.That(provider.TotalTokenUsage.CompletionTokens, Is.EqualTo(5));
            Assert.That(provider.TotalTokenUsage.CacheHitTokens, Is.EqualTo(3));
            Assert.That(provider.TotalTokenUsage.CacheMissTokens, Is.EqualTo(7));
        }


        [Test]
        public async Task AnthropicProvider_ChatStreamingAsync_ParsesSse()
        {
            var sse =
                "event: message_start\n" +
                "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":10,\"cache_read_input_tokens\":2}}}\n\n" +
                "event: content_block_start\n" +
                "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n" +
                "event: content_block_delta\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n" +
                "event: content_block_stop\n" +
                "data: {\"type\":\"content_block_stop\",\"index\":0}\n\n" +
                "event: message_delta\n" +
                "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":5}}\n\n" +
                "event: message_stop\n" +
                "data: {\"type\":\"message_stop\"}\n\n";

            var handler = new FakeHttpHandler { Responder = req => sse };
            var provider = new AnthropicProvider(new AnthropicOptions { ApiKey = "sk-ant", Model = "claude-test" }, new HttpClient(handler));
            var progress = new SyncProgress<ChatStreamDelta>();

            var reply = await provider.ChatStreamingAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "hello" } },
                "system",
                progress);

            Assert.That(reply.Answer, Is.EqualTo("Hello"));
            Assert.That(reply.FinishReason, Is.EqualTo("end_turn"));
            Assert.That(progress.Reported.Any(d => d.ContentDelta == "Hello"), Is.True);
            Assert.That(progress.Reported.Any(d => d.Phase == ChatStreamPhase.ContentBegin), Is.True);
            Assert.That(progress.Reported.Any(d => d.Phase == ChatStreamPhase.ContentEnd), Is.True);
            Assert.That(provider.TotalTokenUsage.PromptTokens, Is.EqualTo(10));
            Assert.That(provider.TotalTokenUsage.CompletionTokens, Is.EqualTo(5));
        }

        [Test]
        public async Task OllamaModelSource_GetModels_ParsesTags()
        {
            var handler = new FakeHttpHandler
            {
                Responder = req => "{\"models\":[{\"name\":\"llama3:latest\"},{\"name\":\"qwen2.5:7b\"}]}"
            };
            var source = new OllamaModelSource("http://localhost:11434", new HttpClient(handler));

            var models = await source.GetModelsAsync();

            Assert.That(models.Count, Is.EqualTo(2));
            Assert.That(models[0].Id, Is.EqualTo("llama3:latest"));
            Assert.That(models[1].Id, Is.EqualTo("qwen2.5:7b"));
            Assert.That(handler.LastRequest.RequestUri.ToString(), Is.EqualTo("http://localhost:11434/api/tags"));
        }
    }
}

