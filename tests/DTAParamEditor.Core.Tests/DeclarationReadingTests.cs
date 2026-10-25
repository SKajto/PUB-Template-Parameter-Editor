using DTAParamEditor.Core.Format;
using DTAParamEditor.Core.Model;

namespace DTAParamEditor.Core.Tests;

/// <summary>Immutable inline fixtures shaped exactly like the observed native declaration layout.</summary>
internal static class Fixtures
{
    /// <summary>Two-variable template exercising every supported field shape.</summary>
    public const string TwoVariableTemplate =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<template><metadata><property name=\"version\" value=\"2\"></property></metadata>" +
        "<dataset><variables>" +
        "<variable><feature tag=\"definition\">" +
        "<property name=\"name\" value=\"alpha\"></property>" +
        "<property name=\"description\"><expression type=\"constant\">First parameter</expression></property>" +
        "<property name=\"type\" value=\"user\"></property>" +
        "<property name=\"access\" value=\"internal\"></property>" +
        "<property name=\"default\" value=\"HELLO\"></property>" +
        "<property name=\"central variable\" value=\"\"></property>" +
        "</feature></variable>" +
        "<variable><feature tag=\"definition\">" +
        "<property name=\"name\" value=\"beta\"></property>" +
        "<property name=\"description\"><expression type=\"constant\">Second &amp; special</expression></property>" +
        "<property name=\"type\" value=\"user\"></property>" +
        "<property name=\"access\" value=\"external\"></property>" +
        "<property name=\"default\" value=\"\"></property>" +
        "<property name=\"central variable\" value=\"\"></property>" +
        "</feature></variable>" +
        "</variables></dataset>" +
        "<styles><style><feature tag=\"definition\"><property name=\"name\" value=\"Title\"></property></feature></style></styles>" +
        "<content><element id=\"1\" tag=\"container\"><expression tag=\"content\" type=\"script\"><code>alpha + beta</code>" +
        "<expression_variables><expression_variable type=\"variable\" context=\"\">alpha</expression_variable></expression_variables>" +
        "</expression></element></content></template>";

    public static SourceText Source(string xml) => SourceText.FromBytes(
        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml));
}

public class FormatDetectionTests
{
    [Fact]
    public void ZipMagicIsDetectedAsContainer()
    {
        byte[] bytes = [0x50, 0x4B, 0x03, 0x04, .. new byte[32]];
        Assert.Equal(TemplateFormat.DtaZipContainer, TemplateFormatDetector.Detect(bytes));
    }

    [Fact]
    public void XmlPrologIsDetectedAsPlainTemplate()
    {
        byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes("<?xml version=\"1.0\"?><template/>");
        Assert.Equal(TemplateFormat.PlainXmlTemplate, TemplateFormatDetector.Detect(bytes));
    }

    [Fact]
    public void BomPrefixedXmlIsDetected()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. new System.Text.UTF8Encoding(false).GetBytes("<?xml version=\"1.0\"?><template/>")];
        Assert.Equal(TemplateFormat.PlainXmlTemplate, TemplateFormatDetector.Detect(bytes));
    }

    [Fact]
    public void UnknownBytesAreRejected()
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes("BINARY?");
        Assert.Equal(TemplateFormat.Unknown, TemplateFormatDetector.Detect(bytes));
    }
}

