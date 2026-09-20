using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ScheduleAssistant.Presentation.Controls;

/// <summary>Converts persisted category colors into a card accent brush.</summary>
public sealed class CategoryColorConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string colorText || string.IsNullOrWhiteSpace(colorText))
        {
            return DependencyProperty.UnsetValue;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorText)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return DependencyProperty.UnsetValue;
        }
        catch (InvalidCastException)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
