using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Agent.ChatHost.Extended.Logging
{
    /// <summary>Loads the NLog configuration and points the file target at the resolved log directory.</summary>
    public static class LoggingBootstrap
    {
        private static bool _configured;

        public static void Configure()
        {
            if (_configured)
                return;
            _configured = true;

            var logDir = LogPaths.ResolveLogDirectory();
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");

            LoggingConfiguration config;
            if (File.Exists(configPath))
            {
                config = new XmlLoggingConfiguration(configPath);
            }
            else
            {
                // Minimal fallback so logging still works when the .config is missing.
                config = new LoggingConfiguration();
                var fileTarget = new FileTarget("file")
                {
                    FileName = Path.Combine(logDir, "agent-chat-host-extended.log"),
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner= ${exception:format=tostring}}"
                };
                config.AddTarget("file", fileTarget);
                config.AddRuleForAllLevels(fileTarget);
            }

            // Override the ${var:logdir} placeholder used in NLog.config with the resolved directory.
            config.Variables["logdir"] = logDir;

            LogManager.Configuration = config;
        }
    }
}
