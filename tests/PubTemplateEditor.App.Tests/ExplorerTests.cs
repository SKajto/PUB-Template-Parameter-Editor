using System.IO;
using System.Windows.Controls;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using PubTemplateEditor.TestSupport;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// S2 explorer behaviour: active-template focus, explicit read, parameter search and
/// access filtering with live counts, and the read-only detail panel.
/// </summary>
[Collection("WPF shell")]
public class ExplorerTests
{
    private readonly WpfHost _host;

    public ExplorerTests(WpfHost host) => _host = host;

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The native sample when this machine has one, otherwise a synthetic container of the same
    /// shape: the .dta samples are user content and are not part of the repository.
    /// </summary>
    private static string SampleCopy(string name, string dir) => TestSamples.CopyNativeOrSynthetic(dir, name);

    private WorkspaceViewModel NewViewModel(string settingsDir, string templateDir) =>
        new(new SettingsService(settingsDir), new EditorSettings { LastFolder = templateDir });

    [Fact]
    public async Task ReadFillsTheTableAndFocusStaysSeparateFromReadSelection()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        // Content of this project's own making, so the numbers below are the test's contract and not a
        // fact about somebody's template: one declaration in the first file, three in the second.
        TestSamples.WriteSingleDeclarationContainer(templates, "alpha.dta");
        TestSamples.WriteSyntheticContainer(templates, "beta.dta");

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(settingsDir, templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            // The first listed file becomes the focused template without changing scope.
            Assert.Equal("alpha.dta", vm.ActiveFile!.FileName);
            Assert.All(vm.AllFiles, f => Assert.True(f.IsSelectedForRead));
            Assert.Equal(1, vm.TotalParameterCount); // alpha.dta declares exactly one variable
            Assert.Equal(1, vm.MatchedParameterCount);
            Assert.False(vm.ShowEmptyNotRead);

            // FILE-05: focusing another template never touches read selection.
            var other = vm.AllFiles.Single(f => f.FileName == "beta.dta");
            vm.SelectActiveFile(other);
            Assert.Equal("beta.dta", vm.ActiveFile.FileName);
            Assert.True(other.IsActive);
            Assert.False(vm.AllFiles.Single(f => f.FileName == "alpha.dta").IsActive);
            Assert.Equal(2, vm.SelectedReadCount);
            // The table shows the focused template, not the sum of everything that was read.
            Assert.Equal(3, vm.TotalParameterCount);
            Assert.Contains("3 matched of 3 parameters", vm.ActiveParameterSummary);
        });
    }

    [Fact]
    public async Task SearchAndAccessFilterNarrowTheTableWithLiveCounts()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        // Three declarations with known names and access values: ProjectName and EmptyDefault are
        // external, Budget is internal. Every count asserted below follows from that fixture.
        TestSamples.WriteSyntheticContainer(templates, "parameters.dta");

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(settingsDir, templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            Assert.Equal(3, vm.MatchedParameterCount);

            // Names are matched case-sensitively and anchored at the start, which is the rule the
            // reference editor uses; the exact start of a name finds its row.
            vm.ParameterQuery = "Budget";
            Assert.Equal(1, vm.MatchedParameterCount);
            Assert.Single(vm.VisibleParameters);
            Assert.Equal("Budget", vm.VisibleParameters[0].Name);

            // The same word in another case is still a name-region attempt: it does not fall through
            // to the description, which case-insensitively would have matched "Budget in EUR".
            vm.ParameterQuery = "budget";
            Assert.Equal(0, vm.MatchedParameterCount);
            Assert.True(vm.ShowEmptyNoMatches);

            // Descriptions match anywhere in the text, case-insensitively.
            vm.ParameterQuery = "name of the project";
            Assert.Equal(1, vm.MatchedParameterCount);
            vm.ParameterQuery = "Project";
            Assert.Equal(1, vm.MatchedParameterCount);
            vm.ParameterQuery = "PROJECT";
            Assert.Equal(0, vm.MatchedParameterCount); // names keep their case

            vm.ParameterQuery = "no-such-parameter-at-all";
            Assert.Equal(0, vm.MatchedParameterCount);
            Assert.True(vm.ShowEmptyNoMatches);
            Assert.False(vm.ShowEmptyNotRead);

            vm.ParameterQuery = null;
            Assert.Equal(3, vm.MatchedParameterCount);

            // Access filter (the access property, not the XML type property).
            int internalOnly = vm.AllParameters.Count(p =>
                string.Equals(p.AccessLabel, "internal", StringComparison.OrdinalIgnoreCase));
            vm.SelectedAccessFilter = AccessFilterChoice.Internal;
            Assert.Equal(internalOnly, vm.MatchedParameterCount);
            Assert.All(vm.VisibleParameters, p => Assert.Equal("internal", p.AccessLabel, ignoreCase: true));

            Assert.Equal(1, internalOnly); // Budget

            vm.SelectedAccessFilter = AccessFilterChoice.External;
            Assert.Equal(3 - internalOnly, vm.MatchedParameterCount);

            vm.SelectedAccessFilter = AccessFilterChoice.All;
            Assert.Equal(3, vm.MatchedParameterCount);
        });
    }

    [Fact]
    public async Task DetailPanelShowsTheSelectedDeclarationFields()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        SampleCopy("example.dta", templates);

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(settingsDir, templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            var table = window.ParameterTable;
            Assert.IsType<ListView>(table);

            var row = vm.VisibleParameters.First();
            vm.SelectedParameter = row;
            Assert.True(vm.HasSelectedParameter);
            Assert.Equal(row.Name, vm.DetailName);
            Assert.Equal(row.AccessLabel, vm.DetailAccess);
            Assert.Equal(row.XmlTypeLabel, vm.DetailXmlType);
            Assert.Equal(row.DefaultFullValue, vm.DetailDefaultValue);
            Assert.Equal(row.DescriptionFullValue, vm.DetailDescription);

            // Values are carried exactly, without trimming or type coercion.
            // The rendered detail controls must show exactly those values.
            Assert.Equal(row.Name, window.DetailNameLabel.Text);
            Assert.Equal(row.DefaultFullValue, window.DetailValueEditor.Text);
            Assert.Equal(row.AccessLabel, window.DetailAccessEditor.Text);
            Assert.Equal(row.DescriptionFullValue, window.DetailDescriptionEditor.Text);
            Assert.Equal(row.XmlTypeLabel, window.DetailXmlTypeLabel.Text);
            // S2 kept every field read-only; S3 opens the observed fields and locks the rest.

            var declaration = row.Declaration;
            Assert.Equal(declaration.DefaultValue.LogicalValue, vm.DetailDefaultValue);
            Assert.Equal(declaration.Description.LogicalValue, vm.DetailDescription);

            // S3: the form edits exactly the fields whose stored shape was observed, and locks the rest.
            Assert.False(vm.CanEditName);
            Assert.Equal(declaration.DefaultValue.Capability == FieldCapability.Editable, !window.DetailValueEditor.IsReadOnly);
            Assert.Equal(declaration.Description.Capability == FieldCapability.Editable, !window.DetailDescriptionEditor.IsReadOnly);
            if (declaration.Access.Capability == FieldCapability.Editable)
            {
                Assert.True(vm.CanEditAccess);
                Assert.Contains(vm.DetailAccess, vm.AccessChoices);
            }
            else
            {
                Assert.False(vm.CanEditAccess);
                Assert.True(window.DetailAccessEditor.IsReadOnly);
            }

            Assert.Equal(0, vm.UnappliedFieldCount);
            Assert.False(vm.HasUnappliedFormEdits);
            Assert.False(vm.HasStagedChanges);

            // A read-only shape is explained instead of silently hidden.
            if (row.HasReadOnlyShape)
            {
                Assert.True(vm.HasDetailReadOnlyReason);
                Assert.Contains("unsupported", vm.DetailReadOnlyReason, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    [Fact]
    public void SelectAndClearVisibleChangeOnlyTheReadScope()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        SampleCopy("example.dta", templates);
        SampleCopy("requirements_specification_yps.dta", templates);
        File.WriteAllText(Path.Combine(templates, "notes.txt"), "not a template");

        _host.Do(() =>
        {
            var vm = NewViewModel(settingsDir, templates);
            Assert.Equal(2, vm.TotalFileCount);
            Assert.All(vm.AllFiles, f => Assert.True(f.IsSelectedForRead));

            vm.ClearVisibleCommand.Execute(null);
            Assert.Equal(0, vm.SelectedReadCount);
            Assert.Contains("visible", vm.ScopeDescription, StringComparison.OrdinalIgnoreCase);

            // FILE-05: focusing never re-checks a checkbox.
            vm.SelectActiveFile(vm.AllFiles.First());
            Assert.Equal(0, vm.SelectedReadCount);

            // Filtering narrows what "visible" means for the scope buttons.
            vm.FilterText = "example";
            Assert.Single(vm.VisibleFiles);
            vm.SelectVisibleCommand.Execute(null);
            Assert.Equal(1, vm.SelectedReadCount);
            Assert.True(vm.AllFiles.Single(f => f.FileName == "example.dta").IsSelectedForRead);
            Assert.False(vm.AllFiles.Single(f => f.FileName == "requirements_specification_yps.dta").IsSelectedForRead);

            // The filter must not change which template is focused.
            Assert.Equal("example.dta", vm.ActiveFile!.FileName);
        });
    }

    [Fact]
    public async Task ReadFallsBackToVisibleTemplatesAndRefusesAnEmptyScope()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        TestSamples.WriteSingleDeclarationContainer(templates, "example.dta");

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(settingsDir, templates);
            vm.ClearVisibleCommand.Execute(null);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            Assert.Equal(0, vm.SelectedReadCount);
            Assert.Contains("No readable templates", vm.ActionError);
            Assert.True(vm.HasActionError);
            Assert.True(vm.ShowEmptyNotRead);

            vm.SelectVisibleCommand.Execute(null);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            // A successful read clears the previous error state.
            Assert.False(vm.HasActionError);
            Assert.False(vm.ShowEmptyNotRead);
            Assert.Equal(1, vm.TotalParameterCount);
        });
    }

    [Fact]
    public void EmptyStatesAreMutuallyExclusive()
    {
        string settingsDir = TempDir("settings");
        string templates = TempDir("templates");
        SampleCopy("example.dta", templates);

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = templates });
            // A listed but unread template shows the "not read yet" state only.
            Assert.NotNull(vm.ActiveFile);
            Assert.True(vm.ShowEmptyNotRead);
            Assert.False(vm.ShowEmptyNoActiveFile);
            Assert.False(vm.ShowEmptyNoMatches);

            vm.FilterText = "zzz";
            Assert.True(vm.ShowNoFileMatches);
            Assert.Empty(vm.VisibleFiles);
            Assert.Equal(1, vm.SelectedReadCount); // filtering never changes the read scope
        });
    }
}
