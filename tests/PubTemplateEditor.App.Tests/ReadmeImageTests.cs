using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using PubTemplateEditor.TestSupport;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// The pictures the README shows.
///
/// **Nothing real is ever drawn.** The native PUB templates are user content: they are not in this
/// repository (docs/SAMPLE_FINDINGS.md records what was learned from them), and their parameter names
/// and values are somebody's business data. A screenshot taken over one would publish exactly what the
/// sample policy keeps in. So these images come from synthetic demo files that this test writes into a
/// private temporary folder: same container shape (a ZIP with template.xml plus unrelated entries), same
/// reading code path, invented content - and the folder is named "PUB demo templates", so the image
/// itself says what it is.
///
/// Generation is deliberate: set PUBTE_CAPTURE_README_IMAGES=1 and run this class. A normal test run
/// never rewrites a tracked picture; it only checks that the pictures the README points at exist and
/// are real, decodable, non-blank images.
/// </summary>
[Collection("WPF shell")]
public class ReadmeImageTests
{
    private readonly WpfHost _host;
    private readonly ITestOutputHelper _out;

    public ReadmeImageTests(WpfHost host, ITestOutputHelper output)
    {
        _host = host;
        _out = output;
    }

    private static Rect WindowRect => new(0, 0, 1240, 780);

    private static string ImagesDir => Path.Combine(TestSamples.RepoRoot(), "docs", "images");

    private static bool CaptureRequested =>
        Environment.GetEnvironmentVariable("PUBTE_CAPTURE_README_IMAGES") == "1";

    // ---------------------------------------------------------------- generated demo content

    private record Field(string Name, string Default, string Access, string Description);

    private static readonly (string File, Field[] Fields)[] DemoTemplates =
    [
        ("welcome_pack.dta",
        [
            new("customer_first_name", "Mr", "external", "First name on the account"),
            new("customer_last_name", "Example", "external", "Family name on the account"),
            new("welcome_message", "Welcome aboard, %customer_first_name%!", "external", "Greeting printed on the card"),
            new("activation_code", "000-000", "internal", "Code used to activate the account"),
            new("sender_name", "Example Company", "external", "Who the letter is from"),
            new("support_phone", "+00 000 0000", "internal", "Support number shown in the footer"),
            new("valid_from", "01.01.2026", "external", "First day the card can be used"),
        ]),
        ("campaign_email.dta",
        [
            new("offer_percent", "15", "internal", "Discount granted by this campaign"),
            new("coupon_code", "WELCOME15", "external", "Code to enter at checkout"),
            new("expiry_date", "31.12.2026", "external", "Last day the offer is valid"),
            new("product_name", "Starter Pack", "external", "Product the campaign promotes"),
            new("unsubscribe_url", "https://example.invalid/unsubscribe", "internal", "Link in the footer"),
        ]),
        ("loyalty_card.dta",
        [
            new("loyalty_tier", "Silver", "external", "Current membership level"),
            new("points_balance", "0", "internal", "Points available for redemption"),
            new("card_number", "0000 0000 0000 0000", "external", "Number printed on the card"),
            new("valid_until", "31.12.2027", "external", "Expiry date printed on the card"),
            new("reward_text", "Free delivery on orders over 00.00", "external", "Reward line of the month"),
        ]),
        ("service_reminder.dta",
        [
            new("appointment_date", "01.01.2026", "external", "Date of the scheduled visit"),
            new("technician_name", "A. Serviceperson", "external", "Who is coming"),
            new("service_interval_days", "365", "internal", "Days between two visits"),
            new("reminder_text", "Your appointment is on %appointment_date%", "external", "Body of the reminder"),
        ]),
    ];

