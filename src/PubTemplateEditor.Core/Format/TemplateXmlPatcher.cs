using System.Globalization;
using System.Text;
using System.Xml;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Format;

/// <summary>One value replacement, expressed in the character coordinates of the decoded source.</summary>
public sealed record SourceEdit(int StartChar, int LengthChar, string Replacement, PatchedField Detail);

/// <summary>A field the patcher did write, with the offsets it occupied.</summary>
public sealed record PatchedField(
    int DeclarationId,
    string DeclarationName,
    FieldKind Field,
    string OldValue,
    string NewValue,
    int StartChar,
    int OldLengthChar,
    int NewLengthChar);

/// <summary>A field the patcher refused, with the reason shown to the user.</summary>
public sealed record RejectedField(int DeclarationId, string? DeclarationName, FieldKind Field, string Reason);

/// <summary>
/// Result of one source-preserving patch of one template. Nothing here is written anywhere: the
/// caller decides what to do with <see cref="PatchedText"/>, and must not do anything at all while
/// <see cref="Verified"/> is false.
/// </summary>
public sealed record TemplatePatch
{
    public required string OriginalText { get; init; }
    public required string PatchedText { get; init; }

    /// <summary>
    /// The patched text re-parsed and compared field by field against the plan. A patch is only
    /// usable when this is true; see <see cref="VerificationProblems"/> for what went wrong otherwise.
    /// </summary>
    public required bool Verified { get; init; }

    public IReadOnlyList<PatchedField> Applied { get; init; } = [];
    public IReadOnlyList<RejectedField> Rejected { get; init; } = [];
    public IReadOnlyList<string> VerificationProblems { get; init; } = [];

    public bool IsEmpty => Applied.Count == 0;

    /// <summary>Encodes the patched text the way the original document was encoded.</summary>
    public byte[] ToBytes(TextEncodingKind encoding) => TemplateXmlText.Encode(PatchedText, encoding);
}

