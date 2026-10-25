using System.Xml;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Format;

/// <summary>Result of one declaration parse pass.</summary>
public sealed class ParseResult
{
    public List<Model.VariableDeclaration> Declarations { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Reads declared variables from the verified declaration scope
/// /template/dataset/variables/variable/feature[@tag='definition'] using a
/// structural XmlReader (DtdProcessing.Prohibit, XmlResolver=null). Values are
/// treated strictly as text and never evaluated. Exact source spans for the
/// supported fields are recorded for later lossless patching.
/// </summary>
public static class XmlDeclarationParser
{
    public static ParseResult Parse(SourceText source)
    {
        var result = new ParseResult();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = false,
            // Whitespace must NOT be ignored: XmlReader line info is only reliable
            // when every node is reported, and span anchors depend on it.
            IgnoreWhitespace = false,
        };

        try
        {
            using var stringReader = new StringReader(source.Text);
            using var reader = XmlReader.Create(stringReader, settings);
            Walk(source, reader, result);
        }
        catch (XmlException ex)
        {
            result.Errors.Add($"Malformed XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}");
        }

        return result;
    }

    private static void Walk(SourceText source, XmlReader reader, ParseResult result)
    {
        // Path context: template -> dataset -> variables -> variable -> feature[@tag=definition]
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Depth == 0)
            {
                if (reader.LocalName != "template" || reader.NamespaceURI.Length > 0)
                {
                    result.Errors.Add($"Unexpected root element '{reader.Name}'; declaration reading requires the observed template structure.");
                    return;
                }

                continue;
            }

            if (!IsInDeclarationScope(reader))
            {
                continue;
            }

            if (reader.Depth == 3 && reader.LocalName == "variable" && reader.NamespaceURI.Length == 0)
            {
                ReadVariable(source, reader, result);
            }
        }
    }

    private static bool IsInDeclarationScope(XmlReader reader)
    {
        // Ancestors at depth 1..3 must be dataset/variables exactly.
        if (reader.Depth >= 1)
        {
            string name = reader.LocalName;
            if (reader.Depth == 1 && name != "dataset")
            {
                return false;
            }

            if (reader.Depth == 2 && name != "variables")
            {
                return false;
            }
        }

        return true;
    }

    private static void ReadVariable(SourceText source, XmlReader reader, ParseResult result)
    {
        if (reader.IsEmptyElement)
        {
            result.Warnings.Add($"Variable element at line {((IXmlLineInfo)reader).LineNumber} is empty and contains no definition.");
            return;
        }

        int startLine = ((IXmlLineInfo)reader).LineNumber;
        int startLinePos = ((IXmlLineInfo)reader).LinePosition;
        int declarationId = result.Declarations.Count;
        int depthAtVariable = reader.Depth;

        DeclarationField? name = null;
        DeclarationField? description = null;
        DeclarationField? defaultValue = null;
        DeclarationField? access = null;
        string? xmlType = null;
        bool sawDefinitionFeature = false;
        var otherPropertyNames = new List<string>();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depthAtVariable)
            {
                break;
            }

            if (reader.EOF)
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element || reader.Depth != depthAtVariable + 1)
            {
                continue;
            }

            if (reader.LocalName != "feature" || reader.NamespaceURI.Length > 0)
            {
                result.Warnings.Add($"Unexpected child '{reader.Name}' inside variable at line {startLine}; preserved unchanged.");
                SkipElement(reader, depthAtVariable + 1);
                continue;
            }

            if (reader.GetAttribute("tag") != "definition")
            {
                SkipElement(reader, depthAtVariable + 1);
                continue;
            }

            sawDefinitionFeature = true;
            ReadDefinitionFeature(source, reader, depthAtVariable + 1, result, declarationId,
                startLine, ref name, ref description, ref defaultValue, ref access, ref xmlType, otherPropertyNames);
        }

        if (!sawDefinitionFeature)
        {
            result.Warnings.Add($"Variable at line {startLine} has no definition feature; preserved unchanged and not listed as a declaration.");
            return;
        }

        if (otherPropertyNames.Count > 0)
        {
            result.Warnings.Add(
                $"Declaration '{name?.LogicalValue ?? "(unnamed)"}' contains additional properties ({string.Join(", ", otherPropertyNames)}); they are preserved and read-only.");
        }

        var decl = new Model.VariableDeclaration
        {
            DeclarationId = declarationId,
            ExactName = name?.Present == true ? name!.LogicalValue : null,
            Name = name ?? MissingField(Model.FieldKind.Name, "name"),
            Description = description ?? MissingField(Model.FieldKind.Description, "description"),
            DefaultValue = defaultValue ?? MissingField(Model.FieldKind.DefaultValue, "default"),
            Access = access ?? MissingField(Model.FieldKind.Access, "access"),
            XmlTypeProperty = xmlType,
        };

        result.Declarations.Add(decl);

        string? nameValue = decl.ExactName;
        if (nameValue is not null)
        {
            int occurrences = 0;
            foreach (var existing in result.Declarations)
            {
                if (existing.ExactName == nameValue)
                {
                    occurrences++;
                }
            }

            if (occurrences > 1)
            {
                result.Warnings.Add($"Duplicate declaration name '{nameValue}' in this template; bulk editing will treat this template as an ambiguous target.");
            }
        }
    }

    private static void ReadDefinitionFeature(
        SourceText source,
        XmlReader reader,
        int featureDepth,
        ParseResult result,
        int declarationId,
        int variableLine,
        ref DeclarationField? name,
        ref DeclarationField? description,
        ref DeclarationField? defaultValue,
        ref DeclarationField? access,
        ref string? xmlType,
        List<string> otherPropertyNames)
    {
        int depthAtFeature = featureDepth;
        int propertyStartLine = 0;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depthAtFeature)
            {
                break;
            }

            if (reader.EOF)
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element || reader.Depth != depthAtFeature + 1)
            {
                continue;
            }

            if (reader.LocalName != "property" || reader.NamespaceURI.Length > 0)
            {
                result.Warnings.Add($"Unexpected element '{reader.Name}' inside definition feature at line {variableLine}; preserved unchanged.");
                SkipElement(reader, depthAtFeature + 1);
                continue;
            }

            propertyStartLine = ((IXmlLineInfo)reader).LineNumber;
            string propName = reader.GetAttribute("name") ?? string.Empty;

            switch (propName)
            {
                case "name":
                    name = ReadAttributeProperty(source, reader, depthAtFeature + 1, Model.FieldKind.Name, propName, result);
                    if (name.Capability == Model.FieldCapability.Missing)
                    {
                        result.Warnings.Add($"Name property at line {propertyStartLine} has no usable value attribute.");
                    }

                    break;
                case "description":
                    description = ReadDescriptionProperty(source, reader, depthAtFeature + 1, propName, result);
                    break;
                case "default":
                    defaultValue = ReadAttributeProperty(source, reader, depthAtFeature + 1, Model.FieldKind.DefaultValue, propName, result);
                    break;
                case "access":
                    access = ReadAttributeProperty(source, reader, depthAtFeature + 1, Model.FieldKind.Access, propName, result);
                    if (access.Present && access.LogicalValue is not ("internal" or "external"))
                    {
                        access = access with
                        {
                            Capability = Model.FieldCapability.ReadOnlyUnsupported,
                            CapabilityReason = $"Unobserved access value '{access.LogicalValue}'; only 'internal' and 'external' are supported for editing.",
                        };
                    }

                    break;
                case "type":
                    xmlType = reader.GetAttribute("value");
                    SkipElement(reader, depthAtFeature + 1);
                    break;
                default:
                    otherPropertyNames.Add(propName);
                    SkipElement(reader, depthAtFeature + 1);
                    break;
            }
        }
    }

    /// <summary>Property whose value lives in a value attribute (name/default/access).</summary>
    private static DeclarationField ReadAttributeProperty(SourceText source, XmlReader reader, int propertyDepth, Model.FieldKind kind, string propName, ParseResult result)
    {
        int elementLine = ((IXmlLineInfo)reader).LineNumber;
        int elementLinePos = ((IXmlLineInfo)reader).LinePosition;
        string? logical = reader.GetAttribute("value");
        bool sawValue = false;
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (reader.LocalName == "value")
                {
                    sawValue = true;
                }
            }
            while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }
        reader.MoveToElement();

        SkipElement(reader, propertyDepth);

        if (!sawValue)
        {
            return MissingField(kind, propName);
        }

        ValueSpan? span = TryFindAttributeSpan(source, elementLine, elementLinePos, propName, logical);
        if (span is null)
        {
            return new DeclarationField(kind, true, logical, null, Model.FieldCapability.ReadOnlyUnsupported,
                $"The exact source span of the '{propName}' value attribute could not be verified; the field stays read-only.");
        }

        // XML attribute-value normalization collapses literal CR/LF into spaces. When the raw
        // source span contains newlines, the logical value cannot be compared verbatim and a
        // replacement would destroy the original formatting, so such fields stay read-only.
        string rawSpan = source.Text.Substring(span.Value.StartChar, span.Value.LengthChar);
        if (rawSpan.Contains('\n') || rawSpan.Contains('\r'))
        {
            result.Warnings.Add($"The '{propName}' attribute value at line {elementLine} contains literal newlines; read-only.");
            return new DeclarationField(kind, true, logical, null, Model.FieldCapability.ReadOnlyUnsupported,
                $"The '{propName}' attribute value contains literal newlines; XML normalization prevents verbatim comparison, so it is read-only.");
        }

        if (logical is null || (rawSpan != logical && !EntitiesExpandTo(rawSpan, logical)))
        {
            result.Warnings.Add($"The '{propName}' attribute value at line {elementLine} failed span identity verification; read-only.");
            return new DeclarationField(kind, true, logical, null, Model.FieldCapability.ReadOnlyUnsupported,
                $"The '{propName}' attribute value span failed identity verification; the field stays read-only.");
        }

        return new DeclarationField(kind, true, logical, span, Model.FieldCapability.Editable, null);
    }

    /// <summary>
    /// Description property: observed shape is
    /// &lt;property name="description"&gt;&lt;expression type="constant"&gt;text&lt;/expression&gt;&lt;/property&gt;.
    /// A plain value attribute shape is also detected but treated as unsupported until evidenced.
    /// </summary>
    private static DeclarationField ReadDescriptionProperty(SourceText source, XmlReader reader, int propertyDepth, string propName, ParseResult result)
    {
        int elementLine = ((IXmlLineInfo)reader).LineNumber;
        string? attrValue = reader.GetAttribute("value");

        if (reader.IsEmptyElement)
        {
            if (attrValue is not null)
            {
                result.Warnings.Add($"Description property at line {elementLine} uses a value attribute shape that has not been evidenced; read-only.");
                return new DeclarationField(Model.FieldKind.Description, true, attrValue, null,
                    Model.FieldCapability.ReadOnlyUnsupported, "Unobserved description shape (value attribute); preserved read-only.");
            }

            return MissingField(Model.FieldKind.Description, propName);
        }

        reader.Read();

        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "expression" && reader.Depth == propertyDepth + 1)
        {
            string exprType = reader.GetAttribute("type") ?? string.Empty;
            if (exprType != "constant")
            {
                SkipElement(reader, propertyDepth + 1);
                return new DeclarationField(Model.FieldKind.Description, true, null, null,
                    Model.FieldCapability.ReadOnlyUnsupported,
                    $"Description uses expression type '{exprType}'; only constant descriptions are edit-supported.");
            }

            if (reader.IsEmptyElement)
            {
                // <expression type="constant"/> — self-closing empty constant description.
                ValueSpan span = new(0, 0, 0, 0);
                int spanStart = FindEmptyConstantDescriptionSpan(source, elementLine);
                if (spanStart >= 0)
                {
                    span = new ValueSpan(spanStart, 0, elementLine, 0);
                    return new DeclarationField(Model.FieldKind.Description, true, string.Empty, span,
                        Model.FieldCapability.Editable, null);
                }

                return new DeclarationField(Model.FieldKind.Description, true, string.Empty, null,
                    Model.FieldCapability.ReadOnlyUnsupported, "Empty constant description span could not be verified.");
            }

            // Content begins on the node after the expression start element.
            reader.Read();
            int textStartLine = ((IXmlLineInfo)reader).LineNumber;
            int textStartLinePos = ((IXmlLineInfo)reader).LinePosition;
            string text = string.Empty;
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
            {
                text = reader.NodeType == XmlNodeType.CDATA ? reader.Value : reader.ReadContentAsString();
            }
            else if (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                text = reader.Value;
            }

            if (reader.NodeType != XmlNodeType.EndElement)
            {
                // Element children inside the description: not a simple text shape.
                SkipElement(reader, propertyDepth + 1);
                return new DeclarationField(Model.FieldKind.Description, true, text.Length == 0 ? null : text, null,
                    Model.FieldCapability.ReadOnlyUnsupported, "Description contains child elements; unsupported shape, read-only.");
            }

            int endLine = ((IXmlLineInfo)reader).LineNumber;
            int endLinePos = ((IXmlLineInfo)reader).LinePosition;
            int start = source.GetOffset(textStartLine, textStartLinePos);
            // EndElement line info also points at the element name; the text span ends at '<'.
            int end = source.GetOffset(endLine, endLinePos);
            while (end > start && source.Text[end] != '<')
            {
                end--;
            }
            int length = end - start;

            // Verify span identity: the raw slice must decode to the logical value.
            string raw = source.Text.Substring(start, length);
            if (raw != text && !EntitiesExpandTo(raw, text))
            {
                return new DeclarationField(Model.FieldKind.Description, true, text, null,
                    Model.FieldCapability.ReadOnlyUnsupported, "Description text span failed identity verification; read-only.");
            }

            return new DeclarationField(Model.FieldKind.Description, true, text, new ValueSpan(start, length, textStartLine, textStartLinePos),
                Model.FieldCapability.Editable, null);
        }

        if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA || reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace)
        {
            SkipElement(reader, propertyDepth + 1);
            return new DeclarationField(Model.FieldKind.Description, true, null, null,
                Model.FieldCapability.ReadOnlyUnsupported, "Description uses direct text without a constant expression; unobserved shape, read-only.");
        }

        SkipElement(reader, propertyDepth);
        return MissingField(Model.FieldKind.Description, propName);
    }

    private static bool EntitiesExpandTo(string raw, string expected)
    {
        try
        {
            using var sr = new StringReader($"<v>{raw}</v>");
            using var r = XmlReader.Create(sr, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            r.MoveToContent();
            var sb = new System.Text.StringBuilder();
            if (r.NodeType is XmlNodeType.Text or XmlNodeType.CDATA
                or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                sb.Append(r.Value);
            }
            else
            {
                while (r.Read() && r.NodeType != XmlNodeType.EndElement)
                {
                    if (r.NodeType is XmlNodeType.Text or XmlNodeType.CDATA
                        or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                    {
                        sb.Append(r.Value);
                    }
                }
            }

            return sb.ToString() == expected;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static int FindEmptyConstantDescriptionSpan(SourceText source, int elementLine)
    {
        // Empty constant descriptions occur in two observed shapes:
        //   <expression type="constant"></expression>   (dominant in native samples)
        //   <expression type="constant"/>               (defensive; insertion anchor)
        // The property element and its expression child must sit on one line.
        string line = string.Empty;
        int absolute = 0;
        string[] lines = source.Text.Split('\n');
        if (elementLine < 1 || elementLine > lines.Length)
        {
            return -1;
        }

        for (int i = 0; i < elementLine - 1; i++)
        {
            absolute += lines[i].Length + 1;
        }

        line = lines[elementLine - 1].TrimEnd('\r');

        const string open = "<expression type=\"constant\"></expression>";
        int idx = line.IndexOf(open, StringComparison.Ordinal);
        if (idx >= 0)
        {
            return absolute + idx + "<expression type=\"constant\">".Length;
        }

        return -1;
    }

    /// <summary>
    /// Locates the exact character span of an attribute value inside the element's opening tag.
    /// Verifies the property element's own name attribute to anchor on the right element.
    /// Returns null when the shape cannot be proven (multi-line tags, odd quoting, span/value mismatch).
    /// </summary>
    private static ValueSpan? TryFindAttributeSpan(SourceText source, int elementLine, int elementLinePos, string propName, string? expectedLogical)
    {
        // XmlReader LinePosition for an element start points at the element name,
        // not at the opening '<'. Walk back to the tag start.
        int nameStart = source.GetOffset(elementLine, elementLinePos);
        int start = nameStart;
        while (start > 0 && source.Text[start] != '<')
        {
            start--;
        }

        if (start <= 0 || source.Text[start] != '<' || source.Text[nameStart] != '<' && !char.IsLetter(source.Text[nameStart]))
        {
            return null;
        }

        if (start == 0 && source.Text[0] != '<')
        {
            return null;
        }

        int end = start;
        bool inSingle = false;
        bool inDouble = false;
        for (; end < source.Text.Length; end++)
        {
            char c = source.Text[end];
            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }
            else if (c == '>' && !inSingle && !inDouble)
            {
                break;
            }
        }

        if (end >= source.Text.Length)
        {
            return null;
        }

        // A multi-line opening tag is an unobserved shape in native templates; the span is
        // still located by absolute scan from the verified tag start, but callers treat
        // literal newlines inside the value as read-only (see ReadAttributeProperty).
        string openTag = source.Text.Substring(start, end - start + 1);

        // Anchor: this property element must carry name="propName".
        string nameAttr = $"name=\"{propName}\"";
        string nameAttrAlt = $"name='{propName}'";
        if (!openTag.Contains(nameAttr, StringComparison.Ordinal) && !openTag.Contains(nameAttrAlt, StringComparison.Ordinal))
        {
            return null;
        }

        // Locate value="..." on this element, respecting quotes.
        if (!TryFindAttributeValue(openTag, "value", out int valueStartInTag, out int valueEndInTag))
        {
            return null;
        }

        // Verify the extracted raw slice decodes to the parser's logical value
        // (entity references, numeric character references and CDATA-equivalents expand identically).
        string rawSlice = openTag.Substring(valueStartInTag, valueEndInTag - valueStartInTag);
        if (!AttributeSliceMatches(rawSlice, expectedLogical))
        {
            return null;
        }

        int valueStart = start + valueStartInTag;
        int length = valueEndInTag - valueStartInTag;
        return new ValueSpan(valueStart, length, elementLine, elementLinePos + (valueStartInTag - (nameStart - start)));
    }

    /// <summary>Finds attribute 'name' in the opening tag; spans exclude the surrounding quotes.</summary>
    private static bool TryFindAttributeValue(string openTag, string attrName, out int valueStart, out int valueEnd)
    {
        valueStart = valueEnd = -1;
        int i = 0;
        while (true)
        {
            int idx = openTag.IndexOf(attrName + "=", i, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            // Must follow a whitespace boundary (avoids matching inside other names/values).
            if (idx > 0 && !(openTag[idx - 1] == ' ' || openTag[idx - 1] == '\t' || openTag[idx - 1] == '\n'))
            {
                i = idx + attrName.Length + 1;
                continue;
            }

            int eq = idx + attrName.Length;
            char quote = openTag[eq + 1];
            if (quote != '"' && quote != '\'')
            {
                i = eq + 2;
                continue;
            }

            int close = openTag.IndexOf(quote, eq + 2);
            if (close < 0)
            {
                return false;
            }

            valueStart = eq + 2;
            valueEnd = close;
            return true;
        }
    }

    private static bool AttributeSliceMatches(string rawSlice, string? expectedLogical)
    {
        if (expectedLogical is null)
        {
            return false;
        }

        if (rawSlice == expectedLogical)
        {
            return true;
        }

        try
        {
            using var sr = new StringReader($"<e v=\"{rawSlice}\"/>");
            using var r = XmlReader.Create(sr, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            r.MoveToContent();
            string? decoded = r.GetAttribute("v");
            return decoded == expectedLogical;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static DeclarationField MissingField(Model.FieldKind kind, string propName) =>
        new(kind, false, null, null, Model.FieldCapability.Missing,
            $"The declaration has no '{propName}' property in the observed shape.");

    private static void SkipElement(XmlReader reader, int depth)
    {
        if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                {
                    break;
                }
            }
        }
    }
}
