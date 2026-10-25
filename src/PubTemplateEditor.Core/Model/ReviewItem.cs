namespace PubTemplateEditor.Core.Model;

public enum ReviewItemStatus
{
    Included,
    Excluded,
}

/// <summary>One field change presented in the save review. Deselected items revert only after a fully successful save.</summary>
public sealed record ReviewItem(
    ChangeKey Key,
    string TemplateFileName,
    string DeclarationName,
    FieldKind Field,
    string? BaselineValue,
    string? ProposedValue,
    bool Included);

/// <summary>An immutable snapshot of the review screen state. Proposals changed after the snapshot require renewed review.</summary>
public sealed record ReviewSnapshot(IReadOnlyList<ReviewItem> Items, DateTimeOffset CreatedUtc);