/// <summary>
/// Escaping and encoding helpers kept in one place, because a value written into a source span is
/// only correct if it is escaped the way the reader will undo.
/// </summary>
public static class TemplateXmlText
{
    /// <summary>Escapes a logical value for the inside of a quoted XML attribute.</summary>
    public static string EscapeAttributeValue(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    // Native templates write '>' escaped; keeping that style avoids a needless diff.
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '\r':
                case '\n':
                case '\t':
                    // XML attribute normalization turns these into spaces on the next read, so the
                    // value would not survive a reload. The caller rejects them instead.
                    throw new ArgumentException("A literal carriage return, line feed or tab cannot be stored in an XML attribute.", nameof(value));
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Escapes a logical value for element text content.</summary>
    public static string EscapeTextContent(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    public static bool ContainsForbiddenAttributeCharacter(string value) =>
        value.Contains('\r') || value.Contains('\n') || value.Contains('\t');

    /// <summary>
    /// Decodes the document back to bytes with the encoding it came from, including the byte order
    /// mark when the original had one.
    /// </summary>
    public static byte[] Encode(string text, TextEncodingKind encoding) => encoding switch
    {
        // SourceText keeps the byte order mark inside the decoded text, so re-emitting it here would
        // produce a document that starts with two marks and no longer parses.
        TextEncodingKind.Utf8Bom => text.Length > 0 && text[0] == '﻿'
            ? new UTF8Encoding(false).GetBytes(text)
            : Concat(new byte[] { 0xEF, 0xBB, 0xBF }, new UTF8Encoding(false).GetBytes(text)),
        TextEncodingKind.Utf8 => new UTF8Encoding(false).GetBytes(text),
        // Utf16Le/Be are only detected when a mark was present, so the mark goes back.
        TextEncodingKind.Utf16Le => text.Length > 0 && text[0] == '﻿'
            ? System.Text.Encoding.Unicode.GetBytes(text)
            : Concat(new byte[] { 0xFF, 0xFE }, System.Text.Encoding.Unicode.GetBytes(text.TrimStart('﻿'))),
        TextEncodingKind.Utf16Be => text.Length > 0 && text[0] == '﻿'
            ? System.Text.Encoding.BigEndianUnicode.GetBytes(text)
            : Concat(new byte[] { 0xFE, 0xFF }, System.Text.Encoding.BigEndianUnicode.GetBytes(text.TrimStart('﻿'))),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    private static byte[] Concat(byte[] head, byte[] tail)
    {
        var result = new byte[head.Length + tail.Length];
        head.CopyTo(result, 0);
        tail.CopyTo(result.AsSpan(head.Length));
        return result;
    }

    /// <summary>
    /// Re-parses one captured source span on its own. This is the lexical half of the verification:
    /// it proves what those exact characters mean, without re-implementing XML escaping here.
    /// </summary>
    internal static bool TryReadSpanText(string rawSpan, FieldKind field, out string value)
    {
        value = string.Empty;
        string fragment = field == FieldKind.Description
            ? $"<v>{rawSpan}</v>"
            : $"<v a=\"{rawSpan}\"/>";

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                ConformanceLevel = ConformanceLevel.Document,
            };

            using XmlReader reader = XmlReader.Create(new StringReader(fragment), settings);
            if (!reader.Read() || reader.NodeType != XmlNodeType.Element)
            {
                return false;
            }

            if (field == FieldKind.Description)
            {
                value = reader.ReadElementContentAsString();
            }
            else
            {
                value = reader.GetAttribute("a") ?? string.Empty;
            }

            return true;
        }
        catch (Exception e) when (e is XmlException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>
/// The source-preserving patcher: it rewrites only the character spans belonging to the values being
/// changed and leaves every other byte of the document exactly where it was. Regex editing and whole
/// document re-serialization are both rejected by the specification, and this class is the reason
/// they are not needed.
/// </summary>
public static class TemplateXmlPatcher
{
    /// <summary>
    /// Plans and applies the changes of one template. Every change is checked against the source span
    /// captured when the template was read; a change whose span no longer holds the value that was
    /// staged is refused rather than guessed at.
    /// </summary>
    public static TemplatePatch Patch(TemplateRecord record, IEnumerable<DraftChange> changes)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(changes);

        SourceText source = record.XmlContent?.Source
            ?? throw new InvalidOperationException("The template has no parsed source text to patch.");

        IReadOnlyList<DraftChange> requested = changes.ToList();
        var applied = new List<PatchedField>();
        var rejected = new List<RejectedField>();
        var edits = new List<SourceEdit>();

        var byDeclaration = record.Declarations.ToDictionary(d => d.DeclarationId);

        foreach (DraftChange change in requested)
        {
            int declarationId = change.Key.DeclarationId;
            FieldKind field = change.Key.Field;
            string? name = change.DeclarationName;

            if (!string.Equals(change.Key.FilePath, record.FilePath, StringComparison.Ordinal))
            {
                // A change staged for another file must never be applied here, even if a caller
                // groups the batch wrongly; the identity includes the path for exactly this reason.
                rejected.Add(new RejectedField(declarationId, name, field,
                    "This change belongs to a different template file."));
                continue;
            }

            if (change.ProposedValue is null)
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "No value was staged for this field, so there is nothing to write."));
                continue;
            }

            if (field == FieldKind.Name)
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "Parameter names are read-only; the template keeps the name it has."));
                continue;
            }

            if (!byDeclaration.TryGetValue(declarationId, out VariableDeclaration? declaration))
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "This declaration is not part of the template that was read in this session."));
                continue;
            }

            name = declaration.DisplayName;
            DeclarationField target = declaration.GetField(field);

            if (!target.Present || target.Span is null)
            {
                // Writing into a value that is absent would mean inserting an element or attribute,
                // which is structural editing rather than replacing a span.
                rejected.Add(new RejectedField(declarationId, name, field,
                    "The template has no such property to write into. Adding one is not supported."));
                continue;
            }

            if (target.Capability == FieldCapability.ReadOnlyUnsupported)
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    target.CapabilityReason ?? "This value is kept read-only because it is not simple text."));
                continue;
            }

            ValueSpan span = target.Span.Value;
            string rawSpan = source.Text.Substring(span.StartChar, span.LengthChar);

            if (!TemplateXmlText.TryReadSpanText(rawSpan, field, out string sourceValue))
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "The source span of this value cannot be re-parsed, so the edit is refused."));
                continue;
            }

            if (!string.Equals(sourceValue, change.BaselineValue ?? string.Empty, StringComparison.Ordinal))
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "The template no longer holds the value this change was staged against."));
                continue;
            }

            if (!string.Equals(sourceValue, target.LogicalValue, StringComparison.Ordinal))
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "The value read from the source span differs from the parsed value; the edit is refused."));
                continue;
            }

            string proposed = change.ProposedValue!;
            if (field != FieldKind.Description && TemplateXmlText.ContainsForbiddenAttributeCharacter(proposed))
            {
                rejected.Add(new RejectedField(declarationId, name, field,
                    "A line break or tab in this field would not survive the next read, so it is refused."));
                continue;
            }

            string replacement;
            try
            {
                replacement = field == FieldKind.Description
                    ? TemplateXmlText.EscapeTextContent(proposed)
                    : TemplateXmlText.EscapeAttributeValue(proposed);
            }
            catch (ArgumentException e)
            {
                rejected.Add(new RejectedField(declarationId, name, field, e.Message));
                continue;
            }

            applied.Add(new PatchedField(declarationId, name, field, sourceValue, proposed,
                span.StartChar, span.LengthChar, replacement.Length));
            edits.Add(new SourceEdit(span.StartChar, span.LengthChar, replacement, applied[^1]));
        }

        string patchedText = ApplyEdits(source.Text, edits, applied, rejected);
        var problems = new List<string>();
        bool verified = Verify(source, patchedText, record, applied, problems);

        return new TemplatePatch
        {
            OriginalText = source.Text,
            PatchedText = patchedText,
            Verified = verified,
            Applied = applied,
            Rejected = rejected,
            VerificationProblems = problems,
        };
    }

    /// <summary>
    /// Applies edits from the end of the document backwards, so earlier offsets stay valid. Overlaps
    /// are refused: two spans covering the same characters mean the parse is not trustworthy.
    /// </summary>
    private static string ApplyEdits(
        string text,
        List<SourceEdit> edits,
        List<PatchedField> applied,
        List<RejectedField> rejected)
    {
        List<SourceEdit> ordered = [.. edits.OrderByDescending(e => e.StartChar)];
        var sb = new StringBuilder(text);
        var dropped = new HashSet<PatchedField>();

        // Applying from the end backwards keeps the offsets collected during the parse valid.
        int cursor = text.Length;
        foreach (SourceEdit edit in ordered)
        {
            if (edit.StartChar + edit.LengthChar > cursor)
            {
                rejected.Add(new RejectedField(edit.Detail.DeclarationId, edit.Detail.DeclarationName,
                    edit.Detail.Field, "Two values share the same source span, so nothing here can be located safely."));
                dropped.Add(edit.Detail);
                continue;
            }

            sb.Remove(edit.StartChar, edit.LengthChar);
            sb.Insert(edit.StartChar, edit.Replacement);
            cursor = edit.StartChar;
        }

        if (dropped.Count > 0)
        {
            applied.RemoveAll(a => dropped.Contains(a));
        }

        return sb.ToString();
    }

    /// <summary>
    /// The safety net: parse the patched text and compare it with the plan. Anything other than the
    /// intended values, or a changed declaration count, means the result is never handed to the writer.
    /// </summary>
    private static bool Verify(
        SourceText original,
        string patchedText,
        TemplateRecord record,
        IReadOnlyList<PatchedField> applied,
        List<string> problems)
    {
        if (applied.Count == 0)
        {
            return true;
        }

        var patched = new SourceText([], original.EncodingName, patchedText);
        IReadOnlyList<VariableDeclaration> declarations;
        try
        {
            ParseResult result = XmlDeclarationParser.Parse(patched);
            if (!result.Success)
            {
                problems.AddRange(result.Errors);
                return false;
            }

            declarations = result.Declarations;
        }
        catch (Exception e) when (e is XmlException or FormatException or InvalidOperationException)
        {
            problems.Add($"The patched template no longer parses: {e.Message}");
            return false;
        }

        if (declarations.Count != record.Declarations.Count)
        {
            problems.Add($"The patched template declares {declarations.Count} parameter(s) instead of {record.Declarations.Count}.");
            return false;
        }

        var byId = declarations.ToDictionary(d => d.DeclarationId);
        foreach (VariableDeclaration before in record.Declarations)
        {
            if (!byId.TryGetValue(before.DeclarationId, out VariableDeclaration? after))
            {
                problems.Add($"Declaration '{before.DisplayName}' disappeared from the patched template.");
                continue;
            }

            foreach (FieldKind kind in Enum.GetValues<FieldKind>())
            {
                string expected = ExpectedValue(before, kind, applied);
                DeclarationField field = after.GetField(kind);
                string actual = field.LogicalValue ?? string.Empty;

                if (field.Present && !string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    problems.Add(string.Create(CultureInfo.CurrentCulture,
                        $"After patching, '{before.DisplayName}' {TemplateFieldRules.DisplayName(kind)} is '{actual}' instead of '{expected}'."));
                }

                if (!field.Present && field.Present != before.GetField(kind).Present)
                {
                    problems.Add($"After patching, '{before.DisplayName}' lost its {TemplateFieldRules.DisplayName(kind)} property.");
                }
            }
        }

        return problems.Count == 0;
    }

    private static string ExpectedValue(
        VariableDeclaration declaration,
        FieldKind kind,
        IReadOnlyList<PatchedField> applied)
    {
        PatchedField? edit = applied.FirstOrDefault(a => a.DeclarationId == declaration.DeclarationId && a.Field == kind);
        if (edit is not null)
        {
            return edit.NewValue;
        }

        return declaration.GetField(kind).LogicalValue ?? string.Empty;
    }
}
