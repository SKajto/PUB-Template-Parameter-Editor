using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Media;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Hover popups and the tools line. The table cells are truncated by design, so the whole parameter and
/// the whole file summary have to be reachable without resizing columns or opening every template.
/// </summary>
[Collection("WPF shell")]
public class HoverPopupTests
{
    private readonly WpfHost _host;

    public HoverPopupTests(WpfHost host) => _host = host;

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-hover-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Variable(string name, string defaultValue, string access = "internal", string description = "some description") =>
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

    private static void Realize(ShellWindow window)
    {
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(1240, 780));
        root.Arrange(new Rect(new Size(1240, 780)));
        root.UpdateLayout();
    }

    private async Task<(WorkspaceViewModel vm, string templates)> ReadTemplates(params (string File, string Xml)[] files)
    {
        string templates = TempDir("templates");
        foreach ((string name, string xml) in files)
        {
            File.WriteAllText(Path.Combine(templates, name), xml);
        }

        WorkspaceViewModel vm = null!;
        _host.Do(() => vm = new WorkspaceViewModel(
            new SettingsService(TempDir("settings")), new EditorSettings { LastFolder = templates }));
        await _host.Do(() => vm.ReadParametersCommand.ExecuteAsync(null));
        return (vm, templates);
    }

    [Fact]
    public async Task AParameterRowPopsItsNameValueAndDescription()
    {
        const string description = "Sets how many units the player collects per town";
        (WorkspaceViewModel vm, _) = await ReadTemplates((
            "one.dta",
            TemplateXml(Variable("alpha", "HELLO", description: description),
                        Variable("beta", "TWO", access: "external"))));

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            Realize(window);

            ParameterRowItem row = vm.VisibleParameters.Single(r => r.Name == "alpha");
            var container = (ListViewItem)window.ParameterTable.ItemContainerGenerator.ContainerFromItem(row);
            Assert.True(container is not null, "the parameter row was not realized");

            // The row object is what the row offers as its tooltip; the card is chosen by type.
            Assert.Same(row, container.ToolTip);
            DataTemplate card = (DataTemplate)Application.Current.FindResource(new DataTemplateKey(typeof(ParameterRowItem)));
            Assert.NotNull(card);

            List<string> texts = CardTexts(card, row);
            Assert.Contains("alpha", texts);
            Assert.Contains("HELLO", texts);
            Assert.Contains(description, texts);
            Assert.Contains("internal", texts);
            Assert.Contains("Current value", texts);
            Assert.Contains("Description", texts);
            Assert.Contains("one.dta \u00b7 declaration #" + row.DeclarationId, texts);
            Assert.DoesNotContain("Staged in this session", string.Join("|", texts), StringComparison.Ordinal);

            // Assistive technology gets the same facts as one plain sentence.
            string help = AutomationProperties.GetHelpText(container);
            Assert.Contains("Current value: HELLO", help, StringComparison.Ordinal);
            Assert.Contains("Description: " + description, help, StringComparison.Ordinal);

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public async Task TheParameterPopupSaysWhichValuesAreStagedRatherThanStored()
    {
        (WorkspaceViewModel vm, _) = await ReadTemplates(("one.dta", TemplateXml(Variable("alpha", "OLD"))));

        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.Single();
            vm.DetailDefaultValue = "NEW";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(1, vm.StagedChangeCount);
        });

        _host.Do(() =>
        {
            ParameterRowItem row = vm.VisibleParameters.Single();
            string text = row.RowTooltip;
            Assert.Contains("Current value: NEW", text, StringComparison.Ordinal);
            Assert.Contains("Staged in this session, not saved: default value", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ATemplateRowPopsItsFileWithParameterAndStagedCounts()
    {
        (WorkspaceViewModel vm, string templates) = await ReadTemplates(
            ("one.dta", TemplateXml(Variable("alpha", "OLD"), Variable("beta", "TWO"))),
            ("two.dta", TemplateXml(Variable("gamma", "THREE"))));

        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.DetailDefaultValue = "NEW";
            vm.ApplyEditsCommand.Execute(null);
        });

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            Realize(window);

            TemplateItem one = vm.AllFiles.Single(f => f.FileName == "one.dta");
            TemplateItem two = vm.AllFiles.Single(f => f.FileName == "two.dta");
            DataTemplate card = (DataTemplate)(DataTemplate)Application.Current.FindResource(new DataTemplateKey(typeof(TemplateItem)));
            Assert.NotNull(card);

            var container = (ListBoxItem)window.TemplateRows.ItemContainerGenerator.ContainerFromItem(one);
            Assert.Same(one, container.ToolTip);

            List<string> texts = CardTexts(card, one);
            Assert.Contains("one.dta", texts);
            Assert.Contains("2 parameter(s)", texts);
            Assert.Contains("1 staged change(s), not saved", texts);
            Assert.Contains(Path.Combine(templates, "one.dta"), texts);

            // A file with nothing staged does not claim staged changes; the count stays per file.
            Assert.DoesNotContain("staged change", string.Join("|", CardTexts(card, two)), StringComparison.Ordinal);
            Assert.Contains("1 parameter(s)", CardTexts(card, two));
            Assert.Equal("none staged", two.StagedChangesText);

            Assert.Contains("1 staged change", AutomationProperties.GetHelpText(container), StringComparison.Ordinal);

            // The heading of the parameter panel carries the same card for the active template.
            // The heading offers the active file as its tooltip; the card for it is chosen by type when
            // the popup opens, exactly like the rows in the file list.
            var heading = (ToolTip)window.ActiveTemplateLabel.ToolTip;
            Assert.NotNull(heading);
            Binding headingContent = BindingOperations.GetBinding(heading, ContentControl.ContentProperty)!;
            Assert.Equal("ActiveFile", headingContent.Path.Path);
            Assert.NotNull(Application.Current.FindResource(new DataTemplateKey(typeof(TemplateItem))));

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    [Fact]
    public void AFileThatWasNeverReadSaysSoInsteadOfZeroParameters()
    {
        string templates = TempDir("templates");
        File.WriteAllText(Path.Combine(templates, "one.dta"), TemplateXml(Variable("alpha", "OLD")));

        _host.Do(() =>
        {
            WorkspaceViewModel vm = new(new SettingsService(TempDir("settings")), new EditorSettings { LastFolder = templates });
            TemplateItem file = vm.AllFiles.Single();
            Assert.Contains("not read yet", file.RowTooltip, StringComparison.Ordinal);
            Assert.DoesNotContain("0 parameter", file.RowTooltip, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheRowPopupIsARoundedCardWithRoomInside()
    {
        _host.Do(() =>
        {
            var shellModel = new WorkspaceViewModel(new SettingsService(TempDir("settings")), new EditorSettings());
            var window = new ShellWindow(shellModel);

            // Every popup - hint or card - gets one rounded, shadowed frame from the theme dictionary.
            Style tipStyle = (Style)Application.Current.FindResource(typeof(ToolTip));

            double maxWidth = (double)tipStyle.Setters.Cast<Setter>()
                .First(s => s.Property == FrameworkElement.MaxWidthProperty).Value;
            Assert.True(maxWidth is >= 400 and <= 700, $"the popup is only {maxWidth} px wide");

            var padding = (Thickness)tipStyle.Setters.Cast<Setter>()
                .First(s => s.Property == Control.PaddingProperty).Value;
            Assert.True(padding.Left >= 8 && padding.Bottom >= 6, "the popup has no inner space");

            var edge = (Thickness)tipStyle.Setters.Cast<Setter>()
                .First(s => s.Property == Control.BorderThicknessProperty).Value;
            Assert.True(edge.Left == 1, "the popup has no edge");
            Assert.True((bool)tipStyle.Setters.Cast<Setter>()
                .First(s => s.Property == ToolTip.HasDropShadowProperty).Value, "the popup has no shadow");

            foreach (Type itemType in new[] { typeof(ParameterRowItem), typeof(TemplateItem) })
            {
                var card = (FrameworkElement)((DataTemplate)Application.Current.FindResource(new DataTemplateKey(itemType))).LoadContent();
                Assert.True(card is StackPanel, itemType.Name + " card has no content panel");
                Assert.True(card.MinWidth >= 240, itemType.Name + " card collapses to the width of a cell");
            }

            shellModel.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }

    /// <summary>
    /// Lays out a card template over real row data and returns every text it draws. This is how the
    /// popup is checked without a mouse: the content, the labels and the card shape all have to come
    /// from the same row the pointer rests on.
    /// </summary>
    private static List<string> CardTexts(DataTemplate card, object data)
    {
        var host = new ContentPresenter { Content = data, ContentTemplate = card };
        host.Measure(new Size(460, 800));
        host.Arrange(new Rect(0, 0, 460, 800));
        host.UpdateLayout();

        var texts = new List<string>();
        void Walk(DependencyObject node)
        {
            if (node is TextBlock t && !string.IsNullOrWhiteSpace(t.Text))
            {
                texts.Add(t.Text);
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(host);
        return texts;
    }

    [Fact]
    public async Task SearchAccessFilterAndParameterToolsShareOneLine()
    {
        (WorkspaceViewModel vm, _) = await ReadTemplates(("one.dta", TemplateXml(Variable("alpha", "OLD"))));

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            Realize(window);

            static (double x, double middle) Place(FrameworkElement e, Visual relative)
            {
                Point p = e.TransformToVisual(relative).Transform(new Point(0, 0));
                return (p.X, p.Y + e.ActualHeight / 2);
            }

            var root = (Visual)window.Content;
            (double searchX, double searchY) = Place(window.SearchControl, root);
            (double accessX, double accessY) = Place(window.AccessFilterControl, root);
            (double compareX, double compareY) = Place(window.CompareActionButton, root);
            (double bulkX, double bulkY) = Place(window.BulkActionButton, root);
            (double tableX, double tableY) = Place(window.ParameterTable, root);

            // One line means one height: an input that is taller than the buttons beside it (or a combo
            // box a third shorter) breaks the line even when the centres line up.
            double[] heights =
            [
                window.SearchControl.ActualHeight,
                window.AccessFilterControl.ActualHeight,
                window.CompareActionButton.ActualHeight,
                window.BulkActionButton.ActualHeight,
            ];
            Assert.True(heights.Max() - heights.Min() <= 1,
                "the tools line mixes controls of different heights: " + string.Join("/", heights.Select(h => h.ToString("0.#"))));
            Assert.True(Math.Abs(searchY - accessY) < 4, "the access filter is not in line with the search box");
            Assert.True(Math.Abs(searchY - compareY) < 6, "the parameter tools are not in line with the search box");
            Assert.True(searchX < accessX, "the search box must sit left of the access filter");
            Assert.True(accessX < compareX && compareX < bulkX,
                "Compare and Apply value to templates must sit right of the search and filter");
            Assert.True(tableY > searchY, "the tools line must be above the table it belongs to");
            Assert.True(searchX >= tableX - 20, "the tools line does not start where the table starts");

            vm.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
    }
}
