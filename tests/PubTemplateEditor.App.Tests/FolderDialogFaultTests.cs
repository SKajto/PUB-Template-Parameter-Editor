using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// What the workspace does about the Windows folder dialog, on the evidence of a fault report from
/// another machine: a remembered folder written with '/' separators reached the shell item API, which
/// answered E_INVALIDARG from inside the dialog's own setup, and because nothing in the application was
/// prepared for an exception out of a command, the process ended with no message and the user's staged
/// edits with it.
/// </summary>
[Collection("WPF shell")]
public class FolderDialogFaultTests
{
    private readonly WpfHost _host;

    public FolderDialogFaultTests(WpfHost host) => _host = host;

    /// <summary>Stands in for the shell dialog: records what it was asked for, and refuses when told to.</summary>
    private sealed class FakePicker : IFolderPicker
    {
        public List<string?> Asked { get; } = [];
        public Func<string?, int, string?> Behaviour { get; set; } = (_, _) => null;
        public int Calls => Asked.Count;

        public string? PickFolder(string? initialDirectory, string title)
        {
            Asked.Add(initialDirectory);
            return Behaviour(initialDirectory, Calls);
        }
    }

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-folderdialog-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private WorkspaceViewModel NewModel(EditorSettings settings, IFolderPicker picker, string? templates = null)
    {
        WorkspaceViewModel vm = null!;
        _host.Do(() => vm = new WorkspaceViewModel(new SettingsService(TempDir("settings")), settings, templates, picker));
        return vm;
    }

    private void OpenFolder(WorkspaceViewModel vm) => _host.Do(() => vm.OpenFolderCommand.Execute(null));

    [Fact]
    public void TheDialogIsOfferedNoStartingFolderItCannotBeExpectedToAccept()
    {
        string templates = TempDir("templates");
        var picker = new FakePicker();
        WorkspaceViewModel vm = NewModel(new EditorSettings { LastFolder = templates }, picker, templates);

        OpenFolder(vm);

        Assert.Equal(1, picker.Calls);
        Assert.Equal(templates, picker.Asked[0]);          // a folder that is open and exists: a fair starting point
        Assert.Equal(templates, vm.FolderPath);            // a cancelled dialog changes nothing
    }

    [Fact]
    public void AFolderThatHasGoneAwayIsNotHandedToTheDialog()
    {
        string templates = TempDir("gone");
        var picker = new FakePicker();
        WorkspaceViewModel vm = NewModel(new EditorSettings { LastFolder = templates }, picker, templates);
        Directory.Delete(templates);

        OpenFolder(vm);

        Assert.Equal(1, picker.Calls);
        Assert.Null(picker.Asked[0]);                      // the dialog opens at its own default instead
    }

    [Fact]
    public void ARememberedFolderWrittenWithForwardSlashesIsNormalisedBeforeTheDialogSeesIt()
    {
        string templates = TempDir("slashed");
        string written = templates.Replace('\\', '/');     // what the settings file of the reporting machine held
        var picker = new FakePicker();
        WorkspaceViewModel vm = NewModel(new EditorSettings { LastFolder = written }, picker, templates);

        Assert.Equal(templates, vm.FolderPath);

        OpenFolder(vm);

        Assert.Equal(templates, picker.Asked[0]);
        Assert.DoesNotContain("/", picker.Asked[0]!, StringComparison.Ordinal);
    }

    [Fact]
    public void ADialogTheShellRefusesIsRetriedWithoutAStartingFolder()
    {
        string templates = TempDir("retry-start");
        string chosen = TempDir("retry-chosen");
        var picker = new FakePicker
        {
            Behaviour = (initial, call) => call == 1
                ? throw new ArgumentException("Value does not fall within the expected range.")   // E_INVALIDARG
                : chosen,
        };
        WorkspaceViewModel vm = NewModel(new EditorSettings { LastFolder = templates }, picker, templates);

        OpenFolder(vm);                                    // must not escape: an escaping exception ends the process

        Assert.Equal(2, picker.Calls);
        Assert.Equal(templates, picker.Asked[0]);
        Assert.Null(picker.Asked[1]);
        Assert.Equal(chosen, vm.FolderPath);
        Assert.Contains("default location", vm.ActionNotice ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ADialogThatKeepsRefusingIsReportedAndTheSessionSurvives()
    {
        string templates = TempDir("refusing");
        var picker = new FakePicker
        {
            Behaviour = (_, _) => throw new COMException("The parameter is incorrect. (0x80070057)", unchecked((int)0x80070057)),
        };
        WorkspaceViewModel vm = NewModel(new EditorSettings { LastFolder = templates }, picker, templates);

        OpenFolder(vm);

        Assert.Equal(2, picker.Calls);                      // one retry, not a loop
        Assert.Equal(templates, picker.Asked[0]);
        Assert.Null(picker.Asked[1]);
        Assert.Equal(templates, vm.FolderPath);            // the open folder and its staged work are untouched
        Assert.Contains("folder dialog could not be opened", vm.ActionError ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("dragging", vm.StatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.OpenFolderCommand.CanExecute(null));
    }

    [Fact]
    public void UnusableRememberedFoldersAreDroppedFromTheListAndFromTheFile()
    {
        string templates = TempDir("heal");
        string settingsDirectory = TempDir("settings-heal");
        var loaded = new EditorSettings
        {
            RecentFolders =
            [
                templates.Replace('\\', '/'),
                "relative/templates",
                @"C:\Users\someone\Templates\",
            ],
        };

        WorkspaceViewModel vm = null!;
        var picker = new FakePicker();
        _host.Do(() => vm = new WorkspaceViewModel(new SettingsService(settingsDirectory), loaded, null, picker));

        string[] expected = [templates, @"C:\Users\someone\Templates"];
        Assert.Equal(expected, vm.RecentFolders.ToArray());

        string saved = File.ReadAllText(Path.Combine(settingsDirectory, "settings.json"));
        Assert.Contains(templates.Replace("\\", "\\\\"), saved, StringComparison.Ordinal);
        Assert.DoesNotContain("relative/templates", saved, StringComparison.Ordinal);
    }
}
