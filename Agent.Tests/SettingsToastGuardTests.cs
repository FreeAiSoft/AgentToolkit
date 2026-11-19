using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the settings save/notify toast. The shared <c>SettingsControl</c>
    /// owns its own <c>NotificationBanner</c> (named <c>ToastBanner</c>) and must subscribe to
    /// <c>SettingsModel.SettingsSaved</c> / <c>SettingsModel.Notify</c> so the green "saved" toast
    /// shows without any host wiring.
    /// </summary>
    [TestFixture]
    public class SettingsToastGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string XamlPath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "SettingsControl.xaml");

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "SettingsControl.xaml.cs");

        [Test]
        public void SettingsControl_Xaml_DeclaresToastBanner()
        {
            var xaml = File.ReadAllText(XamlPath);

            Assert.That(xaml, Does.Contain("NotificationBanner"),
                "SettingsControl.xaml must host a NotificationBanner for the save/notify toast.");
            Assert.That(xaml, Does.Contain("x:Name=\"ToastBanner\""),
                "The toast banner must be named ToastBanner.");
        }

        [Test]
        public void SettingsControl_CodeBehind_SubscribesToSettingsEvents()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("SettingsSaved"),
                "SettingsControl must react to SettingsModel.SettingsSaved.");
            Assert.That(code, Does.Contain("Notify"),
                "SettingsControl must react to SettingsModel.Notify.");
            Assert.That(code, Does.Contain("ToastBanner"),
                "SettingsControl must reference its ToastBanner.");
            Assert.That(code, Does.Contain("CloseRequested"),
                "SettingsControl must close the toast on CloseRequested.");
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
