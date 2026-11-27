using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Text-based guard for the self-contained <see cref="AgentChatControl"/> facade. The net8.0
    /// test project cannot reference the net472 WPF assembly, so — like
    /// <see cref="SettingsToastGuardTests"/> — these tests assert the control's surface by reading
    /// its XAML and code-behind.
    /// </summary>
    [TestFixture]
    public class AgentChatControlGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string XamlPath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "AgentChatControl.xaml");

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "AgentChatControl.xaml.cs");

        [Test]
        public void Xaml_HostsAgentTabControl()
        {
            var xaml = File.ReadAllText(XamlPath);

            Assert.That(xaml, Does.Contain("AgentTabControl"),
                "AgentChatControl.xaml must host the shared AgentTabControl.");
            Assert.That(xaml, Does.Contain("x:Name=\"Tabs\""),
                "The hosted AgentTabControl must be named Tabs.");
        }

        [Test]
        public void Code_ExposesConfigurationAndExtensions()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("public string AppDataDirectory { get; set; }"));
            Assert.That(code, Does.Contain("public IAgentRuntimeProvider RuntimeProvider { get; set; }"));
            Assert.That(code, Does.Contain("public IChatHostActions HostActions { get; set; }"));
            Assert.That(code, Does.Contain("public TabItem AddHostTab("));
            Assert.That(code, Does.Contain("public void InsertSettingsSection("));
            Assert.That(code, Does.Contain("ToolkitHostFactory.Build"),
                "AgentChatControl must build its graph through ToolkitHostFactory.");
            Assert.That(code, Does.Contain("new SettingsViewModel"),
                "AgentChatControl must wire the settings tab through the shared SettingsViewModel.");
        }

        [Test]
        public void Code_DefaultsToSettingsDrivenRuntimeAndRebuildsOnSave()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("new SettingsRuntimeProvider"),
                "AgentChatControl must default to the settings-driven runtime, not StubRuntimeProvider.");
            Assert.That(code, Does.Contain("SettingsSaved += OnSettingsSaved"),
                "AgentChatControl must subscribe to SettingsSaved so a newly configured provider is applied.");
            Assert.That(code, Does.Contain("RebuildRuntime"),
                "AgentChatControl must rebuild the agent runner after settings change.");
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
