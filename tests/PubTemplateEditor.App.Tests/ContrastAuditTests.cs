using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Colour-blind-free contrast audit: walks the real visual tree of the shell and every dialog in
/// both themes, resolves the painted foreground of each visible piece of text against the painted
/// background behind it, and fails when the pair is too low in contrast. Catches the class of bug
/// where an explicit Style (or a control template from the Windows theme) leaves text at the
/// default black while the surface behind it has been darkened.
/// </summary>
[Collection("WPF shell")]
public class ContrastAuditTests
{
    private readonly WpfHost _host;

    public ContrastAuditTests(WpfHost host) => _host = host;

    private const double MinimumRatio = 3.0;

    public sealed record Offender(string Owner, string Text, Color Foreground, Color Background, double Ratio);

    private static string ReportPath => Path.Combine(ShellSnapshotTests.ArtifactsDir(), "contrast-report.txt");

    [Theory]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.Dark)]
    public void EveryTextInTheShellIsReadable(ThemeChoice theme)
    {
        string settings = MakeDir("contrast-settings");
        string templates = MakeDir("contrast-templates");
        File.WriteAllText(Path.Combine(templates, "one.dta"), TemplateXml(
            ("alpha", "OLD", "internal", "first parameter"),
            ("beta", "B", "external", "second parameter")));

        WorkspaceViewModel vm = _host.Do(() =>
        {
            ThemeManager.Apply(_host.App, theme);
            return new WorkspaceViewModel(new SettingsService(settings), new EditorSettings { LastFolder = templates });
        });
        Task read = _host.Do(() => vm.ReadParametersCommand.ExecuteAsync(null));
        // The audit itself is synchronous UI-thread work; blocking here is how the shared STA
        // host is driven from an xunit worker thread.
#pragma warning disable xUnit1031
        read.GetAwaiter().GetResult();
#pragma warning restore xUnit1031

        var offenders = new List<Offender>();
        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.First();
            vm.DetailDescription = "typing that is not applied yet";
            vm.DetailDefaultValue = "NEW";
            vm.ApplyEditsCommand.Execute(null);

            // Panels are audited one at a time so an overlay cannot hide an unreadable control
            // underneath it.
            Audit(new ShellWindow(vm) { ShowInTaskbar = false }, offenders);

            vm.OpenBulkCommand.Execute(null);
            Audit(new ShellWindow(vm) { ShowInTaskbar = false }, offenders);
            vm.CloseBulkCommand.Execute(null);

            vm.CompareCommand.Execute(null);
            Audit(new ShellWindow(vm) { ShowInTaskbar = false }, offenders);
            vm.CloseCompareCommand.Execute(null);

            vm.OpenReviewCommand.Execute(null);
            Audit(new ShellWindow(vm) { ShowInTaskbar = false }, offenders);
            vm.CloseReviewCommand.Execute(null);

            // The row popups are not part of the shell's visual tree until a pointer hovers a row, so
            // their cards are rendered here against real data: labels, values and badges on the popup
            // background have to be readable in both themes like everything else.
            Audit(HoverCardWindow(vm), offenders);

            Audit(new UnsavedWorkWindow(new NavigationPrompt(NavigationReason.CloseWorkspace, 2, 3)), offenders);
            Audit(new UnsavedWorkWindow(new NavigationPrompt(NavigationReason.ChangeParameter, 0, 3)), offenders);
            Audit(new LicensesWindow(), offenders);

            DeleteDir(settings);
            DeleteDir(templates);
        });

        StringBuilder report = new();
        report.AppendLine($"theme={theme} offenders={offenders.Count}");
        foreach (Offender o in offenders.OrderBy(o => o.Ratio))
        {
            report.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{o.Ratio:0.00} fg=#{o.Foreground} bg=#{o.Background} {o.Owner} :: {Shorten(o.Text)}"));
        }

        string reportFile = Path.Combine(ShellSnapshotTests.ArtifactsDir(), $"contrast-{theme.ToString().ToLowerInvariant()}.txt");
        File.WriteAllText(reportFile, report.ToString());

        Assert.True(offenders.Count == 0, $"{theme}: {offenders.Count} unreadable text elements {report}");
    }

    /// <summary>
    /// A plain window that hosts the two row cards, framed the way the popup frames them. Kept off the
    /// screen; the audit renders the content, not the window.
    /// </summary>
    private static Window HoverCardWindow(WorkspaceViewModel vm)
    {
        var cards = new StackPanel { Margin = new Thickness(0) };
        foreach (object item in new object[] { vm.VisibleParameters.First(), vm.AllFiles.First() })
        {
            cards.Children.Add(new ContentPresenter
            {
                Content = item,
                ContentTemplate = (DataTemplate)Application.Current.FindResource(new DataTemplateKey(item.GetType())),
            });
        }

        Brush foreground = (Brush)Application.Current.FindResource("ForegroundBrush");
        Brush chrome = (Brush)Application.Current.FindResource("PanelBackgroundBrush");
        System.Windows.Documents.TextElement.SetForeground(cards, foreground);

        return new Window
        {
            Content = new Border { Background = chrome, Padding = new Thickness(10, 8, 10, 8), Child = cards },
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight,
        };
    }

    private static void Audit(Window window, List<Offender> offenders)
    {
        FrameworkElement root = (FrameworkElement)window.Content;
        var rect = new Size(1240, 780);
        root.Measure(rect);
        root.Arrange(new Rect(rect));
        root.UpdateLayout();

        // A window that was never shown has no HWND; render its root into a visual tree that
        // contains the real templates (combo boxes, tabs, scroll bars) before walking it.
        var render = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)rect.Width, (int)rect.Height, 96, 96, PixelFormats.Pbgra32);
        render.Render(root);
        root.UpdateLayout();

        Walk(root, offenders);
    }

    private static void Walk(DependencyObject node, List<Offender> offenders)
    {
        if (node is FrameworkElement fe && fe.Visibility != Visibility.Collapsed)
        {
            if (fe is TextBlock text && !string.IsNullOrWhiteSpace(text.Text) && text.ActualWidth > 1)
            {
                Color? fg = ResolvedColor(text, TextBlock.ForegroundProperty);
                Color? bg = BackgroundBehind(text);
                if (fg is not null && bg is not null)
                {
                    double ratio = Contrast(fg.Value, bg.Value);
                    if (ratio < MinimumRatio)
                    {
                        offenders.Add(new Offender(Owner(text), text.Text, fg.Value, bg.Value, ratio));
                    }
                }
            }
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), offenders);
        }
    }

    private static Color? ResolvedColor(DependencyObject e, DependencyProperty property)
    {
        if (e.GetValue(property) is not SolidColorBrush brush)
        {
            return null;
        }

        // Unfrozen brushes may still be resolving a DynamicResource; Copy() snapshots the current value.
        Color color = brush.Color;
        if (color.A == 0)
        {
            return null;
        }

        return color;
    }

    private static Color? BackgroundBehind(DependencyObject start)
    {
        for (DependencyObject? node = start; node is not null;)
        {
            Brush? brush = node switch
            {
                Window w => w.Background,
                UserControl u => u.Background,
                Border b => b.Background,
                Grid g => g.Background,
                Panel p => p.Background,
                ItemsControl ic => ic.Background,
                TextBlock _ => null,
                _ => null,
            };

            if (node is ContentControl cc)
            {
                brush ??= cc.Background;
            }

            if (brush is SolidColorBrush sb && sb.Color.A > 0)
            {
                return sb.Color;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private static string Owner(DependencyObject e)
    {
        for (DependencyObject? node = VisualTreeHelper.GetParent(e); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                return fe.Name + "/" + e.GetType().Name;
            }
        }

        return e.GetType().Name;
    }

    private static double Contrast(Color fg, Color bg)
    {
        double a = Luminance(fg);
        double b = Luminance(bg);
        double hi = Math.Max(a, b);
        double lo = Math.Min(a, b);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(c.R / 255.0) + 0.7152 * Channel(c.G / 255.0) + 0.0722 * Channel(c.B / 255.0);
    }

    private static string Shorten(string s) => s.Length <= 48 ? s : s[..48] + "…";

    private static string TemplateXml(params (string Name, string Default, string Access, string Description)[] variables)
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

    private static string MakeDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch (IOException) { }
    }
}
