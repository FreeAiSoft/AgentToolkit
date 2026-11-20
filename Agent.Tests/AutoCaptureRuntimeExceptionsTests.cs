using System.IO;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Services;
using Agent.Models;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the "auto-send debugger exceptions" feature. The setting must be
    /// opt-in (default false), round-trip through the model, and be exposed in the shared settings UI.
    /// </summary>
    [TestFixture]
    public class AutoCaptureRuntimeExceptionsTests
    {
        [Test]
        public void SettingsData_DefaultsToFalse()
        {
            Assert.That(new SettingsData().AutoCaptureRuntimeExceptions, Is.False);
        }

        [Test]
        public void SettingsModel_DefaultsToFalse()
        {
            var model = new SettingsModel(new CapturingStore(new SettingsData()));
            Assert.That(model.AutoCaptureRuntimeExceptions, Is.False);
        }

        [Test]
        public void SettingsModel_RoundTripsTheSetting()
        {
            var store = new CapturingStore(new SettingsData { AutoCaptureRuntimeExceptions = true });
            var model = new SettingsModel(store);
            model.Load();

            Assert.That(model.AutoCaptureRuntimeExceptions, Is.True);
            Assert.That(model.ToSettingsData().AutoCaptureRuntimeExceptions, Is.True);
        }

        [Test]
        public void SettingsStoreSource_DefaultsToFalse()
        {
            var code = File.ReadAllText(Path.Combine(RepoRoot, "Agent.Core", "Services", "SettingsStore.cs"));
            Assert.That(code, Does.Contain("AutoCaptureRuntimeExceptions { get; set; } = false;"));
        }

        [Test]
        public void SettingsModelSource_DefaultsToFalse()
        {
            var code = File.ReadAllText(Path.Combine(RepoRoot, "Agent.Models", "SettingsModel.cs"));
            Assert.That(code, Does.Contain("_autoCaptureRuntimeExceptions = false;"));
        }

        [Test]
        public void SettingsControlSource_ExposesTheSetting()
        {
            var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "SettingsControl.xaml"));
            Assert.That(xaml, Does.Contain("Settings.AutoCaptureRuntimeExceptions"));
        }

        private static string RepoRoot => FindRepoRoot();

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Agent.Toolkit.Wpf")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Agent.Tests")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the AgentShared repository root.");
        }

        private sealed class CapturingStore : ISettingsStore
        {
            public SettingsData Data;

            public CapturingStore(SettingsData data) => Data = data ?? new SettingsData();

            public SettingsData Load() => Data;

            public void Save(SettingsData settings) => Data = settings;
        }
    }
}
