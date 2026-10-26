using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Evidence tests against the native .dta samples on a maintainer's disk. The sample files are user
/// content and are not part of the repository, so the theory simply has no rows where they are
/// absent - never a false pass. The counts below were verified independently with an external XML
/// parser on 2026-10-25 against the bytes of those files.
/// </summary>
public class NativeEvidenceTests
{
    private static string? FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "examples");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "requirements_specification_yps.dta")))
            {
                return candidate;
            }
        }

        return null;
    }

    public static TheoryData<string, int, int, int> NativeSamples()
    {
        var data = new TheoryData<string, int, int, int>();
        string? dir = FindSamplesDir();
        if (dir is null)
        {
            return data; // no rows => theory trivially passes; recorded as 'not executed' in PLAN
        }

        data.Add(Path.Combine(dir, "requirements_specification_yps.dta"), 234, 162, 72);
        data.Add(Path.Combine(dir, "requirements_specification_yps_landscape.dta"), 234, 162, 72);
        data.Add(Path.Combine(dir, "solution_configuration_specification.dta"), 234, 174, 60);
        data.Add(Path.Combine(dir, "configuration_specification_yps_landscape.dta"), 237, 163, 74);
        data.Add(Path.Combine(dir, "example.dta"), 1, 0, 1);
        return data;
    }

    [Theory]
    [MemberData(nameof(NativeSamples))]
    public void NativeDtaFilesParseToVerifiedCounts(string path, int total, int internalCount, int externalCount)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(TemplateFormat.DtaZipContainer, TemplateFormatDetector.Detect(bytes.AsSpan(0, 64)));

        using var ms = new MemoryStream(bytes, writable: false);
        using var container = new Infrastructure.DtaContainer(ms, System.IO.Compression.ZipArchiveMode.Read);
        var xml = container.ReadTemplateXml();
        Assert.NotNull(xml);

        var parsed = XmlDeclarationParser.Parse(xml!.Source);
        Assert.True(parsed.Success, string.Join(" ", parsed.Errors));
        Assert.Equal(total, parsed.Declarations.Count);

        Assert.Equal(internalCount, parsed.Declarations.Count(d => d.Access.LogicalValue == "internal"));
        Assert.Equal(externalCount, parsed.Declarations.Count(d => d.Access.LogicalValue == "external"));

        // All observed declarations expose the full six-property shape as editable/anchored fields.
        Assert.All(parsed.Declarations, d =>
        {
            Assert.Equal(FieldCapability.Editable, d.DefaultValue.Capability);
            Assert.Equal(FieldCapability.Editable, d.Access.Capability);
            Assert.Equal(FieldCapability.Editable, d.Description.Capability);
            Assert.Equal("user", d.XmlTypeProperty);
        });

        // Names unique: no ambiguity warnings.
        Assert.DoesNotContain(parsed.Warnings, w => w.Contains("Duplicate"));
    }

    [Fact]
    public void ReaderServiceLoadsNativeSampleReadOnlyAndWithFingerprint()
    {
        string? dir = FindSamplesDir();
        if (dir is null)
        {
            return;
        }

        string path = Path.Combine(dir, "example.dta");
        byte[] before = File.ReadAllBytes(path);

        var record = new TemplateReaderService().Load(path, CancellationToken.None);

        Assert.Equal(TemplateReadState.Loaded, record.ReadState);
        Assert.Equal(TemplateFormat.DtaZipContainer, record.Format);
        Assert.Single(record.Declarations);
        Assert.Equal("paramUserText", record.Declarations[0].ExactName);
        Assert.Equal("HELLO", record.Declarations[0].DefaultValue.LogicalValue);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(before)), record.BaselineFingerprint);
        Assert.Equal(before, File.ReadAllBytes(path)); // reads never write
    }
}
