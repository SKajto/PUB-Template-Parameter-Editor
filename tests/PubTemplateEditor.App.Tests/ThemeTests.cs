using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using PubTemplateEditor.App;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Theme contract tests: both palettes expose the same keys, every DynamicResource used
/// anywhere in the shell resolves under both themes, and switching really changes brushes.
/// </summary>
[Collection("WPF shell")]
public class ThemeTests
{
    private readonly WpfHost _host;

    public ThemeTests(WpfHost host) => _host = host;

    private static Uri ThemeUri(ThemeChoice theme)
    {
        string assembly = typeof(ThemeManager).Assembly.GetName().Name!;
        return new Uri($"pack://application:,,,/{assembly};component/Themes/{theme}.xaml", UriKind.Absolute);
    }

    private static Uri ControlsUri()
    {
        string assembly = typeof(ThemeManager).Assembly.GetName().Name!;
        return new Uri($"pack://application:,,,/{assembly};component/Themes/Controls.xaml", UriKind.Absolute);
    }

    [Fact]
    public void LightAndDarkDefineTheSameKeys()
    {
        _host.Do(() =>
        {
            var light = new ResourceDictionary { Source = ThemeUri(ThemeChoice.Light) };
            var dark = new ResourceDictionary { Source = ThemeUri(ThemeChoice.Dark) };

            var lightKeys = light.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k, StringComparer.Ordinal);
            var darkKeys = dark.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k, StringComparer.Ordinal);

            Assert.Equal(lightKeys, darkKeys);
            Assert.NotEmpty(lightKeys);
        });
    }

    [Fact]
    public void EveryDynamicResourceKeyUsedByTheShellResolvesInBothThemes()
    {
        string xamlDir = Path.Combine(RepoRoot(), "src", "PubTemplateEditor.App");
        var pattern = new Regex(@"\{DynamicResource\s+([A-Za-z0-9_.]+)\}", RegexOptions.Compiled);

        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(xamlDir, "*.xaml", SearchOption.AllDirectories))
        {
            // Themes themselves define their keys; only the shell consumes them.
            if (Path.GetFileName(file) is "Light.xaml" or "Dark.xaml" or "Controls.xaml")
            {
                continue;
            }

            foreach (Match m in pattern.Matches(File.ReadAllText(file)))
            {
                used.Add(m.Groups[1].Value);
            }
        }

        Assert.NotEmpty(used);

        _host.Do(() =>
        {
            foreach (ThemeChoice theme in new[] { ThemeChoice.Light, ThemeChoice.Dark })
            {
                ThemeManager.Apply(_host.App, theme);
                var dictionaries = ((ResourceDictionary)_host.App.Resources).MergedDictionaries;
                if (!dictionaries.Any(d => d.Source == ControlsUri()))
                {
                    dictionaries.Add(new ResourceDictionary { Source = ControlsUri() });
                }

                foreach (string key in used)
                {
                    object? found = _host.App.TryFindResource(key);
                    Assert.NotNull(found);
                }
            }

            // Leave the shell in its default palette for other tests.
            ThemeManager.Apply(_host.App, ThemeChoice.Light);
        });
    }

    [Fact]
    public void SwitchingThemeChangesResolvableBrushes()
    {
        _host.Do(() =>
        {
            ThemeManager.Apply(_host.App, ThemeChoice.Light);
            var lightBrush = (SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush");

            ThemeManager.Apply(_host.App, ThemeChoice.Dark);
            var darkBrush = (SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush");

            Assert.NotEqual(lightBrush.Color, darkBrush.Color);
            Assert.True(lightBrush.Color.R > darkBrush.Color.R, "light palette background should be brighter");

            ThemeManager.Apply(_host.App, ThemeChoice.Light);
        });
    }

    [Fact]
    public void ThemeTogglePersistsTheChoice()
    {
        string settingsDir = Path.Combine(Path.GetTempPath(), "dtaparam-theme-" + Guid.NewGuid().ToString("N"));
        string templateDir = Path.Combine(Path.GetTempPath(), "dtaparam-tpl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateDir);

        try
        {
            _host.Do(() =>
            {
                var settings = new SettingsService(settingsDir);
                var vm = new PubTemplateEditor.App.ViewModels.WorkspaceViewModel(settings, new EditorSettings { Theme = "Light", LastFolder = templateDir });

                ThemeManager.Apply(_host.App, ThemeChoice.Light);
                var before = (SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush");
                Assert.True(before.Color.R > 128);

                vm.ToggleThemeCommand.Execute(null);

                var after = (SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush");
                Assert.True(after.Color.R < 128, "dark palette should be active after toggle");
                Assert.Contains("theme applied and remembered", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

                var reloaded = settings.Load();
                Assert.Equal("Dark", reloaded.Theme);
            });
        }
        finally
        {
            DeleteDir(settingsDir);
            DeleteDir(templateDir);
        }
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubTemplateEditor.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    internal static void DeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best effort; the OS reclaims %TEMP%.
        }
    }
}
