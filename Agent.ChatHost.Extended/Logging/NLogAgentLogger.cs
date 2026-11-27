using System;
using Agent.Core.Contracts;
using NLog;

namespace Agent.ChatHost.Extended.Logging
{
    /// <summary>
    /// <see cref="IAgentLogger"/> adapter over NLog. The toolkit models/stores log through
    /// <see cref="IAgentLogger"/>; this implementation routes those calls into the NLog pipeline
    /// configured by <see cref="LoggingBootstrap"/> (file + debugger targets).
    /// </summary>
    public sealed class NLogAgentLogger : IAgentLogger
    {
        private readonly Logger _logger;

        public NLogAgentLogger()
            : this(LogManager.GetLogger("Agent.ChatHost.Extended"))
        {
        }

        public NLogAgentLogger(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Info(string message) => _logger.Info(message);

        public void Warn(string message) => _logger.Warn(message);

        public void Error(string message) => _logger.Error(message);

        public void Error(Exception exception, string message = null)
        {
            if (exception == null)
            {
                _logger.Error(message ?? string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
                _logger.Error(exception);
            else
                _logger.Error(exception, message);
        }
    }
}
