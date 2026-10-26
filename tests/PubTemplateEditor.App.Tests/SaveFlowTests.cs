using System.IO;
using PubTemplateEditor.App;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using PubTemplateEditor.TestSupport;
using Xunit;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// S4 save-flow tests through the shell: review -> tick -> save. Every template is a synthetic copy
/// written into a private temporary folder, and the backups and journal go into a temporary state
/// folder, so these tests cannot touch a real template or real user state.
/// </summary>
[Collection("WPF shell")]
public sealed class SaveFlowTests : IDisposable
{
    private readonly WpfHost _host;
    private readonly string _root;

    public SaveFlowTests(WpfHost host)
    {
        _host = host;
        _root = Path.Combine(Path.GetTempPath(), $"pubte-s4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temporary folder.
        }
    }

    private string Sub(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string TemplateXml(params string[] variables) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<template><metadata><property name=\"version\" value=\"2\"></property></metadata>" +
        "<dataset><variables>" + string.Concat(variables) + "</variables></dataset></template>";

    private static string Variable(string name, string defaultValue, string description = "desc") =>
        "<variable><feature tag=\"definition\">" +
        $"<property name=\"name\" value=\"{name}\"></property>" +
        $"<property name=\"description\"><expression type=\"constant\">{description}</expression></property>" +
        "<property name=\"type\" value=\"user\"></property>" +
        "<property name=\"access\"><expression type=\"constant\">internal</expression></property>" +
        $"<property name=\"default\" value=\"{defaultValue}\"></property>" +
        "</feature></variable>";

    private string WriteTemplate(string dir, string fileName, string xml)
    {
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, xml, new System.Text.UTF8Encoding(false));
        return path;
    }

    private async Task<WorkspaceViewModel> ReadAllAsync(string templates, string state)
    {
        WorkspaceViewModel vm = null!;
        Task read = Task.CompletedTask;
        _host.Do(() =>
        {
            vm = new WorkspaceViewModel(new SettingsService(Sub("settings")), new EditorSettings { LastFolder = templates }, state);
            read = vm.ReadParametersCommand.ExecuteAsync(null);
        });
        await read;
        return vm;
    }

