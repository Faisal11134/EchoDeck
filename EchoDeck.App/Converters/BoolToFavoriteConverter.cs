using System.Globalization;
using System.Windows.Data;

namespace EchoDeck.App.Converters;

public sealed class BoolToFavoriteConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "\u2605" : "\u2606";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
