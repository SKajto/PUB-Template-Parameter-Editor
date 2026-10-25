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
