using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PubTemplateEditor.App.Infrastructure;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the bound string is null or empty (placeholder overlays).</summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when the bound bool is false (empty states, disabled controls).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Boolean flip, used for TextBox.IsReadOnly so editability comes from the view model.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? false : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? false : true;
}
