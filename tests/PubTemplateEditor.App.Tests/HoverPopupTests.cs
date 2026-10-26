using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

            var popup = (ToolTip)container.ToolTip;
            Assert.NotNull(popup);
            Binding binding = BindingOperations.GetBinding(popup, ContentControl.ContentProperty)!;
            Assert.NotNull(binding);
            Assert.Equal(nameof(ParameterRowItem.RowTooltip), binding.Path.Path);

            string text = row.RowTooltip;
            Assert.StartsWith("alpha", text, StringComparison.Ordinal);
            Assert.Contains("Current value: HELLO", text, StringComparison.Ordinal);
            Assert.Contains($"Description: {description}", text, StringComparison.Ordinal);
            Assert.Contains("Access: internal", text, StringComparison.Ordinal);
            Assert.Contains("one.dta", text, StringComparison.Ordinal);

            // Nothing staged: the popup must not invent a staged line.
            Assert.DoesNotContain("Staged", text, StringComparison.Ordinal);

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

            var container = (ListBoxItem)window.TemplateRows.ItemContainerGenerator.ContainerFromItem(one);
            var popup = (ToolTip)container.ToolTip;
            Assert.Equal(nameof(TemplateItem.RowTooltip),
                BindingOperations.GetBinding(popup, ContentControl.ContentProperty)!.Path.Path);

            string text = one.RowTooltip;
            Assert.StartsWith("one.dta", text, StringComparison.Ordinal);
            Assert.Contains("2 parameter(s)", text, StringComparison.Ordinal);
            Assert.Contains("1 staged change(s), not saved", text, StringComparison.Ordinal);
            Assert.Contains(Path.Combine(templates, "one.dta"), text, StringComparison.Ordinal);

            // A file with nothing staged does not claim staged changes; the count is per file.
            Assert.DoesNotContain("staged change", two.RowTooltip, StringComparison.Ordinal);
            Assert.Contains("1 parameter(s)", two.RowTooltip, StringComparison.Ordinal);

            // The heading of the parameter panel repeats the same summary for the active template.
            var heading = (ToolTip)window.ActiveTemplateLabel.ToolTip;
            Assert.Equal("ActiveFile.RowTooltip",
                BindingOperations.GetBinding(heading, ContentControl.ContentProperty)!.Path.Path);

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
    public void TheRowPopupStyleWrapsAndStaysInsideTheScreen()
    {
        _host.Do(() =>
        {
            var shellModel = new WorkspaceViewModel(new SettingsService(TempDir("settings")), new EditorSettings());
            var window = new ShellWindow(shellModel);

            // The style lives in the merged application dictionary, not in the window's own resources.
            Style style = (Style)Application.Current.Resources["RowToolTipStyle"];
            Assert.NotNull(style);
            Assert.NotNull(style);

            double maxWidth = (double)style.Setters.Cast<Setter>()
                .First(s => s.Property == FrameworkElement.MaxWidthProperty).Value;
            Assert.True(maxWidth is >= 360 and <= 640, $"the row popup is {maxWidth} px wide");

            var contentTemplate = (DataTemplate)style.Setters.Cast<Setter>()
                .First(s => s.Property == ContentControl.ContentTemplateProperty).Value;
            var text = (TextBlock)contentTemplate.LoadContent();
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);

            shellModel.NavigationGuard = _ => NavigationDecision.KeepEditing;
            window.Close();
        });
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
