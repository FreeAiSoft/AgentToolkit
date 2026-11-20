using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the notification banner redesign: the banner must fill its
    /// background with a semi-transparent version of the accent color instead of drawing a
    /// 1px accent border.
    /// </summary>
    [TestFixture]
    public class NotificationBannerStyleGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string XamlPath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "Notification", "NotificationBanner.xaml");

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "Notification", "NotificationBanner.xaml.cs");

        [Test]
        public void Banner_UsesSemiTransparentBackgroundInsteadOfBorder()
        {
            var xaml = File.ReadAllText(XamlPath);

            Assert.That(xaml, Does.Contain("Background=\"{Binding AccentBackgroundBrush, ElementName=Root}\""),
                "NotificationBanner must fill its background with the semi-transparent accent brush.");
            Assert.That(xaml, Does.Not.Contain("BorderBrush=\"{Binding AccentBrush"),
                "NotificationBanner must not draw the accent border anymore.");
            Assert.That(xaml, Does.Not.Contain("BorderThickness=\"1\""),
                "NotificationBanner must not have a 1px border.");
        }

        [Test]
        public void Banner_CodeBehind_ComputesAccentBackground()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("AccentBackgroundBrush"),
                "NotificationBanner must expose AccentBackgroundBrush.");
            Assert.That(code, Does.Contain("Color.FromArgb"),
                "NotificationBanner must derive the background by adding transparency to the accent color.");
            Assert.That(code, Does.Contain("OnAccentBrushChanged"),
                "AccentBackgroundBrush must follow AccentBrush changes.");
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