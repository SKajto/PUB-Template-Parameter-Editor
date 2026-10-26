using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// One template row of the compare panel. The values shown are the session's effective values, so a
/// row can carry a draft marker: comparing "what I am about to save" against the other templates is
/// the whole point of the panel.
/// </summary>
public sealed class ComparisonRowItem : ViewModelBase
{
    public ComparisonRowItem(TemplateComparison comparison)
    {
        Comparison = comparison;
    }

    public TemplateComparison Comparison { get; }

    public string FilePath => Comparison.FilePath;

    public string FileName => Comparison.FileName;

    public CompareState State => Comparison.State;

    public bool IsSelf => Comparison.State == CompareState.Self;

    public string StateLabel => Comparison.State switch
    {
        CompareState.Self => "active template",
        CompareState.Same => "same",
        CompareState.Different => "different",
        CompareState.Missing => "parameter not declared",
        CompareState.Ineligible => "not changeable",
        CompareState.NotLoaded => "not read in this session",
        _ => Comparison.State.ToString(),
    };

    /// <summary>Explains ambiguous, unreadable or unread templates instead of hiding them.</summary>
    public string Reason => Comparison.Reason ?? string.Empty;

    public bool HasReason => Comparison.Reason is not null;

    public bool HasUnsavedDrafts => Comparison.HasUnsavedDrafts;

    public bool ShowFieldDiffs => Comparison.Differences.Count > 0;

    public IReadOnlyList<CompareFieldRowItem> Fields =>
        Comparison.Differences.Select(d => new CompareFieldRowItem(d)).ToList();

    public string DiffSummary => Comparison.State switch
    {
        CompareState.Different => string.Join(", ", Comparison.Differences
            .Where(d => d.State != FieldCompareState.Equal)
            .Select(d => TemplateFieldRules.DisplayName(d.Field))),
        _ => string.Empty,
    };
}

/// <summary>One field of a compare row: state label, both effective values and the line diff.</summary>
public sealed class CompareFieldRowItem
{
    public CompareFieldRowItem(FieldDiff diff)
    {
        Diff = diff;
    }

    public FieldDiff Diff { get; }

    public string FieldLabel => TemplateFieldRules.DisplayName(Diff.Field);

    public FieldCompareState State => Diff.State;

    public bool IsDifferent => Diff.State != FieldCompareState.Equal;

    public string StateLabel => Diff.State switch
    {
        FieldCompareState.Equal => "same",
        FieldCompareState.Different => "different",
        FieldCompareState.MissingField => "field missing",
        FieldCompareState.EmptyValue => "empty value",
        _ => Diff.State.ToString(),
    };

    public string ActiveValue => Show(Diff.ActiveValue);

    public string TargetValue => Show(Diff.TargetValue);

    public bool ActiveHasDraft => Diff.ActiveHasDraft;

    public bool TargetHasDraft => Diff.TargetHasDraft;

    public IReadOnlyList<DiffLine> Lines => Diff.Lines;

    /// <summary>Empty and missing must not look identical in the panel.</summary>
    private static string Show(string? value) => value switch
    {
        null => "(no such field)",
        "" => "(empty)",
        _ => value,
    };
}

/// <summary>
/// One staged change in the review list. Identity is the change key (file, declaration, field), never
/// the displayed name: two templates can hold the same parameter, and one template can declare it
/// twice. The proposed value is shown exactly as staged, including surrounding spaces.
/// </summary>
public sealed class StagedReviewRow : ViewModelBase
{
    private bool _isSelected = true;

    public StagedReviewRow(DraftChange change)
    {
        Change = change;
    }

    public DraftChange Change { get; }

    public ChangeKey Key => Change.Key;

    public FieldKind Field => Change.Key.Field;

    public string FilePath => Change.Key.FilePath;

    public string FileName => System.IO.Path.GetFileName(Change.Key.FilePath);

    public string ParameterName => Change.DeclarationName;

    public string FieldLabel => TemplateFieldRules.DisplayName(Change.Key.Field);

    public string BaselineText => Show(Change.BaselineValue);

    public string ProposedText => Show(Change.ProposedValue);

    private TextDelta? _delta;

    /// <summary>
    /// What differs, in the shape a reviewer reads: unchanged context around the changed stretch. The
    /// review cards show both full values anyway; this is what makes the difference easy to find when a
    /// description is a long single line.
    /// </summary>
    public TextDelta Delta => _delta ??= new TextDelta(Change.BaselineValue, Change.ProposedValue);

    public bool HasValueDiff => Delta.HasChange;

    /// <summary>Nothing is shared between the two values, so a "changed part" strip would be noise.</summary>
    public bool IsCompleteReplacement => Delta.IsCompleteReplacement;

