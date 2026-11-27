using System;
using System.IO;

namespace Agent.ChatHost.Extended.Logging
{
    /// <summary>
    /// Resolves the well-known log directory for the extended host.
    ///
    /// Primary location is <c>&lt;repo-root&gt;/logs</c>: when the app runs from the source tree
    /// (Debug/F5) we walk up from the executable until we find <c>AgentToolkit.slnx</c>, so the
    /// agent can read the logs in Plan mode (the solution root is inside the Plan-mode read scope).
    /// When the solution marker is not found (published/installed copy), we fall back to
    /// <c>%LocalAppData%\AgentChatHost.Extended\logs</c>.
    /// </summary>
    public static class LogPaths
    {
        private const string SolutionMarker = "AgentToolkit.slnx";
        private const string LogFolderName = "logs";

        private static string _logDirectory;

        public static string ResolveLogDirectory()
        {
            if (_logDirectory != null)
                return _logDirectory;

            var repoRoot = FindRepoRoot();
            var dir = repoRoot != null
                ? Path.Combine(repoRoot, LogFolderName)
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AgentChatHost.Extended",
                    LogFolderName);

            Directory.CreateDirectory(dir);
            _logDirectory = dir;
            return dir;
        }

        public static string ResolveLogFile()
            => Path.Combine(ResolveLogDirectory(), "agent-chat-host-extended.log");

        private static string FindRepoRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, SolutionMarker)))
                    return current;

                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrEmpty(parent) || parent == current)
                    return null;
                current = parent;
            }

            return null;
        }
    }
}
