using System.IO;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using Xunit;
using ShellWindow = PubTemplateEditor.App.MainWindow;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// S3 interaction tests: the details form stages changes, compare and bulk explain every target,
/// and navigation that would throw away work asks first. Every template under test is a synthetic
/// copy written into a temporary folder; authoritative samples are never used as edit targets.
/// </summary>
[Collection("WPF shell")]
public class EditingInteractionTests
{
    private readonly WpfHost _host;

    public EditingInteractionTests(WpfHost host) => _host = host;

    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-s3-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string TemplateXml(params string[] variables) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<template><metadata><property name=\"version\" value=\"2\"></property></metadata>" +
        "<dataset><variables>" + string.Concat(variables) + "</variables></dataset></template>";

    private static string Variable(string name, string? defaultValue, string access = "internal", string description = "desc")
    {
        string def = defaultValue is null ? string.Empty : $"<property name=\"default\" value=\"{defaultValue}\"></property>";
        return "<variable><feature tag=\"definition\">" +
               $"<property name=\"name\" value=\"{name}\"></property>" +
               $"<property name=\"description\"><expression type=\"constant\">{description}</expression></property>" +
               "<property name=\"type\" value=\"user\"></property>" +
               $"<property name=\"access\" value=\"{access}\"></property>" +
               def +
               "</feature></variable>";
    }