    /// <summary>
    /// A private folder whose **leaf name is the label the picture shows**, which is why the GUID is the
    /// parent and "PUB demo templates" is the folder the workspace opens: the explorer header must not
    /// look like a debug path, and a viewer must be able to tell from the image alone that the content
    /// is invented. A per-run parent also means nothing can be inherited from an older run.
    /// </summary>
    private static string DemoFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "pubte-readme-demo-" + Guid.NewGuid().ToString("N"),
            "PUB demo templates");
        foreach ((string file, Field[] fields) in DemoTemplates)
        {
            TestSamples.WriteSyntheticContainer(root, file, TemplateXml(fields));
        }

        Assert.Equal(DemoTemplates.Select(t => t.File).OrderBy(n => n, StringComparer.Ordinal),
            Directory.GetFiles(root).Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal));

        return root;
    }

    private static string TemplateXml(IEnumerable<Field> fields)
    {
        string body = string.Concat(fields.Select(f =>
            "<variable><feature tag=\"definition\">" +
            $"<property name=\"name\" value=\"{f.Name}\"></property>" +
            $"<property name=\"description\"><expression type=\"constant\">{f.Description}</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            $"<property name=\"access\" value=\"{f.Access}\"></property>" +
            $"<property name=\"default\" value=\"{f.Default}\"></property>" +
            "<property name=\"central variable\" value=\"\"></property>" +
            "</feature></variable>"));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><template><dataset><variables>"
             + body + "</variables></dataset></template>";
    }

    // ------------------------------------------------------------------------ generation

    [Fact]
    public async Task GenerateReadmeImagesWhenTheyAreAskedFor()
    {
        if (!CaptureRequested)
        {
            // Rewriting a tracked picture must never happen as a side effect of `dotnet test`.
            // ReadmeImagesTheReadmePointsAtAreRealPictures is the part that always runs.
            return;
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Directory.CreateDirectory(ImagesDir);
        string templates = DemoFolder();
        string settings = Path.Combine(Path.GetTempPath(), "pubte-readme-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settings);

        // A screenshot session must not write anything - not even to its own throwaway copies.
        var before = Directory.GetFiles(templates).ToDictionary(p => Path.GetFileName(p), Sha256);

        WorkspaceViewModel vm = null!;
        _host.Do(() => vm = new WorkspaceViewModel(
            new SettingsService(settings),
            new EditorSettings { Theme = nameof(ThemeChoice.Dark), LastFolder = templates }));

        Task read = _host.Do(() => vm.ReadParametersCommand.ExecuteAsync(null));
        await read;
        _out.WriteLine($"parameters read   {watch.ElapsedMilliseconds} ms");

        _host.Do(() =>
        {
            ThemeManager.Apply(_host.App, ThemeChoice.Dark);
            try
            {
                var window = new ShellWindow(vm) { ShowInTaskbar = false };

                // The list is sorted by name, so the template the picture focuses on is selected rather
                // than assumed.
                vm.SelectActiveFile(vm.AllFiles.First(f => f.FileName == "welcome_pack.dta"));

                // Bindings, not guesses: a mistyped path would leave panels empty and fail here.
                Assert.Equal("PUB demo templates", vm.FolderName);
                Assert.Equal(DemoTemplates.Length, vm.TotalFileCount);
                Assert.Equal(DemoTemplates.Length, vm.SelectedReadCount);
                Assert.Equal(DemoTemplates.Length.ToString(), window.FileCountBadge.Text);
                Assert.Equal($"{DemoTemplates.Length} selected for reading", window.SelectedCountLabel.Text);
                Assert.Equal("welcome_pack.dta", vm.ActiveTemplateName);
                Assert.Contains("parameters", vm.ActiveParameterSummary, StringComparison.Ordinal);
                Assert.Equal("welcome_pack.dta", window.ActiveTemplateLabel.Text);
                Assert.Equal(7, window.ParameterTable.Items.Count);
                Assert.Contains("welcome_message", Rows(window).Select(r => r.Name));
                Assert.Contains("activation_code", Rows(window).Select(r => r.Name));

                vm.SelectedParameter = vm.VisibleParameters.First(r => r.Name == "welcome_message");
                Assert.Equal("welcome_message", window.DetailNameLabel.Text);
                Assert.Equal("Welcome aboard, %customer_first_name%!", window.DetailValueEditor.Text);

                var hero = Capture(window, "app-dark.png");
                AssertRenderedDarkAndFilled(hero, window, "the parameter table of the hero image");
                _out.WriteLine($"hero captured     {watch.ElapsedMilliseconds} ms");

                // The review step: three staged field changes over two templates, nothing written.
                Stage(vm, "welcome_pack.dta", "welcome_message", "Welcome, %customer_first_name% - your card is ready.");
                Stage(vm, "welcome_pack.dta", "support_phone", "+00 000 000 000");
                Stage(vm, "campaign_email.dta", "offer_percent", "20");
                Assert.Equal(3, vm.StagedChangeCount);

                vm.OpenReviewCommand.Execute(null);
                Assert.True(vm.IsReviewOpen);
                Assert.Equal(3, vm.ReviewRows.Count);
                Assert.Contains("3 of 3", vm.ReviewSummary, StringComparison.Ordinal);
                Assert.Contains("2 template", vm.ReviewSummary, StringComparison.Ordinal);
                Assert.Contains("welcome_message", vm.ReviewRows.Select(r => r.ParameterName));
                Assert.Contains("offer_percent", vm.ReviewRows.Select(r => r.ParameterName));
                Assert.True(window.ReviewList.Items.Count > 0, "the review panel rendered no group");

                var review = Capture(window, "review-dark.png");
                AssertRenderedDarkAndFilled(review, window, "the review panel of the review image");
                _out.WriteLine($"review captured   {watch.ElapsedMilliseconds} ms");
                vm.CloseReviewCommand.Execute(null);

                // Closing with staged work asks first, and the real prompt is modal. The test seam
                // records the question instead of pumping a dialog nobody presses - without this the
                // capture sat waiting for a button for two minutes, which is what it once did.
                var closeQuestions = new List<NavigationPrompt>();
                vm.NavigationGuard = prompt =>
                {
                    closeQuestions.Add(prompt);
                    return NavigationDecision.DiscardFormsAndStaged;
                };

                window.Close();
                Assert.Single(closeQuestions);
                Assert.Equal(NavigationReason.CloseWorkspace, closeQuestions[0].Reason);
            }
            finally
            {
                ThemeManager.Apply(_host.App, ThemeChoice.Light);
            }
        });

        foreach (string file in Directory.GetFiles(templates))
        {
            Assert.Equal(before[Path.GetFileName(file)], Sha256(file));
        }

        Directory.Delete(Path.GetDirectoryName(templates)!, recursive: true);
        Directory.Delete(settings, recursive: true);
        _out.WriteLine($"cleanup done      {watch.ElapsedMilliseconds} ms");
    }

    private static IEnumerable<ParameterRowItem> Rows(ShellWindow window) =>
        window.ParameterTable.Items.Cast<ParameterRowItem>();

    private static void Stage(WorkspaceViewModel vm, string fileName, string parameter, string value)
    {
        if (!string.Equals(vm.ActiveFile?.FileName, fileName, StringComparison.Ordinal))
        {
            vm.SelectActiveFile(vm.AllFiles.First(f => f.FileName == fileName));
        }

        vm.SelectedParameter = vm.VisibleParameters.First(r => r.Name == parameter);
        vm.DetailDefaultValue = value;
        vm.ApplyEditsCommand.Execute(null);
    }

    private static RenderTargetBitmap Capture(Window window, string fileName)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(WindowRect.Size);
        root.Arrange(WindowRect);
        root.UpdateLayout();

        var render = new RenderTargetBitmap(
            (int)WindowRect.Width, (int)WindowRect.Height, 96, 96, PixelFormats.Pbgra32);
        render.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        string path = Path.Combine(ImagesDir, fileName);
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        Assert.True(new FileInfo(path).Length > 20_000,
            $"{fileName} is suspiciously small for a rendered workspace");
        return render;
    }

    /// <summary>
    /// A stand-in for looking at the picture: the dark theme must really be applied (dark averages), the
    /// panels must have real size, and rendered text must show up as light pixels. It cannot judge
    /// beauty - that stays a human gate - but a blank, unthemed or collapsed panel fails here.
    /// </summary>
    private void AssertRenderedDarkAndFilled(RenderTargetBitmap render, ShellWindow window, string what)
    {
        var root = (FrameworkElement)window.Content;
        int width = render.PixelWidth, height = render.PixelHeight;
        var pixels = new int[width * height];
        render.CopyPixels(pixels, 4 * width, 0);

        // The whole window: dark, with the variety a themed workspace with text produces.
        Assert.True(pixels.Select(p => p & 0xFFFFFF).Distinct().Count() > 250,
            $"{what}: only a few colours, the theme or the layout produced nothing");

        FrameworkElement subject = window.ParameterTable;
        string subjectName = "parameter table";
        if (what.Contains("review"))
        {
            subject = window.ReviewList;
            subjectName = "review list";
        }

        Point origin = subject.TransformToVisual(root).Transform(new Point(0, 0));
        var stats = RegionStats(pixels, width, height,
            origin.X, origin.Y, subject.ActualWidth, subject.ActualHeight);
        _out.WriteLine($"{what}: {subjectName} at ({origin.X:F0},{origin.Y:F0}) {subject.ActualWidth:F0}x{subject.ActualHeight:F0}"
            + $" -> mean {stats.MeanLuminance:F0}, max {stats.MaxLuminance}, light {stats.LightPixels}, colours {stats.Colours}");

        Assert.True(stats.Pixels > 30_000,
            $"{what}: the {subjectName} got only {stats.Pixels} pixels of space - a collapsed panel is not a screenshot");
        Assert.True(stats.MeanLuminance < 90,
            $"{what}: the {subjectName} averages luminance {stats.MeanLuminance:F0}, which is not the dark theme");
        Assert.True(stats.MaxLuminance > 150,
            $"{what}: nothing light inside the {subjectName} - text would be invisible");
        Assert.True(stats.LightPixels > 200,
            $"{what}: only {stats.LightPixels} light pixels in the {subjectName}, too few for rendered rows of text");
        Assert.True(stats.Colours > 40,
            $"{what}: the {subjectName} has only {stats.Colours} colours");
    }

    private static (int Pixels, double MeanLuminance, int MaxLuminance, int LightPixels, int Colours) RegionStats(
        int[] pixels, int width, int height, double x, double y, double w, double h)
    {
        int x0 = Math.Max(0, (int)Math.Round(x));
        int y0 = Math.Max(0, (int)Math.Round(y));
        int x1 = Math.Min(width, (int)Math.Round(x + w));
        int y1 = Math.Min(height, (int)Math.Round(y + h));

        var colours = new HashSet<int>();
        long sum = 0;
        int max = 0, light = 0, count = 0;

        for (int row = y0; row < y1; row++)
        {
            for (int col = x0; col < x1; col++)
            {
                int argb = pixels[row * width + col];
                int a = (argb >> 24) & 0xFF;
                Assert.True(a > 240, "a rendered panel pixel is transparent");

                int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
                colours.Add(argb & 0xFFFFFF);
                int luminance = (r * 299 + g * 587 + b * 114) / 1000;
                sum += luminance;
                count++;
                if (luminance > max) max = luminance;
                if (luminance > 140) light++;
            }
        }

        return (count, count == 0 ? 0 : (double)sum / count, max, light, colours.Count);
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // -------------------------------------------------------------------------- the standing check

    [Fact]
    public void ReadmeImagesTheReadmePointsAtAreRealPictures()
    {
        _host.Do(() =>
        {
            string root = TestSamples.RepoRoot();
            string readme = File.ReadAllText(Path.Combine(root, "README.md"));

            var referenced = Regex.Matches(readme, @"docs/images/[A-Za-z0-9._\-/]+\.png")
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.NotEmpty(referenced); // a README that quietly lost its picture is not acceptable

            foreach (string relative in referenced)
            {
                string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(path), $"README shows {relative}, which is not in the repository");

                var frame = BitmapFrame.Create(new Uri(path, UriKind.Absolute),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.True(frame.PixelWidth >= 1000 && frame.PixelHeight >= 500,
                    $"{relative} is {frame.PixelWidth}x{frame.PixelHeight}, too small to be the workspace");

                var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                var pixels = new int[converted.PixelWidth * converted.PixelHeight];
                converted.CopyPixels(pixels, 4 * converted.PixelWidth, 0);

                int colours = pixels.Select(p => p & 0xFFFFFF).Distinct().Count();
                Assert.True(colours > 200,
                    $"{relative} has only {colours} colours - it looks blank, flattened or cropped wrong");

                int dark = pixels.Count(p => Luminance(p) < 90);
                Assert.True(dark > pixels.Length / 3,
                    $"{relative} is claimed to be the dark theme, but only {dark} of {pixels.Length} pixels are dark");
                Assert.True(pixels.Any(p => Luminance(p) > 150),
                    $"{relative} has no light pixels, so text would be missing");
            }

            // No orphan pictures: an image in that folder is shown by the README or it is dead weight
            // in every clone.
            foreach (string file in Directory.GetFiles(Path.Combine(root, "docs", "images")))
            {
                string relative = "docs/images/" + Path.GetFileName(file);
                Assert.True(referenced.Contains(relative, StringComparer.OrdinalIgnoreCase),
                    $"{relative} is in the repository but no document points at it");
            }
        });
    }

    private static int Luminance(int argb)
    {
        int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        return (r * 299 + g * 587 + b * 114) / 1000;
    }
}
