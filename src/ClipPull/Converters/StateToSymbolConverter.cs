using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ClipPull.ViewModels;
using Wpf.Ui.Controls;

namespace ClipPull.Converters;

internal sealed class StateToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            QueueItemState.Completed => SymbolRegular.CheckmarkCircle24,
            QueueItemState.AlreadyDownloaded => SymbolRegular.CheckmarkCircle24,
            QueueItemState.Failed => SymbolRegular.ErrorCircle24,
            QueueItemState.Cancelled => SymbolRegular.Dismiss24,
            _ => SymbolRegular.VideoClip20
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal sealed class ActiveStateToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is QueueItemState.Active ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
