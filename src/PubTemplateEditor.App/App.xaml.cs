using System.Windows;
using System.Windows.Threading;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App;

public partial class App : Application
{
    private readonly SettingsService _settingsService = new();
    private readonly UiFaultGuard _faultGuard = new(new CrashLog());

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Dispatcher.UnhandledException += OnUnhandledUiException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledDomainException;

        EditorSettings settings = _settingsService.Load();

        ThemeChoice theme = SettingsService.ParseTheme(settings.Theme)
            ?? (IsSystemDark() ? ThemeChoice.Dark : ThemeChoice.Light);
        ThemeManager.Apply(this, theme);

        var workspace = new WorkspaceViewModel(_settingsService, settings);
        var window = new MainWindow(workspace);
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// A fault that reached the dispatcher is one no command handled. The decision - record, tell the
    /// user, decide whether to carry on - is the guard's; see <see cref="UiFaultGuard"/>. Keeping the
    /// session alive is right here because every command in this workspace is self-contained: a failed
    /// one leaves the drafts, the folder and the read scope of the others as they were.
    /// </summary>
    private void OnUnhandledUiException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        string context = $"window={MainWindow?.GetType().Name ?? "none"}, theme={ThemeManager.LastApplied}";
        args.Handled = _faultGuard.Handle("unhandled UI exception", args.Exception, ReportFault, context);
    }

    /// <summary>
    /// Last-chance record for a fault on another thread, which cannot be recovered from. Reading and
    /// saving run asynchronously, so this is the one place a background fault can still be written down
    /// before the process ends.
    /// </summary>
    private void OnUnhandledDomainException(object sender, UnhandledExceptionEventArgs args)
    {
        _faultGuard.Handle("fatal unhandled exception", args.ExceptionObject as Exception,
                           report: null, context: $"isTerminating={args.IsTerminating}");
    }

    /// <summary>
    /// One readable sentence about what failed and where the details are, so a fault on a machine the
    /// maintainer cannot reach arrives as a report instead of as a window that disappeared.
    /// </summary>
    private static void ReportFault(string? logPath, bool keepRunning)
    {
        MessageBox.Show(
            "An action failed unexpectedly and did not complete." + Environment.NewLine + Environment.NewLine
            + (keepRunning
                ? "The program stayed open; your staged edits and the open folder are still there."
                : "Several actions have failed in this session, so the program is closing.")
            + (logPath is null
                ? "\n\nThe fault could not be written to a log file."
                : $"\n\nDetails: {logPath}"),
            ProductInfo.Name,
            MessageBoxButton.OK,
            keepRunning ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    /// <summary>Reads the OS Apps-use-light-theme registry value (0 =&gt; dark).</summary>
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
