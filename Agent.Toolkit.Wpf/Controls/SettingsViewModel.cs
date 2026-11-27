using Agent.Models;
using Agent.Mvvm;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Drop-in view model for the shared <see cref="SettingsControl"/>: exposes the
    /// <see cref="SettingsModel"/> through <see cref="Settings"/> and the command dispatcher
    /// through the inherited <see cref="BaseViewModel.Cmd"/>. A host that does not need
    /// host-specific settings sections can use this class directly instead of declaring its own
    /// per-project view model.
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        public SettingsViewModel(SettingsModel model)
        {
            Settings = model;
            SetModel(model, null);
        }

        public SettingsModel Settings { get; }
    }
}
