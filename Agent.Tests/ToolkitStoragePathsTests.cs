using System.IO;
using Agent.Core.Services;
using NUnit.Framework;

namespace Agent.Tests
{
    [TestFixture]
    public class ToolkitStoragePathsTests
    {
        private const string AppData = @"C:\Users\tester\AppData\Roaming\MyApp";

        [Test]
        public void ToolkitDirectory_IsAgentToolkitUnderAppData()
        {
            Assert.That(
                ToolkitStoragePaths.ToolkitDirectory(AppData),
                Is.EqualTo(Path.Combine(AppData, "AgentToolkit")));
        }

        [Test]
        public void WorkspacesRoot_IsUnderToolkitDirectory()
        {
            Assert.That(
                ToolkitStoragePaths.WorkspacesRoot(AppData),
                Is.EqualTo(Path.Combine(ToolkitStoragePaths.ToolkitDirectory(AppData), "workspaces")));
        }

        [Test]
        public void WorkspaceDirectory_AppendsWorkspaceId()
        {
            Assert.That(
                ToolkitStoragePaths.WorkspaceDirectory(AppData, "abc123"),
                Is.EqualTo(Path.Combine(ToolkitStoragePaths.WorkspacesRoot(AppData), "abc123")));
        }

        [Test]
        public void SettingsDatabase_WithWorkspace_IsScoped()
        {
            Assert.That(
                ToolkitStoragePaths.SettingsDatabase(AppData, "abc123"),
                Is.EqualTo(Path.Combine(ToolkitStoragePaths.WorkspaceDirectory(AppData, "abc123"), "toolkit.db")));
        }

        [Test]
        public void SettingsDatabase_WithEmptyWorkspace_IsGlobal()
        {
            Assert.That(
                ToolkitStoragePaths.SettingsDatabase(AppData, ""),
                Is.EqualTo(Path.Combine(ToolkitStoragePaths.ToolkitDirectory(AppData), "toolkit.db")));
        }

        [Test]
        public void SettingsDatabase_WithWhitespaceWorkspace_IsGlobal()
        {
            Assert.That(
                ToolkitStoragePaths.SettingsDatabase(AppData, "   "),
                Is.EqualTo(Path.Combine(ToolkitStoragePaths.ToolkitDirectory(AppData), "toolkit.db")));
        }

        [Test]
        public void Paths_PreserveSpacesAndSpecialCharactersInAppDataDirectory()
        {
            var appData = @"C:\Users\tester\AppData\Roaming\My App (Beta) #1 [v2]";

            Assert.That(
                ToolkitStoragePaths.ToolkitDirectory(appData),
                Is.EqualTo(Path.Combine(appData, "AgentToolkit")));
            Assert.That(
                ToolkitStoragePaths.SettingsDatabase(appData, "workspace-1"),
                Is.EqualTo(Path.Combine(appData, "AgentToolkit", "workspaces", "workspace-1", "toolkit.db")));
            Assert.That(
                ToolkitStoragePaths.SettingsDatabase(appData, ""),
                Is.EqualTo(Path.Combine(appData, "AgentToolkit", "toolkit.db")));
        }
    }
}
