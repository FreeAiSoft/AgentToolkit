using System.Diagnostics;
using System.Windows;
using Agent.ChatHost.Extended.Logging;
using Agent.Models;
using Agent.Toolkit.Wpf.Theming;
using Material.Icons.WPF;
using NLog;

namespace Agent.ChatHost.Extended
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load NLog config and point ${var:logdir} at the resolved log directory.
            LoggingBootstrap.Configure();

            // WpfLog (the shared WPF controls' internal logger) writes through
            // System.Diagnostics.Trace. Forward it into the same NLog pipeline so control
            // diagnostics land in the same file.
            Trace.Listeners.Add(new NLogTraceListener { Name = "Trace" });

            // The toolkit draws icons through MaterialIconDataProvider.Get<Geometry>, which needs
            // the WPF geometry parser installed by Material.Icons.WPF. Touching the public control's
            // static state runs its type initializer, which registers that parser.
            _ = MaterialIcon.KindProperty;

            // Host owns the toolkit theme and applies a fixed dark palette for manual testing.
            AgentThemeManager.SetMode(AgentThemeMode.Auto);
            AgentThemeManager.Apply("Dark");

            var log = LogManager.GetCurrentClassLogger();
            log.Info("Agent.ChatHost.Extended starting. Log directory: " + LogPaths.ResolveLogDirectory());

            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogManager.Shutdown();
            base.OnExit(e);
        }
    }
}
