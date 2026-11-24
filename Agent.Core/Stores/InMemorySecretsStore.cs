using Agent.Core.Contracts;

namespace Agent.Core.Stores
{
    /// <summary>
    /// In-memory <see cref="ISecretsStore"/>. The API key lives only for the process lifetime;
    /// nothing is encrypted or written to disk. Use only for tests and smoke hosts.
    /// </summary>
    public class InMemorySecretsStore : ISecretsStore
    {
        private string _apiKey = "";

        public string LoadApiKey() => _apiKey;

        public void SaveApiKey(string apiKey) => _apiKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;

        public void ClearApiKey() => _apiKey = "";
    }
}
