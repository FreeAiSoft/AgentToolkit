using System;
using System.Diagnostics;

namespace Agent.Toolkit.Wpf.Diagnostics
{
    /// <summary>
    /// Minimal trace logger for the shared WPF controls. The controls must not depend on a host
    /// logging framework, so they write through <see cref="Trace"/>; a host can attach its own
    /// trace listeners if it wants these messages persisted (e.g. to a file, as the RPAWriter2
    /// host did via NLog).
    /// </summary>
    internal static class WpfLog
    {
        public static void ChatDebug(string message) => Trace.TraceInformation(message);

        public static void Info(string message) => Trace.TraceInformation(message);

        public static void Warn(string message) => Trace.TraceWarning(message);

        public static void Error(string message) => Trace.TraceError(message);

        public static void Error(Exception exception, string message = null)
        {
            if (exception == null)
            {
                Trace.TraceError(message ?? string.Empty);
                return;
            }

            Trace.TraceError((message ?? exception.Message) + Environment.NewLine + exception);
        }
    }
}
