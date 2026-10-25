using System.Text;

namespace PubTemplateEditor.Core.Format;

public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be,
}

/// <summary>
/// Immutable decoded template text with the original bytes, detected encoding and
/// line index. All source spans in the domain are character offsets into <see cref="Text"/>.
/// </summary>
public sealed class SourceText
{
    public byte[] OriginalBytes { get; }
    public TextEncodingKind EncodingName { get; }
    public string Text { get; }
    private readonly int[] _lineStarts;

    public SourceText(byte[] bytes, TextEncodingKind encoding, string text)
    {
        OriginalBytes = bytes;
        EncodingName = encoding;
        Text = text;
        _lineStarts = ComputeLineStarts(text);
    }

    public static SourceText FromBytes(byte[] bytes)
    {
        TextEncodingKind encoding;
        int offset = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = TextEncodingKind.Utf8Bom;
            // The BOM is retained in the decoded text: byte-for-byte round-trip and
            // character offsets must cover the whole document, including U+FEFF.
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = TextEncodingKind.Utf16Le;
            offset = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = TextEncodingKind.Utf16Be;
            offset = 2;
        }
        else
        {
            encoding = TextEncodingKind.Utf8;
        }

        string text = encoding switch
        {
            TextEncodingKind.Utf16Le => System.Text.Encoding.Unicode.GetString(bytes, offset, bytes.Length - offset),
            TextEncodingKind.Utf16Be => System.Text.Encoding.BigEndianUnicode.GetString(bytes, offset, bytes.Length - offset),
            // Strict UTF8 without BOM stripping: the U+FEFF mark must stay visible in the
            // decoded text so character offsets cover the whole document.
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes, offset, bytes.Length - offset),
        };

        if (text.Contains('\0'))
        {
            throw new FormatException("Template text contains NUL characters; the encoding could not be confirmed.");
        }

        return new SourceText(bytes, encoding, text);
    }

    /// <summary>Converts a 1-based line/position from XmlReader into a absolute character offset.</summary>
    public int GetOffset(int line, int linePosition)
    {
        if (line < 1 || line > _lineStarts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        return _lineStarts[line - 1] + linePosition - 1;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                starts.Add(i + 1);
            }
            else if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }
}
