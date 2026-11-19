using System.Windows;
using System.Windows.Controls;
using Agent.Models;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Context-window indicator: a fill bar plus text lines (window fill, cache split, session
    /// statistics). Purely presentational: everything is bound to <see cref="ChatModel.ContextUsage"/>
    /// (the model's single, PropertyChanged-driven snapshot). The <see cref="Model"/> is assigned as
    /// the control's DataContext, so no code here reads the model directly — updates arrive through
    /// the model's <c>ContextUsage</c> PropertyChanged notifications and WPF bindings.
    /// </summary>
    public partial class ContextIndicator : UserControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            nameof(Model), typeof(ChatModel), typeof(ContextIndicator),
            new PropertyMetadata(null, OnModelChanged));

        public static readonly DependencyProperty ShowDetailsProperty = DependencyProperty.Register(
            nameof(ShowDetails), typeof(bool), typeof(ContextIndicator),
            new PropertyMetadata(true));

        public ContextIndicator()
        {
            InitializeComponent();
        }

        /// <summary>Chat model the indicator is bound to (its <see cref="ChatModel.ContextUsage"/> is shown).</summary>
        public ChatModel Model
        {
            get => (ChatModel)GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }

        /// <summary>Whether the text lines below the fill bar are visible.</summary>
        public bool ShowDetails
        {
            get => (bool)GetValue(ShowDetailsProperty);
            set => SetValue(ShowDetailsProperty, value);
        }

        private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ContextIndicator)d).DataContext = e.NewValue;

    }
}

