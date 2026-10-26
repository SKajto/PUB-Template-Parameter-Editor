using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using PubTemplateEditor.TestSupport;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Shell behaviour around the S0/S1 scope: window construction, remembered folder,
/// explorer listing without parsing, filtering and the explicit read command.
/// </summary>
[Collection("WPF shell")]
public class ShellTests : IDisposable
{
    private readonly WpfHost _host;
    private readonly List<string> _tempDirs = [];

    public ShellTests(WpfHost host) => _host = host;

    private string NewTempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"dtaparam-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    /// <summary>
    /// A throwaway folder holding template files. Where the native samples are on disk they are used;
    /// without them (a fresh clone, CI) synthetic containers of the same names and shape stand in, so
    /// the shell is exercised the same way either way.
    /// </summary>
    private string SampleFolder(params string[] names)
    {
        string dir = NewTempDir("templates");
        string[] effective = names.Length == 0
            ? TestSamples.HasNative("example.dta")
                ? Directory.GetFiles(TestSamples.SamplesDirectory, "*.dta")
                    .Select(Path.GetFileName)!.ToArray()
                : new[] { "example.dta", "requirements_specification_yps.dta", "solution_configuration_specification.dta" }
            : names;

        foreach (string name in effective)
        {
            TestSamples.CopyNativeOrSynthetic(dir, name!);
        }

        return dir;
    }

