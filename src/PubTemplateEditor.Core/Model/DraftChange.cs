namespace PubTemplateEditor.Core.Model;

/// <summary>Identity of one editable field of one declaration in one template file.</summary>
public readonly record struct ChangeKey(string FilePath, int DeclarationId, FieldKind Field);

/// <summary>
/// An in-memory staged edit. Drafts never touch disk; only a completed review Save writes.
/// </summary>
public sealed record DraftChange(
    ChangeKey Key,
    string? BaselineValue,
    string? ProposedValue,
    bool ProposedFromDraft)
{
    /// <summary>
    /// Declaration name at staging time, for review/list display. Identity is always the key
    /// (file + declaration + field), never the name, so an ambiguous or unnamed declaration is
    /// still addressable.
    /// </summary>
    public required string DeclarationName { get; init; }

    public string FilePath => Key.FilePath;

    public int DeclarationId => Key.DeclarationId;

    public FieldKind Field => Key.Field;

    public bool IsNoOp => string.Equals(BaselineValue, ProposedValue, StringComparison.Ordinal);
}
