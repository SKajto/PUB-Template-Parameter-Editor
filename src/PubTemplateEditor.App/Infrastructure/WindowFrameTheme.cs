using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Paints the window frame - caption, border and title text - with the theme the application is showing.
/// WPF only draws the client area; the frame belongs to the desktop window manager, so it is asked
/// through dwmapi.dll, a built-in Windows API (no third-party dependency). Calls that an older Windows
/// build does not support simply return an error and the window keeps the system frame, which is why no
/// result is treated as fatal.
/// </summary>
public static class WindowFrameTheme
{
    /// <summary>19 on Windows 10 builds before 20H1, 20 from 20H1 on.</summary>
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    /// <summary>Windows 11 only; ignored with an error on older systems.</summary>
    private const int DwmwaBorderColor = 34;

    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Follows the app's own light/dark choice (including "follow Windows", already resolved by
    /// <see cref="ThemeManager"/>), and re-applies when the user switches the interface theme.
    /// </summary>
    public static void Attach(Window window)
    {
        void ApplyNow() => Apply(window, EffectiveIsDark(), ThemeColours());

        window.SourceInitialized += (_, _) => { ApplyNow(); ThemeManager.Applied += Handler; };
        window.Closed += (_, _) => ThemeManager.Applied -= Handler;
        void Handler(ThemeChoice _) => ApplyNow();

        // A window that is already realized (dialogs opened after start-up) needs no wait.
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            ApplyNow();
        }
    }

    public static bool EffectiveIsDark() => ThemeManager.LastApplied == ThemeChoice.Dark;

    public static void Apply(Window window, bool dark, (Color Caption, Color Text, Color Border) colours)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int useDark = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
        }

        int caption = ToColorRef(colours.Caption);
        int text = ToColorRef(colours.Text);
        int border = ToColorRef(colours.Border);
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }

    /// <summary>
    /// The frame takes the same surfaces the panels use, so the title bar reads as part of the window
    /// instead of a separate strip. Falls back to the light palette if a key is missing.
    /// </summary>
    public static (Color Caption, Color Text, Color Border) ThemeColours()
    {
        ResourceDictionary resources = Application.Current.Resources;
        return (Named(resources, "PanelBackgroundColor", Color.FromRgb(0xFF, 0xFF, 0xFF)),
                Named(resources, "ForegroundColor", Color.FromRgb(0x36, 0x41, 0x50)),
                Named(resources, "BorderColor", Color.FromRgb(0xD8, 0xE0, 0xEB)));
    }

    private static Color Named(ResourceDictionary resources, string key, Color fallback) =>
        resources[key] is Color color ? color : fallback;

    /// <summary>COLORREF is 0x00BBGGRR, byte order reversed from a WPF colour.</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}