    private static string Write(string dir, string fileName, string xml)
    {
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private WorkspaceViewModel NewViewModel(string settingsDir, string templateDir) =>
        new(new SettingsService(settingsDir), new EditorSettings { LastFolder = templateDir });

    private async Task<WorkspaceViewModel> ReadAllAsync(string settingsDir, string templateDir)
    {
        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = NewViewModel(settingsDir, templateDir);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;
        return vm;
    }

    [Fact]
    public async Task TypingAValueAndApplyingStagesItWithoutTouchingTheFile()
    {
        string templates = TempDir("templates");
        string path = Write(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        byte[] bytesBefore = File.ReadAllBytes(path);

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            var row = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = row;

            Assert.Equal("OLD", vm.DetailDefaultValue);
            Assert.False(vm.HasUnappliedFormEdits);
            Assert.True(!window.DetailValueEditor.IsReadOnly);

            vm.DetailDefaultValue = "NEW VALUE";
            Assert.True(vm.HasUnappliedFormEdits);
            Assert.Equal(1, vm.UnappliedFieldCount);
            Assert.False(vm.HasStagedChanges);
            Assert.Contains("unapplied", vm.FormSummary, StringComparison.OrdinalIgnoreCase);

            vm.ApplyEditsCommand.Execute(null);

            Assert.Equal(1, vm.StagedChangeCount);
            Assert.True(vm.HasStagedChanges);
            Assert.Equal("1 staged change(s) in 1 template(s)", vm.StagedSummary);
            Assert.True(vm.DetailHasDefaultValueDraft);
            Assert.False(vm.HasUnappliedFormEdits);
            Assert.Equal(0, vm.UnappliedFieldCount);

            // The table shows the session state, with a marker that it is only staged.
            Assert.Equal("NEW VALUE", row.DefaultPreview);
            Assert.True(row.HasDrafts);
            Assert.Equal("default value", row.DraftedFieldsSummary);
            Assert.Equal(1, vm.ActiveFile!.StagedEditCount);
            Assert.Contains("1 staged", window.StagedSummaryLabel.Text, StringComparison.Ordinal);

            // The parsed baseline still describes the file, and the file itself is untouched.
            Assert.Equal("OLD", row.Declaration.DefaultValue.LogicalValue);
            Assert.Equal(bytesBefore, File.ReadAllBytes(path));

            Assert.Contains("Staged 1 field change(s)", vm.ActionNotice ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task TypingTheStoredValueAgainRemovesTheStagedChange()
    {
        string templates = TempDir("templates");
        Write(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var row = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = row;

            vm.DetailDefaultValue = "NEW";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(1, vm.StagedChangeCount);

            vm.DetailDefaultValue = "OLD";
            Assert.True(vm.HasUnappliedFormEdits); // differs from the staged value
            vm.ApplyEditsCommand.Execute(null);

            Assert.Equal(0, vm.StagedChangeCount);
            Assert.False(vm.HasStagedChanges);
            Assert.False(row.HasDrafts);
            Assert.False(vm.DetailHasDefaultValueDraft);
            Assert.Equal("OLD", row.DefaultPreview);
            Assert.Equal(0, vm.ActiveFile!.StagedEditCount);
        });
    }

    [Fact]
    public async Task AccessOutsideTheObservedVocabularyIsRefusedAndTheTypingStaysForCorrection()
    {
        string templates = TempDir("templates");
        Write(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD", access: "internal")));

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var row = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = row;

            Assert.Equal(new[] { "internal", "external" }, vm.AccessChoices.ToArray());

            foreach (string invalid in new[] { "Internal", "external " })
            {
                vm.DetailAccess = invalid;
                vm.ApplyEditsCommand.Execute(null);

                Assert.Equal(0, vm.StagedChangeCount);
                Assert.True(vm.HasActionError);
                Assert.Contains("internal / external", vm.ActionError!, StringComparison.Ordinal);
                Assert.Equal(invalid, vm.DetailAccess); // kept so the user can correct it
            }

            vm.DetailAccess = "external";
            vm.ApplyEditsCommand.Execute(null);

            Assert.Equal(1, vm.StagedChangeCount);
            Assert.True(vm.DetailHasAccessDraft);
            Assert.Equal("external", row.AccessLabel);
            Assert.Equal("internal", row.Declaration.Access.LogicalValue);
        });
    }

    [Fact]
    public async Task SwitchingParameterAsksAboutUnappliedTypingAndAlwaysKeepsStagedWork()
    {
        string templates = TempDir("templates");
        Write(templates, "t1.dta", TemplateXml(
            Variable("alpha", "OLD"),
            Variable("beta", "B")));

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var prompts = new List<NavigationPrompt>();
            NavigationDecision decision = NavigationDecision.KeepEditing;
            vm.NavigationGuard = prompt =>
            {
                prompts.Add(prompt);
                return decision;
            };

            var alpha = vm.VisibleParameters.Single(r => r.Name == "alpha");
            var beta = vm.VisibleParameters.Single(r => r.Name == "beta");

            vm.SelectedParameter = alpha;
            vm.DetailDefaultValue = "STAGED";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(1, vm.StagedChangeCount);
            Assert.Empty(prompts); // no unsaved form state, so switching is quiet

            vm.DetailDescription = "typed but not applied";
            Assert.True(vm.HasUnappliedFormEdits);

            vm.SelectedParameter = beta;
            Assert.Single(prompts);
            Assert.Equal(NavigationReason.ChangeParameter, prompts[0].Reason);
            Assert.Equal(1, prompts[0].UnappliedFieldCount);
            Assert.Equal(1, prompts[0].StagedChangeCount);
            Assert.Same(alpha, vm.SelectedParameter); // refused: the selection rewinds
            Assert.Equal("typed but not applied", vm.DetailDescription);
            Assert.Equal(1, vm.StagedChangeCount);

            decision = NavigationDecision.DiscardForms;
            vm.SelectedParameter = beta;
            Assert.Same(beta, vm.SelectedParameter);
            Assert.Equal(1, vm.StagedChangeCount); // staged work survives the discard of typing
            Assert.False(vm.HasUnappliedFormEdits);

            // Coming back shows the staged value, not the stored one, and marks it.
            vm.SelectedParameter = alpha;
            Assert.Equal("STAGED", vm.DetailDefaultValue);
            Assert.True(vm.DetailHasDefaultValueDraft);
            Assert.Equal("desc", vm.DetailDescription); // the discarded typing is really gone
            Assert.False(vm.DetailHasDescriptionDraft);

            decision = NavigationDecision.ApplyForms;
            vm.DetailAccess = "external";
            vm.SelectedParameter = beta;
            Assert.Equal(2, vm.StagedChangeCount);
            Assert.Same(beta, vm.SelectedParameter);
        });
    }

