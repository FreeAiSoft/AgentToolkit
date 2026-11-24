using System;
using Agent.Core.Contracts;

namespace Agent.Core.Services
{
    /// <summary>
    /// Shared no-op <see cref="IAgentLogger"/> used by every toolkit component that logs but has
    /// no host logger injected. Centralized here so each store/service does not declare its own
    /// private copy.
    /// </summary>
    public sealed class NoOpLogger : IAgentLogger
    {
        public static readonly NoOpLogger Instance = new NoOpLogger();

        private NoOpLogger() { }

        public void Info(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }

        public void Error(Exception exception, string message = null) { }
    }
}
