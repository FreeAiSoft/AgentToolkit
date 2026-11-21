using System;
using Agent.Core.Contracts;
using NLog;

namespace Agent.ChatHost
{
    /// <summary>
    /// Host-side <see cref="IAgentLogger"/> backed by NLog. The standalone ChatHost injects this
    /// into <see cref="Agent.Models.ChatModel"/> so the toolkit model can log through its
    /// framework-agnostic seam while the concrete sink (a chat-log file) stays in the host.
    /// </summary>
    public sealed class NLogAgentLogger : IAgentLogger
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public void Info(string message) => Log.Info(message);

        public void Warn(string message) => Log.Warn(message);

        public void Error(string message) => Log.Error(message);

        public void Error(Exception exception, string message = null) => Log.Error(exception, message);
    }
}