    private static Dictionary<string, string> Fingerprints(string dir) =>
        Directory.GetFiles(dir).ToDictionary(
            f => Path.GetFileName(f)!,
            f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    [Fact]
    public void WindowConstructsBindsAndResolvesNamedParts()
    {
        string settingsDir = NewTempDir("settings");
        string templateDir = SampleFolder("example.dta");

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings());
            var window = new ShellWindow(vm);

            Assert.Same(vm, window.DataContext);
            Assert.Equal("PUB Template Parameter Editor", window.Title);
            Assert.NotNull(window.FilterControl);
            Assert.Same(vm, window.Workspace);

            // Resource lookup through the visual tree proves the theme dictionaries apply.
            window.ApplyTemplate();
            var brush = window.TryFindResource("WindowBackgroundBrush") as SolidColorBrush;
            Assert.NotNull(brush);

            // Ctrl+F plumbing: the view model request reaches the view's filter box.
            bool raised = false;
            vm.FilterFocusRequested += () => raised = true;
            vm.FocusFilterCommand.Execute(null);
            Assert.True(raised);
        });
    }

    [Fact]
    public void RememberedFolderIsRestoredListedAndPersisted()
    {
        string settingsDir = NewTempDir("settings");
        string templateDir = SampleFolder("example.dta", "requirements_specification_yps.dta");
        var service = new SettingsService(settingsDir);

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(service, new EditorSettings { LastFolder = templateDir });

            Assert.Equal(templateDir, vm.FolderPath);
            Assert.Equal(2, vm.AllFiles.Count);
            Assert.Equal(["example.dta", "requirements_specification_yps.dta"],
                vm.AllFiles.Select(f => f.FileName).ToArray());
            Assert.All(vm.AllFiles, f => Assert.Null(f.Record)); // FILE-04: listing never parses
            Assert.Contains("Press Read parameters", vm.StatusMessage);

            // GEN-01: the folder survives a reload from disk.
            Assert.Equal(templateDir, service.Load().LastFolder);
        });
    }

    [Fact]
    public void MissingRememberedFolderProducesAClearMessage()
    {
        string settingsDir = NewTempDir("settings");
        string ghost = Path.Combine(Path.GetTempPath(), $"dtaparam-missing-{Guid.NewGuid():N}");

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = ghost });

            Assert.Null(vm.FolderPath);
            Assert.Empty(vm.AllFiles);
            Assert.Contains("could not be opened", vm.StatusMessage);
        });
    }

    [Fact]
    public void NonTemplateFilesAreNotOffered()
    {
        string settingsDir = NewTempDir("settings");
        string dir = SampleFolder("example.dta");
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "not a template");
        File.WriteAllText(Path.Combine(dir, "template.zip"), "container look-alike");

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = dir });
            Assert.Single(vm.AllFiles);
            Assert.Equal("example.dta", vm.AllFiles[0].FileName);
        });
    }

    [Fact]
    public void FilterNarrowsTheExplorerWithoutChangingReadSelection()
    {
        string settingsDir = NewTempDir("settings");
        string dir = SampleFolder();

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = dir });
            int total = vm.AllFiles.Count;
            Assert.Equal(5, total);
            Assert.Equal(total, vm.SelectedReadCount); // newly listed files start selected

            vm.FilterText = "solution";
            Assert.Single(vm.VisibleFiles);
            Assert.Equal("solution_configuration_specification.dta", vm.VisibleFiles[0].FileName);
            Assert.Equal(total, vm.SelectedReadCount); // filtering never changes selection

            vm.FilterText = "";
            Assert.Equal(total, vm.VisibleFiles.Count);

            vm.ClearVisibleCommand.Execute(null);
            Assert.Equal(0, vm.SelectedReadCount);
            vm.SelectVisibleCommand.Execute(null);
            Assert.Equal(total, vm.SelectedReadCount);
        });
    }

    [Fact]
    public async Task ExplicitReadParsesSelectedTemplatesAndLeavesFilesUntouched()
    {
        string settingsDir = NewTempDir("settings");
        string dir = SampleFolder("requirements_specification_yps.dta", "solution_configuration_specification.dta");
        var before = Fingerprints(dir);

        Task readTask = Task.CompletedTask;
        WorkspaceViewModel vm = null!;

        _host.Do(() =>
        {
            vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = dir });
            // ExecuteAsync must be started on the UI thread; awaiting it off-thread keeps
            // the dispatcher free to run the continuations the command posts back.
            readTask = vm.ReadParametersCommand.ExecuteAsync(null);
        });

        await readTask;

        _host.Do(() =>
        {
            Assert.Equal(2, vm.AllFiles.Count(f => f.Record?.ReadState == TemplateReadState.Loaded));
            // 234 declarations is what the native sample holds; the synthetic stand-in holds three, and
            // what matters here is that reading really filled the record.
            int expectedDeclarations = TestSamples.HasNative("requirements_specification_yps.dta") ? 234 : 3;
            Assert.Equal(expectedDeclarations,
                vm.AllFiles.Single(f => f.FileName == "requirements_specification_yps.dta").Record!.Declarations.Count);
            Assert.Contains("declaration(s) total", vm.StatusMessage);
            Assert.False(vm.IsBusy);
            Assert.Equal(0.0, vm.Progress);
        });

        Assert.Equal(before, Fingerprints(dir)); // reading never writes templates
    }

    [Fact]
    public async Task BrokenTemplateStaysVisibleWithAnErrorStatus()
    {
        string settingsDir = NewTempDir("settings");
        string dir = SampleFolder("example.dta");
        string broken = Path.Combine(dir, "broken.dta");
        File.WriteAllBytes(broken, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]); // truncated ZIP magic

        Task readTask = Task.CompletedTask;
        WorkspaceViewModel vm = null!;

        _host.Do(() =>
        {
            vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = dir });
            readTask = vm.ReadParametersCommand.ExecuteAsync(null);
        });

        await readTask;

        _host.Do(() =>
        {
            var bad = vm.AllFiles.Single(f => f.FileName == "broken.dta");
            Assert.Equal(TemplateReadState.Error, bad.Record!.ReadState);
            Assert.True(bad.HasError);
            Assert.Contains("Error", bad.Status);
            Assert.Contains("had errors", vm.StatusMessage);
            Assert.NotNull(vm.AllFiles.Single(f => f.FileName == "example.dta").Record);
        });
    }

    public void Dispose()
    {
        foreach (string dir in _tempDirs)
        {
            ThemeTests.DeleteDir(dir);
        }
    }
}
