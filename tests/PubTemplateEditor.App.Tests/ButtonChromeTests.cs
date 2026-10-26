using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;
using ShapePath = System.Windows.Shapes.Path;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// The button layer of the shell: every command button carries a glyph beside its label, the parameter
/// tools hang on the list they act on, and the save review has exactly one entry point for the whole
/// session instead of a per-parameter copy that suggests it only covers the selected row.
/// </summary>
[Collection("WPF shell")]
public class ButtonChromeTests : IDisposable
{
    private readonly WpfHost _host;
    private readonly List<string> _tempDirs = [];

    public ButtonChromeTests(WpfHost host) => _host = host;

    private string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-chrome-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private ShellWindow OpenShell()
    {
        string settings = TempDir("settings");
        string templates = Path.Combine(TempDir("templates"), "t1.dta");
        File.WriteAllText(templates,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<template><dataset><variables>" +
            "<variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"alpha\"></property>" +
            "<property name=\"description\"><expression type=\"constant\">d</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            "<property name=\"access\" value=\"internal\"></property>" +
            "<property name=\"default\" value=\"old\"></property>" +
            "</feature></variable></variables></dataset></template>");

        ShellWindow window = null!;
        _host.Do(() =>
        {
            var vm = new WorkspaceViewModel(new SettingsService(settings), new EditorSettings { LastFolder = Path.GetDirectoryName(templates)! });
            window = new ShellWindow(vm);
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(1240, 780));
            root.Arrange(new Rect(new Size(1240, 780)));
            root.UpdateLayout();
        });

        return window;
    }

    public void Dispose()
    {
        foreach (string dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of throwaway folders.
            }
        }
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;

            foreach (DependencyObject grand in Tree(child))
            {
                yield return grand;
            }
        }
    }

    private static bool IsInside(DependencyObject node, DependencyObject ancestor)
    {
        for (DependencyObject? current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void EveryCommandButtonShowsAGlyphBesideItsLabel()
    {
        ShellWindow window = OpenShell();

        _host.Do(() =>
        {
            (string what, Button button)[] commands =
            [
                ("choose folder", window.ChooseFolderActionButton),
                ("read parameters", window.ReadParametersActionButton),
                ("review and save", window.ReviewOpenButton),
                ("discard all", window.DiscardAllActionButton),
                ("compare", window.CompareActionButton),
                ("apply value to templates", window.BulkActionButton),
                ("apply edits", window.ApplyEditsActionButton),
                ("undo unapplied typing", window.RevertFormActionButton),
                ("discard staged of parameter", window.DiscardDraftsActionButton),
                ("save to templates", window.ReviewSaveActionButton),
                ("confirm bulk value", window.BulkConfirmActionButton),
            ];

            foreach ((string what, Button button) in commands)
            {
                Geometry? icon = Iconics.GetIcon(button);
                Assert.True(icon is not null, $"{what} has no glyph next to its label");

                button.ApplyTemplate();
                // Parts of the shell are collapsed until a parameter is selected, so the button is laid out
                // on its own before its glyph box is measured.
                button.Measure(new Size(240, 40));
                button.Arrange(new Rect(new Size(240, 40)));
                button.UpdateLayout();
                Viewbox? host = Tree(button).OfType<Viewbox>().FirstOrDefault();
                ShapePath? path = Tree(button).OfType<ShapePath>().FirstOrDefault();

                Assert.True(host is not null, $"{what} renders no icon host");
                Assert.Equal(Visibility.Visible, host!.Visibility);
                Assert.Same(icon, path?.Data);
                Assert.True(button.Content is string label && !string.IsNullOrWhiteSpace(label),
                    $"{what} must keep a real text label, not only a glyph");

                // A collapsed or zero-sized host would pass the checks above while drawing nothing, so the
                // glyph is also measured: it has to occupy a legible box next to the label.
                // The glyph keeps the aspect ratio of its 24x24 drawing box, so a wide icon measures short;
                // what matters is that it draws at a legible size instead of collapsing.
                Assert.True(host.ActualWidth >= 12 && host.ActualHeight >= 8,
                    $"{what} icon host is {host.ActualWidth}x{host.ActualHeight}");
                Assert.True(path!.Data.Bounds.Width > 1 && path.Data.Bounds.Height > 1,
                    $"{what} glyph has an empty geometry");
            }
        });
    }

    [Fact]
    public void TheParameterToolsHangOnTheParameterListAndNotOnTheDetailsForm()
    {
        ShellWindow window = OpenShell();

        _host.Do(() =>
        {
            Assert.True(IsInside(window.CompareActionButton, window.ParameterActionStrip),
                "compare acts on the selected row, so it belongs above the parameter table");
            Assert.True(IsInside(window.BulkActionButton, window.ParameterActionStrip));
            Assert.False(IsInside(window.CompareActionButton, window.DetailActionsPanel));
            Assert.False(IsInside(window.BulkActionButton, window.DetailActionsPanel));

            // The form actions stay with the form they apply.
            Assert.True(IsInside(window.ApplyEditsActionButton, window.DetailActionsPanel));
        });
    }

    [Fact]
    public void TheSaveReviewHasASingleSessionWideEntryPoint()
    {
        ShellWindow window = OpenShell();

        _host.Do(() =>
        {
            Button[] allButtons = Tree((FrameworkElement)window.Content).OfType<Button>().ToArray();
            Button[] openers = allButtons
                .Where(b => ReferenceEquals(b.Command, window.Workspace.OpenReviewCommand))
                .ToArray();

            // Guard the walk itself: an empty tree would make this test pass without looking at anything.
            Assert.True(allButtons.Length >= 8, $"only {allButtons.Length} buttons realized");
            Assert.Single(openers);
            Assert.Same(window.ReviewOpenButton, openers[0]);

            // The single entry says what it covers, and its key gesture is written down next to it.
            Assert.Equal("Review and save", window.ReviewOpenButton.Content);
            Assert.Contains("Ctrl+S", (string)window.ReviewOpenButton.ToolTip);
        });
    }
}
