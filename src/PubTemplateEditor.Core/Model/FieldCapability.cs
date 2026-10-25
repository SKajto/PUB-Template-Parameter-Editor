namespace PubTemplateEditor.Core.Model;

/// <summary>Editable/read-only status of one logical field of a declaration.</summary>
public enum FieldCapability
{
    /// <summary>The exact source span is known and lossless editing is established.</summary>
    Editable,

    /// <summary>The field exists but lossless editing is not established; shown read-only with explanation.</summary>
    ReadOnlyUnsupported,

    /// <summary>The field is absent in the declaration (distinct from an empty value).</summary>
    Missing,

    /// <summary>Names are read-only in the first release by product decision.</summary>
    ReadOnlyByDesign,
}
