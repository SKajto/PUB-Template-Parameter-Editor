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
    public void EveryStaticResourceKeyUsedByTheShellIsDefined()
    {
        // A missing style otherwise only surfaces as a crash when the shell is constructed.
        var used = new SortedSet<string>(StringComparer.Ordinal);
        var defined = new SortedSet<string>(StringComparer.Ordinal);
        string dir = Path.Combine(RepoRoot(), "src", "PubTemplateEditor.App");
        var staticPattern = new Regex(@"\{StaticResource\s+([^}]+)\}", RegexOptions.Compiled);
        var keyPattern = new Regex(@"x:Key=""([A-Za-z0-9_.]+)""", RegexOptions.Compiled);

        foreach (string file in Directory.GetFiles(dir, "*.xaml", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in staticPattern.Matches(text))
            {
                string key = m.Groups[1].Value.Trim();
                if (key.Contains("x:Type"))
                {
                    continue; // implicit theme keys such as {x:Type Button}
                }
                used.Add(key);
            }

            if (Path.GetFileName(file) is "Light.xaml" or "Dark.xaml" or "Controls.xaml" or "App.xaml")
            {
                foreach (Match m in keyPattern.Matches(text))
                {
                    defined.Add(m.Groups[1].Value);
                }
            }
        }

        var missing = used.Except(defined).ToArray();
        Assert.True(missing.Length == 0, "undefined resources: " + string.Join(", ", missing));
    }

    [Fact]
    public void ShellXamlCarriesNoHardCodedColours()
    {
        // Colours must come from the theme dictionaries, otherwise Dark mode stays unreadable.
        var hex = new Regex("#[0-9A-Fa-f]{3,8}\\b", RegexOptions.Compiled);
        string dir = Path.Combine(RepoRoot(), "src", "PubTemplateEditor.App");

        foreach (string file in Directory.GetFiles(dir, "*.xaml", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) is "Light.xaml" or "Dark.xaml")
            {
                continue;
            }

            Assert.False(hex.IsMatch(File.ReadAllText(file)), $"hard-coded colour in {Path.GetFileName(file)}");
        }
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
    public void ThemeSelectionAppliesAndPersists()
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
                Assert.True(((SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush")).Color.R > 128);

                vm.SetThemeCommand.Execute("Dark");

                Assert.True(((SolidColorBrush)_host.App.FindResource("WindowBackgroundBrush")).Color.R < 128,
                    "dark palette should be active after selecting Dark");
                Assert.True(vm.IsDarkSelected);
                Assert.False(vm.IsLightSelected);
                Assert.Equal("Dark", settings.Load().Theme);

                // System follows the Windows app preference and is stored as such.
                vm.SetThemeCommand.Execute("System");
                Assert.True(vm.IsSystemSelected);
                Assert.Equal("System", settings.Load().Theme);
                ThemeChoice resolved = ThemeManager.Resolve(_host.App, ThemeChoice.System);
                Assert.True(resolved is ThemeChoice.Light or ThemeChoice.Dark);

                ThemeManager.Apply(_host.App, ThemeChoice.Light);
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
