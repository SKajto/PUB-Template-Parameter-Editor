using System.Text;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Tests;

/// <summary>
/// Source-preserving patching. The requirement is not "produces valid XML" but "changes exactly the
/// characters of the value that was edited": these tests compare the patched text against the
/// original region by region, so an accidental re-serialization or a shifted offset fails loudly.
/// Nothing here touches disk.
/// </summary>
public class TemplatePatcherTests
{
    private const string FileA = @"C:\templates\a.dta";

    private const string Template =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
        "<template>\r\n" +
        "  <metadata>\r\n" +
        "    <property name=\"version\" value=\"2\"></property>\r\n" +
        "    <property name=\"script\" value=\"&gt;= 1 &amp; 2\"></property>\r\n" +
        "  </metadata>\r\n" +
        "  <dataset>\r\n" +
        "    <variables>\r\n" +
        "      <variable>\r\n" +
        "        <feature tag=\"definition\">\r\n" +
        "          <property name=\"name\" value=\"alpha\"></property>\r\n" +
        "          <property name=\"description\"><expression type=\"constant\">first &lt; note</expression></property>\r\n" +
        "          <property name=\"type\" value=\"user\"></property>\r\n" +
        "          <property name=\"access\" value=\"internal\"></property>\r\n" +
        "          <property name=\"default\" value=\"OLD_A\"></property>\r\n" +
        "        </feature>\r\n" +
        "      </variable>\r\n" +
        "      <variable>\r\n" +
        "        <feature tag=\"definition\">\r\n" +
        "          <property name=\"name\" value=\"beta\"></property>\r\n" +
        "          <property name=\"description\"><expression type=\"constant\">B</expression></property>\r\n" +
        "          <property name=\"type\" value=\"user\"></property>\r\n" +
        "          <property name=\"access\" value=\"external\"></property>\r\n" +
        "          <property name=\"default\" value=\"B_DEFAULT\"></property>\r\n" +
        "        </feature>\r\n" +
        "      </variable>\r\n" +
        "    </variables>\r\n" +
        "    <layout><image id=\"picture\" data=\"aGVsbG8=\"/><script><![CDATA[x = 1 < 2;]]></script></layout>\r\n" +
        "  </dataset>\r\n" +
        "</template>\r\n";

    private static TemplateRecord Loaded(string path = FileA, string xml = Template)
    {
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
        SourceText source = SourceText.FromBytes(bytes);
        ParseResult parsed = XmlDeclarationParser.Parse(source);
        Assert.True(parsed.Success, string.Join("; ", parsed.Errors));

        return new TemplateRecord
        {
            FilePath = path,
            Format = TemplateFormat.PlainXmlTemplate,
            ReadState = TemplateReadState.Loaded,
            XmlContent = new TemplateXmlContent
            {
                EntryName = "template.xml",
                EntryBytes = bytes,
                Source = source,
                EntryFingerprint = "baseline",
            },
            Declarations = parsed.Declarations,
            Warnings = parsed.Warnings,
            BaselineFingerprint = "baseline",
        };
    }

    private static DraftChange Change(
        TemplateRecord record,
        string declarationName,
        FieldKind field,
        string proposed,
        string? baseline = null)
    {
        VariableDeclaration declaration = record.Declarations.First(d => d.ExactName == declarationName);
        return new DraftChange(
            new ChangeKey(record.FilePath, declaration.DeclarationId, field),
            baseline ?? declaration.GetField(field).LogicalValue,
            proposed,
            ProposedFromDraft: false)
        {
            DeclarationName = declarationName,
        };
    }

    /// <summary>
    /// The strongest form of the preservation claim: take the original text, splice the new value
    /// into the exact span the parse recorded, and require the patch output to equal that character
    /// for character. Anything else - re-serialization, normalized whitespace, a shifted offset - fails.
    /// </summary>
    private static string Spliced(TemplateRecord record, string declarationName, FieldKind field, string replacement)
    {
        ValueSpan span = record.Declarations.First(d => d.ExactName == declarationName).GetField(field).Span!.Value;
        return record.XmlContent!.Source.Text.Remove(span.StartChar, span.LengthChar).Insert(span.StartChar, replacement);
    }

