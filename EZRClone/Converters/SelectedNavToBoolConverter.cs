using System.Globalization;
using System.Windows.Data;

namespace EZRClone.Converters;

public class SelectedNavToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string selectedNav || parameter is not string targetNav)
            return false;

        return string.Equals(selectedNav, targetNav, StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected && parameter is string targetNav)
            return targetNav;

        return Binding.DoNothing;
    }
}
