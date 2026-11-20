using System.Text;
using Agent.Core.Models;

namespace Agent.Core.Services
{
    /// <summary>
    /// Pure formatter for the per-message clipboard copy action. Extracted from the WPF chat
    /// control so the exact copy text can be asserted without any UI dependency.
    /// </summary>
    public static class ChatMessageCopyFormatter
    {
        /// <summary>
        /// Formats <paramref name="message"/> for the clipboard: the display text followed by
        /// the reasoning block when reasoning is present. Null yields an empty string.
        /// </summary>
        public static string Format(ChatMessage message)
        {
            if (message == null)
                return "";

            var sb = new StringBuilder();
            sb.Append(message.DisplayText);
            if (!string.IsNullOrWhiteSpace(message.Reasoning))
            {
                sb.AppendLine();
                sb.AppendLine("[Reasoning]");
                sb.Append(message.Reasoning);
            }
            return sb.ToString();
        }
    }
}
