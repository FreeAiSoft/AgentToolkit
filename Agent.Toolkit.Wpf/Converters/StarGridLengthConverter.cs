using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Agent.Toolkit.Wpf.Converters
{
    /// <summary>
    /// Converts a 0..100 fill ratio into a star-sized <see cref="GridLength"/> so the
    /// context-window bar columns can be bound directly to the model's computed percentages.
    /// </summary>
    public sealed class StarGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ratio = value is double d ? d
                      : value is int i ? i
                      : value is float f ? f
                      : 0.0;

            return new GridLength(Math.Max(0.0, ratio), GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
