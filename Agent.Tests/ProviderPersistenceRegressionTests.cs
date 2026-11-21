using System;
using System.IO;
using Agent.Core.Contracts;
using Agent.Core.Providers;
using Agent.Core.Services;
using Agent.Models;
using NUnit.Framework;

namespace Agent.Tests
{
    [TestFixture]
    public class ProviderPersistenceRegressionTests
    {
        private string _root;

        [SetUp]
        public void SetUp() =>
            _root = Path.Combine(Path.GetTempPath(), "provider_persist_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }

        [Test]
        public void ProviderSettings_SurviveRestart_EvenWhenWorkspaceChanges()
        {
            var saveStore = new ToolkitSettingsStore(_root, new FakeWorkspaceProvider("workspace-a"));
            var model = new SettingsModel(saveStore);
            model.Load();

            model.ProviderIndex = IndexOf(AiProviderId.AzureOpenAi);
            model.BaseUrl = "https://my-resource.openai.azure.com";
            model.Model = "gpt-4o-mini";
            model.AzureApiVersion = "2024-10-21";
            model.AzureDeploymentName = "my-deployment";
            model.SaveSettingsCommand();

            var restartedStore = new ToolkitSettingsStore(_root, null);
            var restartedModel = new SettingsModel(restartedStore);
            restartedModel.Load();

            Assert.That(restartedModel.ProviderId, Is.EqualTo(AiProviderId.AzureOpenAi));
            Assert.That(restartedModel.BaseUrl, Is.EqualTo("https://my-resource.openai.azure.com"));
            Assert.That(restartedModel.Model, Is.EqualTo("gpt-4o-mini"));
            Assert.That(restartedModel.AzureApiVersion, Is.EqualTo("2024-10-21"));
            Assert.That(restartedModel.AzureDeploymentName, Is.EqualTo("my-deployment"));
        }

        [Test]
        public void ApiKey_SurvivesRestart_WhenNoSecretsStoreIsInjected()
        {
            var saveStore = new ToolkitSettingsStore(_root, new FakeWorkspaceProvider("workspace-a"));
            var model = new SettingsModel(saveStore);
            model.Load();

            model.ProviderIndex = IndexOf(AiProviderId.OpenAi);
            model.BaseUrl = "https://api.openai.com/v1";
            model.ApiKey = "sk-test-123";
            model.SaveSettingsCommand();

            var restartedStore = new ToolkitSettingsStore(_root, null);
            var restartedModel = new SettingsModel(restartedStore);
            restartedModel.Load();

            Assert.That(restartedModel.ApiKey, Is.EqualTo("sk-test-123"));
        }

        [Test]
        public void ApiKey_SurvivesProviderSwitch_WhenNoSecretsStoreIsInjected()
        {
            var saveStore = new ToolkitSettingsStore(_root);
            var model = new SettingsModel(saveStore);
            model.Load();

            model.ProviderIndex = IndexOf(AiProviderId.DeepSeek);
            model.ApiKey = "sk-deepseek-test";
            model.SaveSettingsCommand();

            var restartedModel = new SettingsModel(new ToolkitSettingsStore(_root));
            restartedModel.Load();

            Assert.That(restartedModel.ProviderId, Is.EqualTo(AiProviderId.DeepSeek));
            Assert.That(restartedModel.ApiKey, Is.EqualTo("sk-deepseek-test"));
        }


        [Test]
        public void ApiKey_IsKeptInSettingsPayload_WhenNoSecretsStoreIsInjected()
        {
            var store = new CapturingSettingsStore(new SettingsData { ProviderId = AiProviderId.OpenAi });
            var model = new SettingsModel(store);
            model.Load();
            model.ApiKey = "sk-test-123";

            model.SaveSettingsCommand();

            Assert.That(store.Data.ApiKey, Is.EqualTo("sk-test-123"),
                "Without ISecretsStore the API key must stay in SettingsData so the plain/legacy store persists it.");
        }

        [Test]
        public void ApiKey_IsRoutedToSecretsStore_AndClearedFromPayload_WhenSecretsStoreIsInjected()
        {
            var store = new CapturingSettingsStore(new SettingsData { ProviderId = AiProviderId.OpenAi });
            var secrets = new FakeSecretsStore();
            var model = new SettingsModel(store, secretsStore: secrets);
            model.Load();
            model.ApiKey = "sk-test-456";

            model.SaveSettingsCommand();

            Assert.That(secrets.Key, Is.EqualTo("sk-test-456"),
                "With ISecretsStore the API key must be routed to the secrets store.");
            Assert.That(store.Data.ApiKey, Is.EqualTo(""),
                "With ISecretsStore the API key must not leak into the plain settings payload.");
        }

        private static int IndexOf(string providerId)
        {
            var all = AiProviderCatalog.All;
            for (var i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Id, providerId, StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        private sealed class FakeWorkspaceProvider : IWorkspaceProvider
        {
            private readonly WorkspaceInfo _workspace;

            public FakeWorkspaceProvider(string workspaceId) =>
                _workspace = new WorkspaceInfo
                {
                    Id = workspaceId,
                    Path = "C:\\fake\\" + workspaceId,
                    Name = workspaceId
                };

            public WorkspaceInfo GetCurrentWorkspace() => _workspace;

            public event EventHandler WorkspaceChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class CapturingSettingsStore : ISettingsStore
        {
            public SettingsData Data;

            public CapturingSettingsStore(SettingsData data) => Data = data ?? new SettingsData();

            public SettingsData Load() => Data;

            public void Save(SettingsData settings) => Data = settings;
        }

        private sealed class FakeSecretsStore : ISecretsStore
        {
            public string Key { get; private set; } = "";

            public string LoadApiKey() => Key;

            public void SaveApiKey(string apiKey) => Key = apiKey ?? "";

            public void ClearApiKey() => Key = "";
        }
    }
}
