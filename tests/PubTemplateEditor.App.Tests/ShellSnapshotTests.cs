using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
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

    private static void DeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch (IOException) { }
    }
}
