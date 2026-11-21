using System;
using System.IO;
using Agent.Core.Contracts;
using Agent.Core.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Agent.Tests
{
    [TestFixture]
    public class SqliteSettingsStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "toolkit_settings_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        [Test]
        public void Save_ThenLoad_RoundTripsSettings()
        {
            var store = new ToolkitSettingsStore(_root);
            var settings = new SettingsData
            {
                Model = "custom-model",
                Temperature = 0.7,
                ContextWindowSize = 123456,
                AllowReadFiles = false
            };

            store.Save(settings);

            var loaded = store.Load();
            Assert.That(loaded.Model, Is.EqualTo("custom-model"));
            Assert.That(loaded.Temperature, Is.EqualTo(0.7));
            Assert.That(loaded.ContextWindowSize, Is.EqualTo(123456));
            Assert.That(loaded.AllowReadFiles, Is.False);
        }

        [Test]
        public void Load_OnEmptyDatabase_ReturnsNormalizedDefaults()
        {
            var store = new ToolkitSettingsStore(_root);
            var defaults = new SettingsData();
            SettingsData.Normalize(defaults);

            var loaded = store.Load();

            Assert.That(loaded.Model, Is.EqualTo(defaults.Model));
            Assert.That(loaded.BaseUrl, Is.EqualTo(defaults.BaseUrl));
            Assert.That(loaded.ContextWindowSize, Is.EqualTo(defaults.ContextWindowSize));
            Assert.That(loaded.Temperature, Is.EqualTo(defaults.Temperature));
        }
        [Test]
        public void Load_WithDifferentWorkspaceProviders_UsesSharedGlobalDatabase()
        {
            var workspaceA = new FakeWorkspaceProvider("workspace-a");
            var workspaceB = new FakeWorkspaceProvider("workspace-b");

            var storeA = new ToolkitSettingsStore(_root, workspaceA);
            var storeB = new ToolkitSettingsStore(_root, workspaceB);

            storeA.Save(new SettingsData { Model = "model-a" });
            storeB.Save(new SettingsData { Model = "model-b" });

            // Settings are global: the two stores share the same toolkit.db regardless of the
            // workspace provider, so the last write wins and no per-workspace folders are created.
            Assert.That(storeA.Load().Model, Is.EqualTo("model-b"));
            Assert.That(storeB.Load().Model, Is.EqualTo("model-b"));

            Assert.That(File.Exists(ToolkitStoragePaths.SettingsDatabase(_root, "")), Is.True);
            Assert.That(Directory.Exists(ToolkitStoragePaths.WorkspacesRoot(_root)), Is.False);
        }



        [Test]
        public void Load_WithoutWorkspaceProvider_UsesGlobalDatabase()
        {
            var store = new ToolkitSettingsStore(_root);

            store.Save(new SettingsData { Model = "global-model" });

            Assert.That(store.Load().Model, Is.EqualTo("global-model"));
            Assert.That(File.Exists(ToolkitStoragePaths.SettingsDatabase(_root, "")), Is.True);
            Assert.That(Directory.Exists(ToolkitStoragePaths.WorkspacesRoot(_root)), Is.False);
        }

        [Test]
        public void Load_WithLegacySettingsFile_ImportsOnce()
        {
            var legacyFile = Path.Combine(_root, "legacy", "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyFile));
            File.WriteAllText(legacyFile, JsonConvert.SerializeObject(new SettingsData
            {
                Model = "legacy-model",
                Temperature = 0.42
            }));

            var store = new ToolkitSettingsStore(_root, null, legacyFile);

            var loaded = store.Load();

            Assert.That(loaded.Model, Is.EqualTo("legacy-model"));
            Assert.That(loaded.Temperature, Is.EqualTo(0.42));

            // The legacy file is preserved; a subsequent load must read the migrated database row.
            File.WriteAllText(legacyFile, JsonConvert.SerializeObject(new SettingsData { Model = "changed-legacy" }));
            Assert.That(store.Load().Model, Is.EqualTo("legacy-model"));
        }

        [Test]
        public void Store_CreatesFolderAndDatabaseLazily()
        {
            var store = new ToolkitSettingsStore(_root);

            Assert.That(Directory.Exists(ToolkitStoragePaths.ToolkitDirectory(_root)), Is.False);

            store.Load();

            Assert.That(Directory.Exists(ToolkitStoragePaths.ToolkitDirectory(_root)), Is.True);
            Assert.That(File.Exists(ToolkitStoragePaths.SettingsDatabase(_root, "")), Is.True);
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
