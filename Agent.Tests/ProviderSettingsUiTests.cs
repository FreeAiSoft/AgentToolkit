using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Providers;
using Agent.Core.Services;
using Agent.Models;

namespace Agent.Tests
{
    /// <summary>
    /// Locks the provider-selection UI contract: the settings model must expose a provider index,
    /// provider-specific field visibility and Azure defaults, and the OpenAI-compatible provider
    /// must build the Azure wire format (<c>deployments/{name}</c> + <c>api-version</c> + <c>api-key</c>).
    /// </summary>
    [TestFixture]
    public class ProviderSettingsUiTests
    {
        [Test]
        public void SettingsModel_ProviderIndex_RoundTripsProviderId_ForEveryProvider()
        {
            var model = NewModel(new SettingsData());
            model.Load();

            var all = AiProviderCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                model.ProviderIndex = i;
                Assert.That(model.ProviderId, Is.EqualTo(all[i].Id),
                    "ProviderIndex {0} should map to catalog id {1}", i, all[i].Id);
                Assert.That(model.ProviderIndex, Is.EqualTo(i));
            }
        }

        [Test]
        public void SettingsModel_UnknownProviderId_NormalizesToOpenAi()
        {
            var model = NewModel(new SettingsData { ProviderId = "does-not-exist" });
            model.Load();

            Assert.That(model.ProviderId, Is.EqualTo(AiProviderId.OpenAi));
        }

        [Test]
        public void SettingsModel_ApplyProviderDefaults_Ollama_FillsBaseUrlModelAndWindow()
        {
            var model = NewModel(new SettingsData());
            model.Load();
            model.ProviderIndex = IndexOf(AiProviderId.Ollama);

            Assert.That(model.BaseUrl, Is.EqualTo("http://localhost:11434/v1"));
            Assert.That(model.Model, Is.EqualTo("llama3.2"));
            Assert.That(model.ContextWindowSize, Is.EqualTo(8192));
        }

        [Test]
        public void SettingsModel_ApplyProviderDefaults_Anthropic_FillsEndpointAndModel()
        {
            var model = NewModel(new SettingsData());
            model.Load();
            model.ProviderIndex = IndexOf(AiProviderId.Anthropic);

            Assert.That(model.BaseUrl, Is.EqualTo("https://api.anthropic.com"));
            Assert.That(model.Model, Is.EqualTo("claude-sonnet-4-5"));
        }

        [Test]
        public void SettingsModel_ApplyProviderDefaults_Azure_FillsAzureApiVersion()
        {
            var model = NewModel(new SettingsData());
            model.Load();
            model.ProviderIndex = IndexOf(AiProviderId.AzureOpenAi);

            var spec = AiProviderCatalog.Find(AiProviderId.AzureOpenAi);
            Assert.That(model.AzureApiVersion, Is.EqualTo(spec.DefaultAzureApiVersion));
        }

        [Test]
        public void SettingsModel_ApplyProviderDefaults_DoesNotOverwriteUserAzureApiVersion()
        {
            var model = NewModel(new SettingsData());
            model.Load();
            model.AzureApiVersion = "2025-01-01-preview";
            model.ProviderIndex = IndexOf(AiProviderId.AzureOpenAi);

            Assert.That(model.AzureApiVersion, Is.EqualTo("2025-01-01-preview"));
        }

        [Test]
        public void SettingsModel_IsApiKeyVisible_False_ForOllamaAndLmStudio()
        {
            var model = NewModel(new SettingsData());
            model.Load();

            model.ProviderIndex = IndexOf(AiProviderId.Ollama);
            Assert.That(model.IsApiKeyVisible, Is.False);

            model.ProviderIndex = IndexOf(AiProviderId.LmStudio);
            Assert.That(model.IsApiKeyVisible, Is.False);

            model.ProviderIndex = IndexOf(AiProviderId.OpenAi);
            Assert.That(model.IsApiKeyVisible, Is.True);
        }

        [Test]
        public void SettingsModel_IsAzureVisible_True_OnlyForAzure()
        {
            var model = NewModel(new SettingsData());
            model.Load();

            model.ProviderIndex = IndexOf(AiProviderId.AzureOpenAi);
            Assert.That(model.IsAzureVisible, Is.True);

            model.ProviderIndex = IndexOf(AiProviderId.OpenAi);
            Assert.That(model.IsAzureVisible, Is.False);

            model.ProviderIndex = IndexOf(AiProviderId.Anthropic);
            Assert.That(model.IsAzureVisible, Is.False);
        }