    /// <summary>
    /// Leading or trailing spaces and tabs are invisible in a table, and the editor deliberately does not
    /// trim them. Say so rather than let a reviewer wonder whether the value really ends with a space.
    /// </summary>
    public bool HasHiddenWhitespace => ShowsHiddenWhitespace(Change.BaselineValue) || ShowsHiddenWhitespace(Change.ProposedValue);

    public string HiddenWhitespaceHint
    {
        get
        {
            static string EdgeOf(string? value)
            {
                if (value is not { Length: > 0 })
                {
                    return string.Empty;
                }

                bool starts = char.IsWhiteSpace(value[0]);
                bool ends = char.IsWhiteSpace(value[^1]);
                return (starts, ends) switch
                {
                    (true, true) => "starts and ends with whitespace",
                    (true, false) => "starts with whitespace",
                    (false, true) => "ends with whitespace",
                    _ => string.Empty,
                };
            }

            string baseline = EdgeOf(Change.BaselineValue);
            string proposed = EdgeOf(Change.ProposedValue);
            if (baseline.Length == 0 && proposed.Length == 0)
            {
                return string.Empty;
            }

            return "Whitespace at the edge of a value is kept: in the template "
                 + (baseline.Length > 0 ? baseline : "no edge whitespace")
                 + (proposed.Length > 0 ? $"; the proposed value {proposed}" : string.Empty);
        }
    }

    private static bool ShowsHiddenWhitespace(string? value) =>
        value is { Length: > 0 } && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
            {
                Raise(nameof(SelectionMark));
            }
        }
    }

    /// <summary>Visual cue besides the tick box, so the state is not colour-only.</summary>
    public string SelectionMark => _isSelected ? "\u2713" : "\u2013";

    /// <summary>A staged read-only name change: the save step will refuse it, so say so here.</summary>
    public bool IsNoOp => Change.IsNoOp;

    public string NoOpHint => IsNoOp
        ? "Parameter names are read-only. This change was staged from the comparison view and cannot be written."
        : string.Empty;

    /// <summary>Short writable/read-only marker for the review table.</summary>
    public string WriteStateText => IsNoOp ? "read-only" : "yes";

    private static string Show(string? value) => value switch
    {
        null => "(no such property)",
        "" => "(empty)",
        _ => value,
    };
}

/// <summary>
/// One template file inside the save review: its staged changes are kept together so a reviewer can
/// judge "what happens to this file" at a glance. The rows are the same objects as the flat list, so
/// ticking, discarding and saving address one row set, not two copies.
/// </summary>
public sealed class ReviewFileGroup : ViewModelBase
{
    public ReviewFileGroup(string filePath, IReadOnlyList<StagedReviewRow> rows)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
        Rows = rows;
    }

    public string FilePath { get; }

    public string FileName { get; }

    public IReadOnlyList<StagedReviewRow> Rows { get; }

    public string ChangeCountText => Rows.Count == 1 ? "1 change" : $"{Rows.Count} changes";

    public string Headline => $"{FileName} - {ChangeCountText}";
}

/// <summary>
/// One candidate of the bulk panel. Ineligible candidates stay visible with their reason; the
/// checkbox is disabled so a user cannot stage a change the save review would have to guess about.
/// </summary>
public sealed class BulkTargetRowItem : ViewModelBase
{
    private bool _isChecked;

    public BulkTargetRowItem(BulkTarget target)
    {
        Target = target;
    }

    public BulkTarget Target { get; }

    public string FilePath => Target.FilePath;

    public string FileName => Target.FileName;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (Set(ref _isChecked, value))
            {
                Raise(nameof(IsSelected));
            }
        }
    }

    public bool IsSelectable => Target.IsSelectable;

    public bool IsSelected => IsChecked && IsSelectable;

    public string StatusLabel => Target.Status switch
    {
        BulkTargetStatus.Eligible => "eligible",
        BulkTargetStatus.Missing => "no such parameter",
        BulkTargetStatus.Ambiguous => "declared more than once",
        BulkTargetStatus.ReadOnlyShape => "read-only value",
        BulkTargetStatus.NotLoaded => "not read in this session",
        _ => Target.Status.ToString(),
    };

    public string Reason => Target.Reason ?? string.Empty;

    public bool HasReason => !string.IsNullOrEmpty(Target.Reason);

    public string CurrentValueText => Target.CurrentValue switch
    {
        null => "(no default property)",
        "" => "(empty)",
        _ => Target.CurrentValue,
    };

    public bool CurrentHasDraft => Target.CurrentHasDraft;
}
