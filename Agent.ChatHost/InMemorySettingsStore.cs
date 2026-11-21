using Agent.Core.Contracts;
using Agent.Core.Services;

namespace Agent.ChatHost
{
    /// <summary>
    /// Minimal in-memory <see cref="ISettingsStore"/> for the standalone host. Settings start from
    /// defaults and live only for the process lifetime, which is fine for manual UI smoke-testing.
    /// </summary>
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private SettingsData _data;

        public InMemorySettingsStore()
        {
            _data = new SettingsData();
            SettingsData.Normalize(_data);
        }

        public SettingsData Load() => _data;

        public void Save(SettingsData settings)
        {
            if (settings != null)
                _data = settings;
        }
    }
}
