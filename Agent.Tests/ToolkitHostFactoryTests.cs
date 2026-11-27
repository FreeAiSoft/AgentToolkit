using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Core.Stores;
using Agent.Models;

namespace Agent.Tests
{
    /// <summary>
    /// Locks the self-contained chat graph assembled by <see cref="ToolkitHostFactory"/>: defaults
    /// must build a working Chat/Settings pair, overrides must win, and the default store must
    /// persist settings under <c>&lt;appData&gt;\AgentToolkit\toolkit.db</c>.
    /// </summary>
    [TestFixture]
    public class ToolkitHostFactoryTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "AgentToolkitFactoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort cleanup; SQLite may briefly keep the file handle open */ }
        }

        [Test]
        public void Build_WithDefaults_CreatesModelsAndResolvesDefaultSeams()
        {
            var context = ToolkitHostFactory.Build(new ToolkitChatConfiguration { AppDataDirectory = _tempRoot });

            Assert.That(context.Chat, Is.Not.Null);
            Assert.That(context.Settings, Is.Not.Null);
            Assert.That(context.SettingsStore, Is.InstanceOf<ToolkitSettingsStore>());
            Assert.That(context.HistoryStore, Is.InstanceOf<InMemoryChatHistoryStore>());
            Assert.That(context.AppDataDirectory, Is.EqualTo(_tempRoot));
        }

        [Test]
        public void Build_WithOverrides_UsesProvidedSeams()
        {
            var settingsStore = new SentinelSettingsStore();
            var historyStore = new SentinelHistoryStore();

            var context = ToolkitHostFactory.Build(new ToolkitChatConfiguration
            {
                AppDataDirectory = _tempRoot,
                SettingsStore = settingsStore,
                HistoryStore = historyStore
            });

            Assert.That(context.SettingsStore, Is.SameAs(settingsStore));
            Assert.That(context.HistoryStore, Is.SameAs(historyStore));
        }

        [Test]
        public void Build_DefaultStore_PersistsSettingsToToolkitDb()
        {
            var context = ToolkitHostFactory.Build(new ToolkitChatConfiguration { AppDataDirectory = _tempRoot });

            context.Settings.BaseUrl = "https://example.test/v1";
            context.Settings.SaveSettingsCommand();

            var db = Path.Combine(_tempRoot, "AgentToolkit", "toolkit.db");
            Assert.That(File.Exists(db), Is.True,
                "Settings must be persisted under <appData>\\AgentToolkit\\toolkit.db");

            var reloaded = context.SettingsStore.Load();
            Assert.That(reloaded.BaseUrl, Is.EqualTo("https://example.test/v1"));
        }

        [Test]
        public void DefaultAppDataDirectory_IsApplicationDataRoot()
        {
            Assert.That(
                ToolkitHostFactory.DefaultAppDataDirectory,
                Is.EqualTo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
        }

        private sealed class SentinelSettingsStore : ISettingsStore
        {
            public SettingsData Load() => new SettingsData();
            public void Save(SettingsData settings) { }
        }

        private sealed class SentinelHistoryStore : IChatHistoryStore
        {
            public string WorkspaceId { get; set; } = "";

            public void Initialize() { }

            public IReadOnlyList<ChatConversation> ListConversations(int limit = 50)
                => new List<ChatConversation>();

            public ChatConversation LoadConversation(string id) => null;

            public void SaveConversation(ChatConversation conversation) { }

            public void SetConversationImportant(string id, bool isImportant) { }

            public void DeleteConversation(string id) { }

            public void DeleteAllConversations() { }
        }
    }
}
