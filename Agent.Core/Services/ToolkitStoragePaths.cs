using System.IO;

namespace Agent.Core.Services
{
    /// <summary>
    /// Pure path builder for the toolkit's self-managed persistence folder. The toolkit stores
    /// its state under <c>&lt;appDataDirectory&gt;\AgentToolkit</c>: a global <c>toolkit.db</c> for
    /// settings without a workspace, and <c>workspaces\&lt;workspaceId&gt;\toolkit.db</c> for
    /// workspace-scoped settings.
    /// </summary>
    public static class ToolkitStoragePaths
    {
        /// <summary>Folder created by the toolkit under the host's app-data root.</summary>
        public const string ToolkitFolderName = "AgentToolkit";

        /// <summary>Folder that holds every per-workspace database.</summary>
        public const string WorkspacesFolderName = "workspaces";

        /// <summary>SQLite database file name.</summary>
        public const string DatabaseFileName = "toolkit.db";

        /// <summary>Root folder of all toolkit state: <c>&lt;appData&gt;\AgentToolkit</c>.</summary>
        public static string ToolkitDirectory(string appDataDirectory) =>
            Path.Combine(appDataDirectory, ToolkitFolderName);

        /// <summary>Root folder for per-workspace state: <c>&lt;appData&gt;\AgentToolkit\workspaces</c>.</summary>
        public static string WorkspacesRoot(string appDataDirectory) =>
            Path.Combine(ToolkitDirectory(appDataDirectory), WorkspacesFolderName);

        /// <summary>Folder for one workspace: <c>&lt;appData&gt;\AgentToolkit\workspaces\&lt;id&gt;</c>.</summary>
        public static string WorkspaceDirectory(string appDataDirectory, string workspaceId) =>
            Path.Combine(WorkspacesRoot(appDataDirectory), workspaceId);

        /// <summary>
        /// Database file for a workspace, or the global <c>toolkit.db</c> when
        /// <paramref name="workspaceId"/> is null, empty or whitespace.
        /// </summary>
        public static string SettingsDatabase(string appDataDirectory, string workspaceId) =>
            string.IsNullOrWhiteSpace(workspaceId)
                ? Path.Combine(ToolkitDirectory(appDataDirectory), DatabaseFileName)
                : Path.Combine(WorkspaceDirectory(appDataDirectory, workspaceId), DatabaseFileName);
    }
}
