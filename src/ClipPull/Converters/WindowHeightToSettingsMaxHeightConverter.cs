using System.Globalization;
using System.Windows.Data;

namespace ClipPull.Converters;

internal sealed class WindowHeightToSettingsMaxHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var height = value is double windowHeight ? windowHeight : 620d;
        return Math.Max(360d, height - 100d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
