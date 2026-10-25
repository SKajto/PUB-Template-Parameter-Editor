using System.Windows.Media;
using Microsoft.Win32;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Reads the Windows "choose your mode" app preference. Registry access is a built-in
/// Windows API; a missing or unreadable value falls back to Light rather than failing.
/// </summary>
public static class SystemThemePreference
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static ThemeChoice Current()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("AppsUseLightTheme") is int useLight)
            {
                return useLight == 0 ? ThemeChoice.Dark : ThemeChoice.Light;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Intentional: a theme preference is never a reason to fail startup.
        }

        return ThemeChoice.Light;
    }
}
