using System;
using NUnit.Framework;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class TabDefinitionTests
    {
        [Test]
        public void Constructor_NullKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TabDefinition(null, "Header"));
        }

        [Test]
        public void Constructor_EmptyKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TabDefinition("", "Header"));
        }

        [Test]
        public void Constructor_NullHeader_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TabDefinition("Key", null));
        }

        [Test]
        public void Constructor_EmptyHeader_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TabDefinition("Key", ""));
        }

        [Test]
        public void Constructor_ValidArgs_SetsAllProperties()
        {
            var tab = new TabDefinition("Spec", "Spec", "Spec tooltip", "FileDocumentOutline", "FileDocument");

            Assert.That(tab.Key, Is.EqualTo("Spec"));
            Assert.That(tab.Header, Is.EqualTo("Spec"));
            Assert.That(tab.ToolTip, Is.EqualTo("Spec tooltip"));
            Assert.That(tab.OutlineIconKey, Is.EqualTo("FileDocumentOutline"));
            Assert.That(tab.FilledIconKey, Is.EqualTo("FileDocument"));
        }

        [Test]
        public void CoreTabs_Chat_HasCorrectKeyAndIcons()
        {
            Assert.That(CoreTabs.Chat.Key, Is.EqualTo("Chat"));
            Assert.That(CoreTabs.Chat.OutlineIconKey, Is.EqualTo("MessageOutline"));
            Assert.That(CoreTabs.Chat.FilledIconKey, Is.EqualTo("Message"));
        }

        [Test]
        public void CoreTabs_Settings_HasCorrectKeyAndIcons()
        {
            Assert.That(CoreTabs.Settings.Key, Is.EqualTo("Settings"));
            Assert.That(CoreTabs.Settings.OutlineIconKey, Is.EqualTo("CogOutline"));
            Assert.That(CoreTabs.Settings.FilledIconKey, Is.EqualTo("Cog"));
        }

        [Test]
        public void CoreTabs_All_ContainsChatAndSettingsInOrder()
        {
            Assert.That(CoreTabs.All.Count, Is.EqualTo(2));
            Assert.That(CoreTabs.All[0].Key, Is.EqualTo("Chat"));
            Assert.That(CoreTabs.All[1].Key, Is.EqualTo("Settings"));
        }

        [Test]
        public void Add_Null_Throws()
        {
            var collection = new TabDefinitionCollection();
            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
        }

        [Test]
        public void Add_DuplicateKey_Throws()
        {
            var collection = new TabDefinitionCollection();
            collection.Add(new TabDefinition("Chat", "Chat"));

            Assert.Throws<ArgumentException>(() => collection.Add(new TabDefinition("Chat", "Chat 2")));
        }

        [Test]
        public void TryGet_ExistingKey_ReturnsDefinition()
        {
            var collection = new TabDefinitionCollection();
            var expected = new TabDefinition("Spec", "Spec");
            collection.Add(expected);

            Assert.That(collection.TryGet("Spec", out var actual), Is.True);
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void TryGet_MissingKey_ReturnsFalse()
        {
            var collection = new TabDefinitionCollection();
            collection.Add(new TabDefinition("Spec", "Spec"));

            Assert.That(collection.TryGet("Missing", out var actual), Is.False);
            Assert.That(actual, Is.Null);
        }

        [Test]
        public void ContainsKey_ExistingKey_ReturnsTrue()
        {
            var collection = new TabDefinitionCollection();
            collection.Add(new TabDefinition("Spec", "Spec"));

            Assert.That(collection.ContainsKey("Spec"), Is.True);
        }

        [Test]
        public void ContainsKey_MissingKey_ReturnsFalse()
        {
            var collection = new TabDefinitionCollection();
            collection.Add(new TabDefinition("Spec", "Spec"));

            Assert.That(collection.ContainsKey("Missing"), Is.False);
        }

        [Test]
        public void IndexOf_ReturnsCorrectIndex()
        {
            var collection = new TabDefinitionCollection();
            collection.Add(new TabDefinition("Chat", "Chat"));
            collection.Add(new TabDefinition("Spec", "Spec"));
            collection.Add(new TabDefinition("Settings", "Settings"));

            Assert.That(collection.IndexOf("Chat"), Is.EqualTo(0));
            Assert.That(collection.IndexOf("Spec"), Is.EqualTo(1));
            Assert.That(collection.IndexOf("Settings"), Is.EqualTo(2));
            Assert.That(collection.IndexOf("Missing"), Is.EqualTo(-1));
        }
    }
}