    [Fact]
    public async Task ComparePanelShowsSelfDifferencesMissingTargetsAndDraftAwareValues()
    {
        string templates = TempDir("templates");
        Write(templates, "t1.dta", TemplateXml(Variable("alpha", "HELLO", access: "internal")));
        Write(templates, "t2.dta", TemplateXml(Variable("alpha", "OTHER", access: "external")));
        Write(templates, "t3.dta", TemplateXml(Variable("gamma", "HELLO")));

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var window = new ShellWindow(vm);
            var alpha = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = alpha;

            Assert.False(vm.IsCompareOpen);
            vm.CompareCommand.Execute(null);
            Assert.True(vm.IsCompareOpen);

            ComparisonRowItem Self() => vm.CompareRows.Single(r => r.IsSelf);
            Assert.Equal("t1.dta", Self().FileName);

            ComparisonRowItem other = vm.CompareRows.Single(r => r.FileName == "t2.dta");
            Assert.Equal(CompareState.Different, other.State);
            Assert.Equal("different", other.StateLabel);
            Assert.Contains("default value", other.DiffSummary, StringComparison.Ordinal);
            Assert.Contains("access", other.DiffSummary, StringComparison.Ordinal);
            CompareFieldRowItem value = other.Fields.Single(f => f.FieldLabel == "default value");
            Assert.Equal("HELLO", value.ActiveValue);
            Assert.Equal("OTHER", value.TargetValue);
            Assert.True(value.IsDifferent);

            ComparisonRowItem missing = vm.CompareRows.Single(r => r.FileName == "t3.dta");
            Assert.Equal(CompareState.Missing, missing.State);
            Assert.Contains("No declaration named 'alpha'", missing.Reason, StringComparison.Ordinal);

            Assert.Contains("1 different", vm.CompareSummary, StringComparison.Ordinal);
            Assert.Contains("1 without this parameter", vm.CompareSummary, StringComparison.Ordinal);
            Assert.Contains("t1.dta", window.CompareSummaryLabel.Text + string.Join(",", vm.CompareRows.Select(r => r.FileName)),
                StringComparison.Ordinal);

            // Draft-aware: the comparison follows what would be saved, and says it is a draft.
            vm.DetailDefaultValue = "DRAFT";
            vm.ApplyEditsCommand.Execute(null);
            value = vm.CompareRows.Single(r => r.FileName == "t2.dta").Fields.Single(f => f.FieldLabel == "default value");
            Assert.Equal("DRAFT", value.ActiveValue);
            Assert.True(value.ActiveHasDraft);

            // Only the read selection narrows the scope.
            vm.CompareSelectedOnly = true;
            vm.AllFiles.Single(f => f.FileName == "t3.dta").IsSelectedForRead = false;
            vm.CompareCommand.Execute(null);
            Assert.DoesNotContain(vm.CompareRows, r => r.FileName == "t3.dta");

            vm.CloseCompareCommand.Execute(null);
            Assert.False(vm.IsCompareOpen);
            Assert.Empty(vm.CompareRows);
        });
    }

    [Fact]
    public async Task BulkPanelExplainsEveryTargetAndStagesOnlyEligibleOnes()
    {
        string templates = TempDir("templates");
        Write(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD_A", access: "external", description: "A desc")));
        Write(templates, "t2.dta", TemplateXml(Variable("alpha", "X"), Variable("alpha", "Y"))); // ambiguous
        Write(templates, "t3.dta", TemplateXml(Variable("gamma", "G")));                        // name not declared
        Write(templates, "t4.dta", TemplateXml(Variable("alpha", null)));                       // no default property
        Write(templates, "t5.dta", TemplateXml(Variable("alpha", "NEVER READ")));               // left out of the read scope

        string settings = TempDir("settings");
        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            // t5 stays out of the read scope, so compare/bulk must explain it as not read.
            vm = NewViewModel(settings, templates);
            vm.AllFiles.Single(f => f.FileName == "t5.dta").IsSelectedForRead = false;
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;

        _host.Do(() =>
        {
            var alpha = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.SelectedParameter = alpha;

            vm.OpenBulkCommand.Execute(null);
            Assert.True(vm.IsBulkOpen);
            Assert.Equal("OLD_A", vm.BulkValueDraft);

            BulkTargetRowItem ByName(string name) => vm.BulkTargets.Single(t => t.FileName == name);
            Assert.Equal(BulkTargetStatus.Eligible, ByName("t1.dta").Target.Status);
            Assert.True(ByName("t1.dta").IsChecked);
            Assert.Equal(BulkTargetStatus.Ambiguous, ByName("t2.dta").Target.Status);
            Assert.False(ByName("t2.dta").IsSelectable);
            Assert.False(ByName("t2.dta").IsChecked);
            Assert.Contains("declared 2 times", ByName("t2.dta").Reason, StringComparison.Ordinal);
            Assert.Equal(BulkTargetStatus.Missing, ByName("t3.dta").Target.Status);
            Assert.Equal(BulkTargetStatus.ReadOnlyShape, ByName("t4.dta").Target.Status);
            Assert.Contains("no 'default' property", ByName("t4.dta").Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(BulkTargetStatus.NotLoaded, ByName("t5.dta").Target.Status);
            Assert.Equal("not read in this session", ByName("t5.dta").StatusLabel);

            Assert.Contains("can receive this value", vm.BulkSummary, StringComparison.Ordinal);

            vm.BulkValueDraft = "SAME FOR ALL";
            vm.ConfirmBulkCommand.Execute(null);

            Assert.False(vm.IsBulkOpen);
            Assert.Equal(1, vm.StagedChangeCount);
            Assert.Equal("SAME FOR ALL", alpha.DefaultPreview);
            Assert.Equal(1, vm.AllFiles.Single(f => f.FileName == "t1.dta").StagedEditCount);
            Assert.Equal(0, vm.AllFiles.Single(f => f.FileName == "t2.dta").StagedEditCount);

            // Bulk touched the default value only: access and description stay as they are.
            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == "alpha");
            Assert.Equal("external", vm.DetailAccess);
            Assert.Equal("A desc", vm.DetailDescription);
            Assert.False(vm.DetailHasAccessDraft);
            Assert.False(vm.DetailHasDescriptionDraft);
            Assert.True(vm.DetailHasDefaultValueDraft);
        });
    }

    [Fact]
    public async Task ReReadAndCloseAskBeforeDiscardingStagedChanges()
    {
        string templates = TempDir("templates");
        string path = Write(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        byte[] bytesBefore = File.ReadAllBytes(path);

        WorkspaceViewModel vm = await ReadAllAsync(TempDir("settings"), templates);

        _host.Do(() =>
        {
            var prompts = new List<NavigationPrompt>();
            NavigationDecision decision = NavigationDecision.KeepEditing;
            vm.NavigationGuard = prompt =>
            {
                prompts.Add(prompt);
                return decision;
            };

            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.DetailDefaultValue = "DRAFTED";
            vm.ApplyEditsCommand.Execute(null);
            Assert.Equal(1, vm.StagedChangeCount);

            Assert.False(vm.ConfirmClose());
            Assert.Single(prompts);
            Assert.Equal(NavigationReason.CloseWorkspace, prompts[0].Reason);
            Assert.Equal(1, vm.StagedChangeCount); // nothing lost

            Task reRead = vm.ReadParametersCommand.ExecuteAsync(null);
            Assert.Equal(NavigationReason.ReReadTemplates, prompts[^1].Reason);
            Assert.Equal(1, vm.StagedChangeCount);

            vm.DiscardAllStagedCommand.Execute(null); // refused by the same guard
            Assert.Equal(1, vm.StagedChangeCount);

            decision = NavigationDecision.DiscardFormsAndStaged;
            Assert.True(vm.ConfirmClose());
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.Equal(bytesBefore, File.ReadAllBytes(path)); // discarding never writes either
        });
    }

    [Fact]
    public void ThePromptWindowOffersOnlyChoicesThatMatchThePendingWork()
    {
        _host.Do(() =>
        {
            var formOnly = new UnsavedWorkWindow(new NavigationPrompt(NavigationReason.ChangeParameter, 2, 0));
            Assert.Equal(2, formOnly.Prompt.UnappliedFieldCount);
            Assert.Equal(System.Windows.Visibility.Collapsed, formOnly.DiscardAllChoice.Visibility);
            Assert.True(formOnly.ApplyChoice.IsEnabled);
            Assert.Contains("2 field change(s)", formOnly.MessageLabel.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("staged change(s) exist", formOnly.MessageLabel.Text, StringComparison.Ordinal);

            var withDrafts = new UnsavedWorkWindow(new NavigationPrompt(NavigationReason.CloseWorkspace, 0, 3));
            Assert.Equal(System.Windows.Visibility.Visible, withDrafts.DiscardAllChoice.Visibility);
            Assert.False(withDrafts.ApplyChoice.IsEnabled);
            Assert.Contains("3 staged change(s)", withDrafts.MessageLabel.Text, StringComparison.Ordinal);
            Assert.Contains("closing the editor", withDrafts.MessageLabel.Text, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(NavigationDecision.KeepEditing, withDrafts.Decision);
            withDrafts.DiscardAllChoice.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(NavigationDecision.DiscardFormsAndStaged, withDrafts.Decision);
        });
    }
}
