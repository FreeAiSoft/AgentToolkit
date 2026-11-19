using Agent.Core.Contracts;
using Agent.Core.Services;
using Agent.Models;
using Agent.Mvvm;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Locks the model contract the shared <c>SettingsControl</c> relies on to render its
    /// own save/notify toast: <c>SaveSettingsCommand</c> raises <c>SettingsSaved</c> and
    /// <c>NotifyMessage</c> raises <c>Notify</c> with the original message and severity.
    /// </summary>
    [TestFixture]
    public class SettingsModelNotifyTests
    {
        [Test]
        public void SaveSettingsCommand_RaisesSettingsSaved()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            var raised = 0;
            model.SettingsSaved += (_, __) => raised++;

            model.SaveSettingsCommand();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void NotifyMessage_RaisesNotify_WithMessageAndLevel()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            Notify received = null;
            model.Notify += (_, notify) => received = notify;

            model.NotifyMessage(new Notify { Message = "hello", NotifyLevel = NotifyLevel.Success });

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Message, Is.EqualTo("hello"));
            Assert.That(received.NotifyLevel, Is.EqualTo(NotifyLevel.Success));
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            private readonly SettingsData _data;

            public FakeSettingsStore(SettingsData data) => _data = data ?? new SettingsData();

            public SettingsData Load() => _data;

            public void Save(SettingsData settings)
            {
            }
        }
    }
}
