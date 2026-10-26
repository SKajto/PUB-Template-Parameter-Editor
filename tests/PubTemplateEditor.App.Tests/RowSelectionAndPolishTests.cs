using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using PubTemplateEditor.TestSupport;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// S5 polish in the shell: whole-row template selection, resizable parameter columns, the folder controls
/// where the templates are listed, remembered folders, drop-to-open, and the keys that act on staged
/// changes.
/// </summary>
[Collection("WPF shell")]
public class RowSelectionAndPolishTests
{
    private readonly WpfHost _host;

    public RowSelectionAndPolishTests(WpfHost host) => _host = host;

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CopySample(string name, string dir) => TestSamples.CopyNativeOrSynthetic(dir, name);

    private static WorkspaceViewModel NewViewModel(string settingsDir, string templateDir) =>
        new(new SettingsService(settingsDir), new EditorSettings { LastFolder = templateDir });

    private static void Realize(FrameworkElement root, double width = 1240, double height = 780)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(new Size(width, height)));
        root.UpdateLayout();
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

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        DependencyObject? current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    // ------------------------------------------------------------------ row selection

    [Fact]
    public void ChoosingATemplateMarksTheWholeRowInsteadOfAnOptionButton()
    {
        string templates = TempDir("templates");
        CopySample("example.dta", templates);
        CopySample("requirements_specification_yps.dta", templates);

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(TempDir("settings"), templates);
            var window = new ShellWindow(vm);
            Realize((FrameworkElement)window.Content);

            ListBox rows = window.TemplateRows;
            Assert.Equal(2, rows.Items.Count);

            // No option button anywhere in the file list; the theme switcher keeps its own radio group.
            Assert.DoesNotContain(Tree(rows), e => e is RadioButton);

            var item = (TemplateItem)rows.Items[1];
            vm.SelectActiveFile(item);
            rows.UpdateLayout();

            var container = (ListBoxItem)rows.ItemContainerGenerator.ContainerFromItem(item);
            Assert.True(container.IsSelected, "the chosen template row is not selected");
            Assert.Equal(HorizontalAlignment.Stretch, container.HorizontalContentAlignment);

            var chrome = (Border)container.Template.FindName("Chrome", container)!;
            var accentBar = (System.Windows.Shapes.Rectangle)container.Template.FindName("AccentBar", container)!;
            Assert.Equal(Visibility.Visible, accentBar.Visibility);

            // The marking is the complete row, not a label-sized chip around the file name.
            Assert.True(chrome.ActualWidth >= rows.ActualWidth - 26,
                $"only {chrome.ActualWidth} of the {rows.ActualWidth} px row is marked");

            var unchosen = (ListBoxItem)rows.ItemContainerGenerator.ContainerFromItem(rows.Items[0]);
            Assert.False(unchosen.IsSelected);
            Assert.Equal(Visibility.Collapsed, ((System.Windows.Shapes.Rectangle)unchosen.Template.FindName("AccentBar", unchosen)!).Visibility);

            // The check box for reading stays a separate decision on the same row.
            var readBoxes = Tree(rows).OfType<CheckBox>().ToList();
            Assert.Equal(2, readBoxes.Count);
            vm.SelectActiveFile((TemplateItem)rows.Items[0]);
            Assert.Same(vm.AllFiles[0], vm.ActiveFile);

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public async Task ChoosingARowInTheListGoesPastTheUnsavedWorkGuard()
    {
        string templates = TempDir("templates");
        CopySample("example.dta", templates);
        CopySample("requirements_specification_yps.dta", templates);

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(TempDir("settings"), templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            Realize((FrameworkElement)window.Content);

            TemplateItem original = vm.ActiveFile!;
            var other = vm.AllFiles.Single(f => !ReferenceEquals(f, original));

            vm.SelectedParameter = vm.VisibleParameters.Single();
            vm.DetailDefaultValue = "TYPED BUT NOT APPLIED";
            Assert.True(vm.HasUnappliedFormEdits);

            var prompts = new List<NavigationReason>();
            vm.NavigationGuard = prompt =>
            {
                prompts.Add(prompt.Reason);
                return NavigationDecision.KeepEditing;
            };

            ListBox rows = window.TemplateRows;
            rows.SelectedItem = other;
            rows.UpdateLayout();

            Assert.Single(prompts);
            Assert.Equal(NavigationReason.ChangeTemplate, prompts[0]);
            Assert.Same(original, vm.ActiveFile);

            // The list may not keep showing the refused row as selected while the workspace still
            // displays the previous template.
            Assert.Same(original, rows.SelectedItem);
            Assert.Equal("TYPED BUT NOT APPLIED", vm.DetailDefaultValue);

            vm.NavigationGuard = _ => NavigationDecision.DiscardForms;
            rows.SelectedItem = other;
            rows.UpdateLayout();
            Assert.Same(other, vm.ActiveFile);
            Assert.Same(other, rows.SelectedItem);

            vm.NavigationGuard = null;
            window.Close();
        });
    }

    // ---------------------------------------------------------------- column resizing

    [Fact]
    public void ParameterColumnsAreNarrowedAndWidenedByTheUser()
    {
        string templates = TempDir("templates");
        CopySample("example.dta", templates);

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(TempDir("settings"), templates);
            var window = new ShellWindow(vm);
            GridView view = (GridView)window.ParameterTable.View!;

            // Start narrow enough that every header is on screen: a header the user cannot see is a
            // header whose handle cannot be tested, and the point is that each of the four is draggable.
            foreach (GridViewColumn column in view.Columns)
            {
                column.Width = 110;
            }

            Realize((FrameworkElement)window.Content);

            var headers = Tree(window.ParameterTable).OfType<GridViewColumnHeader>()
                .Where(h => h.Column is not null) // the header row adds a filler for the scrollbar gutter
                .ToList();
            Assert.Equal(4, headers.Count);

            foreach (GridViewColumnHeader header in headers)
            {
                Thumb? gripper = Tree(header).OfType<Thumb>().FirstOrDefault(t => t.Name == "PART_HeaderGripper");
                Assert.True(gripper is not null, $"the '{header.Content}' column has no drag handle");
                Assert.True(gripper!.IsHitTestVisible, $"the handle of '{header.Content}' cannot be grabbed");
                Assert.True(gripper.ActualWidth >= 8,
                    $"the handle of '{header.Content}' is only {gripper.ActualWidth} px wide, too thin to hit");

                // WPF manages the device cursor on the realized element, so the resize cursor is checked
                // where it is declared: in the style applied to that very handle.
                Style? style = gripper.Style;
                Assert.NotNull(style);
                Assert.Contains(style!.Setters.Cast<Setter>(),
                    s => s.Property == FrameworkElement.CursorProperty && Equals(s.Value, Cursors.SizeWE));
            }

            // Dragging a handle changes the column; a fixed layout would ignore it.
            GridViewColumn name = view.Columns[0];
            GridViewColumnHeader nameHeader = headers.Single(h => h.Column == name);
            double before = nameHeader.ActualWidth;
            name.Width = 260;
            ((FrameworkElement)window.Content).UpdateLayout();

            Assert.True(nameHeader.ActualWidth > before + 100,
                $"the column stayed at {before} -> {nameHeader.ActualWidth} after the width changed");

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public void WidthsTheUserDragsAreRestoredInTheNextWindow()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        CopySample("example.dta", templates);
        double[] dragged = [431, 77, 305, 199];

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(settings, templates);
            var window = new ShellWindow(vm);
            Realize((FrameworkElement)window.Content);

            GridView view = (GridView)window.ParameterTable.View!;
            for (int i = 0; i < view.Columns.Count; i++)
            {
                Assert.NotEqual(dragged[i], view.Columns[i].Width); // starts from the layout defaults
                view.Columns[i].Width = dragged[i];
            }

            window.Close();
        });

        EditorSettings stored = new SettingsService(settings).Load();
        Assert.Equal("431|77|305|199", string.Join("|", stored.ParameterColumnWidths));

        _host.Do(() =>
        {
            WorkspaceViewModel vm = new(new SettingsService(settings), new SettingsService(settings).Load());
            var window = new ShellWindow(vm);
            GridView view = (GridView)window.ParameterTable.View!;
            Assert.Equal("431|77|305|199", string.Join("|", view.Columns.Select(c => c.Width)));

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public void AnUnusableStoredColumnWidthSetIsIgnored()
    {
        string settings = TempDir("settings");
        string templates = TempDir("templates");
        CopySample("example.dta", templates);

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(settings, templates);
            vm.SaveParameterColumnWidths([100, double.NaN, 100, 100]);
            vm.SaveParameterColumnWidths([3]);
            var window = new ShellWindow(vm);
            GridView view = (GridView)window.ParameterTable.View!;
            Assert.Equal(4, view.Columns.Count);
            Assert.All(view.Columns, c => Assert.True(c.Width >= 40, "a broken width list reached the columns"));

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });

        // Rejected sets never reach the file; only the widths the shell actually shows can be stored.
        IReadOnlyList<double> stored = new SettingsService(settings).Load().ParameterColumnWidths;
        Assert.All(stored, w => Assert.True(!double.IsNaN(w) && !double.IsInfinity(w) && w >= 40 && w <= 8000));
    }

    // -------------------------------------------------------------------- folder entry

    [Fact]
    public void TheFolderIsNamedOnceAndTheChooserSitsWithTheTemplatesItLists()
    {
        string xaml = File.ReadAllText(Path.Combine(ThemeTests.RepoRoot(), "src", "PubTemplateEditor.App", "MainWindow.xaml"));
        int named = xaml.Split("{Binding FolderName}").Length - 1;
        Assert.Equal(1, named);

        string templates = TempDir("templates");
        CopySample("example.dta", templates);

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(TempDir("settings"), templates);
            var window = new ShellWindow(vm);
            Realize((FrameworkElement)window.Content);

            Assert.True(IsDescendantOf(window.FolderNameLabel, window.TemplatesPanel),
                "the folder name is not in the template panel");
            Assert.True(IsDescendantOf(window.ChooseFolderActionButton, window.TemplatesPanel),
                "Choose folder is not in the template panel");
            Assert.DoesNotContain(Tree(window), e => e is TextBlock t && t.Text == vm.FolderName
                                                     && !IsDescendantOf(t, window.TemplatesPanel));

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public void OpenedFoldersAreRememberedAndReopenedFromTheList()
    {
        string settings = TempDir("settings");
        string first = TempDir("first");
        string second = TempDir("second");
        CopySample("example.dta", first);
        CopySample("example.dta", second);

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(settings, first);
            Assert.Equal(first, vm.RecentFolders.Single());

            Assert.True(vm.TryOpenDroppedPath(second));
            Assert.Equal($"{second}|{first}", string.Join("|", vm.RecentFolders));

            // Re-opening a folder moves it to the front instead of duplicating it.
            Assert.True(vm.TryOpenDroppedPath(first));
            Assert.Equal($"{first}|{second}", string.Join("|", vm.RecentFolders));

            vm.OpenRecentFolderCommand.Execute(second);
            Assert.Equal(second, vm.FolderPath);
            Assert.Equal($"{second}|{first}", string.Join("|", vm.RecentFolders));

            // The same folder list is handed to the next session.
            EditorSettings stored = new SettingsService(settings).Load();
            Assert.Equal($"{second}|{first}", string.Join("|", stored.RecentFolders));

            WorkspaceViewModel reopened = new(new SettingsService(settings), stored);
            Assert.Equal($"{second}|{first}", string.Join("|", reopened.RecentFolders));
            Assert.True(reopened.HasRecentFolders);
        });
    }

    [Fact]
    public void TheRecentListStaysShortAndKeepsAFolderThatDisappeared()
    {
        string settings = TempDir("settings");
        var folders = new List<string>();
        for (int i = 0; i < WorkspaceViewModel.RecentFolderLimit + 3; i++)
        {
            string dir = TempDir($"folder{i}");
            CopySample("example.dta", dir);
            folders.Add(dir);
        }

        _host.Do(() =>
        {
            WorkspaceViewModel vm = NewViewModel(settings, folders[0]);
            foreach (string folder in folders.Skip(1))
            {
                Assert.True(vm.TryOpenDroppedPath(folder));
            }

            Assert.Equal(WorkspaceViewModel.RecentFolderLimit, vm.RecentFolders.Count);
            Assert.Equal(folders[^1], vm.RecentFolders[0]);
            Assert.DoesNotContain(folders[0], vm.RecentFolders);

            string gone = folders[^2]; // still remembered; only the oldest entries fall off the list
            Directory.Delete(gone, recursive: true);
            vm.OpenRecentFolderCommand.Execute(gone);

            Assert.Contains("could not be opened", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(gone, vm.RecentFolders);
        });
    }

    [Fact]
    public void ADroppedTemplateOpensItsFolderAndAnythingElseIsRefused()
    {
        string templates = TempDir("templates");
        string sample = CopySample("example.dta", templates);
        string notes = Path.Combine(templates, "notes.txt");
        File.WriteAllText(notes, "not a template");

        _host.Do(() =>
        {
            WorkspaceViewModel vm = new(new SettingsService(TempDir("settings")), new EditorSettings());
            Assert.Null(vm.FolderPath);

            // A path that does not exist is refused instead of guessed at.
            Assert.False(vm.TryOpenDroppedPath(Path.Combine(templates, "does-not-exist.dta")));
            Assert.Null(vm.FolderPath);
            Assert.Contains(".dta", vm.StatusMessage, StringComparison.Ordinal);

            // A real template file opens the folder that holds it; the workspace is folder-scoped.
            Assert.True(vm.TryOpenDroppedPath(sample));
            Assert.Equal(templates, vm.FolderPath);
            Assert.Contains("1 template file", vm.StatusMessage, StringComparison.Ordinal);

            string other = TempDir("other");
            Assert.True(vm.TryOpenDroppedPath(other));
            Assert.Equal(other, vm.FolderPath);

            Assert.False(vm.TryOpenDroppedPath(notes));
            Assert.Equal(other, vm.FolderPath);
            Assert.Contains(".dta", vm.StatusMessage, StringComparison.Ordinal);

            Assert.False(vm.TryOpenDroppedPath(null));
            Assert.False(vm.TryOpenDroppedPath("   "));

            var window = new ShellWindow(vm);
            Assert.True(window.AllowDrop);
            Assert.Equal(Visibility.Collapsed, window.DropHintPanel.Visibility);

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    // ------------------------------------------------------------------ staged keys

    [Fact]
    public async Task UndoTakesBackTheLastStagedChangeAndLeavesTheEarlierOne()
    {
        string templates = TempDir("templates");
        string sample = CopySample("example.dta", templates);
        string before = File.ReadAllText(sample);

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(TempDir("settings"), templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.Single();
            vm.DetailDefaultValue = "STAGED ONE";
            vm.ApplyEditsCommand.Execute(null);
            vm.DetailDescription = "STAGED TWO";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(2, vm.StagedChangeCount);

            vm.OpenReviewCommand.Execute(null);
            Assert.Equal(2, vm.ReviewRows.Count);

            Assert.True(vm.UndoLastStagedCommand.CanExecute(null));
            vm.UndoLastStagedCommand.Execute(null);

            Assert.Equal(1, vm.StagedChangeCount);
            StagedReviewRow left = vm.ReviewRows.Single();
            Assert.Equal("default value", left.FieldLabel);
            Assert.Equal("STAGED ONE", left.ProposedText);
            Assert.Contains("description", vm.ActionNotice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nothing was written", vm.ActionNotice, StringComparison.OrdinalIgnoreCase);

            vm.UndoLastStagedCommand.Execute(null);
            Assert.False(vm.HasStagedChanges);
            Assert.False(vm.UndoLastStagedCommand.CanExecute(null));
            Assert.Equal(before, File.ReadAllText(sample));
        });
    }

    [Fact]
    public async Task EscapeClosesThePanelThatIsOnTopOneStepAtATime()
    {
        string templates = TempDir("templates");
        CopySample("example.dta", templates);

        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(TempDir("settings"), templates);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            Assert.False(vm.CloseOverlayCommand.CanExecute(null));

            vm.SelectedParameter = vm.VisibleParameters.Single();
            vm.CompareCommand.Execute(null);
            Assert.True(vm.IsCompareOpen);
            vm.CloseOverlayCommand.Execute(null);
            Assert.False(vm.IsCompareOpen);

            vm.OpenBulkCommand.Execute(null);
            vm.OpenReviewCommand.Execute(null);
            Assert.True(vm.IsReviewOpen && vm.IsBulkOpen);

            vm.CloseOverlayCommand.Execute(null);
            Assert.False(vm.IsReviewOpen);
            Assert.True(vm.IsBulkOpen);

            vm.CloseOverlayCommand.Execute(null);
            Assert.False(vm.IsBulkOpen);
            Assert.False(vm.CloseOverlayCommand.CanExecute(null));
        });
    }

    [Fact]
    public void TheShortcutsTheShellAdvertisesAreTheOnesItWires()
    {
        string xaml = File.ReadAllText(Path.Combine(ThemeTests.RepoRoot(), "src", "PubTemplateEditor.App", "MainWindow.xaml"));
        int start = xaml.IndexOf("<Window.InputBindings>", StringComparison.Ordinal);
        string block = xaml[start..xaml.IndexOf("</Window.InputBindings>", start, StringComparison.Ordinal)];

        var bindings = System.Text.RegularExpressions.Regex.Matches(block, @"<KeyBinding\s+([^>]*?)/>")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .Select(attrs =>
            {
                string Modifier(string name) =>
                    System.Text.RegularExpressions.Regex.Match(attrs, $"Modifiers=\"([^\"]+)\"") is { Success: true } mm ? mm.Groups[1].Value : "";
                string Key() => System.Text.RegularExpressions.Regex.Match(attrs, "Key=\"([^\"]+)\"").Groups[1].Value;
                string Command() => System.Text.RegularExpressions.Regex.Match(attrs, "Command=\"\\{Binding ([A-Za-z0-9_]+)").Groups[1].Value;
                return (Key: Key(), Modifier: Modifier("Modifiers"), Command: Command());
            })
            .ToList();

        var expected = new (string Key, string Modifier, string Command)[]
        {
            ("O", "Control", "OpenFolderCommand"),
            ("F5", "", "ReadParametersCommand"),
            ("S", "Control", "OpenReviewCommand"),
            ("Enter", "Control", "ApplyEditsCommand"),
            ("Z", "Control", "UndoLastStagedCommand"),
            ("Escape", "", "CloseOverlayCommand"),
        };

        foreach ((string key, string modifier, string command) in expected)
        {
            Assert.Contains((key, modifier, command), bindings);
            Assert.NotNull(typeof(WorkspaceViewModel).GetProperty(command));
        }

        // The footer hint is only honest if it names keys the window actually binds.
        foreach (string hint in new[] { "Ctrl+Enter", "Ctrl+Z", "Ctrl+S", "Esc" })
        {
            Assert.Contains(hint, xaml, StringComparison.Ordinal);
            Assert.Contains(bindings, b => hint switch
            {
                "Ctrl+Enter" => b.Key == "Enter" && b.Modifier == "Control",
                "Ctrl+Z" => b.Key == "Z" && b.Modifier == "Control",
                "Ctrl+S" => b.Key == "S" && b.Modifier == "Control",
                _ => b.Key == "Escape",
            });
        }
    }
}