public class DeclarationReadingTests
{
    [Fact]
    public void ReadsBothDeclarationsAndSkipsExpressionReferences()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));

        Assert.True(parsed.Success);
        Assert.Equal(2, parsed.Declarations.Count);

        var alpha = parsed.Declarations[0];
        Assert.Equal("alpha", alpha.ExactName);
        Assert.Equal("HELLO", alpha.DefaultValue.LogicalValue);
        Assert.Equal("internal", alpha.Access.LogicalValue);
        Assert.Equal("First parameter", alpha.Description.LogicalValue);
        Assert.Equal("user", alpha.XmlTypeProperty);
        Assert.Equal(FieldCapability.Editable, alpha.DefaultValue.Capability);
        Assert.Equal(FieldCapability.Editable, alpha.Access.Capability);
        Assert.Equal(FieldCapability.Editable, alpha.Description.Capability);
        Assert.True(alpha.Name.Present); // name capability itself is enforced at the edit layer

        var beta = parsed.Declarations[1];
        Assert.Equal("beta", beta.ExactName);
        Assert.Equal(string.Empty, beta.DefaultValue.LogicalValue); // empty default distinct from missing
        Assert.Equal("Second & special", beta.Description.LogicalValue); // entity decoded for display
    }

    [Fact]
    public void NamePropertyIsRecordedPresentAndItsSpanAnchored()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));
        var alpha = parsed.Declarations[0];

        Assert.True(alpha.Name.Present);
        Assert.Equal("alpha", alpha.Name.LogicalValue);
        Assert.NotNull(alpha.Name.Span);
        int start = alpha.Name.Span!.Value.StartChar;
        Assert.Equal("alpha", Fixtures.TwoVariableTemplate.Substring(start, alpha.Name.Span!.Value.LengthChar));
    }

    [Fact]
    public void ExactValuesArePreservedIncludingUnicodeQuotesAndWhitespace()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"weird\"></property>" +
            "<property name=\"description\"><expression type=\"constant\">Ünïcödé ✓</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            "<property name=\"access\" value=\"external\"></property>" +
            "<property name=\"default\" value=\"  padded &amp; 'quoted' &quot;dq&quot; A&#65;&#x42; &#10;\"></property>" +
            "</feature></variable></variables></dataset></template>";

        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        Assert.Single(parsed.Declarations);
        string value = parsed.Declarations[0].DefaultValue.LogicalValue!;

        Assert.Equal("  padded & 'quoted' \"dq\" AAB \n", value);
        Assert.StartsWith("  ", value); // never trimmed
        Assert.NotNull(parsed.Declarations[0].DefaultValue.Span);
    }

    [Fact]
    public void AttributeSpansVerifyAgainstLogicalValues()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));
        string text = Fixtures.TwoVariableTemplate;

        foreach (var decl in parsed.Declarations)
        {
            foreach (var kind in new[] { FieldKind.DefaultValue, FieldKind.Access, FieldKind.Name })
            {
                var field = decl.GetField(kind);
                Assert.NotNull(field.Span);
                string raw = text.Substring(field.Span!.Value.StartChar, field.Span.Value.LengthChar);
                Assert.Equal(field.LogicalValue, raw); // no entities in these fixtures: raw must equal logical exactly
            }
        }
    }

    [Fact]
    public void DescriptionSpanPointsAtRawTextIncludingEntities()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));
        var field = parsed.Declarations[1].Description;
        Assert.Equal(FieldCapability.Editable, field.Capability);
        string raw = Fixtures.TwoVariableTemplate.Substring(field.Span!.Value.StartChar, field.Span.Value.LengthChar);
        Assert.Equal("Second &amp; special", raw);
        Assert.Equal("Second & special", field.LogicalValue);
    }

    [Fact]
    public void DuplicateNamesAreWarnedAndBothKept()
    {
        string xml = Fixtures.TwoVariableTemplate.Replace("value=\"beta\"", "value=\"alpha\"");
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));

        Assert.Equal(2, parsed.Declarations.Count);
        Assert.Contains(parsed.Warnings, w => w.Contains("Duplicate declaration name 'alpha'"));
    }

    [Fact]
    public void NonConstantDescriptionsStayReadOnly()
    {
        string xml = Fixtures.TwoVariableTemplate.Replace(
            "<expression type=\"constant\">First parameter</expression>",
            "<expression type=\"script\">name + \"x\"</expression>");
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));

        var field = parsed.Declarations[0].Description;
        Assert.Equal(FieldCapability.ReadOnlyUnsupported, field.Capability);
        Assert.Contains("constant", field.CapabilityReason);
    }

    [Fact]
    public void UnobservedAccessValueStaysReadOnly()
    {
        string xml = Fixtures.TwoVariableTemplate.Replace("access\" value=\"internal\"", "access\" value=\"public\"");
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));

        var field = parsed.Declarations[0].Access;
        Assert.Equal(FieldCapability.ReadOnlyUnsupported, field.Capability);
        Assert.Equal("public", field.LogicalValue);
    }

    [Fact]
    public void MissingPropertiesAreDistinctFromEmptyValues()
    {
        const string xml =
            "<?xml version=\"1.0\"?><template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"sparse\"></property>" +
            "<property name=\"default\" value=\"\"></property>" +
            "</feature></variable></variables></dataset></template>";

        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        var decl = parsed.Declarations.Single();

        Assert.Equal(FieldCapability.Missing, decl.Description.Capability);
        Assert.False(decl.Description.Present);
        Assert.Equal(FieldCapability.Editable, decl.DefaultValue.Capability);
        Assert.Equal(string.Empty, decl.DefaultValue.LogicalValue);
    }

    [Fact]
    public void LiteralNewlineInAttributeValueIsReadOnlyForNormalization()
    {
        // A literal CR/LF inside an attribute value is collapsed by XML normalization;
        // verbatim comparison is impossible, so the field must stay read-only.
        const string xml =
            "<?xml version=\"1.0\"?><template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"crlf\"></property>" +
            "<property name=\"default\" value=\"a\r\nb\"></property>" +
            "</feature></variable></variables></dataset></template>";

        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        var decl = parsed.Declarations.Single();

        Assert.Equal(FieldCapability.ReadOnlyUnsupported, decl.DefaultValue.Capability);
        Assert.Contains(parsed.Warnings, w => w.Contains("literal newlines", StringComparison.Ordinal));
    }

    [Fact]
    public void StyleNamesAreNeverTreatedAsDeclarations()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));
        Assert.DoesNotContain(parsed.Declarations, d => d.ExactName == "Title");
    }

    [Fact]
    public void ExpressionVariableReferencesAreNotDeclarations()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(Fixtures.TwoVariableTemplate));
        Assert.DoesNotContain(parsed.Declarations, d => d.DeclarationId > 1);
        Assert.Equal(2, parsed.Declarations.Count);
    }

    [Fact]
    public void MalformedXmlProducesRecoverableError()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source("<template><dataset><variables><variable>"));
        Assert.False(parsed.Success);
        Assert.NotEmpty(parsed.Errors);
    }

    [Fact]
    public void DtdIsRejected()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE template SYSTEM \"x.dtd\"><template><dataset><variables/></dataset></template>";
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        Assert.False(parsed.Success);
    }

    [Fact]
    public void EmptyVariablesSectionIsAllowed()
    {
        const string xml = "<?xml version=\"1.0\"?><template><dataset><variables></variables></dataset></template>";
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        Assert.True(parsed.Success);
        Assert.Empty(parsed.Declarations);
    }

    [Fact]
    public void WrongRootElementIsRejected()
    {
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source("<?xml version=\"1.0\"?><nottemplate/>"));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Errors, e => e.Contains("Unexpected root element"));
    }
}