    [Fact]
    public void ADefaultChangeRewritesOnlyTheCharactersOfThatValue()
    {
        TemplateRecord record = Loaded();
        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "alpha", FieldKind.DefaultValue, "NEW_A")]);

        Assert.True(patch.Verified);
        Assert.Single(patch.Applied);
        Assert.Empty(patch.Rejected);

        Assert.Equal(Spliced(record, "alpha", FieldKind.DefaultValue, "NEW_A"), patch.PatchedText);

        // Everything outside the value — the BOM-less header, CRLF line ends, indentation, the CDATA
        // script, the image element and the other declaration — is untouched.
        Assert.True(patch.PatchedText.Contains("<script><![CDATA[x = 1 < 2;]]></script>", StringComparison.Ordinal));
        Assert.True(patch.PatchedText.Contains("<image id=\"picture\" data=\"aGVsbG8=\"/>", StringComparison.Ordinal));
        Assert.True(patch.PatchedText.Contains("value=\"&gt;= 1 &amp; 2\"", StringComparison.Ordinal));
        Assert.Equal(Template.Length + "NEW_A".Length - "OLD_A".Length, patch.PatchedText.Length);
    }

    [Fact]
    public void SeveralChangesInOneTemplateKeepEveryOffsetHonest()
    {
        TemplateRecord record = Loaded();
        var changes = new[]
        {
            Change(record, "alpha", FieldKind.DefaultValue, "LONGER_VALUE_THAN_BEFORE"),
            Change(record, "alpha", FieldKind.Description, "second < note & more"),
            Change(record, "beta", FieldKind.Access, "internal"),
            Change(record, "beta", FieldKind.DefaultValue, "B"),
        };

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, changes);

        Assert.True(patch.Verified);
        Assert.Equal(4, patch.Applied.Count);
        Assert.Empty(patch.Rejected);
        Assert.NotEqual(record.XmlContent!.Source.Text, patch.PatchedText);

        // The patched text is parsed by the same reader that produced the spans, which is the only
        // proof that the later edits landed on their own value and not on shifted characters.
        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patch.ToBytes(TextEncodingKind.Utf8)));
        Assert.True(again.Success, string.Join("; ", again.Errors));
        Assert.Equal(2, again.Declarations.Count);

        VariableDeclaration alpha = again.Declarations.First(d => d.ExactName == "alpha");
        VariableDeclaration beta = again.Declarations.First(d => d.ExactName == "beta");
        Assert.Equal("LONGER_VALUE_THAN_BEFORE", alpha.DefaultValue.LogicalValue);
        Assert.Equal("second < note & more", alpha.Description.LogicalValue);
        Assert.Equal("internal", beta.Access.LogicalValue);
        Assert.Equal("B", beta.DefaultValue.LogicalValue);

        // Untouched fields keep their values: beta's description was not part of the plan.
        Assert.Equal("B", beta.Description.LogicalValue);
        Assert.Equal("user", alpha.XmlTypeProperty);
        Assert.Equal(2, again.Declarations.Count(d => d.Name.Present));

    }

    [Fact]
    public void ValuesNeedingEscapesAreWrittenEscapedAndReadBackUnchanged()
    {
        TemplateRecord record = Loaded();
        const string tricky = "5 < 6 & 7 > 3 \"quoted\"";

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "alpha", FieldKind.DefaultValue, tricky)]);
        Assert.True(patch.Verified);
        Assert.True(patch.PatchedText.Contains("value=\"5 &lt; 6 &amp; 7 &gt; 3 &quot;quoted&quot;\"", StringComparison.Ordinal));

        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patch.ToBytes(TextEncodingKind.Utf8)));
        Assert.Equal(tricky, again.Declarations.First(d => d.ExactName == "alpha").DefaultValue.LogicalValue);
    }

    [Fact]
    public void ADescendingTextValueIsWrittenAsTextNotAsMarkup()
    {
        TemplateRecord record = Loaded();
        TemplatePatch patch = TemplateXmlPatcher.Patch(
            record,
            [Change(record, "beta", FieldKind.Description, "a < b & c > d")]);

        Assert.True(patch.Verified);
        Assert.True(patch.PatchedText.Contains("<expression type=\"constant\">a &lt; b &amp; c &gt; d</expression>", StringComparison.Ordinal));

        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patch.ToBytes(TextEncodingKind.Utf8)));
        Assert.Equal("a < b & c > d", again.Declarations.First(d => d.ExactName == "beta").Description.LogicalValue);
    }

    [Fact]
    public void AnAttributeCannotContainALineBreak()
    {
        TemplateRecord record = Loaded();
        TemplatePatch patch = TemplateXmlPatcher.Patch(
            record,
            [Change(record, "alpha", FieldKind.DefaultValue, "one\r\ntwo")]);

        Assert.Empty(patch.Applied);
        Assert.Single(patch.Rejected);
        Assert.Contains("line break", patch.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(record.XmlContent!.Source.Text, patch.PatchedText);
    }

    [Fact]
    public void NamesAreNeverWritten()
    {
        TemplateRecord record = Loaded();
        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "alpha", FieldKind.Name, "renamed")]);

        Assert.Empty(patch.Applied);
        Assert.Single(patch.Rejected);
        Assert.Contains("read-only", patch.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(record.XmlContent!.Source.Text, patch.PatchedText);
        Assert.True(patch.IsEmpty);
    }

    [Fact]
    public void AChangeWhoseSourceSpanNoLongerHoldsTheStagedBaselineIsRefused()
    {
        TemplateRecord record = Loaded();

        // Staged against a value the file does not contain: the fingerprint check happens earlier, but
        // the span check is what proves the file was not edited by someone else in the meantime.
        DraftChange stale = Change(record, "alpha", FieldKind.DefaultValue, "NEW", baseline: "SOME_OTHER_VALUE");
        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [stale]);

        Assert.Empty(patch.Applied);
        Assert.Single(patch.Rejected);
        Assert.Contains("no longer holds", patch.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(record.XmlContent!.Source.Text, patch.PatchedText);
        Assert.True(patch.Verified); // an all-refused patch is a no-op, and says so
    }

    [Fact]
    public void AChangeStagedForAnotherFileIsRefused()
    {
        TemplateRecord record = Loaded();
        VariableDeclaration declaration = record.Declarations.First(d => d.ExactName == "alpha");

        var foreign = new DraftChange(
            new ChangeKey(@"C:\templates\other.dta", declaration.DeclarationId, FieldKind.DefaultValue),
            declaration.DefaultValue.LogicalValue,
            "MUST_NOT_APPEAR",
            ProposedFromDraft: false)
        {
            DeclarationName = "alpha",
        };

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [foreign]);

        Assert.Empty(patch.Applied);
        Assert.Contains("different template file", patch.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MUST_NOT_APPEAR", patch.PatchedText, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsentPropertyIsNotCreated()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"nodefault\"></property>" +
            "<property name=\"description\"><expression type=\"constant\">d</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            "<property name=\"access\" value=\"internal\"></property>" +
            "</feature></variable></variables></dataset></template>";

        TemplateRecord record = Loaded(FileA, xml);
        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "nodefault", FieldKind.DefaultValue, "created")]);

        Assert.Empty(patch.Applied);
        Assert.Contains("no such property", patch.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(record.XmlContent!.Source.Text, patch.PatchedText);
    }

    [Fact]
    public void NonAsciiValuesAndABomSurviveTheRoundTrip()
    {
        const string xml =
            "\uFEFF<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<template><dataset><variables><variable><feature tag=\"definition\">" +
            "<property name=\"name\" value=\"größe\"></property>" +
            "<property name=\"description\"><expression type=\"constant\">Änderung</expression></property>" +
            "<property name=\"type\" value=\"user\"></property>" +
            "<property name=\"access\" value=\"internal\"></property>" +
            "<property name=\"default\" value=\"Grün\"></property>" +
            "</feature></variable></variables></dataset></template>";

        byte[] originalBytes = new UTF8Encoding(true).GetBytes(xml);
        SourceText source = SourceText.FromBytes(originalBytes);
        Assert.Equal(TextEncodingKind.Utf8Bom, source.EncodingName);

        ParseResult parsed = XmlDeclarationParser.Parse(source);
        Assert.True(parsed.Success, "parse errors: " + string.Join("; ", parsed.Errors));
        Assert.NotEmpty(parsed.Declarations);
        TemplateRecord record = new()
        {
            FilePath = FileA,
            Format = TemplateFormat.PlainXmlTemplate,
            ReadState = TemplateReadState.Loaded,
            XmlContent = new TemplateXmlContent
            {
                EntryName = "template.xml",
                EntryBytes = originalBytes,
                Source = source,
                EntryFingerprint = "baseline",
            },
            Declarations = parsed.Declarations,
        };

        // Character offsets and byte offsets differ as soon as non-ASCII text appears before the
        // edited value; the patch works in characters and encodes once, at the end.
        TemplatePatch patch = TemplateXmlPatcher.Patch(
            record,
            [new DraftChange(new ChangeKey(FileA, parsed.Declarations[0].DeclarationId, FieldKind.DefaultValue),
                "Grün", "ÄNDERUNG 日本語", false) { DeclarationName = "größe" }]);

        Assert.True(patch.Verified);
        byte[] bytes = patch.ToBytes(source.EncodingName);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(bytes));
        Assert.Equal("ÄNDERUNG 日本語", again.Declarations[0].DefaultValue.LogicalValue);
        Assert.Equal("größe", again.Declarations[0].ExactName);
    }

    [Fact]
    public void EncodingRoundTripsTheOriginalBytesForEveryDetectedEncoding()
    {
        // Encode is used on the whole patched document; if it did not reproduce the original bytes of
        // an unpatched document, the writer would be rewriting the file's shape, not its values.
        (string Label, byte[] Bytes, TextEncodingKind Expected)[] cases =
        [
            ("utf8", new UTF8Encoding(false).GetBytes(Template), TextEncodingKind.Utf8),
            ("utf8+bom", new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(Template)).ToArray(), TextEncodingKind.Utf8Bom),
            ("utf16le", System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(Template)).ToArray(), TextEncodingKind.Utf16Le),
            ("utf16be", new byte[] { 0xFE, 0xFF }.Concat(System.Text.Encoding.BigEndianUnicode.GetBytes(Template)).ToArray(), TextEncodingKind.Utf16Be),
        ];

        foreach ((string label, byte[] bytes, TextEncodingKind expected) in cases)
        {
            SourceText source = SourceText.FromBytes(bytes);
            Assert.Equal(expected, source.EncodingName);
            Assert.True(bytes.SequenceEqual(TemplateXmlText.Encode(source.Text, source.EncodingName)), $"{label}: encoding round trip must reproduce the original bytes");
        }
    }

    [Fact]
    public void APairedEmptyConstantDescriptionGainsItsTextInTheGapBeforeTheEndTag()
    {
        // The dominant empty shape in native templates. There is no text node, so the only anchor is the
        // zero-length region in front of </expression>; anchoring on the end tag's own line info instead
        // writes the value into the tag and corrupts the document.
        string xml = Template.Replace(
            "<expression type=\"constant\">first &lt; note</expression>",
            "<expression type=\"constant\"></expression>",
            StringComparison.Ordinal);
        Assert.NotEqual(Template, xml);
        TemplateRecord record = Loaded(FileA, xml);

        DeclarationField description = record.Declarations[0].Description;
        Assert.Equal(string.Empty, description.LogicalValue);
        Assert.NotNull(description.Span);
        Assert.Equal(0, description.Span!.Value.LengthChar);
        Assert.Equal('<', record.XmlContent!.Source.Text[description.Span.Value.StartChar]);

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "alpha", FieldKind.Description, "first & <last>")]);
        Assert.Single(patch.Applied);
        Assert.True(patch.Verified, string.Join("; ", patch.VerificationProblems));
        Assert.Equal(Spliced(record, "alpha", FieldKind.Description, "first &amp; &lt;last&gt;"), patch.PatchedText);

        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patch.ToBytes(record.XmlContent!.Source.EncodingName)));
        Assert.Equal("first & <last>", again.Declarations[0].Description.LogicalValue);
    }

    [Fact]
    public void AnEmptyValueIsWrittenAsAnEmptyValueNotAsAQuoteOrARemoval()
    {
        TemplateRecord record = Loaded();
        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [Change(record, "alpha", FieldKind.DefaultValue, string.Empty)]);

        Assert.True(patch.Verified);
        Assert.True(patch.PatchedText.Contains("value=\"\"></property>", StringComparison.Ordinal),
            "an empty default must stay an empty attribute, not vanish");

        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patch.ToBytes(TextEncodingKind.Utf8)));
        Assert.Equal(string.Empty, again.Declarations.First(d => d.ExactName == "alpha").DefaultValue.LogicalValue);
        Assert.True(again.Declarations.First(d => d.ExactName == "alpha").DefaultValue.Present);
    }

    [Fact]
    public void APatchThatTheVerificationStepDisagreesWithIsNeverReturnedAsUsable()
    {
        TemplateRecord record = Loaded();

        // Force the patcher to disagree with itself: a span that points at markup instead of a value.
        VariableDeclaration original = record.Declarations.First(d => d.ExactName == "alpha");
        var tampered = record.Declarations.Select(d => d == original ? WithBrokenSpan(d) : d).ToList();
        TemplateRecord broken = new()
        {
            FilePath = record.FilePath,
            Format = record.Format,
            ReadState = record.ReadState,
            XmlContent = record.XmlContent,
            Declarations = tampered,
        };

        TemplatePatch patch = TemplateXmlPatcher.Patch(broken, [Change(broken, "alpha", FieldKind.DefaultValue, "X")]);
        Assert.Empty(patch.Applied);
        Assert.True(patch.Rejected.Count > 0);
        Assert.Equal(record.XmlContent!.Source.Text, patch.PatchedText);
    }

    private static VariableDeclaration WithBrokenSpan(VariableDeclaration d)
    {
        ValueSpan span = d.DefaultValue.Span!.Value;
        return new VariableDeclaration
        {
            DeclarationId = d.DeclarationId,
            ExactName = d.ExactName,
            Name = d.Name,
            Description = d.Description,
            // A span that lands on the surrounding markup rather than on the value: the span check
            // has to refuse the edit before a single character is rewritten.
            DefaultValue = d.DefaultValue with { Span = new ValueSpan(span.StartChar - 8, 5, 0, 0) },
            Access = d.Access,
            XmlTypeProperty = d.XmlTypeProperty,
        };
    }
}
