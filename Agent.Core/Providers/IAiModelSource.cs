using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Core.Providers
{
    /// <summary>Discovers the models offered by a provider at runtime (e.g. Ollama <c>/api/tags</c>).</summary>
    public interface IAiModelSource
    {
        string ProviderId { get; }

        Task<IReadOnlyList<AiModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    }
}
