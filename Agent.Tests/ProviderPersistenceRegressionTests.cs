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
    }
}
