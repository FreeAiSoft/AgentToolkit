using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Core.Stores
{
    /// <summary>
    /// In-memory <see cref="IUiStateStore"/>. State lives only for the process lifetime.
    /// </summary>
    public class InMemoryUiStateStore : IUiStateStore
    {
        private UiStateData _state = new UiStateData();

        public UiStateData Load() => _state;

        public void Save(UiStateData state)
        {
            if (state != null)
                _state = state;
        }
    }
}
