using System.Windows;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App;

public partial class App : Application
{
    private readonly SettingsService _settingsService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EditorSettings settings = _settingsService.Load();

        ThemeChoice theme = SettingsService.ParseTheme(settings.Theme)
            ?? (IsSystemDark() ? ThemeChoice.Dark : ThemeChoice.Light);
        ThemeManager.Apply(this, theme);

        var workspace = new WorkspaceViewModel(_settingsService, settings);
        var window = new MainWindow(workspace);
        MainWindow = window;
        window.Show();
    }

    /// <summary>Reads the OS Apps-use-light-theme registry value (0 => dark).</summary>
    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
