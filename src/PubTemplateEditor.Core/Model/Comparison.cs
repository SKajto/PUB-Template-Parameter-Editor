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

public sealed record FieldDiff(FieldKind Field, FieldCompareState State, IReadOnlyList<DiffLine> Lines);

public sealed record TemplateComparison(
    string FilePath,
    string FileName,
    CompareState State,
    bool HasUnsavedDrafts,
    string? Reason,
    IReadOnlyList<FieldDiff> Differences);
