using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Save review surface: the staged changes of the whole session, one row per field, each with its own
/// tick box. Opening, selecting and discarding must never write a template - the write step is the
/// only thing that may, and in this build it is disabled on purpose.
/// </summary>
[Collection("WPF shell")]
public class ReviewTests
{
    private readonly WpfHost _host;

    public ReviewTests(WpfHost host) => _host = host;

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-review-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Variable(string name, string defaultValue, string access = "internal", string description = "desc") =>
        "<variable><feature tag=\"definition\">" +
        $"<property name=\"name\" value=\"{name}\"></property>" +
        $"<property name=\"description\"><expression type=\"constant\">{description}</expression></property>" +
        "<property name=\"type\" value=\"user\"></property>" +
        $"<property name=\"access\" value=\"{access}\"></property>" +
        $"<property name=\"default\" value=\"{defaultValue}\"></property>" +
        "</feature></variable>";

    private static string TemplateXml(params string[] variables) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<template><dataset><variables>" + string.Concat(variables) + "</variables></dataset></template>";

    private static string Write(string dir, string fileName, string xml)
    {
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private async Task<WorkspaceViewModel> ReadAllAsync(string settingsDir, string templateDir)
    {
        WorkspaceViewModel vm = null!;
        _host.Do(() => vm = new WorkspaceViewModel(new SettingsService(settingsDir), new EditorSettings { LastFolder = templateDir }));
        Task read = _host.Do(() => vm.ReadParametersCommand.ExecuteAsync(null));
        await read;
        return vm;
    }

    /// <summary>
    /// Stages one field of one parameter of one template. The details form always shows the active
    /// template, so the active file is switched first (FILE-05: focus is separate from read state).
    /// </summary>
    private void Stage(WorkspaceViewModel vm, string fileName, string parameterName, FieldKind field, string value)
    {
        _host.Do(() =>
        {
            if (!string.Equals(vm.ActiveFile?.FileName, fileName, StringComparison.Ordinal))
            {
                vm.SelectActiveFile(vm.AllFiles.First(f => f.FileName == fileName));
            }

            vm.SelectedParameter = vm.VisibleParameters.First(r => r.Name == parameterName);
            switch (field)
            {
                case FieldKind.Description:
                    vm.DetailDescription = value;
                    break;
                default:
                    vm.DetailDefaultValue = value;
                    break;
            }

            vm.ApplyEditsCommand.Execute(null);
        });
    }

    [Fact]
    public async Task ReviewListsEveryStagedChangeOfTheSession()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "B")));
        Write(templates, "two.dta", TemplateXml(Variable("alpha", "OLD_A", access: "external")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);

        _host.Do(() => Assert.False(vm.OpenReviewCommand.CanExecute(null))); // nothing staged yet

        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW_A");
        Stage(vm, "one.dta", "beta", FieldKind.Description, "new description");
        Stage(vm, "two.dta", "alpha", FieldKind.DefaultValue, "OTHER");

        _host.Do(() =>
        {
            Assert.Equal(3, vm.StagedChangeCount);
            vm.OpenReviewCommand.Execute(null);
            Assert.True(vm.IsReviewOpen);
            Assert.Equal(3, vm.ReviewRows.Count);

            // One row per field of the change key, not one row per parameter.
            Assert.Equal(2, vm.ReviewRows.Count(r => r.FileName == "one.dta"));
            Assert.Single(vm.ReviewRows, r => r.FileName == "two.dta");
            StagedReviewRow description = vm.ReviewRows.Single(r => r.Field == FieldKind.Description);
            Assert.Equal("new description", description.ProposedText);
            Assert.Equal("desc", description.BaselineText);
            Assert.Equal("default value", vm.ReviewRows.Single(r => r.FileName == "two.dta").FieldLabel);
            Assert.Contains("3 of 3 staged change(s) selected", vm.ReviewSummary, StringComparison.Ordinal);

            // The review is a viewer here: no template on disk may have changed.
            Assert.Contains("OLD_A", File.ReadAllText(Path.Combine(templates, "one.dta")), StringComparison.Ordinal);
            Assert.Contains("OLD_A", File.ReadAllText(Path.Combine(templates, "two.dta")), StringComparison.Ordinal);
        });

        _host.Do(() =>
        {
            vm.CloseReviewCommand.Execute(null);
            Assert.False(vm.IsReviewOpen);
            DeleteDir(settings);
            DeleteDir(templates);
        });
    }

    [Fact]
    public async Task DiscardingInReviewRemovesOnlyTheTickedRowsAndKeepsFilesUntouched()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        string one = Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "KEEP_ME")));
        string two = Write(templates, "two.dta", TemplateXml(Variable("alpha", "OLD_A")));
        byte[] oneBefore = File.ReadAllBytes(one);
        byte[] twoBefore = File.ReadAllBytes(two);

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW_A");
        Stage(vm, "two.dta", "alpha", FieldKind.DefaultValue, "NEW_B");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);

            // Untick the change in two.dta, then discard the rest: only the ticked proposal goes away.
            foreach (StagedReviewRow row in vm.ReviewRows.Where(r => r.FileName == "two.dta"))
            {
                row.IsSelected = false;
            }

            Assert.Equal(1, vm.SelectedReviewCount);
            vm.DiscardSelectedReviewCommand.Execute(null);

            Assert.Equal(1, vm.StagedChangeCount);
            Assert.Single(vm.ReviewRows);
            Assert.Equal("two.dta", vm.ReviewRows[0].FileName);
            Assert.False(vm.ReviewRows[0].IsSelected); // the unticked row survived
        });

        Assert.Equal(oneBefore, File.ReadAllBytes(one));
        Assert.Equal(twoBefore, File.ReadAllBytes(two));

        _host.Do(() =>
        {
            // Discarding the last row closes the review instead of showing an empty list.
            vm.SelectAllReviewCommand.Execute(null);
            vm.DiscardSelectedReviewCommand.Execute(null);
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.False(vm.IsReviewOpen);
            DeleteDir(settings);
            DeleteDir(templates);
        });
    }

    [Fact]
    public async Task SavingIsOfferedForATickedChangeAndRefusedWhileNothingIsTicked()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        string one = Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD_A")));
        byte[] before = File.ReadAllBytes(one);

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW_A");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);

            // A real, ticked change can be saved: the command is live and explains what saving does.
            Assert.True(vm.CanSaveReview);
            Assert.True(vm.SaveReviewCommand.CanExecute(null));
            Assert.Contains("Only the ticked changes are written", vm.SaveUnavailableReason, StringComparison.Ordinal);

            // Untick everything and the offer goes away with a reason, without writing anything.
            vm.SelectNoneReviewCommand.Execute(null);
            Assert.False(vm.CanSaveReview);
            Assert.False(vm.SaveReviewCommand.CanExecute(null));
            Assert.Contains("Tick at least one change", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllBytes(one));

            var window = new ShellWindow(vm) { ShowInTaskbar = false };
            Assert.Equal(Visibility.Visible, window.ReviewList.Visibility);
            Assert.Single(window.ReviewList.Items);
            // Closing with staged work asks first; the seam keeps the modal dialog out of the test.
            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
            vm.NavigationGuard = null;
        });

        Assert.Equal(before, File.ReadAllBytes(one));

        _host.Do(() =>
        {
            vm.CloseReviewCommand.Execute(null);
            DeleteDir(settings);
            DeleteDir(templates);
        });
    }

    [Fact]
    public async Task ReviewKeepsItsTicksWhenMoreWorkIsStagedWhileOpen()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "B"), Variable("gamma", "G")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW_A");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            vm.SelectNoneReviewCommand.Execute(null);
            Assert.Equal(0, vm.SelectedReviewCount);
        });

        // Staging another field while the review is open rebuilds the list. The row the user unticked
        // must stay unticked; only a genuinely new change enters ticked.
        Stage(vm, "one.dta", "beta", FieldKind.Description, "typed while review is open");

        _host.Do(() =>
        {
            Assert.Equal(2, vm.ReviewRows.Count);
            Assert.False(vm.ReviewRows.Single(r => r.Field == FieldKind.DefaultValue).IsSelected);
            Assert.True(vm.ReviewRows.Single(r => r.Field == FieldKind.Description).IsSelected);
            Assert.Equal(1, vm.SelectedReviewCount);

            vm.SelectAllReviewCommand.Execute(null);
            Assert.Equal(2, vm.SelectedReviewCount);
            vm.SelectNoneReviewCommand.Execute(null);
            Assert.Equal(0, vm.SelectedReviewCount);
        });

        _host.Do(() =>
        {
            vm.CloseReviewCommand.Execute(null);
            DeleteDir(settings);
            DeleteDir(templates);
        });
    }

    /// <summary>
    /// A typo in a binding path shows up as an empty cell, not as an exception. The review table binds
    /// every column to one property of <see cref="StagedReviewRow"/>; each expected column is checked
    /// against the XAML and against the row type, so a rename cannot silently blank a column.
    /// </summary>
    [Fact]
    public void EveryReviewColumnBindsToAPropertyOfTheReviewRow()
    {
        string xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "PubTemplateEditor.App", "MainWindow.xaml"));
        int start = xaml.IndexOf("x:Name=\"ReviewListView\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "ReviewListView is missing from MainWindow.xaml");
        // Only the columns bind to a row object; the ListView's own ItemsSource binds the view model.
        int grid = xaml.IndexOf("<GridView", start, StringComparison.Ordinal);
        string block = xaml[grid..xaml.IndexOf("</ListView>", start, StringComparison.Ordinal)];

        var bound = new HashSet<string>(
            System.Text.RegularExpressions.Regex.Matches(block, @"\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        string[] expected =
        [
            nameof(StagedReviewRow.IsSelected),
            nameof(StagedReviewRow.ParameterName),
            nameof(StagedReviewRow.FileName),
            nameof(StagedReviewRow.FieldLabel),
            nameof(StagedReviewRow.BaselineText),
            nameof(StagedReviewRow.ProposedText),
            nameof(StagedReviewRow.WriteStateText),
            nameof(StagedReviewRow.NoOpHint),
        ];

        foreach (string path in expected)
        {
            Assert.True(bound.Contains(path), $"the review table does not bind {path}");
            Assert.NotNull(typeof(StagedReviewRow).GetProperty(path));
        }

        foreach (string path in bound)
        {
            Type current = typeof(StagedReviewRow);
            foreach (string segment in path.Split('.'))
            {
                PropertyInfo? property = current.GetProperty(segment);
                Assert.True(property is not null, $"the review table binds {path}, which has no {segment} on {current.Name}");
                current = property!.PropertyType;
            }
        }
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubTemplateEditor.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private static void DeleteDir(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary folder is noise, not a test failure.
        }
    }
}
