using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Renders the shell to a bitmap inside the STA host. This proves the layout arranges
/// without exceptions and leaves a PNG that can be inspected by eye; it is a
/// software-rendered snapshot, not a screen capture of an interactive desktop session.
/// </summary>
[Collection("WPF shell")]
public class ShellSnapshotTests
{
    private readonly WpfHost _host;

    public ShellSnapshotTests(WpfHost host) => _host = host;

    internal static string ArtifactsDir()
    {
        string dir = Path.Combine(ThemeTests.RepoRoot(), "artifacts", "screenshots");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Rect WindowRect => new(0, 0, 1240, 780);

    private void Snapshot(string fileName, ThemeChoice theme, Action<WorkspaceViewModel>? prepare = null)
    {
        _host.Do(() =>
        {
            string settingsDir = Path.Combine(Path.GetTempPath(), "pubte-snap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(settingsDir);
            var settings = new EditorSettings
            {
                Theme = theme.ToString(),
                LastFolder = Path.Combine(ThemeTests.RepoRoot(), "examples"),
            };
            var vm = new WorkspaceViewModel(new SettingsService(settingsDir), settings);
            prepare?.Invoke(vm);

            ThemeManager.Apply(_host.App, theme);
            var window = new ShellWindow(vm);
            window.ShowInTaskbar = false;
            window.Measure(WindowRect.Size);
            window.Arrange(WindowRect);
            window.UpdateLayout();

            // An unshown Window has no HWND, so RenderTargetBitmap renders its root
            // visual instead: measure/arrange that element directly and draw it.
            var root = (FrameworkElement)window.Content;

            root.Measure(WindowRect.Size);
            root.Arrange(WindowRect);
            root.UpdateLayout();

            // Structural layout proof: the three panels are arranged side by side and the
            // table/filters get real space. (Visual inspection by eye is a human gate.)
            (double x, double y, double w, double h) Box(FrameworkElement e)
            {
                var p = e.TransformToVisual(root).Transform(new Point(0, 0));
                return (p.X, p.Y, e.ActualWidth, e.ActualHeight);
            }

            var explorer = Box(window.FilterControl);
            var table = Box(window.ParameterTable);
            var search = Box(window.SearchControl);
            Assert.True(explorer.w > 100 && explorer.h > 10, "explorer filter box has no space");
            Assert.True(explorer.x < table.x, "explorer must sit left of the parameter table");
            Assert.True(search.x < table.x + table.w && search.x > table.x, "search box belongs to the table header");
            Assert.True(table.w > 380 && table.h > 200, "parameter table is too small");

            var render = new RenderTargetBitmap(
                (int)WindowRect.Width, (int)WindowRect.Height, 96, 96, PixelFormats.Pbgra32);
            render.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(render));
            using var stream = File.Create(Path.Combine(ArtifactsDir(), fileName));
            encoder.Save(stream);

            // A completely uniform image would mean the theme or layout produced nothing.
            var pixels = new int[(int)WindowRect.Width * (int)WindowRect.Height];
            render.CopyPixels(pixels, 4 * (int)WindowRect.Width, 0);
            Assert.True(pixels.Distinct().Count() > 32, $"snapshot {fileName} looks blank ({pixels.Distinct().Count()} colours)");

            // Rendered text must carry the view model values: a mistyped binding path
            // would leave these empty and fail here instead of silently shipping.
            Assert.Equal(vm.ProductName, window.BrandTitleLabel.Text);
            Assert.Equal(vm.FolderName, window.FolderNameLabel.Text);
            Assert.Equal(vm.TotalFileCount.ToString(), window.FileCountBadge.Text);
            Assert.Equal($"{vm.SelectedReadCount} selected for reading", window.SelectedCountLabel.Text);
            Assert.Equal(vm.ScopeDescription, window.ScopeLabel.Text);
            Assert.NotNull(vm.ActiveFile);
            Assert.Equal(vm.ActiveTemplateName, window.ActiveTemplateLabel.Text);
            Assert.Equal("Parameters not read yet", window.SummaryLabel.Text);
            Assert.Equal($"{vm.TotalFileCount} template(s) in folder", window.FooterCountLabel.Text);
            Assert.Equal("Offline · Review Save writes selected changes", window.FooterOperationLabel.Text);
            Assert.Equal(vm.VersionLabel, Assert.IsType<string>(window.VersionLabel.Content));
            Assert.StartsWith("Version ", vm.VersionLabel);

            window.Close();
            DeleteDir(settingsDir);
        });
    }

    [Fact]
    public void LightShellRendersAndWritesASnapshot() => Snapshot("shell-light.png", ThemeChoice.Light);

    [Fact]
    public void DarkShellRendersAndWritesASnapshot()
    {
        Snapshot("shell-dark.png", ThemeChoice.Dark);
        _host.Do(() => ThemeManager.Apply(_host.App, ThemeChoice.Light));
    }

    /// <summary>Renders the About/licence window so its layout can be reviewed offline as well.</summary>
    [Fact]
    public void AboutWindowRendersAndWritesASnapshot() => _host.Do(() =>
    {
        ThemeManager.Apply(_host.App, ThemeChoice.Light);
        LicensesWindow window = new();
        var rect = new Rect(0, 0, 860, 680);
        var root = (FrameworkElement)window.Content;
        root.Measure(rect.Size);
        root.Arrange(rect);
        root.UpdateLayout();

        var render = new RenderTargetBitmap((int)rect.Width, (int)rect.Height, 96, 96, PixelFormats.Pbgra32);
        render.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var stream = File.Create(ArtifactsDir() + Path.DirectorySeparatorChar + "about-light.png");
        encoder.Save(stream);

        var pixels = new int[(int)rect.Width * (int)rect.Height];
        render.CopyPixels(pixels, 4 * (int)rect.Width, 0);
        Assert.True(pixels.Distinct().Count() > 32, "about snapshot looks blank");
        window.Close();
    });

    /// <summary>
    /// S3 panels: compare, bulk and the unsaved-work prompt are rendered and written as PNGs so a
    /// human can review them, and their layout is asserted so an empty overlay never passes silently.
    /// </summary>
    [Fact]
    public async Task EditingPanelsRenderAndWriteSnapshots()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pubte-snap-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "t1.dta"), EditableTemplate(("alpha", "OLD_A", "external", "A description"), ("beta", "B", "internal", "shared description")));
        File.WriteAllText(Path.Combine(dir, "t2.dta"), EditableTemplate(("alpha", "OTHER", "external", "shared description")));
        File.WriteAllText(Path.Combine(dir, "t3.dta"), EditableTemplate(("alpha", "OLD_A", "internal", "shared description"), ("gamma", "G", "internal", "shared description")));
        File.WriteAllText(Path.Combine(dir, "t4.dta"), EditableTemplate(("alpha", "X", "internal", "first"), ("alpha", "Y", "internal", "second")));

        WorkspaceViewModel[] holder = new WorkspaceViewModel[1];
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            string settings = Path.Combine(Path.GetTempPath(), "pubte-snap-set-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(settings);
            holder[0] = new WorkspaceViewModel(new SettingsService(settings), new EditorSettings { LastFolder = dir });
            read = holder[0].ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        WorkspaceViewModel vm = holder[0];
        Assert.Equal(4, vm.TotalFileCount);

        _host.Do(() =>
        {
            ThemeManager.Apply(_host.App, ThemeChoice.Light);

            ParameterRowItem alpha = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = alpha;
            vm.DetailDefaultValue = "NEW VALUE";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(1, vm.StagedChangeCount);

            var window = new ShellWindow(vm) { ShowInTaskbar = false };

            // The staged state has to be visible in the explorer badge, not only in the view model.
            Assert.Equal(Visibility.Visible, window.ExplorerStagedBadge.Visibility);
            Assert.Contains("1 staged", window.StagedSummaryLabel.Text, StringComparison.Ordinal);
            RenderShell(window, "editing-staged-light.png");

            vm.CompareCommand.Execute(null);
            Assert.True(vm.IsCompareOpen);
            Assert.Equal(4, vm.CompareRows.Count); // self plus three other templates
            // t1's staged value is what the comparison uses, so t2 and t3 both differ on it.
            Assert.Contains("2 different", vm.CompareSummary, StringComparison.Ordinal);
            Assert.Contains("0 without this parameter", vm.CompareSummary, StringComparison.Ordinal);
            Assert.Contains("1 with staged changes", vm.CompareSummary, StringComparison.Ordinal);
            Assert.True(vm.CompareRows.Single(r => r.IsSelf).HasUnsavedDrafts);
            Assert.Equal(CompareState.Ineligible, vm.CompareRows.Single(r => r.FileName == "t4.dta").State);
            RenderShell(window, "editing-compare-light.png");
            vm.CloseCompareCommand.Execute(null);

            vm.OpenBulkCommand.Execute(null);
            Assert.True(vm.IsBulkOpen);
            Assert.Equal(3, vm.BulkEligibleCount); // t4 declares the name twice, so it cannot be written safely
            Assert.Contains("of 4 template(s)", vm.BulkSummary, StringComparison.Ordinal);
            RenderShell(window, "editing-bulk-light.png");
            vm.CloseBulkCommand.Execute(null);

            // Save review: the staged changes of the whole session in one table, each with its own tick.
            vm.OpenReviewCommand.Execute(null);
            Assert.True(vm.IsReviewOpen);
            Assert.Contains("1 of 1 staged change(s) selected", vm.ReviewSummary, StringComparison.Ordinal);
            Assert.Contains("1 template(s)", vm.ReviewSummary, StringComparison.Ordinal);
            Assert.Single(vm.ReviewRows);
            RenderShell(window, "editing-review-light.png");
            vm.CloseReviewCommand.Execute(null);
            Assert.False(vm.IsReviewOpen);

            // Closing with staged work must ask first. The real prompt is modal, so the test seam
            // records the question instead of pumping a dialog on the STA thread (EditingInteractionTests
            // and InteractiveCloseGuardUsesTheRealPromptWindow cover the dialog itself).
            var closeQuestions = new List<NavigationPrompt>();
            vm.NavigationGuard = p =>
            {
                closeQuestions.Add(p);
                return NavigationDecision.DiscardFormsAndStaged;
            };
            window.Close();
            Assert.Single(closeQuestions);
            Assert.Equal(NavigationReason.CloseWorkspace, closeQuestions[0].Reason);
            RenderPrompt(new UnsavedWorkWindow(new NavigationPrompt(NavigationReason.ReReadTemplates, 2, 3)), "prompt-unsaved-light.png");
            DeleteDir(dir);
        });
    }

    /// <summary>Plain-XML template with the six observed properties per declaration.</summary>
    private static string EditableTemplate(params (string Name, string Default, string Access, string Description)[] variables)
    {
        string body = string.Concat(variables.Select(v =>
            "<variable><feature tag=\"definition\">" +
            $"<property name=\"name\" value=\"{v.Name}\"></property>" +
            $"<property name=\"description\"><expression type=\"constant\">{v.Description}</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            $"<property name=\"access\" value=\"{v.Access}\"></property>" +
            $"<property name=\"default\" value=\"{v.Default}\"></property>" +
            "</feature></variable>"));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><template><dataset><variables>" +
               body + "</variables></dataset></template>";
    }

    private void RenderShell(ShellWindow window, string fileName)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(WindowRect.Size);
        root.Arrange(WindowRect);
        root.UpdateLayout();

        var render = new RenderTargetBitmap((int)WindowRect.Width, (int)WindowRect.Height, 96, 96, PixelFormats.Pbgra32);
        render.Render(root);
        Save(render, fileName);

        var pixels = new int[(int)WindowRect.Width * (int)WindowRect.Height];
        render.CopyPixels(pixels, 4 * (int)WindowRect.Width, 0);
        Assert.True(pixels.Distinct().Count() > 32, $"snapshot {fileName} looks blank");
    }

    private static void RenderPrompt(UnsavedWorkWindow prompt, string fileName)
    {
        Assert.Contains("2 field change(s)", prompt.MessageLabel.Text, StringComparison.Ordinal);
        Assert.Equal(Visibility.Visible, prompt.DiscardAllChoice.Visibility);

        var root = (FrameworkElement)prompt.Content;
        var rect = new Rect(0, 0, 480, 260);
        root.Measure(rect.Size);
        root.Arrange(rect);
        root.UpdateLayout();

        var render = new RenderTargetBitmap((int)rect.Width, (int)rect.Height, 96, 96, PixelFormats.Pbgra32);
        render.Render(root);
        Save(render, fileName);

        var pixels = new int[(int)rect.Width * (int)rect.Height];
        render.CopyPixels(pixels, 4 * (int)rect.Width, 0);
        Assert.True(pixels.Distinct().Count() > 32, $"snapshot {fileName} looks blank");

        prompt.Close();
    }

    private static void Save(RenderTargetBitmap render, string fileName)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var stream = File.Create(Path.Combine(ArtifactsDir(), fileName));
        encoder.Save(stream);
    }

    private static void DeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch (IOException) { }
    }
}
