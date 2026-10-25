namespace PubTemplateEditor.Core.Format;

/// <summary>The template.xml member extracted from a DTA ZIP container, plus its own identity.</summary>
public sealed class TemplateXmlContent
{
    /// <summary>Entry name inside the container (observed native value: 'template.xml').</summary>
    public required string EntryName { get; init; }

    public required byte[] EntryBytes { get; init; }

    public required SourceText Source { get; init; }

    /// <summary>SHA-256 hex of the inner entry bytes at load time.</summary>
    public required string EntryFingerprint { get; init; }
}
