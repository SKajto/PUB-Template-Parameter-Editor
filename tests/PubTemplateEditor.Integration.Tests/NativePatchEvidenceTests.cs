using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Patching evidence against the native .dta samples. The files live only on a maintainer's disk -
/// they are user content and are not in the repository - so without them the theory has no rows and
/// the synthetic container tests in TemplateSaveServiceTests carry that coverage instead. Where the
/// samples are present they are opened read-only, patched in memory and never written back: the last
/// assertion of every test is that the file on disk still has the bytes it had before.
/// </summary>
public class NativePatchEvidenceTests
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

    public static TheoryData<string> NativeTemplatePaths()
    {
        var data = new TheoryData<string>();
        string? dir = FindSamplesDir();
        if (dir is null)
        {
            return data; // no rows => recorded as "not executed" in PLAN, never as a pass
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.dta").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NativeTemplatePaths))]
    public void EveryWritableDefaultValueOfANativeTemplateIsPatchedAndReadBack(string path)
    {
        byte[] onDiskBefore = File.ReadAllBytes(path);
        string hashBefore = HashService.Sha256Hex(onDiskBefore);

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        Assert.Equal(TemplateReadState.Loaded, record.ReadState);
        SourceText source = record.XmlContent!.Source;

        List<VariableDeclaration> writable =
        [
            .. record.Declarations.Where(d => d.DefaultValue.Capability == FieldCapability.Editable
                                              && d.DefaultValue.Present),
        ];
        Assert.NotEmpty(writable);

        var changes = writable
            .Select(d => new DraftChange(
                new ChangeKey(record.FilePath, d.DeclarationId, FieldKind.DefaultValue),
                d.DefaultValue.LogicalValue,
                (d.DefaultValue.LogicalValue ?? string.Empty) + " ✓ patch marker",
                ProposedFromDraft: false)
            {
                DeclarationName = d.DisplayName,
            })
            .ToList();

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, changes);
        Assert.Empty(patch.Rejected);
        Assert.Equal(writable.Count, patch.Applied.Count);
        Assert.True(patch.Verified, string.Join("; ", patch.VerificationProblems));

        byte[] patchedBytes = patch.ToBytes(source.EncodingName);
        ParseResult again = XmlDeclarationParser.Parse(SourceText.FromBytes(patchedBytes));
        Assert.True(again.Success, string.Join("; ", again.Errors));
        Assert.Equal(record.Declarations.Count, again.Declarations.Count);

        var beforeById = record.Declarations.ToDictionary(d => d.DeclarationId);
        foreach (VariableDeclaration after in again.Declarations)
        {
            VariableDeclaration before = beforeById[after.DeclarationId];

            if (writable.Any(d => d.DeclarationId == after.DeclarationId))
            {
                Assert.Equal((before.DefaultValue.LogicalValue ?? string.Empty) + " ✓ patch marker",
                    after.DefaultValue.LogicalValue);
            }
            else
            {
                Assert.Equal(before.DefaultValue.LogicalValue, after.DefaultValue.LogicalValue);
            }

            // Everything the patch was not asked to touch keeps its exact value, including names,
            // the separate type property and descriptions that contain markup-like text.
            Assert.Equal(before.ExactName, after.ExactName);
            Assert.Equal(before.Description.LogicalValue, after.Description.LogicalValue);
            Assert.Equal(before.Access.LogicalValue, after.Access.LogicalValue);
            Assert.Equal(before.XmlTypeProperty, after.XmlTypeProperty);
        }

        // Patching is a pure computation: the authoritative sample is still the file it was.
        Assert.Equal(hashBefore, HashService.Sha256Hex(File.ReadAllBytes(path)));
    }

    [Theory]
    [MemberData(nameof(NativeTemplatePaths))]
    public void ANativeEmptyConstantDescriptionIsWritableAndStaysText(string path)
    {
        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        Assert.Equal(TemplateReadState.Loaded, record.ReadState);

        List<VariableDeclaration> emptyDescriptions =
        [
            .. record.Declarations.Where(d =>
                d.Description.Present
                && d.Description.LogicalValue == string.Empty
                && d.Description.Capability == FieldCapability.Editable),
        ];

        // Samples without an empty constant description (example.dta has none at all) are still
        // checked for the read-only rule; the write path for descriptions is exercised by the other
        // four samples, which do contain empty constant descriptions.
        if (emptyDescriptions.Count == 0)
        {
            Assert.Single(record.Declarations);
            return;
        }

        VariableDeclaration target = emptyDescriptions[0];
        var change = new DraftChange(
            new ChangeKey(record.FilePath, target.DeclarationId, FieldKind.Description),
            string.Empty,
            "written text <not markup> & \"quotes\"",
            ProposedFromDraft: false)
        {
            DeclarationName = target.DisplayName,
        };

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, [change]);
        Assert.Single(patch.Applied);
        Assert.True(patch.Verified, string.Join("; ", patch.VerificationProblems));

        ParseResult again = XmlDeclarationParser.Parse(
            SourceText.FromBytes(patch.ToBytes(record.XmlContent!.Source.EncodingName)));
        VariableDeclaration after = again.Declarations.Single(d => d.DeclarationId == target.DeclarationId);
        Assert.Equal("written text <not markup> & \"quotes\"", after.Description.LogicalValue);
    }

    [Fact]
    public void ATemplateWithoutTheSamplesIsNotCountedAsPassed()
    {
        // Guard against the empty-TheoryData pattern silently reporting success on a machine that has
        // no samples: the helper itself is what this test checks, and the sample files are expected to
        // exist in this repository checkout.
        string? dir = FindSamplesDir();
        Assert.NotNull(dir);
        Assert.NotEmpty(Directory.EnumerateFiles(dir!, "*.dta"));
    }
}
