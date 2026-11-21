using Agent.Models;
using Agent.Mvvm;

namespace Agent.ChatHost
{
    /// <summary>
    /// Data context for the toolkit <c>SettingsControl</c>: exposes the <see cref="SettingsModel"/>
    /// through <c>Settings</c> and the command dispatcher through the inherited <c>Cmd</c>.
    /// </summary>
    public sealed class SettingsHostViewModel : BaseViewModel
    {
        public SettingsHostViewModel(SettingsModel model)
        {
            Settings = model;
            SetModel(model, null);
        }

        public SettingsModel Settings { get; }
    }
}