public class SourceTextTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Utf8BomAndPlainBothDecode(bool bom)
    {
        byte[] payload = bom
            ? [0xEF, 0xBB, 0xBF, .. new System.Text.UTF8Encoding(false).GetBytes("<template/>")]
            : new System.Text.UTF8Encoding(false).GetBytes("<template/>");
        var source = SourceText.FromBytes(payload);
        Assert.Equal(bom ? "\uFEFF<template/>" : "<template/>", source.Text);
        Assert.Equal(bom ? TextEncodingKind.Utf8Bom : TextEncodingKind.Utf8, source.EncodingName);
        Assert.Equal("<template/>", source.Text.TrimStart('\uFEFF'));
    }

    [Fact]
    public void Utf16LeIsDetectedAndDecoded()
    {
        byte[] payload = System.Text.Encoding.Unicode.GetBytes("<template/>");
        byte[] withBom = [0xFF, 0xFE, .. payload];
        var source = SourceText.FromBytes(withBom);
        Assert.Equal(TextEncodingKind.Utf16Le, source.EncodingName);
        Assert.Equal("<template/>", source.Text.TrimStart('\uFEFF'));
    }

    [Fact]
    public void LineOffsetsRoundTripWithCrlf()
    {
        string text = "abc\r\ndefg\r\n";
        var source = new SourceText(System.Text.Encoding.UTF8.GetBytes(text), TextEncodingKind.Utf8, text);
        Assert.Equal(0, source.GetOffset(1, 1));
        Assert.Equal(5, source.GetOffset(2, 1)); // after "abc\r\n"
    }

    [Fact]
    public void SingleQuotedAttributeIsSpanAnchorable()
    {
        const string xml =
            "<?xml version=\"1.0\"?><template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name='name' value='qname'></property>" +
            "<property name='access' value='internal'></property>" +
            "</feature></variable></variables></dataset></template>";
        var parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        var decl = parsed.Declarations.Single();
        Assert.Equal("qname", decl.Name.LogicalValue);
        Assert.NotNull(decl.Access.Span);
        Assert.Equal("internal", xml.Substring(decl.Access.Span!.Value.StartChar, decl.Access.Span.Value.LengthChar));
    }
}

public class LineDiffTests
{
    [Fact]
    public void DetectsWhitespaceAndCaseDifferences()
    {
        var diff = LineDiff.Diff("line one\nline two", "line one\nLine  two");
        Assert.Contains(diff, d => d.Changed);
        Assert.DoesNotContain(diff, d => d.Changed && d.OldText == "line one");
    }

    [Fact]
    public void EmptyInputsProduceNoChangedLines()
    {
        Assert.All(LineDiff.Diff("", ""), d => Assert.False(d.Changed));
    }
}