    private void Stage(WorkspaceViewModel vm, string parameterName, string value)
    {
        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == parameterName);
            vm.DetailDefaultValue = value;
            vm.ApplyEditsCommand.Execute(null);
        });
    }

    private async Task SaveAsync(WorkspaceViewModel vm)
    {
        Task save = Task.CompletedTask;
        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            save = vm.SaveReviewCommand.ExecuteAsync(null);
        });
        await save;
    }

    private static string CurrentHash(string path) => HashService.Sha256Hex(File.ReadAllBytes(path));

    [Fact]
    public async Task SavingTheReviewWritesTheStagedChangeAndReadsTheFileBack()
    {
        string templates = Sub("templates");
        string path = WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "NEW VALUE");

        await SaveAsync(vm);

        _host.Do(() =>
        {
            Assert.True(vm.LastSaveOutcome == "Written", $"expected a written result, got '{vm.LastSaveOutcome}': {vm.ActionError} {vm.SaveOutcomeSummary}");
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.False(vm.IsSaving);

            // The panel stays open after an attempt: the result card is the proof, and closing the panel
            // would hide it. The card names the files and says where the replaced copy went.
            Assert.True(vm.IsReviewOpen, "the review stays open so the result can be read");
            Assert.True(vm.SaveOutcomeIsOk, "the result card does not read as a success");
            Assert.Equal("Saved 1 field(s) in 1 template file(s)", vm.SaveOutcomeTitle);
            Assert.Contains("t1.dta:", vm.SaveOutcomeSummary, StringComparison.Ordinal);
            Assert.Contains("backups", vm.BackupDirectoryHint, StringComparison.Ordinal);
            Assert.Contains("Saved 1 field(s) in 1 template(s)", vm.ActionNotice ?? string.Empty, StringComparison.Ordinal);

            // The editor re-read the file it wrote, so its baseline matches the new bytes.
            Assert.Equal(CurrentHash(path), vm.ActiveFile!.Record!.BaselineFingerprint);
            ParameterRowItem row = vm.VisibleParameters.Single(r => r.Name == "alpha");
            Assert.Equal("NEW VALUE", row.Declaration.DefaultValue.LogicalValue);
            Assert.False(row.HasDrafts);
        });

        string saved = File.ReadAllText(path);
        Assert.Contains("value=\"NEW VALUE\"", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"OLD\"", saved, StringComparison.Ordinal);
        Assert.Contains("<property name=\"name\" value=\"alpha\"></property>", saved, StringComparison.Ordinal);
        Assert.Contains("desc", saved, StringComparison.Ordinal);

        // A backup of the previous bytes and a journal line exist in the state folder that was passed in.
        string[] backups = Directory.GetFiles(Path.Combine(state, "backups"));
        Assert.Single(backups);
        Assert.Contains("value=\"OLD\"", File.ReadAllText(backups[0]), StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(state, "recovery-journal.jsonl"));
        string journal = File.ReadAllText(Path.Combine(state, "recovery-journal.jsonl"));
        Assert.Contains("\"confirmed\"", journal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileChangedBySomeoneElseIsRefusedAndTheChangeStaysStaged()
    {
        string templates = Sub("templates");
        string path = WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "MINE");

        // The file moves under the editor between review and save.
        string tampered = File.ReadAllText(path) + " ";
        File.WriteAllText(path, tampered, new System.Text.UTF8Encoding(false));
        string tamperedHash = CurrentHash(path);

        await SaveAsync(vm);

        _host.Do(() =>
        {
            Assert.Equal("Rejected", vm.LastSaveOutcome);
            Assert.Contains("changed after it was read", vm.ActionError ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, vm.StagedChangeCount);
            Assert.True(vm.IsReviewOpen, "the review stays open so the situation can be dealt with");
            Assert.True(vm.CanSaveReview);
            Assert.Single(vm.ReviewRows);
            Assert.True(vm.ReviewRows[0].IsSelected);
        });

        Assert.Equal(tamperedHash, CurrentHash(path));
        Assert.Contains("value=\"OLD\"", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(state, "backups")));
    }

    [Fact]
    public async Task OnlyTheTickedChangesAreWrittenAndTheRestStayStaged()
    {
        string templates = Sub("templates");
        string path = WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "OLD_B")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "A_NEW");
        Stage(vm, "beta", "B_NEW");

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            Assert.Equal(2, vm.ReviewRows.Count);
            vm.ReviewRows.Single(r => r.ParameterName == "beta").IsSelected = false;
        });

        await SaveOnlyAsync(vm);

        _host.Do(() =>
        {
            Assert.True((vm.ActionNotice ?? string.Empty).Contains("Saved 1 field(s) in 1 template(s)", StringComparison.Ordinal),
                $"notice='{vm.ActionNotice}' error='{vm.ActionError}' summary='{vm.SaveOutcomeSummary}' rows={vm.ReviewRows.Count} staged={vm.StagedChangeCount}");
            Assert.Equal(1, vm.StagedChangeCount);
            Assert.True(vm.IsReviewOpen);
            Assert.Single(vm.ReviewRows);
            Assert.Equal("beta", vm.ReviewRows[0].ParameterName);
        });

        string saved = File.ReadAllText(path);
        Assert.Contains("value=\"A_NEW\"", saved, StringComparison.Ordinal);
        Assert.Contains("value=\"OLD_B\"", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("B_NEW", saved, StringComparison.Ordinal);
    }

    /// <summary>The review is already open in this test, so only the command is executed.</summary>
    private async Task SaveOnlyAsync(WorkspaceViewModel vm)
    {
        Task save = Task.CompletedTask;
        _host.Do(() => { save = vm.SaveReviewCommand.ExecuteAsync(null); });
        await save;
    }

    [Fact]
    public async Task SavingWhileTheFormHoldsUnappliedTyping()
    {
        string templates = Sub("templates");
        string path = WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD"), Variable("beta", "OLD_B")));
        string state = Sub("state");
        WorkspaceViewModel vm = await ReadAllAsync(templates, state);

        var prompts = new List<NavigationReason>();
        _host.Do(() =>
        {
            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == "alpha");
            vm.DetailDefaultValue = "A_NEW";
            vm.ApplyEditsCommand.Execute(null);
            vm.DetailDescription = "TYPED_NOT_APPLIED";
            vm.NavigationGuard = p =>
            {
                prompts.Add(p.Reason);
                return NavigationDecision.ApplyForms;
            };
            vm.OpenReviewCommand.Execute(null);
            Assert.True(vm.CanSaveReview);
        });

        await SaveOnlyAsync(vm);

        string saved = File.ReadAllText(path);
        _host.Do(() =>
        {
            // Starting a save must not stop to ask a navigation question: the typed description is not
            // part of the staged set, so it stays in the form and the staged default value is written.
            Assert.True(prompts.Count == 0, "the save asked a navigation question: " + string.Join(",", prompts));
            Assert.Contains("value=\"A_NEW\"", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("TYPED_NOT_APPLIED", saved, StringComparison.Ordinal);
            Assert.False(vm.HasStagedChanges);
            Assert.Equal("Saved 1 field(s) in 1 template file(s)", vm.SaveOutcomeTitle);
            Assert.Equal("TYPED_NOT_APPLIED", vm.DetailDescription);
        });
    }

    /// <summary>
    /// What a completed save leaves behind. A change that was written is gone from the stage - the badge,
    /// the review row and the parameter marker - while a change the user deliberately left unticked stays
    /// where it was, because "not now" is not "not ever".
    /// </summary>
    [Fact]
    public async Task ACompletedSaveEmptiesTheStageOfEverythingItWrote()
    {
        string templates = Sub("templates");
        string path = WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD_A"), Variable("beta", "OLD_B")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "A_NEW");
        Stage(vm, "beta", "B_NEW");
        Assert.Equal(2, vm.StagedChangeCount);

        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            vm.ReviewRows.Single(r => r.ParameterName == "beta").IsSelected = false;
        });
        await SaveOnlyAsync(vm);

        _host.Do(() =>
        {
            Assert.Equal("Written", vm.LastSaveOutcome);
            Assert.Equal(1, vm.StagedChangeCount);
            Assert.Contains("beta", vm.ReviewRows.Single().ParameterName, StringComparison.Ordinal);
            // Unticking is remembered across the rebuild, so a leftover row is not silently re-selected.
            Assert.False(vm.ReviewRows.Single().IsSelected, "a change the user cleared came back ticked");
            Assert.Contains("Tick at least one", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });

        string partlySaved = File.ReadAllText(path);
        Assert.Contains("value=\"A_NEW\"", partlySaved, StringComparison.Ordinal);
        Assert.Contains("value=\"OLD_B\"", partlySaved, StringComparison.Ordinal);

        _host.Do(() => vm.SelectAllReviewCommand.Execute(null));
        await SaveOnlyAsync(vm);

        _host.Do(() =>
        {
            Assert.False(vm.HasStagedChanges, "the saved change stayed in the stage");
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.Empty(vm.ReviewRows);
            Assert.Equal("Written", vm.LastSaveOutcome);
            Assert.Equal("Saved 1 field(s) in 1 template file(s)", vm.SaveOutcomeTitle);
            Assert.Contains("Nothing is staged", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.Contains("stage of everything saved is cleared", vm.ActionNotice ?? string.Empty, StringComparison.Ordinal);
        });

        string fullySaved = File.ReadAllText(path);
        Assert.Contains("value=\"B_NEW\"", fullySaved, StringComparison.Ordinal);
        Assert.DoesNotContain("OLD_B", fullySaved, StringComparison.Ordinal);

        // The parameter rows drop their staged markers too, so nothing in the shell claims pending work.
        _host.Do(() =>
        {
            Assert.All(vm.VisibleParameters, p => Assert.False(p.HasDrafts));
            Assert.All(vm.AllFiles, f => Assert.Equal(0, f.StagedEditCount));
        });
    }

    [Fact]
    public async Task TheSaveActionStaysDisabledWhileNothingUsefulIsTicked()
    {
        string templates = Sub("templates");
        WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);

        _host.Do(() =>
        {
            Assert.False(vm.SaveReviewCommand.CanExecute(null));
            // Nothing has been staged yet, so the honest sentence is "nothing is staged" - not a claim
            // that staged values already match the file, which is what an empty list used to read as.
            Assert.Contains("Nothing is staged", vm.SaveUnavailableReason, StringComparison.Ordinal);
            Assert.DoesNotContain("nothing to write", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });

        Stage(vm, "alpha", "OLD"); // the stored value again: not a change at all

        _host.Do(() =>
        {
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.False(vm.HasStagedChanges);
        });

        Stage(vm, "alpha", "NEW");
        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            vm.SelectNoneReviewCommand.Execute(null);
            Assert.False(vm.SaveReviewCommand.CanExecute(null));
            Assert.Contains("Tick at least one change", vm.SaveUnavailableReason, StringComparison.Ordinal);
            vm.SelectAllReviewCommand.Execute(null);
            Assert.True(vm.SaveReviewCommand.CanExecute(null));
        });

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ASaveOfEveryTickedChangeInSeveralTemplatesReportsEachFileSeparately()
    {
        string templates = Sub("templates");
        string first = WriteTemplate(templates, "a.dta", TemplateXml(Variable("alpha", "OLD_A")));
        string second = WriteTemplate(templates, "b.dta", TemplateXml(Variable("beta", "OLD_B")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "A_NEW");

        _host.Do(() =>
        {
            // the second template becomes active, then its own parameter is staged; that file then
            // changes under the editor before the save
            // the second template is made active first: the parameter table shows the active template
            vm.SelectActiveFileCommand.Execute(vm.AllFiles.Single(f => f.FilePath == second));
            vm.SelectedParameter = vm.VisibleParameters.Single(r => r.Name == "beta");
            vm.DetailDescription = "B_DESC";
            vm.ApplyEditsCommand.Execute(null);
        });

        File.AppendAllText(second, " ");

        await SaveAsync(vm);

        _host.Do(() =>
        {
            Assert.True((vm.ActionNotice ?? string.Empty).Contains("Saved 1 field(s) in 1 template(s)", StringComparison.Ordinal)
                        && (vm.ActionNotice ?? string.Empty).Contains("not written", StringComparison.Ordinal),
                $"notice='{vm.ActionNotice}' error='{vm.ActionError}' summary='{vm.SaveOutcomeSummary}' rows={vm.ReviewRows.Count} staged={vm.StagedChangeCount}");
            Assert.Contains("changed after it was read", vm.ActionError ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, vm.StagedChangeCount);
        });

        Assert.Contains("value=\"A_NEW\"", File.ReadAllText(first), StringComparison.Ordinal);
        Assert.Contains("OLD_B", File.ReadAllText(second), StringComparison.Ordinal);
    }

    private static string? SamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "examples");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "example.dta")))
            {
                return candidate;
            }
        }

        return null;
    }

    [Fact]
    public async Task ASavedChangeInARealContainerSurvivesASecondSaveWithoutLookingLikeAnOutsideEdit()
    {
        // A real .dta container: the native sample when the machine has one, otherwise a synthetic
        // container with a schema entry and a non-XML mapping entry, so the round trip is comparable.
        string templates = Sub("templates");
        string path = TestSamples.CopyNativeOrSynthetic(templates, "example.dta");

        // Every other entry of the container must come back byte for byte.
        var entriesBefore = ReadEntries(path);
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, vm.VisibleParameters[0].Name, "SHELL_ROUND_TRIP");
        await SaveAsync(vm);

        _host.Do(() =>
        {
            Assert.True(vm.LastSaveOutcome == "Written", $"{vm.LastSaveOutcome}: {vm.ActionError} {vm.SaveOutcomeSummary}");
            Assert.Equal(0, vm.StagedChangeCount);
        });

        var entriesAfter = ReadEntries(path);
        Assert.Equal(entriesBefore.Keys, entriesAfter.Keys);
        foreach ((string name, byte[] before) in entriesBefore)
        {
            if (!string.Equals(name, "template.xml", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(before.SequenceEqual(entriesAfter[name]), $"{name} must keep its bytes");
            }
        }

        // A second edit of the same file saves again cleanly: the reload after the first save keeps the
        // baseline honest, so the editor does not mistake its own write for an outside edit.
        Stage(vm, vm.VisibleParameters[0].Name, "SECOND_SAVE");
        await SaveAsync(vm);

        _host.Do(() =>
        {
            Assert.True(vm.LastSaveOutcome == "Written", $"second save: {vm.LastSaveOutcome}: {vm.ActionError} {vm.SaveOutcomeSummary}");
            Assert.Equal(0, vm.StagedChangeCount);
            Assert.Equal("SECOND_SAVE", vm.VisibleParameters[0].Declaration.DefaultValue.LogicalValue);
        });

        Assert.Equal(2, Directory.GetFiles(Path.Combine(state, "backups")).Length);
    }

    private static System.Collections.Generic.Dictionary<string, byte[]> ReadEntries(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read);
        var map = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (System.IO.Compression.ZipArchiveEntry entry in zip.Entries)
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            map[entry.FullName] = ms.ToArray();
        }

        return map;
    }

    [Fact]
    public async Task TheSaveButtonIsDisabledWhileASaveIsRunning()
    {
        string templates = Sub("templates");
        WriteTemplate(templates, "t1.dta", TemplateXml(Variable("alpha", "OLD")));
        string state = Sub("state");

        WorkspaceViewModel vm = await ReadAllAsync(templates, state);
        Stage(vm, "alpha", "NEW");

        Task save = Task.CompletedTask;
        _host.Do(() =>
        {
            vm.OpenReviewCommand.Execute(null);
            save = vm.SaveReviewCommand.ExecuteAsync(null);

            // Still the same dispatcher turn, so the write cannot have finished here: the guard has to
            // be holding, and a second save attempt has nowhere to go.
            Assert.True(vm.IsSaving, "the guard flag is set as soon as the save starts");
            Assert.False(vm.SaveReviewCommand.CanExecute(null));
            Assert.False(vm.CanSaveReview);
            Assert.Contains("already running", vm.SaveUnavailableReason, StringComparison.Ordinal);
        });

        await save;

        _host.Do(() =>
        {
            Assert.False(vm.IsSaving);
            Assert.False(vm.SaveReviewCommand.CanExecute(null), "nothing is staged any more, so saving is off again");
        });
    }
}
