namespace DTAParamEditor.Core.Model;

/// <summary>One logical value together with its exact source span and edit capability.</summary>
public sealed record DeclarationField(
    FieldKind Kind,
    bool Present,
    string? LogicalValue,
    ValueSpan? Span,
    FieldCapability Capability,
    string? CapabilityReason);
