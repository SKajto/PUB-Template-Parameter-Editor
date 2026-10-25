using System.Collections.ObjectModel;
using System.Windows.Input;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// Review step of the save workflow. Staging, comparing and bulk editing never touch a file; this is
/// the single place where the session's staged changes are listed as a whole, per field, before they
/// could be written. The write path itself is milestone S4, so the primary action stays disabled and
/// says why rather than pretending to save.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private bool _isReviewOpen;

    public RelayCommand OpenReviewCommand { get; private set; } = null!;

    public RelayCommand CloseReviewCommand { get; private set; } = null!;

    public RelayCommand SelectAllReviewCommand { get; private set; } = null!;

    public RelayCommand SelectNoneReviewCommand { get; private set; } = null!;

    public RelayCommand DiscardSelectedReviewCommand { get; private set; } = null!;

    public RelayCommand SaveReviewCommand { get; private set; } = null!;

    public ObservableCollection<StagedReviewRow> ReviewRows { get; } = [];

    public bool IsReviewOpen => _isReviewOpen;

    /// <summary>Write-back needs the source-span patcher, backups and the recovery journal (S4).</summary>
    public bool CanSaveReview => false;

    public string SaveUnavailableReason =>
        "Writing templates is not part of this build: the review list is shown first, the save step "
        + "arrives with the source-preserving patcher, backups and recovery journal.";

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
        // Present but disabled: the enabled button belongs to the write milestone, and a silent
        // no-op would be worse than a button that explains itself.
        SaveReviewCommand = new RelayCommand(() => { }, () => false);
    }

    private void OpenReview()
    {
        _reviewDeselected.Clear(); // a new review session starts fully ticked
        RebuildReviewRows();
        _isReviewOpen = true;
        Raise(nameof(IsReviewOpen));
        Raise(nameof(ReviewSummary));
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
        foreach (DraftChange change in Drafts.Changes
                     .OrderBy(c => c.DeclarationName, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase)
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

                Raise(nameof(SelectedReviewCount));
                Raise(nameof(ReviewSummary));
            };
            ReviewRows.Add(row);
        }

        _reviewDeselected.RemoveWhere(key => ReviewRows.All(r => r.Key != key));
        Raise(nameof(SelectedReviewCount));
        Raise(nameof(ReviewSummary));
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

        Raise(nameof(SelectedReviewCount));
        Raise(nameof(ReviewSummary));
    }

    /// <summary>
    /// Removes the selected drafts from the session. Nothing is written: "do not save this field"
    /// means "forget the proposal", which is the only safe meaning before the write step exists.
    /// </summary>
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
    }
}
