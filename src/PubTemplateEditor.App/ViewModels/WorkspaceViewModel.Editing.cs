using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>What the user is trying to do while the editor form holds unapplied changes.</summary>
public enum NavigationReason
{
    ChangeParameter,
    ChangeTemplate,
    ReReadTemplates,
    DiscardStagedChanges,
    CloseWorkspace,
}

/// <summary>How the workspace may continue once unsaved work has been resolved.</summary>
public enum NavigationDecision
{
    KeepEditing,
    ApplyForms,
    DiscardForms,
    DiscardFormsAndStaged,
}

/// <summary>Prompt shown to the user; the view renders it, the view model decides what happens next.</summary>
public sealed record NavigationPrompt(NavigationReason Reason, int UnappliedFieldCount, int StagedChangeCount);

/// <summary>One staged or compared field of the details form.</summary>
public sealed record EditorFieldState(FieldKind Field, string Label, bool CanEdit, bool HasDraft, string? Reason);

public sealed partial class WorkspaceViewModel
{
    private string _detailName = string.Empty;
    private string _detailXmlType = string.Empty;
    private string _detailAccess = string.Empty;
    private string _detailDefaultValue = string.Empty;
    private string _detailDescription = string.Empty;
    private string? _bulkValueDraft;
    private bool _isCompareOpen;
    private bool _isBulkOpen;
    private bool _compareSelectedOnly;
    private bool _canEditAccess;
    private bool _canEditDefaultValue;
    private bool _canEditDescription;
    private string _compareSummary = string.Empty;
    private string _bulkSummary = string.Empty;

    public RelayCommand ApplyEditsCommand { get; private set; } = null!;
    public RelayCommand RevertFormCommand { get; private set; } = null!;
    public RelayCommand DiscardDraftsForParameterCommand { get; private set; } = null!;
    public RelayCommand CompareCommand { get; private set; } = null!;
    public RelayCommand CloseCompareCommand { get; private set; } = null!;
    public RelayCommand OpenBulkCommand { get; private set; } = null!;
    public RelayCommand CloseBulkCommand { get; private set; } = null!;

    /// <summary>Ctrl+Z: drops the most recently staged change of this session.</summary>
    public RelayCommand UndoLastStagedCommand { get; private set; } = null!;

    /// <summary>Esc: closes whichever overlay panel is currently on top.</summary>
    public RelayCommand CloseOverlayCommand { get; private set; } = null!;
    public RelayCommand ConfirmBulkCommand { get; private set; } = null!;
    public RelayCommand DiscardAllStagedCommand { get; private set; } = null!;

    /// <summary>
    /// Hook the view installs to ask the user what to do with unapplied editor changes. The view
    /// model never opens a window itself, and a missing hook means "stay put", which is the safe
    /// default: nothing is discarded and nothing is written.
    /// </summary>
    public Func<NavigationPrompt, NavigationDecision>? NavigationGuard { get; set; }

    // --------------------------------------------------------------- details form

    public string DetailName => _detailName;

    public string DetailXmlType => _detailXmlType;

    public string DetailAccess
    {
        get => _detailAccess;
        set => Set(ref _detailAccess, value);
    }

    public string DetailDefaultValue
    {
        get => _detailDefaultValue;
        set => Set(ref _detailDefaultValue, value);
    }

    public string DetailDescription
    {
        get => _detailDescription;
        set => Set(ref _detailDescription, value);
    }

    /// <summary>Names are read-only in this product; the UI binds IsReadOnly to this instead of a literal.</summary>
    public bool CanEditName => false;

    public bool CanEditAccess => _selectedParameter is not null && _canEditAccess;

    public bool CanEditDefaultValue => _selectedParameter is not null && _canEditDefaultValue;

    public bool CanEditDescription => _selectedParameter is not null && _canEditDescription;

