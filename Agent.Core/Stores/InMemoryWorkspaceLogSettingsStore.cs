using Agent.Core.Contracts;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// In-memory <see cref="IWorkspaceLogSettingsStore"/>. Settings live only for the process lifetime.
    /// </summary>
    public class InMemoryWorkspaceLogSettingsStore : IWorkspaceLogSettingsStore
    {
        private WorkspaceLogSettings _settings = new WorkspaceLogSettings();

        public WorkspaceLogSettings Load() => _settings;

        public void Save(WorkspaceLogSettings settings)
        {
            if (settings != null)
                _settings = settings;
        }
    }
}
