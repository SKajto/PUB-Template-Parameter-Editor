namespace DTAParamEditor.Core.Model;

/// <summary>
/// A declared variable ("parameter") inside the verified declaration scope
/// /template/dataset/variables/variable/feature[@tag='definition'].
/// Expression references elsewhere in a template are never declarations.
/// </summary>
public sealed class VariableDeclaration
{
    /// <summary>Stable identity within one loaded template (ordinal of the variable element).</summary>
    public required int DeclarationId { get; init; }

    /// <summary>Exact declared name as stored; never trimmed. Null when the name property is missing/ambiguous.</summary>
    public required string? ExactName { get; init; }

    public required DeclarationField Name { get; init; }
    public required DeclarationField Description { get; init; }
    public required DeclarationField DefaultValue { get; init; }
    public required DeclarationField Access { get; init; }

    /// <summary>The separate XML type property (for example 'user'); preserved, never edited, not the access field.</summary>
    public required string? XmlTypeProperty { get; init; }

    public DeclarationField GetField(FieldKind kind) => kind switch
    {
        FieldKind.Name => Name,
        FieldKind.Description => Description,
        FieldKind.DefaultValue => DefaultValue,
        FieldKind.Access => Access,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public string DisplayName => string.IsNullOrEmpty(ExactName) ? "(unnamed declaration)" : ExactName!;
}
