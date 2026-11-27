using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Agent.Core.Models;
using Agent.Toolkit.Wpf.Diagnostics;

namespace Agent.Toolkit.Wpf.Controls.Chat
{
    public partial class AgentTextMessageControl : UserControl
    {
        private ChatMessage _message;

        public AgentTextMessageControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogVisualState("loaded");
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_message != null)
            {
                _message.PropertyChanged -= OnMessagePropertyChanged;
                _message = null;
            }

            _message = DataContext as ChatMessage;
            if (_message == null)
                return;

            _message.PropertyChanged += OnMessagePropertyChanged;

            WpfLog.ChatDebug(
                "[agenttext] bound kind=" + _message.Kind +
                " textLen=" + (_message.Text?.Length ?? -1) +
                " reasonLen=" + (_message.Reasoning?.Length ?? -1) +
                " thinking=" + _message.IsThinking +
                " partial=" + _message.IsPartial +
                " showReasoning=" + _message.ShowReasoning +
                " blocks=" + (_message.Blocks?.Count ?? -1) +
                " text='" + Preview(_message.Text) + "'" +
                " reasoning='" + Preview(_message.Reasoning) + "'");
        }

        private void OnMessagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ChatMessage.IsThinking))
                return;

            // Let the OneWay binding push IsExpanded first, then read the visual state.
            Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() => LogVisualState("thinking=" + _message.IsThinking)));
        }

        private void LogVisualState(string reason)
        {
            if (ReasoningExpander == null || AnswerList == null)
                return;

            WpfLog.ChatDebug(
                "[agenttext] " + reason +
                " expanderIsExpanded=" + ReasoningExpander.IsExpanded +
                " expanderVisibility=" + ReasoningExpander.Visibility +
                " answerItems=" + AnswerList.Items.Count);
        }

        private static string Preview(string text, int max = 120)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            var single = text.Replace("\r", "\\r").Replace("\n", "\\n");
            return single.Length <= max ? single : single.Substring(0, max) + "…";
        }
    }
}