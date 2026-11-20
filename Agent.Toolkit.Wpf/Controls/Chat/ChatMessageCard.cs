using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Agent.Core.Models;

namespace Agent.Toolkit.Wpf.Controls.Chat
{
    /// <summary>
    /// Shared chrome for every chat message: an accent border, a header with a collapse
    /// chevron, the timestamp and the copy button, and a content area for the per-kind body.
    /// The chrome lives in a <see cref="ControlTemplate"/> (see ChatResources.xaml), so each
    /// per-kind control supplies its body as this control's <see cref="ContentControl.Content"/>.
    /// Emits bubbling routed events for copy/question actions.
    /// </summary>
    public class ChatMessageCard : ContentControl
    {
        static ChatMessageCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChatMessageCard), new FrameworkPropertyMetadata(typeof(ChatMessageCard)));
        }

        public ChatMessageCard()
        {
            DataContextChanged += OnDataContextChanged;
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(
                nameof(IsExpanded),
                typeof(bool),
                typeof(ChatMessageCard),
                new PropertyMetadata(false));

        public static readonly RoutedEvent CopyRequestedEvent =
            EventManager.RegisterRoutedEvent("CopyRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        public static readonly RoutedEvent QuestionAnsweredEvent =
            EventManager.RegisterRoutedEvent("QuestionAnswered", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        public static readonly RoutedEvent QuestionSkippedEvent =
            EventManager.RegisterRoutedEvent("QuestionSkipped", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        public static readonly RoutedEvent ApprovalDecidedEvent =
            EventManager.RegisterRoutedEvent("ApprovalDecided", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        public static readonly RoutedEvent CheckpointRestoreRequestedEvent =
            EventManager.RegisterRoutedEvent("CheckpointRestoreRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        public static readonly RoutedEvent DiffOpenRequestedEvent =
            EventManager.RegisterRoutedEvent("DiffOpenRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageCard));

        private Ellipse _executingDot;
        private ScaleTransform _executingDotScale;
        private ChatMessage _message;
        private int _dotGeneration;

        /// <summary>Whether the message body is visible. Defaults to true (expanded).</summary>
        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Event handlers cannot live in the shared ResourceDictionary (it has no
            // code-behind), so wire the template buttons up here.
            if (GetTemplateChild("ToggleButton") is Button toggle)
                toggle.Click += OnToggleExpanded;
            if (GetTemplateChild("CopyButton") is Button copy)
                copy.Click += OnCopy;

            _executingDot = GetTemplateChild("ExecutingDot") as Ellipse;
            _executingDotScale = new ScaleTransform();
            if (_executingDot != null)
                _executingDot.RenderTransform = _executingDotScale;

            HookDataContext();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookDataContext();
        }

        private void HookDataContext()
        {
            if (_message != null)
            {
                _message.PropertyChanged -= OnMessagePropertyChanged;
                _message = null;
            }

            _message = DataContext as ChatMessage;
            if (_message != null)
                _message.PropertyChanged += OnMessagePropertyChanged;

            UpdateExecutingDot(_message?.IsExecuting == true);
        }

        private void OnMessagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatMessage.IsExecuting))
                UpdateExecutingDot(((ChatMessage)sender).IsExecuting);
        }

        private void UpdateExecutingDot(bool executing)
        {
            if (_executingDot == null || _executingDotScale == null)
                return;

            int generation = ++_dotGeneration;

            if (executing)
            {
                _executingDot.BeginAnimation(OpacityProperty, null);
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                _executingDot.Opacity = 0;
                _executingDot.Visibility = Visibility.Visible;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                fadeIn.Completed += (s, e) =>
                {
                    if (generation == _dotGeneration)
                        StartPulse();
                };
                _executingDot.BeginAnimation(OpacityProperty, fadeIn);

                var scaleIn = new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(180));
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
            }
            else
            {
                double current = _executingDot.Opacity;
                _executingDot.BeginAnimation(OpacityProperty, null);
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _executingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                if (current <= 0.01 || _executingDot.Visibility != Visibility.Visible)
                {
                    _executingDot.Opacity = 0;
                    _executingDot.Visibility = Visibility.Collapsed;
                    return;
                }

                _executingDot.Opacity = current;
                var fadeOut = new DoubleAnimation(current, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, e) =>
                {
                    if (generation == _dotGeneration)
                    {
                        _executingDot.Opacity = 0;
                        _executingDot.Visibility = Visibility.Collapsed;
                    }
                };
                _executingDot.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void StartPulse()
        {
            if (_executingDot == null || _executingDotScale == null)
                return;
            if (_executingDot.Visibility != Visibility.Visible)
                return;

            var pulse = new DoubleAnimation(1, 0.15, TimeSpan.FromMilliseconds(550))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            _executingDot.BeginAnimation(OpacityProperty, pulse);

            var scalePulse = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(550))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            _executingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, scalePulse);
            _executingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, scalePulse);
        }

        private void OnToggleExpanded(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new ChatMessageRoutedEventArgs(CopyRequestedEvent, DataContext as ChatMessage));
        }
    }
}
