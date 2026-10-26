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
    /// A typo in a binding path shows up as a blank card, not as an exception. The review list binds group
    /// properties on <see cref="ReviewFileGroup"/> and row properties on <see cref="StagedReviewRow"/>; the
    /// properties a reviewer needs to judge a change are named here, and every path inside the list is
    /// resolved against the type that owns it, so a rename cannot silently blank part of the review.
    /// </summary>
    [Fact]
    public void EveryReviewCardBindingResolvesOnTheTypeThatOwnsIt()
    {
        string xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "PubTemplateEditor.App", "MainWindow.xaml"));
        int start = xaml.IndexOf("x:Name=\"ReviewGroupsList\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "ReviewGroupsList is missing from MainWindow.xaml");
        // The list's own ItemsSource binds the view model; only what is inside the list binds a group or a row.
        int contentStart = xaml.IndexOf('>', start) + 1;
        string block = xaml[contentStart..xaml.IndexOf("</ScrollViewer>", contentStart, StringComparison.Ordinal)];

        var bound = new HashSet<string>(
            System.Text.RegularExpressions.Regex.Matches(block, @"\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        string[] rowPaths =
        [
            nameof(StagedReviewRow.IsSelected),
            nameof(StagedReviewRow.ParameterName),
            nameof(StagedReviewRow.FieldLabel),
            nameof(StagedReviewRow.BaselineText),
            nameof(StagedReviewRow.ProposedText),
            nameof(StagedReviewRow.WriteStateText),
            nameof(StagedReviewRow.NoOpHint),
            nameof(StagedReviewRow.HasValueDiff),
            nameof(StagedReviewRow.IsCompleteReplacement),
            nameof(StagedReviewRow.HasHiddenWhitespace),
            nameof(StagedReviewRow.HiddenWhitespaceHint),
            $"{nameof(StagedReviewRow.Delta)}.{nameof(PubTemplateEditor.Core.Editing.TextDelta.PrefixShown)}",
            $"{nameof(StagedReviewRow.Delta)}.{nameof(PubTemplateEditor.Core.Editing.TextDelta.RemovedShown)}",
            $"{nameof(StagedReviewRow.Delta)}.{nameof(PubTemplateEditor.Core.Editing.TextDelta.AddedShown)}",
            $"{nameof(StagedReviewRow.Delta)}.{nameof(PubTemplateEditor.Core.Editing.TextDelta.SuffixShown)}",
        ];

        foreach (string path in rowPaths)
        {
            Assert.True(bound.Contains(path), $"the review does not bind {path}");
            Resolve(typeof(StagedReviewRow), path);
        }

        string[] groupPaths =
        [
            nameof(ReviewFileGroup.Rows),
            nameof(ReviewFileGroup.ChangeCountText),
            nameof(ReviewFileGroup.FilePath),
        ];

        foreach (string path in groupPaths)
        {
            Assert.True(bound.Contains(path), $"the review does not bind {path}");
            Resolve(typeof(ReviewFileGroup), path);
        }

        // Nothing else may dangle: every path in the block belongs to the group or to the row.
        foreach (string path in bound)
        {
            Assert.True(TryResolve(typeof(StagedReviewRow), path) || TryResolve(typeof(ReviewFileGroup), path),
                $"the review binds {path}, which belongs to neither a staged row nor a file group");
        }
    }

    private static void Resolve(Type start, string path)
    {
        Assert.True(TryResolve(start, path), $"{path} does not resolve from {start.Name}");
    }

    private static bool TryResolve(Type start, string path)
    {
        Type current = start;
        foreach (string segment in path.Split('.'))
        {
            PropertyInfo? property = current.GetProperty(segment);
            if (property is null)
            {
                return false;
            }

            current = property.PropertyType;
        }

        return true;
    }

    /// <summary>
    /// The reason the review exists: a reviewer has to be able to read the value. A GridView clipped
    /// these to a column width, so the card layout is checked against a value far longer than the panel.
    /// </summary>
    [Fact]
    public async Task TheReviewShowsAWholeLongValueInsteadOfCuttingItOff()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        string longBaseline = "Sets how many units the player collects per level " + new string('.', 260) + " level";
        string longProposed = "Sets how many units the player collects per town " + new string('.', 260) + " town";
        Write(templates, "one.dta", TemplateXml(Variable("alpha", longBaseline)));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, longProposed);

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            var window = new ShellWindow(vm) { ShowInTaskbar = false };
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(1240, 780));
            root.Arrange(new Rect(new Size(1240, 780)));
            root.UpdateLayout();

            TextBlock[] values = Tree(window.ReviewList).OfType<TextBlock>().ToArray();
            TextBlock? baseline = values.FirstOrDefault(t => t.Text == longBaseline);
            TextBlock? proposed = values.FirstOrDefault(t => t.Text == longProposed);

            Assert.True(baseline is not null, "the full value in the template is not shown in the review");
            Assert.True(proposed is not null, "the full proposed value is not shown in the review");
            Assert.Equal(TextWrapping.Wrap, baseline!.TextWrapping);
            Assert.Equal(TextTrimming.None, baseline.TextTrimming);
            // The middle panel is only part of a 1240 px window; what matters is that the value gets the
            // whole width of its card and wraps, instead of being cut at a fixed column width.
            Assert.True(baseline.ActualWidth > 240, $"value column is only {baseline.ActualWidth} wide");
            Assert.True(baseline.ActualHeight > 40, $"value draws in {baseline.ActualHeight} px, not wrapped");

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
            vm.NavigationGuard = null;
        });
    }

    [Fact]
    public async Task TheReviewMarksTheExactStretchThatChanges()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "Sets how many units the player collects per level")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "Sets how many units the player collects per town");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            var window = new ShellWindow(vm) { ShowInTaskbar = false };
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(1240, 780));
            root.Arrange(new Rect(new Size(1240, 780)));
            root.UpdateLayout();

            TextBlock[] pieces = Tree(window.ReviewList).OfType<TextBlock>().ToArray();
            TextBlock? removed = pieces.FirstOrDefault(t => t.Text == "level" && t.TextDecorations.Count > 0);
            TextBlock? added = pieces.FirstOrDefault(t => t.Text == "town" && t.TextDecorations.Count > 0);

            Assert.True(removed is not null, "the removed stretch is not marked in the review");
            Assert.True(added is not null, "the added stretch is not marked in the review");

            // The unchanged context stays visible too, shortened from the inner edge so the changed
            // stretch is on screen without showing hundreds of characters of context.
            Assert.Contains(pieces, t => t.Text!.StartsWith("…", StringComparison.Ordinal)
                                         && t.Text!.EndsWith("collects per ", StringComparison.Ordinal));

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
            vm.NavigationGuard = null;
        });
    }

    [Fact]
    public async Task TheReviewGroupsStagedChangesOneTemplateFileAtATime()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "OLD_B")));
        Write(templates, "two.dta", TemplateXml(Variable("alpha", "OLD_A2")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW_A");
        Stage(vm, "one.dta", "beta", FieldKind.DefaultValue, "NEW_B");
        Stage(vm, "two.dta", "alpha", FieldKind.DefaultValue, "NEW_A2");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);

            Assert.Equal(3, vm.ReviewRows.Count);
            Assert.Equal(2, vm.ReviewGroups.Count);
            Assert.Equal("one.dta|two.dta", string.Join("|", vm.ReviewGroups.Select(g => g.FileName)));
            Assert.Equal(2, vm.ReviewGroups[0].Rows.Count);
            Assert.Equal("2 changes", vm.ReviewGroups[0].ChangeCountText);
            Assert.Same(vm.ReviewRows.Single(r => r.FileName == "two.dta"), vm.ReviewGroups[1].Rows.Single());

            var window = new ShellWindow(vm) { ShowInTaskbar = false };
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(1240, 780));
            root.Arrange(new Rect(new Size(1240, 780)));
            root.UpdateLayout();

            Assert.Equal(2, window.ReviewList.Items.Count);
            var headers = Tree(window.ReviewList).OfType<TextBlock>()
                .Where(t => t.Text is "one.dta" or "two.dta")
                .Select(t => t.Text)
                .Distinct()
                .ToList();
            Assert.Equal("one.dta|two.dta", string.Join("|", headers));

            // Ticking stays a per-change decision, inside the group it belongs to.
            vm.ReviewRows.Single(r => r.FileName == "two.dta").IsSelected = false;
            Assert.Equal(2, vm.SelectedReviewCount);

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
            vm.NavigationGuard = null;
        });
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject node)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
            yield return child;

            foreach (DependencyObject grand in Tree(child))
            {
                yield return grand;
            }
        }
    }

    /// <summary>
    /// The sentence under the review list explains why saving is or is not offered. An empty list must not
    /// read as "your staged changes already match the file": that is what it claimed while real changes
    /// were listed above it, because the text was evaluated once and never raised again.
    /// </summary>
    [Fact]
    public void AnEmptyReviewNeverClaimsTheStagedValuesAlreadyMatch()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD")));

        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settings), new EditorSettings { LastFolder = templates });
            Assert.Empty(vm.ReviewRows);
            Assert.False(vm.CanSaveReview);
            Assert.Contains("Nothing is staged", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.DoesNotContain("nothing to write", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task TheSaveOfferFollowsTheStagedChangesInsteadOfGoingStale()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        var raised = new List<string>();

        _host.Do(() => vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty));

        _host.Do(() =>
        {
            Assert.Contains("Nothing is staged", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.False(vm.CanSaveReview);
        });

        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW");

        _host.Do(() =>
        {
            Assert.True(vm.CanSaveReview, "a real staged change does not offer saving");
            Assert.DoesNotContain("nothing to write", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.Contains("Only the ticked changes are written", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.True(vm.SaveReviewCommand.CanExecute(null), "the save command stayed disabled");
        });

        _host.Do(() =>
        {
            raised.Clear();
            vm.OpenReviewCommand.Execute(null);

            // The review footer binds plain properties, so opening has to raise them; without this the
            // panel shows whatever the text was when it was first built.
            Assert.Contains(nameof(WorkspaceViewModel.CanSaveReview), raised);
            Assert.Contains(nameof(WorkspaceViewModel.SaveUnavailableReason), raised);
            Assert.DoesNotContain("Nothing is staged", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnTickingEveryChangeTakesTheSaveOfferAwayAndSaysWhy()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        Write(templates, "one.dta", TemplateXml(Variable("alpha", "OLD")));

        WorkspaceViewModel vm = await ReadAllAsync(settings, templates);
        Stage(vm, "one.dta", "alpha", FieldKind.DefaultValue, "NEW");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            Assert.True(vm.CanSaveReview);

            vm.SelectNoneReviewCommand.Execute(null);
            Assert.False(vm.CanSaveReview);
            Assert.False(vm.SaveReviewCommand.CanExecute(null));
            Assert.Contains("Tick at least one", vm.SaveUnavailableReason, StringComparison.Ordinal);

            vm.SelectAllReviewCommand.Execute(null);
            Assert.True(vm.CanSaveReview);
            Assert.True(vm.SaveReviewCommand.CanExecute(null));
            Assert.Contains("Only the ticked changes are written", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });
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
