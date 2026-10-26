using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Attached properties that let a themed control draw a leading icon without repeating the icon
/// markup at every call site. The button template in Controls.xaml looks the geometry up here, so a
/// command button stays one readable line of XAML: label, command and glyph.
/// </summary>
public static class Iconics
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(Geometry),
            typeof(Iconics),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static void SetIcon(DependencyObject element, Geometry? value) => element.SetValue(IconProperty, value);

    public static Geometry? GetIcon(DependencyObject element) => (Geometry?)element.GetValue(IconProperty);

    // A control template trigger cannot be redefined by a BasedOn style, so the button template reads its
    // interaction fills from here instead of hard-coding them. An accent button then keeps its own accent
    // hover shade while a plain button gets the subtle selection fill.
    public static readonly DependencyProperty HoverBrushProperty =
        DependencyProperty.RegisterAttached("HoverBrush", typeof(Brush), typeof(Iconics), new PropertyMetadata(null));

    public static readonly DependencyProperty PressedBrushProperty =
        DependencyProperty.RegisterAttached("PressedBrush", typeof(Brush), typeof(Iconics), new PropertyMetadata(null));

    public static void SetHoverBrush(DependencyObject element, Brush? value) => element.SetValue(HoverBrushProperty, value);

    public static Brush? GetHoverBrush(DependencyObject element) => (Brush?)element.GetValue(HoverBrushProperty);

    public static void SetPressedBrush(DependencyObject element, Brush? value) => element.SetValue(PressedBrushProperty, value);

    public static Brush? GetPressedBrush(DependencyObject element) => (Brush?)element.GetValue(PressedBrushProperty);
}

/// <summary>Visible when the bound value is present, collapsed when it is null.</summary>
public sealed class PresentObjectToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