        [Test]
        public void SettingsModel_Save_Load_RoundTripsAzureFields()
        {
            var store = new CapturingSettingsStore(new SettingsData { ProviderId = AiProviderId.AzureOpenAi });
            var model = new SettingsModel(store);
            model.Load();
            model.AzureApiVersion = "2024-10-21";
            model.AzureDeploymentName = "my-deployment";

            model.SaveSettingsCommand();

            Assert.That(store.Data.AzureApiVersion, Is.EqualTo("2024-10-21"));
            Assert.That(store.Data.AzureDeploymentName, Is.EqualTo("my-deployment"));
        }

        [Test]
        public async Task OpenAiProvider_Azure_BuildsAzureUrl_AndApiKeyHeader()
        {
            var handler = new FakeHttpHandler { Responder = req => Completion("ok") };
            var provider = new OpenAiCompatibleProvider(
                new OpenAiOptions
                {
                    BaseUrl = "https://my-resource.openai.azure.com",
                    ApiKey = "azure-key",
                    Model = "gpt-4o-mini",
                    IsAzure = true,
                    AzureApiVersion = "2024-06-01",
                    AzureDeploymentName = "my-deployment"
                },
                new HttpClient(handler));

            await provider.ChatAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "hello" } },
                "system");

            var uri = handler.LastRequest.RequestUri;
            Assert.That(uri.AbsolutePath, Is.EqualTo("/openai/deployments/my-deployment/chat/completions"));
            Assert.That(uri.Query, Does.Contain("api-version=2024-06-01"));

            Assert.That(handler.LastRequest.Headers.TryGetValues("api-key", out var apiKeys), Is.True);
            Assert.That(apiKeys.Single(), Is.EqualTo("azure-key"));
            Assert.That(handler.LastRequest.Headers.Authorization, Is.Null);
        }

        [Test]
        public async Task OpenAiProvider_Azure_EmptyDeployment_FallsBackToModel()
        {
            var handler = new FakeHttpHandler { Responder = req => Completion("ok") };
            var provider = new OpenAiCompatibleProvider(
                new OpenAiOptions
                {
                    BaseUrl = "https://my-resource.openai.azure.com",
                    ApiKey = "azure-key",
                    Model = "gpt-4o-mini",
                    IsAzure = true,
                    AzureApiVersion = "2024-06-01",
                    AzureDeploymentName = ""
                },
                new HttpClient(handler));

            await provider.ChatAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "hello" } },
                "system");

            Assert.That(handler.LastRequest.RequestUri.AbsolutePath,
                Is.EqualTo("/openai/deployments/gpt-4o-mini/chat/completions"));
        }

        [Test]
        public async Task OpenAiProvider_NonAzure_UsesBearerAndPlainUrl()
        {
            var handler = new FakeHttpHandler { Responder = req => Completion("ok") };
            var provider = new OpenAiCompatibleProvider(
                new OpenAiOptions { BaseUrl = "https://api.openai.com/v1", ApiKey = "sk-test", Model = "gpt-4o-mini" },
                new HttpClient(handler));

            await provider.ChatAsync(
                new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = "hello" } },
                "system");

            Assert.That(handler.LastRequest.RequestUri.ToString(), Is.EqualTo("https://api.openai.com/v1/chat/completions"));
            Assert.That(handler.LastRequest.Headers.Authorization.Scheme, Is.EqualTo("Bearer"));
            Assert.That(handler.LastRequest.Headers.Authorization.Parameter, Is.EqualTo("sk-test"));
        }

        [Test]
        public void SettingsData_Defaults_AzureFieldsAreEmpty()
        {
            var data = new SettingsData();
            Assert.That(data.AzureApiVersion, Is.EqualTo(""));
            Assert.That(data.AzureDeploymentName, Is.EqualTo(""));
        }

        private static int IndexOf(string providerId)
        {
            var all = AiProviderCatalog.All;
            for (var i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Id, providerId, StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        private static SettingsModel NewModel(SettingsData data)
            => new SettingsModel(new CapturingSettingsStore(data));

        private static string Completion(string content)
            => "{\"choices\":[{\"message\":{\"content\":\"" + content + "\"}}]}";

        private sealed class CapturingSettingsStore : ISettingsStore
        {
            public SettingsData Data;

            public CapturingSettingsStore(SettingsData data) => Data = data ?? new SettingsData();

            public SettingsData Load() => Data;

            public void Save(SettingsData settings) => Data = settings;
        }
    }
}
