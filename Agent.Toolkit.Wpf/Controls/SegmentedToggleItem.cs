using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>One segment of a <see cref="SegmentedToggle"/> control.</summary>
    /// <remarks>
    /// Derives from <see cref="DependencyObject"/> so that <see cref="Accent"/> can be a
    /// dependency property. The accent is assigned in XAML with a
    /// <c>{DynamicResource Theme.ModePlan}</c>-style markup extension (see
    /// <c>ChatControl.xaml</c>), which can only target a dependency property on a dependency
    /// object; a plain CLR property would throw <c>XamlParseException</c> at load time.
    /// </remarks>
    public sealed class SegmentedToggleItem : DependencyObject, INotifyPropertyChanged
    {
        private string _label = "";
        private object? _value;
        private bool _showSeparator = true;

        /// <summary>Identifies the <see cref="Accent"/> dependency property.</summary>
        public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
            nameof(Accent), typeof(Brush), typeof(SegmentedToggleItem),
            new PropertyMetadata(Brushes.Transparent));

        /// <summary>Text displayed inside the segment.</summary>
        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; OnPropertyChanged(); } }
        }

        /// <summary>Value selected through <see cref="SegmentedToggle.SelectedValue"/>.</summary>
        public object? Value
        {
            get => _value;
            set { if (!Equals(_value, value)) { _value = value; OnPropertyChanged(); } }
        }

        /// <summary>Background brush of the segment while it is selected.</summary>
        public Brush Accent
        {
            get => (Brush)GetValue(AccentProperty);
            set => SetValue(AccentProperty, value);
        }

        /// <summary>Whether the vertical separator after this segment is visible.</summary>
        public bool ShowSeparator
        {
            get => _showSeparator;
            set { if (_showSeparator != value) { _showSeparator = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
