namespace PubTemplateEditor.Core.Model;

public enum CompareState
{
    /// <summary>Same template (reference column).</summary>
    Self,

    /// <summary>All compared fields match the active template.</summary>
    Same,

    /// <summary>At least one field differs.</summary>
    Different,

    /// <summary>The template does not declare this exact name at all.</summary>
    Missing,

    /// <summary>The template is loaded but the name is ambiguous (duplicates) or unreadable.</summary>
    Ineligible,

    /// <summary>The template is not loaded into the session.</summary>
    NotLoaded,
}

public enum FieldCompareState
{
    Equal,
    Different,
    MissingField,
    EmptyValue,
}

/// <summary>Line-oriented diff fragment for readable old/new presentation (including whitespace differences).</summary>
public sealed record DiffLine(bool Changed, string Text);

/// <summary>
/// One field of one compared template. Values are shown exactly as stored: an empty string and a
/// missing field are different states, and a draft-marked value tells the user the comparison is
/// looking at an in-memory proposal rather than the file on disk.
/// </summary>
public sealed record FieldDiff(
    FieldKind Field,
    FieldCompareState State,
    string? ActiveValue,
    string? TargetValue,
    bool ActiveHasDraft,
    bool TargetHasDraft,
    IReadOnlyList<DiffLine> Lines);

public sealed record TemplateComparison(
    string FilePath,
    string FileName,
    CompareState State,
    bool HasUnsavedDrafts,
    string? Reason,
    IReadOnlyList<FieldDiff> Differences)
{
    /// <summary>Target declaration identity when the exact name matched (null when missing/ambiguous).</summary>
    public int? DeclarationId { get; init; }
}
