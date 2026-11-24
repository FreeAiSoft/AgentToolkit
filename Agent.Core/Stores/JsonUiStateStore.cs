using System;
using System.IO;
using Newtonsoft.Json;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// JSON-file <see cref="IUiStateStore"/>. Persists <see cref="UiStateData"/> to a single file;
    /// failures fall back to defaults / are ignored, matching the other toolkit stores.
    /// </summary>
    public class JsonUiStateStore : IUiStateStore
    {
        private readonly string _filePath;
        private readonly IAgentLogger _logger;

        public JsonUiStateStore(string filePath, IAgentLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", nameof(filePath));

            _filePath = filePath;
            _logger = logger ?? NoOpLogger.Instance;
        }

        public UiStateData Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new UiStateData();

                var data = JsonConvert.DeserializeObject<UiStateData>(File.ReadAllText(_filePath));
                return data ?? new UiStateData();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "JsonUiStateStore.Load failed; falling back to defaults.");
                return new UiStateData();
            }
        }

        public void Save(UiStateData state)
        {
            if (state == null)
                return;

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_filePath, JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "JsonUiStateStore.Save failed.");
            }
        }
    }
}