    /// <summary>The only access values the native templates observed; the UI offers these as choices.</summary>
    public IReadOnlyList<string> AccessChoices { get; } = TemplateFieldRules.SupportedAccessChoices;

    /// <summary>Access values the templates never used, so the UI can explain why they are rejected.</summary>
    public IReadOnlyList<string> RejectedAccessExamples { get; } =
        ["Internal", "EXTERNAL", "public", " unknown"];

    /// <summary>Field-by-field state of the form: editability, draft presence, and why not.</summary>
    public IReadOnlyList<EditorFieldState> EditorFields
    {
        get
        {
            ParameterRowItem? row = _selectedParameter;
            if (row is null)
            {
                return [];
            }

            return
            [
                new(FieldKind.Name, "Name", false, false, "Parameter names are read-only."),
                new(FieldKind.Description, "Description", _canEditDescription,
                    Drafts.HasChange(row.File.FilePath, row.DeclarationId, FieldKind.Description),
                    row.Declaration.Description.CapabilityReason),
                new(FieldKind.Access, "Access", _canEditAccess,
                    Drafts.HasChange(row.File.FilePath, row.DeclarationId, FieldKind.Access),
                    row.Declaration.Access.CapabilityReason),
                new(FieldKind.DefaultValue, "Default value", _canEditDefaultValue,
                    Drafts.HasChange(row.File.FilePath, row.DeclarationId, FieldKind.DefaultValue),
                    row.Declaration.DefaultValue.CapabilityReason),
            ];
        }
    }

    /// <summary>True when the form differs from what the session currently holds (baseline + drafts).</summary>
    public bool HasUnappliedFormEdits => UnappliedFieldCount > 0;

