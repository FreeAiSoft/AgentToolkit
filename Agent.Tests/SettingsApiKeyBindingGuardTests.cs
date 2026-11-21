using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the settings API-key field. <see cref="System.Windows.Controls.PasswordBox"/>
    /// has no <see cref="System.Windows.Controls.PasswordBox.Password"/> dependency property, so the
    /// shared <c>SettingsControl</c> must explicitly push the typed value into
    /// <see cref="Agent.Models.SettingsModel.ApiKey"/> (and pull the loaded value back into the box).
    /// Without this wiring the key never reaches the model and is silently not persisted.
    /// </summary>
    [TestFixture]
    public class SettingsApiKeyBindingGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string XamlPath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "SettingsControl.xaml");

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "SettingsControl.xaml.cs");

        [Test]
        public void SettingsControl_Xaml_WiresApiKeyPasswordChanged()
        {
            var xaml = File.ReadAllText(XamlPath);

            Assert.That(xaml, Does.Contain("x:Name=\"ApiKeyBox\""),
                "SettingsControl.xaml must keep the API key PasswordBox named ApiKeyBox.");
            Assert.That(xaml, Does.Contain("PasswordChanged=\"OnApiKeyChanged\""),
                "The ApiKeyBox must raise PasswordChanged so the typed key is copied into the model.");
        }

        [Test]
        public void SettingsControl_CodeBehind_SyncsApiKeyBetweenPasswordBoxAndModel()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("void OnApiKeyChanged"),
                "SettingsControl must handle PasswordChanged.");
            Assert.That(code, Does.Contain("_settingsModel.ApiKey = ApiKeyBox.Password"),
                "The typed API key must be pushed into SettingsModel.ApiKey.");
            Assert.That(code, Does.Contain("SyncApiKeyToPasswordBox"),
                "SettingsControl must pull the loaded API key back into the PasswordBox.");
            Assert.That(code, Does.Contain("_settingsModel.PropertyChanged += OnSettingsModelPropertyChanged"),
                "SettingsControl must follow SettingsModel.ApiKey changes.");
            Assert.That(code, Does.Contain("_settingsModel.PropertyChanged -= OnSettingsModelPropertyChanged"),
                "SettingsControl must detach its property-changed subscription.");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
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
    }
}
