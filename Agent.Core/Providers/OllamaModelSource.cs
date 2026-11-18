using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Agent.Core.Providers
{
    /// <summary>
    /// Lists locally pulled Ollama models via the native <c>/api/tags</c> endpoint.
    /// The base URL is the Ollama server root (e.g. <c>http://localhost:11434</c>),
    /// not the OpenAI-compatible <c>/v1</c> mount.
    /// </summary>
    public class OllamaModelSource : IAiModelSource
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public OllamaModelSource(string baseUrl, HttpClient http = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434" : baseUrl.Trim().TrimEnd('/');
            _http = http ?? new HttpClient();
        }

        public string ProviderId => AiProviderId.Ollama;

        public async Task<IReadOnlyList<AiModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _http.GetAsync(_baseUrl + "/api/tags", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = JObject.Parse(json);

            var models = new List<AiModelInfo>();
            if (root["models"] is JArray arr)
            {
                foreach (var item in arr)
                {
                    var name = item["name"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    models.Add(new AiModelInfo { Id = name, Name = name });
                }
            }
            return models;
        }
    }
}
