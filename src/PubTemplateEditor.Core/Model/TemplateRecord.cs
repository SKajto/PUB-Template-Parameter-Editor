namespace PubTemplateEditor.Core.Model;

public enum TemplateReadState
{
    NotRead,
    Loaded,
    Error,
}

/// <summary>One template file loaded into the working session, with its baseline bytes and fingerprint.</summary>
public sealed class TemplateRecord
{
    public required string FilePath { get; init; }

    public Core.Format.TemplateFormat Format { get; set; } = Core.Format.TemplateFormat.Unknown;

    public TemplateReadState ReadState { get; set; } = TemplateReadState.NotRead;

    /// <summary>Any read/parse problem; the file remains visible and other files stay usable.</summary>
    public string? ReadError { get; set; }

    /// <summary>SHA-256 hex of the baseline bytes on disk at load time (conflict detection fingerprint).</summary>
    public string? BaselineFingerprint { get; set; }

    /// <summary>For the ZIP container format: inner template.xml bytes and fingerprint at load time.</summary>
    public Format.TemplateXmlContent? XmlContent { get; set; }

    public IReadOnlyList<VariableDeclaration> Declarations { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public string FileName => Path.GetFileName(FilePath);
}
