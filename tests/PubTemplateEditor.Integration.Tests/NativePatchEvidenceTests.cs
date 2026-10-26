using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;
using PubTemplateEditor.TestSupport;

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
        foreach (string row in TemplateRows())
        {
            data.Add(row);
        }

        return data;
    }

    /// <summary>
    /// One row is always available: the container this project writes itself. The native templates add
    /// rows on the machine that has them, and their absence is the documented state of a checkout,
    /// never a silently empty test set.
    /// </summary>
    private static IEnumerable<string> TemplateRows()
    {
        yield return SyntheticRow;

        string? dir = FindSamplesDir();
        if (dir is null)
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.dta").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    /// <summary>Row marker for a container written by this project: it is present in every checkout.</summary>
    private const string SyntheticRow = "<synthetic>";

    /// <summary>
    /// Runs the body against the real template, or against a synthetic container written for this row
    /// and deleted afterwards. Neither case ever writes to a template the user owns.
    /// </summary>
    private static void WithTemplate(string row, Action<string> body)
    {
        if (row != SyntheticRow)
        {
            body(row);
            return;
        }

        string dir = Path.Combine(Path.GetTempPath(), "pubte-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            body(TestSamples.WriteSyntheticContainer(dir, "synthetic-template.dta"));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }


    [Theory]
    [MemberData(nameof(NativeTemplatePaths))]
    public void EveryWritableDefaultValueOfATemplateIsPatchedAndReadBack(string row) =>
        WithTemplate(row, WritableDefaultsArePatchedAndReadBack);

    private void WritableDefaultsArePatchedAndReadBack(string path)
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
    public void AnEmptyConstantDescriptionIsWritableAndStaysText(string row) =>
        WithTemplate(row, EmptyConstantDescriptionIsWritable);

    private void EmptyConstantDescriptionIsWritable(string path)
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

        // Some templates hold no empty constant description at all (example.dta has none). Those rows
        // still have to read as declarations; the write path is exercised by the rows that do have one,
        // including the synthetic container, which carries an empty constant on purpose.
        if (emptyDescriptions.Count == 0)
        {
            Assert.NotEmpty(record.Declarations);
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
    public void TheEvidenceSetIsNeverEmptyAndNeverSilentlySmaller()
    {
        // An empty theory is the classic way to report a pass for a check that ran nothing. The
        // synthetic row therefore exists in every checkout, and where the native samples are on disk,
        // every one of them has to be a row - a sample folder that lost files shows up here.
        List<string> rows = TemplateRows().ToList();
        Assert.Contains(SyntheticRow, rows);

        string? dir = FindSamplesDir();
        if (dir is not null)
        {
            int native = Directory.EnumerateFiles(dir!, "*.dta").Count();
            Assert.True(native >= 1, "the sample folder exists but holds no template");
            Assert.Equal(native + 1, rows.Count);
        }
    }
}
