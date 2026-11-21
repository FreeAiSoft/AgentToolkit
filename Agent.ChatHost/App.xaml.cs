using System.Windows;
using Agent.Models;
using Agent.Toolkit.Wpf.Theming;
using Material.Icons.WPF;

namespace Agent.ChatHost
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The toolkit draws icons through MaterialIconDataProvider.Get<Geometry>, which needs
            // the WPF geometry parser installed by Material.Icons.WPF. Touching the public control's
            // static state runs its type initializer, which registers that parser. Without this every
            // Path resolves to Geometry.Empty and the icons are invisible.
            _ = MaterialIcon.KindProperty;

            // Host owns the toolkit theme and applies a fixed dark palette for manual testing.
            AgentThemeManager.SetMode(AgentThemeMode.Auto);
            AgentThemeManager.Apply("Dark");

            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }
}