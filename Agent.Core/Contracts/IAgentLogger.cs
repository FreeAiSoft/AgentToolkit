using System;

namespace Agent.Core.Contracts
{
    /// <summary>
    /// Minimal logging seam for the model layer. Models log through this interface instead of a
    /// concrete logger; the host injects its own implementation (e.g. NLog writing to the
    /// chat-log file) and a no-op default is used when none is provided.
    /// </summary>
    public interface IAgentLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(Exception exception, string message = null);
    }
}
