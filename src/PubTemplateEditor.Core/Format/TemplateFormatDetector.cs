namespace PubTemplateEditor.Core.Format;

public enum TemplateFormat
{
    /// <summary>ZIP container holding template.xml plus schemas/properties (observed native .dta layout).</summary>
    DtaZipContainer,

    /// <summary>Bare XML template file (observed in the dated XML reference evidence).</summary>
    PlainXmlTemplate,

    /// <summary>Not recognized; the file stays read-only with an explanation.</summary>
    Unknown,
}

/// <summary>
/// Byte-level format detection. Never trusts the extension alone.
/// Evidence (2026-10, examples/*.dta): native .dta files are ZIP archives
/// (Deflate, data descriptors, UTF-8 entry names) containing template.xml plus
/// schemas/ and template_*.properties entries.
/// </summary>
public static class TemplateFormatDetector
{
    public static TemplateFormat Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 2 && head[0] == (byte)'P' && head[1] == (byte)'K' &&
            (head[2] == 0x03 || head[2] == 0x05 || (head[2] == 0x07 && head[3] == 0x08)))
        {
            return TemplateFormat.DtaZipContainer;
        }

        ReadOnlySpan<byte> xml = head;
        // Skip a BOM if present.
        if (xml.Length >= 3 && xml[0] == 0xEF && xml[1] == 0xBB && xml[2] == 0xBF)
        {
            xml = xml[3..];
        }

        while (xml.Length > 0 && (xml[0] == (byte)' ' || xml[0] == (byte)'\t' || xml[0] == (byte)'\r' || xml[0] == (byte)'\n'))
        {
            xml = xml[1..];
        }

        if (xml.Length >= 5 && xml.StartsWith("<?xml"u8))
        {
            return TemplateFormat.PlainXmlTemplate;
        }

        return TemplateFormat.Unknown;
    }
}
