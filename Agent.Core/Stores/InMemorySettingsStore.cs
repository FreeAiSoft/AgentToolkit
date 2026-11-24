using Agent.Core.Contracts;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// In-memory <see cref="ISettingsStore"/>. Settings start from defaults and live only for the
    /// process lifetime; useful for smoke hosts and tests that must not touch disk.
    /// </summary>
    public class InMemorySettingsStore : ISettingsStore
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
