namespace DTAParamEditor.Core.Model;

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
    public bool IsNoOp => string.Equals(BaselineValue, ProposedValue, StringComparison.Ordinal);
}