    public int UnappliedFieldCount
    {
        get
        {
            ParameterRowItem? row = _selectedParameter;
            if (row is null)
            {
                return 0;
            }

            string path = row.File.FilePath;
            int id = row.DeclarationId;
            int count = 0;
            if (_canEditAccess && !string.Equals(_detailAccess, row.EffectiveAccess, StringComparison.Ordinal))
            {
                count++;
            }

            if (_canEditDefaultValue && !string.Equals(_detailDefaultValue, EffectiveOrEmpty(path, id, FieldKind.DefaultValue, row), StringComparison.Ordinal))
            {
                count++;
            }

            if (_canEditDescription && !string.Equals(_detailDescription, EffectiveOrEmpty(path, id, FieldKind.Description, row), StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }

    public string FormSummary => _selectedParameter is null
        ? "Select a parameter to inspect it."
        : HasUnappliedFormEdits
            ? $"{UnappliedFieldCount} unapplied field change(s) in the form."
            : Drafts.CountForDeclaration(_selectedParameter.File.FilePath, _selectedParameter.DeclarationId) > 0
                ? $"{Drafts.CountForDeclaration(_selectedParameter.File.FilePath, _selectedParameter.DeclarationId)} staged change(s) not saved yet."
                : "No pending change for this parameter.";

    public bool HasFormSummaryHint => _selectedParameter is not null;

    /// <summary>Per-field draft markers so the form can show which values are only staged in memory.</summary>
    public bool DetailHasAccessDraft => HasFieldDraft(FieldKind.Access);

    public bool DetailHasDefaultValueDraft => HasFieldDraft(FieldKind.DefaultValue);

    public bool DetailHasDescriptionDraft => HasFieldDraft(FieldKind.Description);

    public bool DetailHasAnyDraft => _selectedParameter is not null &&
        Drafts.CountForDeclaration(_selectedParameter.File.FilePath, _selectedParameter.DeclarationId) > 0;

    /// <summary>Badge for the whole form: staged drafts, unapplied typing, or both.</summary>
    public bool DetailIsDirty => HasUnappliedFormEdits || DetailHasAnyDraft;

    private bool HasFieldDraft(FieldKind field) =>
        _selectedParameter is not null &&
        Drafts.HasChange(_selectedParameter.File.FilePath, _selectedParameter.DeclarationId, field);

    /// <summary>
    /// Per-field hint under the editor: what a staged value means, or why the field cannot be
    /// changed. The reason comes from the parser, so the UI never invents its own explanation.
    /// </summary>
    public string DetailDefaultValueNote => FieldNote(FieldKind.DefaultValue);

    public string DetailAccessNote => FieldNote(FieldKind.Access);

    public string DetailDescriptionNote => FieldNote(FieldKind.Description);

    private string FieldNote(FieldKind field)
    {
        if (_selectedParameter is null)
        {
            return string.Empty;
        }

        if (HasFieldDraft(field))
        {
            return "Staged in memory; nothing is written until the save review.";
        }

        EditorFieldState? state = EditorFields.FirstOrDefault(f => f.Field == field);
        if (state is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(state.Reason))
        {
            return state.Reason!;
        }

        return field == FieldKind.Access
            ? $"Only {TemplateFieldRules.AccessVocabulary} are accepted, exactly as written."
            : "Values are kept character for character; leading and trailing spaces belong to the value.";
    }

    // ------------------------------------------------------------- staged counters

    public int StagedChangeCount => Drafts.Count;

    public bool HasStagedChanges => Drafts.Count > 0;

    public int DirtyTemplateCount => Drafts.CountByFile().Count;

    public string StagedSummary => Drafts.Count == 0
        ? "No staged changes"
        : $"{Drafts.Count} staged change(s) in {DirtyTemplateCount} template(s)";

    // --------------------------------------------------------------------- compare

    public bool IsCompareOpen => _isCompareOpen;

    public bool CompareSelectedOnly
    {
        get => _compareSelectedOnly;
        set
        {
            if (Set(ref _compareSelectedOnly, value))
            {
                RebuildCompareRows();
                RebuildBulkTargets();
            }
        }
    }

    public ObservableCollection<ComparisonRowItem> CompareRows { get; } = [];

    public string CompareSummary
    {
        get => _compareSummary;
        private set
        {
            if (Set(ref _compareSummary, value))
            {
                Raise(nameof(HasCompareSummary));
            }
        }
    }

    public bool HasCompareSummary => !string.IsNullOrEmpty(_compareSummary);

    // ------------------------------------------------------------------------ bulk

    public bool IsBulkOpen => _isBulkOpen;

    public ObservableCollection<BulkTargetRowItem> BulkTargets { get; } = [];

    public string? BulkValueDraft
    {
        get => _bulkValueDraft;
        set => Set(ref _bulkValueDraft, value);
    }

    public string BulkSummary
    {
        get => _bulkSummary;
        private set
        {
            if (Set(ref _bulkSummary, value))
            {
                Raise(nameof(HasBulkSummary));
            }
        }
    }

    public int BulkEligibleCount => BulkTargets.Count(t => t.IsSelectable);

    public bool HasBulkSummary => !string.IsNullOrEmpty(_bulkSummary);

    public int SelectedBulkTargetCount => BulkTargets.Count(t => t.IsChecked && t.IsSelectable);

    // ---------------------------------------------------------------- editing flow

    /// <summary>Loads the details form with the session's current (baseline + staged) values.</summary>
    private void LoadEditorForm()
    {
        ParameterRowItem? row = _selectedParameter;
        if (row is null)
        {
            _detailName = string.Empty;
            _detailXmlType = string.Empty;
            _detailAccess = string.Empty;
            _detailDefaultValue = string.Empty;
            _detailDescription = string.Empty;
            _canEditAccess = _canEditDefaultValue = _canEditDescription = false;
            RaiseAllEditorProperties();
            return;
        }

        VariableDeclaration declaration = row.Declaration;
        _detailName = declaration.ExactName ?? string.Empty;
        _detailXmlType = declaration.XmlTypeProperty ?? "(no type property)";
        _canEditAccess = declaration.Access.Capability == FieldCapability.Editable;
        _canEditDefaultValue = declaration.DefaultValue.Capability == FieldCapability.Editable;
        _canEditDescription = declaration.Description.Capability == FieldCapability.Editable;

        _detailAccess = row.EffectiveAccess ?? string.Empty;
        _detailDefaultValue = row.EffectiveDefaultValue ?? string.Empty;
        _detailDescription = row.EffectiveDescription ?? string.Empty;

        RaiseAllEditorProperties();
    }

    private void RaiseAllEditorProperties()
    {
        foreach (string property in new[]
                 {
                     nameof(DetailName),
                     nameof(DetailXmlType),
                     nameof(DetailAccess),
                     nameof(DetailDefaultValue),
                     nameof(DetailDescription),
                     nameof(CanEditAccess),
                     nameof(CanEditDefaultValue),
                     nameof(CanEditDescription),
                     nameof(EditorFields),
                     nameof(HasUnappliedFormEdits),
                     nameof(UnappliedFieldCount),
                     nameof(FormSummary),
                     nameof(DetailReadOnlyReason),
                     nameof(HasDetailReadOnlyReason),
                     nameof(DetailHasAccessDraft),
                     nameof(DetailHasDefaultValueDraft),
                     nameof(DetailHasDescriptionDraft),
                     nameof(DetailHasAnyDraft),
                     nameof(DetailIsDirty),
                     nameof(DetailDefaultValueNote),
                     nameof(DetailAccessNote),
                     nameof(DetailDescriptionNote),
                 })
        {
            Raise(property);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Stages every field of the form that differs from the session state. Names are never staged;
    /// unsupported or missing fields are reported instead of silently dropped.
    /// </summary>
    private void ApplyEditorForm()
    {
        ParameterRowItem? row = _selectedParameter;
        if (row is null)
        {
            return;
        }

        VariableDeclaration declaration = row.Declaration;
        string path = row.File.FilePath;
        var staged = new List<string>();
        var rejected = new List<string>();

        foreach (EditorFieldState field in EditorFields)
        {
            if (field.Field == FieldKind.Name)
            {
                continue;
            }

            string proposed = field.Field switch
            {
                FieldKind.Access => _detailAccess,
                FieldKind.Description => _detailDescription,
                _ => _detailDefaultValue,
            };

            string current = EffectiveOrEmpty(path, row.DeclarationId, field.Field, row);
            if (string.Equals(proposed, current, StringComparison.Ordinal))
            {
                continue;
            }

            StageResult result = Drafts.Stage(path, declaration, field.Field, proposed);
            switch (result.Status)
            {
                case StageStatus.Staged:
                    staged.Add(field.Label);
                    break;
                case StageStatus.ClearedAsNoOp:
                    break;
                default:
                    rejected.Add($"{field.Label}: {result.Reason}");
                    break;
            }
        }

        if (staged.Count > 0)
        {
            ActionNotice = $"Staged {staged.Count} field change(s) for '{declaration.ExactName}': " +
                           string.Join(", ", staged) + ". Nothing is written until the save review.";
        }

        if (rejected.Count > 0)
        {
            ActionError = "Some fields could not be staged. " + string.Join(" ", rejected);
        }
        else if (staged.Count == 0 && UnappliedFieldCount > 0)
        {
            ActionError = "The form still differs from the session state, but no field could be staged.";
        }

        RefreshAfterDraftChange();
    }

    private void RevertEditorForm()
    {
        LoadEditorForm();
        ActionNotice = null;
        RefreshReviewAndActionStates();
    }

    private void DiscardDraftsForSelectedParameter()
    {
        ParameterRowItem? row = _selectedParameter;
        if (row is null)
        {
            return;
        }

        int removed = Drafts.DiscardForDeclaration(row.File.FilePath, row.DeclarationId);
        ActionNotice = removed == 0
            ? "No staged change to discard for this parameter."
            : $"Discarded {removed} staged change(s) for '{row.Name}'.";
        RefreshAfterDraftChange();
    }

    // -------------------------------------------------------------- compare / bulk

    private void OpenCompare()
    {
        if (_selectedParameter is null)
        {
            ActionError = "Select a parameter to compare.";
            return;
        }

        _isCompareOpen = true;
        RebuildCompareRows();
        Raise(nameof(IsCompareOpen));
    }

    private void CloseCompare()
    {
        _isCompareOpen = false;
        CompareRows.Clear();
        CompareSummary = string.Empty;
        Raise(nameof(IsCompareOpen));
    }

    private void RebuildCompareRows()
    {
        CompareRows.Clear();
        if (!_isCompareOpen || _selectedParameter is null || _activeFile is null)
        {
            return;
        }

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            _activeFile.FilePath,
            _selectedParameter.Declaration,
            CandidateRecords,
            Drafts,
            BulkScope);

        foreach (TemplateComparison row in rows)
        {
            CompareRows.Add(new ComparisonRowItem(row));
        }

        CompareSummary = $"{rows.Count(r => r.State != CompareState.Self)} template(s) checked · " +
                         $"{rows.Count(r => r.State == CompareState.Different)} different · " +
                         $"{rows.Count(r => r.State == CompareState.Missing)} without this parameter · " +
                         $"{rows.Count(r => r.State is CompareState.Ineligible or CompareState.NotLoaded)} not changeable · " +
                         $"{rows.Count(r => r.HasUnsavedDrafts)} with staged changes";
    }

    private void OpenBulk()
    {
        if (_selectedParameter is null)
        {
            ActionError = "Select a parameter to apply a value to templates.";
            return;
        }

        _isBulkOpen = true;
        _bulkValueDraft = _selectedParameter.EffectiveDefaultValue ?? string.Empty;
        RebuildBulkTargets();
        Raise(nameof(IsBulkOpen));
        Raise(nameof(BulkValueDraft));
    }

    private void CloseBulk()
    {
        _isBulkOpen = false;
        BulkTargets.Clear();
        BulkSummary = string.Empty;
        _bulkValueDraft = null;
        Raise(nameof(IsBulkOpen));
        Raise(nameof(BulkValueDraft));
    }

    private void RebuildBulkTargets()
    {
        BulkTargets.Clear();
        if (!_isBulkOpen || _selectedParameter is null)
        {
            return;
        }

        foreach (BulkTarget target in BulkPlan.Plan(_selectedParameter.Name, CandidateRecords, Drafts, BulkScope))
        {
            BulkTargets.Add(new BulkTargetRowItem(target)
            {
                IsChecked = target.Status == BulkTargetStatus.Eligible,
            });
        }

        BulkSummary = $"{BulkTargets.Count(t => t.IsSelectable)} of {BulkTargets.Count} template(s) can receive this value.";
        Raise(nameof(SelectedBulkTargetCount));
        Raise(nameof(BulkEligibleCount));
    }

    private void ConfirmBulkApply()
    {
        if (_selectedParameter is null)
        {
            return;
        }

        string proposed = _bulkValueDraft ?? string.Empty;
        var selected = BulkTargets.Where(t => t.IsChecked).Select(t => t.Target).ToList();
        if (selected.Count == 0)
        {
            ActionError = "No target template is selected for this value.";
            return;
        }

        IReadOnlyList<(BulkTarget Target, StageResult Result)> outcomes =
            BulkPlan.StageValues(_selectedParameter.Name, proposed, selected, CandidateRecords, Drafts);

        int staged = outcomes.Count(o => o.Result.Status == StageStatus.Staged);
        int cleared = outcomes.Count(o => o.Result.Status == StageStatus.ClearedAsNoOp);
        string[] rejected = outcomes
            .Where(o => o.Result.Status == StageStatus.Rejected)
            .Select(o => o.Result.Reason ?? "the change was refused")
            .ToArray();

        var parts = new List<string> { $"Staged the default value in {staged} template(s)." };
        if (cleared > 0)
        {
            parts.Add($"{cleared} target(s) already held that value and needed no change.");
        }

        if (rejected.Length > 0)
        {
            ActionError = string.Join(" ", rejected);
        }

        ActionNotice = string.Join(" ", parts);
        CloseBulk();
        RefreshAfterDraftChange();
    }

    /// <summary>
    /// Templates in scope for compare and bulk: the read selection when the user narrowed it,
    /// otherwise every template the folder listing offered in this session.
    /// </summary>
    private HashSet<string> BulkScope => CompareSelectedOnly
        ? AllFiles.Where(f => f.IsSelectedForRead).Select(f => f.FilePath).ToHashSet(StringComparer.Ordinal)
        : AllFiles.Select(f => f.FilePath).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Records for compare and bulk planning. A listed template that was never read is represented
    /// by a NotRead record, so the panels can name it and explain why it cannot receive a change.
    /// </summary>
    private IReadOnlyList<TemplateRecord> CandidateRecords =>
        AllFiles
            .Where(f => f.IsListable)
            .Select(f => f.Record ?? new TemplateRecord
            {
                FilePath = f.FilePath,
                ReadState = TemplateReadState.NotRead,
                ReadError = "This template has not been read in this session.",
            })
            .ToList();

    private string EffectiveOrEmpty(string path, int declarationId, FieldKind field, ParameterRowItem row) =>
        Drafts.EffectiveValue(path, row.Declaration, field) ?? string.Empty;

    // ----------------------------------------------------------------- navigation

    /// <summary>
    /// Gate for anything that would throw away work: switching template or parameter, re-reading,
    /// discarding everything, closing. Returns true when the workspace may continue.
    /// </summary>
    public bool ConfirmNavigation(NavigationReason reason)
    {
        bool formsDirty = HasUnappliedFormEdits;
        bool draftsDirty = reason is NavigationReason.ReReadTemplates or NavigationReason.CloseWorkspace or NavigationReason.DiscardStagedChanges
            && HasStagedChanges;

        if (!formsDirty && !draftsDirty)
        {
            return true;
        }

        NavigationDecision decision = NavigationGuard?.Invoke(new NavigationPrompt(reason, UnappliedFieldCount, Drafts.Count))
            ?? NavigationDecision.KeepEditing;

        switch (decision)
        {
            case NavigationDecision.ApplyForms:
                ApplyEditorForm();
                return !HasUnappliedFormEdits;

            case NavigationDecision.DiscardForms:
                RevertEditorForm();
                return true;

            case NavigationDecision.DiscardFormsAndStaged:
                RevertEditorForm();
                Drafts.Clear();
                RefreshAfterDraftChange();
                ActionNotice = "Discarded the form changes and every staged change of this session.";
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Window close hook: the view cancels the close when this returns false, so staged work of the
    /// session is never dropped silently by an accidental Alt+F4.
    /// </summary>
    public bool ConfirmClose() => ConfirmNavigation(NavigationReason.CloseWorkspace);

    private void DiscardAllStaged()
    {
        if (!ConfirmNavigation(NavigationReason.DiscardStagedChanges))
        {
            return;
        }

        if (Drafts.HasAny)
        {
            ActionNotice = $"Kept {Drafts.Count} staged change(s); choose discard again to drop them.";
            return;
        }

        ActionNotice = "All staged changes of this session were discarded. No file was written.";
        RefreshAfterDraftChange();
    }

    /// <summary>
    /// Drops the change that was staged last, which is the one a user means by "undo" after editing.
    /// Undo only removes a proposal from this session - it never writes, and it cannot undo a save that
    /// already happened, because a completed save has left the session and is reported in the review.
    /// </summary>
    private void UndoLastStaged()
    {
        DraftChange? last = Drafts.MostRecent;
        if (last is null)
        {
            ActionNotice = "Nothing to undo: this session has no staged change.";
            return;
        }

        Drafts.Remove(last.Key);
        RefreshAfterDraftChange();
        ActionNotice = $"Undid the staged {TemplateFieldRules.DisplayName(last.Field)} of "
                     + $"'{last.DeclarationName}'. Nothing was written to a template.";
    }

    /// <summary>Esc closes the panel that is on top, one step at a time.</summary>
    private void CloseOpenOverlay()
    {
        if (IsReviewOpen)
        {
            CloseReview();
        }
        else if (IsBulkOpen)
        {
            CloseBulk();
        }
        else if (IsCompareOpen)
        {
            CloseCompare();
        }
    }

    // -------------------------------------------------------------------- refresh

    /// <summary>After staging or discarding: refresh rows, counters and the open panels.</summary>
    private void RefreshAfterDraftChange()
    {
        foreach (ParameterRowItem row in AllParameters)
        {
            row.RefreshDraftState();
        }

        foreach (TemplateItem file in AllFiles)
        {
            file.RefreshDraftState();
        }

        // Keep the form on the user's values, but re-derive the draft markers and dirty counts.
        RaiseAllEditorProperties();
        RebuildCompareRows();
        RebuildBulkTargets();
        // The review list mirrors the staged set at all times, not only while the panel is open. The
        // save offer is read from those rows, and a list that was never rebuilt made the footer claim
        // there was nothing to write while real changes were staged.
        RebuildReviewRows();

        RefreshReviewAndActionStates();
        // The review footer reads CanSaveReview / SaveUnavailableReason as ordinary bindings, so staging
        // has to raise them even while the review panel is closed.
        RaiseReviewSaveState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshReviewAndActionStates()
    {
        foreach (string property in new[]
                 {
                     nameof(StagedChangeCount),
                     nameof(HasStagedChanges),
                     nameof(DirtyTemplateCount),
                     nameof(StagedSummary),
                     nameof(SelectedBulkTargetCount),
                 })
        {
            Raise(property);
        }
    }

    private void InitEditingCommands()
    {
        ApplyEditsCommand = new RelayCommand(ApplyEditorForm, () => !IsBusy && HasUnappliedFormEdits);
        RevertFormCommand = new RelayCommand(RevertEditorForm, () => !IsBusy && HasUnappliedFormEdits);
        DiscardDraftsForParameterCommand = new RelayCommand(DiscardDraftsForSelectedParameter,
            () => !IsBusy && _selectedParameter is not null &&
                  Drafts.CountForDeclaration(_selectedParameter.File.FilePath, _selectedParameter.DeclarationId) > 0);
        CompareCommand = new RelayCommand(OpenCompare, () => !IsBusy && _selectedParameter is not null);
        CloseCompareCommand = new RelayCommand(CloseCompare, () => IsCompareOpen);
        OpenBulkCommand = new RelayCommand(OpenBulk, () => !IsBusy && _selectedParameter is not null);
        CloseBulkCommand = new RelayCommand(CloseBulk, () => IsBulkOpen);
        ConfirmBulkCommand = new RelayCommand(ConfirmBulkApply,
            () => !IsBusy && IsBulkOpen && SelectedBulkTargetCount > 0 && _bulkValueDraft is not null);
        DiscardAllStagedCommand = new RelayCommand(DiscardAllStaged,
            () => !IsBusy && (HasStagedChanges || HasUnappliedFormEdits));
        UndoLastStagedCommand = new RelayCommand(UndoLastStaged, () => !IsBusy && Drafts.HasAny);
        CloseOverlayCommand = new RelayCommand(CloseOpenOverlay,
            () => IsReviewOpen || IsBulkOpen || IsCompareOpen);
    }
}
