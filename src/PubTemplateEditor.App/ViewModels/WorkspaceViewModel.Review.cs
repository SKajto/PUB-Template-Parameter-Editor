using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// Review step of the save workflow. Staging, comparing and bulk editing never touch a file; this is
/// the single place where the session's staged changes are listed as a whole, per field, and the only
/// place where a write can start. Only ticked rows are written, one file at a time, and a staged change
/// is dropped from the session only once its own file is confirmed on disk.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly TemplateSaveService _saver;
    private bool _isReviewOpen;
    private bool _isSaving;

    public RelayCommand OpenReviewCommand { get; private set; } = null!;

    public RelayCommand CloseReviewCommand { get; private set; } = null!;

    public RelayCommand SelectAllReviewCommand { get; private set; } = null!;

    public RelayCommand SelectNoneReviewCommand { get; private set; } = null!;

    public RelayCommand DiscardSelectedReviewCommand { get; private set; } = null!;

    public AsyncRelayCommand SaveReviewCommand { get; private set; } = null!;

    public ObservableCollection<StagedReviewRow> ReviewRows { get; } = [];

    /// <summary>The same staged changes, one group per template file, for the review cards.</summary>
    public ObservableCollection<ReviewFileGroup> ReviewGroups { get; } = [];

    public bool IsReviewOpen => _isReviewOpen;

    /// <summary>A save in flight blocks a second one; the command and this flag are the same gate.</summary>
    public bool IsSaving => _isSaving;

    /// <summary>
    /// The one rule that decides whether a save may start. <see cref="CanSaveReview"/> and
    /// <see cref="SaveUnavailableReason"/> read the same evaluation, so the button and the sentence under
    /// it can never disagree - and every change to the staged set raises both, so neither goes stale.
    /// </summary>
    private (bool CanSave, string Why) SaveGate()
    {
        if (_isSaving)
        {
            return (false, "A save is already running. Wait for it to finish.");
        }

        if (IsBusy)
        {
            return (false, "Templates are being read; saving waits until that finishes.");
        }

        if (ReviewRows.Count == 0)
        {
            return (false, "Nothing is staged. Edit a value, apply it, and the change appears here.");
        }

        if (!ReviewRows.Any(r => !r.IsNoOp))
        {
            return (false, "Every staged change already holds the value that is in the file, so there is nothing to write.");
        }

        if (!ReviewRows.Any(r => r.IsSelected))
        {
            return (false, "Tick at least one change in the list to enable saving.");
        }

        return (true, "Only the ticked changes are written. A template is replaced only after the new bytes "
                    + "were read back and checked field by field, and the copy that was there before is kept "
                    + "in the app's backup folder.");
    }

    public bool CanSaveReview => SaveGate().CanSave;

    /// <summary>
    /// Why saving is or is not offered right now. Shown under the review list and as the save button's
    /// tooltip, including while the button is disabled.
    /// </summary>
    public string SaveUnavailableReason => SaveGate().Why;

    /// <summary>Per-file result of the last save attempt, shown as a card in the review panel.</summary>
    public string SaveOutcomeSummary { get; private set; } = string.Empty;

    public bool HasSaveOutcome => LastSaveOutcome.Length > 0;

    /// <summary>
    /// "Written" when every file of the last attempt was confirmed, "Rejected" when none of them was,
    /// "Partly written" for a mixed batch, "Nothing written" when the attempt never reached a file, and
    /// "" before any save was attempted.
    /// </summary>
    public string LastSaveOutcome { get; private set; } = string.Empty;

    /// <summary>Drives the colour of the result card: success and problem cannot share one look.</summary>
    public bool SaveOutcomeIsOk => LastSaveOutcome == "Written";

    public bool SaveOutcomeIsProblem => LastSaveOutcome is "Rejected" or "Partly written" or "Nothing written";

    public string SaveOutcomeTitle => LastSaveOutcome switch
    {
        "Written" => $"Saved {lastFieldsWritten} field(s) in {lastFilesWritten} template file(s)",
        "Partly written" => $"Partly saved: {lastFieldsWritten} field(s) in {lastFilesWritten} file(s), the rest were not written",
        "Rejected" => "Nothing was written",
        "Nothing written" => "Nothing was written",
        _ => string.Empty,
    };

    private int lastFieldsWritten;
    private int lastFilesWritten;

    /// <summary>Where the copy that was replaced is kept, shown with the result of an attempt.</summary>
    public string BackupDirectoryHint =>
        $"Replaced copies are kept in: {_saver.BackupDirectory}";

    /// <summary>Records an attempt that never reached a file, so the panel always says what happened.</summary>
    private void ReportUnwritten(string reason)
    {
        SaveOutcomeSummary = reason;
        LastSaveOutcome = "Nothing written";
        lastFieldsWritten = 0;
        lastFilesWritten = 0;
        RaiseReviewSaveState();
    }

    public int SelectedReviewCount => ReviewRows.Count(r => r.IsSelected);

    public string ReviewSummary => ReviewRows.Count == 0
        ? "Nothing staged"
        : $"{SelectedReviewCount} of {ReviewRows.Count} staged change(s) selected, "
          + $"{ReviewRows.Select(r => r.FilePath).Distinct().Count()} template(s)";

    /// <summary>
    /// Keys the user unticked. Selection is remembered per change key instead of "select everything
    /// when the set is empty", so a rebuild caused by other staging cannot silently re-tick rows.
    /// </summary>
    private readonly HashSet<ChangeKey> _reviewDeselected = [];

    private void InitReviewCommands()
    {
        OpenReviewCommand = new RelayCommand(OpenReview, () => !IsBusy && HasStagedChanges);
        CloseReviewCommand = new RelayCommand(CloseReview);
        SelectAllReviewCommand = new RelayCommand(() => SetAllReviewSelection(true), () => ReviewRows.Count > 0);
        SelectNoneReviewCommand = new RelayCommand(() => SetAllReviewSelection(false), () => ReviewRows.Count > 0);
        DiscardSelectedReviewCommand = new RelayCommand(DiscardSelectedReview,
            () => !IsBusy && ReviewRows.Any(r => r.IsSelected));
        SaveReviewCommand = new AsyncRelayCommand(SaveReviewAsync, () => CanSaveReview);
    }

    private void OpenReview()
    {
        _reviewDeselected.Clear(); // a new review session starts fully ticked
        RebuildReviewRows();
        _isReviewOpen = true;
        Raise(nameof(IsReviewOpen));
        RaiseReviewSaveState();
    }

    private void CloseReview()
    {
        if (!_isReviewOpen)
        {
            return;
        }

        _isReviewOpen = false;
        Raise(nameof(IsReviewOpen));
    }

    /// <summary>
    /// Rebuilds the list while keeping the user's tick marks: staging or discarding while the review
    /// is open must not silently select everything again.
    /// </summary>
    private void RebuildReviewRows()
    {
        ReviewRows.Clear();
        // Grouped by file first: the review cards are read as "what happens to this template", and a
        // batch is written one file at a time, so the file is the unit a reviewer reasons about.
        foreach (DraftChange change in Drafts.Changes
                     .OrderBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(c => c.DeclarationName, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(c => c.Field))
        {
            var row = new StagedReviewRow(change);
            row.IsSelected = !_reviewDeselected.Contains(change.Key);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(StagedReviewRow.IsSelected))
                {
                    return;
                }

                if (row.IsSelected)
                {
                    _reviewDeselected.Remove(row.Key);
                }
                else
                {
                    _reviewDeselected.Add(row.Key);
                }

                RaiseReviewSaveState();
            };
            ReviewRows.Add(row);
        }

        // Same row objects, seen per file. Ticking, discarding and saving keep working on one set.
        ReviewGroups.Clear();
        foreach (IGrouping<string, StagedReviewRow> group in ReviewRows.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            ReviewGroups.Add(new ReviewFileGroup(group.Key, group.ToList()));
        }

        _reviewDeselected.RemoveWhere(key => ReviewRows.All(r => r.Key != key));
        RaiseReviewSaveState();
    }

    /// <summary>
    /// The review footer ("can I save, and if not why") is plain bindings, not command state, so every
    /// staging, ticking or grouping change has to raise it. Command requery alone would leave the sentence
    /// at whatever it was when the panel was first built - which is how an open review could claim there
    /// was nothing to write while real changes were listed above it.
    /// </summary>
    private void RaiseReviewSaveState()
    {
        foreach (string name in new[]
                 {
                     nameof(ReviewRows), nameof(ReviewGroups), nameof(SelectedReviewCount),
                     nameof(ReviewSummary), nameof(CanSaveReview), nameof(SaveUnavailableReason),
                     nameof(HasSaveOutcome), nameof(LastSaveOutcome), nameof(SaveOutcomeSummary),
                     nameof(SaveOutcomeTitle), nameof(SaveOutcomeIsOk), nameof(SaveOutcomeIsProblem),
                     nameof(IsSaving),
                 })
        {
            Raise(name);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void SetAllReviewSelection(bool value)
    {
        _reviewDeselected.Clear();
        if (!value)
        {
            foreach (StagedReviewRow row in ReviewRows)
            {
                _reviewDeselected.Add(row.Key);
            }
        }

        foreach (StagedReviewRow row in ReviewRows)
        {
            row.IsSelected = value;
        }

        RaiseReviewSaveState();
    }

    /// <summary>
    /// Removes the selected drafts from the session. Nothing is written: "do not save this field"
    /// means "forget the proposal", which is the only safe meaning before the write step exists.
    /// </summary>
    /// <summary>
    /// Writes the ticked changes of the review list. Nothing is written as a single all-or-nothing
    /// batch: each template is checked, patched, backed up and replaced on its own, and the result of
    /// every file is reported separately. A staged change leaves the session only when its own file is
    /// confirmed; everything else stays editable so it can be fixed and tried again.
    /// </summary>
    private async Task SaveReviewAsync(CancellationToken token)
    {
        if (_isSaving)
        {
            ActionNotice = "A save is already running.";
            return;
        }

        List<StagedReviewRow> chosen = [.. ReviewRows.Where(r => r.IsSelected && !r.IsNoOp)];
        if (chosen.Count == 0)
        {
            ActionNotice = "Tick at least one change to save.";
            ReportUnwritten("Tick at least one change in the list, then save again.");
            return;
        }

        List<(string FilePath, List<ChangeKey> Keys, SaveRequest Request)> work = [];
        List<string> unusable = [];
        foreach (IGrouping<string, StagedReviewRow> file in chosen.GroupBy(r => r.FilePath, StringComparer.Ordinal))
        {
            TemplateRecord? record = CandidateRecords.FirstOrDefault(r => r.FilePath == file.Key);
            if (record is null || record.ReadState != TemplateReadState.Loaded)
            {
                unusable.Add($"{Path.GetFileName(file.Key)}: the editor no longer holds a read copy of this template.");
                continue;
            }

            List<DraftChange> changes = [.. file.Select(r => Drafts.Changes.FirstOrDefault(c => c.Key == r.Key)).Where(c => c is not null).Cast<DraftChange>()];
            if (changes.Count == 0)
            {
                continue;
            }

            work.Add((file.Key, [.. changes.Select(c => c.Key)], new SaveRequest(record, changes)));
        }

        if (work.Count == 0)
        {
            string why = unusable.Count > 0 ? string.Join(" ", unusable) : "Nothing left to save.";
            ActionError = why;
            ReportUnwritten(why);
            return;
        }

        _isSaving = true;
        RaiseReviewSaveState();
        ActionNotice = null;
        ActionError = null;
        SaveOutcomeSummary = string.Empty;

        List<SaveFileResult> results;
        try
        {
            results = await Task.Run(
                () => work.Select(w => w.Request).Select(r => _saver.SaveOne(r, token)).ToList(),
                token);
        }
        catch (OperationCanceledException)
        {
            _isSaving = false;
            RaiseReviewSaveState();
            ActionNotice = "The save was cancelled. Files already written stay written; the rest are untouched.";
            return;
        }
        finally
        {
            if (_isSaving)
            {
                _isSaving = false;
            }
        }

        List<string> writtenPaths = [];
        int fields = 0;
        List<string> problems = [.. unusable];

        for (int i = 0; i < work.Count; i++)
        {
            SaveFileResult result = results[i];
            if (result.Outcome == SaveOutcome.Written)
            {
                foreach (ChangeKey key in work[i].Keys)
                {
                    Drafts.Remove(key);
                }

                writtenPaths.Add(work[i].FilePath);
                fields += result.FieldsWritten;
            }
            else
            {
                problems.Add($"{Path.GetFileName(result.FilePath)}: {result.Message}");
            }
        }

        lastFieldsWritten = fields;
        lastFilesWritten = writtenPaths.Count;

        // The files that changed are read again so the editor's baseline matches what it just wrote;
        // until that happens a second save of the same row would look like an outside edit.
        await ReloadWrittenTemplatesAsync(writtenPaths, token);

        SaveOutcomeSummary = string.Join(Environment.NewLine,
            [.. results.Select(r => $"{Path.GetFileName(r.FilePath)}: {r.Message}")]);
        LastSaveOutcome = (writtenPaths.Count, problems.Count - unusable.Count) switch
        {
            (> 0, 0) => "Written",
            (0, > 0) => "Rejected",
            (> 0, > 0) => "Partly written",
            _ => LastSaveOutcome,
        };
        Raise(nameof(HasSaveOutcome));
        Raise(nameof(LastSaveOutcome));
        Raise(nameof(SaveOutcomeTitle));
        Raise(nameof(SaveOutcomeIsOk));
        Raise(nameof(SaveOutcomeIsProblem));
        RebuildReviewRows();
        RefreshAfterDraftChange();

        if (writtenPaths.Count > 0)
        {
            ActionNotice = $"Saved {fields} field(s) in {writtenPaths.Count} template(s). "
                + "Original copies are in the app's backup folder; each file was verified after writing."
                + (problems.Count > 0 ? $" {problems.Count} template(s) were not written." : string.Empty);
        }

        if (problems.Count > 0)
        {
            ActionError = string.Join(" ", problems);
        }

        // The panel deliberately stays open after an attempt: the result card, the backup folder and any
        // file that was not written are the proof the user needs, and closing the panel throws it away.
        // Esc or Close ends the review; new staging reopens it with fresh rows.
    }

    /// <summary>
    /// Re-reads only the templates that were just written, so a following save, compare or bulk action
    /// works from the bytes that are now on disk. Other loaded templates keep their records and drafts.
    /// </summary>
    private async Task ReloadWrittenTemplatesAsync(IReadOnlyList<string> paths, CancellationToken token)
    {
        // Re-reading replaces the parameter rows and refills the details form, so typing that was never
        // applied has to be put back afterwards - losing it would be losing the user's work.
        IReadOnlyDictionary<FieldKind, string>? typing = CaptureUnappliedFormEdits();

        foreach (string path in paths)
        {
            TemplateItem? item = AllFiles.FirstOrDefault(f => f.FilePath == path);
            if (item is null)
            {
                continue;
            }

            TemplateRecord record = await Task.Run(() => _reader.Load(item.FilePath, token), token);
            item.Record = record;
            item.Status = record.ReadState == TemplateReadState.Loaded
                ? $"{record.Declarations.Count} parameter(s), saved"
                : $"Error: {record.ReadError}";
            if (ReferenceEquals(item, ActiveFile))
            {
                LoadParameters();
            }
        }

        RefreshCounters();
        RestoreUnappliedFormEdits(typing);
    }

    private void DiscardSelectedReview()
    {
        int removed = 0;
        foreach (StagedReviewRow row in ReviewRows.Where(r => r.IsSelected).ToList())
        {
            if (Drafts.Remove(row.Key))
            {
                removed++;
            }
        }

        RebuildReviewRows();
        RefreshAfterDraftChange();
        if (ReviewRows.Count == 0)
        {
            CloseReview();
        }
        else
        {
            ActionNotice = $"Discarded {removed} staged change(s) of this session. No template was written.";
        }

        RaiseReviewSaveState();
    }
}
