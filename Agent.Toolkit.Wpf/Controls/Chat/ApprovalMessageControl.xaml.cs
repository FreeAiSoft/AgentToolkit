using System.Windows;
using System.Windows.Controls;
using Agent.Toolkit.Wpf.Diagnostics;
using Agent.Core.Models;

namespace Agent.Toolkit.Wpf.Controls.Chat
{
    public partial class ApprovalMessageControl : UserControl
    {
        public ApprovalMessageControl()
        {
            InitializeComponent();
        }

        private void OnApproveClick(object sender, RoutedEventArgs e) => Decide("approved", false);

        private void OnRejectClick(object sender, RoutedEventArgs e) => Decide("rejected", false);

        private void OnApproveAllClick(object sender, RoutedEventArgs e) => Decide("approved", true);

        private void OnRejectAllClick(object sender, RoutedEventArgs e) => Decide("rejected", true);

        private void Decide(string decision, bool applyToAll)
        {
            if (DataContext is not ChatMessage message || message.Payload is not ApprovalPayload payload)
                return;

            payload.ApplyToAll = applyToAll;
            payload.Decision = decision;

            WpfLog.ChatDebug("Approval decision: \"" + decision + "\" (applyToAll=" + applyToAll + ").");
            RaiseEvent(new ChatMessageRoutedEventArgs(ChatMessageCard.ApprovalDecidedEvent, message));
        }
    }
}
