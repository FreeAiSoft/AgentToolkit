using System;
using System.IO;
using Newtonsoft.Json;
using Agent.Core.Contracts;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// JSON-file <see cref="IWorkspaceLogSettingsStore"/>. Persists
    /// <see cref="WorkspaceLogSettings"/> to a single file; failures fall back to defaults /
    /// are ignored.
    /// </summary>
    public class JsonWorkspaceLogSettingsStore : IWorkspaceLogSettingsStore
    {
        private readonly string _filePath;
        private readonly IAgentLogger _logger;

        public JsonWorkspaceLogSettingsStore(string filePath, IAgentLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", nameof(filePath));

            _filePath = filePath;
            _logger = logger ?? NoOpLogger.Instance;
        }

        public WorkspaceLogSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new WorkspaceLogSettings();

                var settings = JsonConvert.DeserializeObject<WorkspaceLogSettings>(File.ReadAllText(_filePath));
                return settings ?? new WorkspaceLogSettings();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "JsonWorkspaceLogSettingsStore.Load failed; falling back to defaults.");
                return new WorkspaceLogSettings();
            }
        }

        public void Save(WorkspaceLogSettings settings)
        {
            if (settings == null)
                return;

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_filePath, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "JsonWorkspaceLogSettingsStore.Save failed.");
            }
        }
    }
}
