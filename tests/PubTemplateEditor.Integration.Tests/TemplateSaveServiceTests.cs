using System.IO.Compression;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Save-path tests. Every test works on a copy of a native sample inside a private temporary folder;
/// the files in examples/ are never opened for writing by anything in this class.
/// </summary>
public sealed class TemplateSaveServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _state;

    public TemplateSaveServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pubsave-" + Guid.NewGuid().ToString("N"));
        _state = Path.Combine(_root, "state");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            // A backup taken from a read-only template is read-only too, so clear the attributes first.
            foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary directories are best-effort cleanup.
        }
    }

    private string CopySample(string name = "example.dta")
    {
        string? samples = FindSamplesDir();
        Assert.NotNull(samples);
        string target = Path.Combine(_root, name);
        File.Copy(Path.Combine(samples!, name), target, overwrite: true);
        return target;
    }

    private static string? FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "examples");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "example.dta")))
            {
                return candidate;
            }
        }

        return null;
    }

    private TemplateSaveService Service() => new(_state);

    private static DraftChange Change(TemplateRecord record, VariableDeclaration declaration, FieldKind field, string proposed) =>
        new(new ChangeKey(record.FilePath, declaration.DeclarationId, field),
            declaration.GetField(field).LogicalValue, proposed, ProposedFromDraft: false)
        {
            DeclarationName = declaration.DisplayName,
        };

    private static IReadOnlyDictionary<string, byte[]> EntriesOf(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e => ReadAll(e), StringComparer.Ordinal);

        static byte[] ReadAll(ZipArchiveEntry entry)
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }

    [Fact]
    public void ASavedDefaultValueReplacesOnlyTheTemplateXmlEntry()
    {
        string path = CopySample();
        IReadOnlyDictionary<string, byte[]> entriesBefore = EntriesOf(path);

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [Change(record, target, FieldKind.DefaultValue, "SAVED VALUE & <x>")]));

        Assert.Equal(SaveOutcome.Written, result.Outcome);
        Assert.Equal(1, result.FieldsWritten);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath!));

        // Every other entry of the container still has exactly its own bytes.
        IReadOnlyDictionary<string, byte[]> entriesAfter = EntriesOf(path);
        Assert.Equal(entriesBefore.Keys, entriesAfter.Keys);
        foreach ((string name, byte[] before) in entriesBefore)
        {
            if (string.Equals(name, "template.xml", StringComparison.OrdinalIgnoreCase))
            {
                Assert.False(before.SequenceEqual(entriesAfter[name]), $"{name} should have changed");
            }
            else
            {
                Assert.True(before.SequenceEqual(entriesAfter[name]), $"{name} must keep its bytes");
            }
        }

        // And the value reads back exactly as staged, including the characters XML has to escape.
        TemplateRecord reread = new TemplateReaderService().Load(path, CancellationToken.None);
        Assert.Equal(TemplateReadState.Loaded, reread.ReadState);
        Assert.Equal("SAVED VALUE & <x>",
            reread.Declarations.Single(d => d.DeclarationId == target.DeclarationId).DefaultValue.LogicalValue);
    }

    [Fact]
    public void ATemplateChangedOutsideTheEditorIsRefusedBeforeAnythingIsWritten()
    {
        string path = CopySample();
        string untouched = HashService.Sha256Hex(File.ReadAllBytes(path));

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        // Someone else edits the same file between the read and the save.
        File.AppendAllText(path, " ");
        string editedHash = HashService.Sha256Hex(File.ReadAllBytes(path));

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [Change(record, target, FieldKind.DefaultValue, "OVERWRITE?")]));

        Assert.Equal(SaveOutcome.Rejected, result.Outcome);
        Assert.Contains("changed after it was read", result.Message, StringComparison.Ordinal);
        Assert.Equal(editedHash, HashService.Sha256Hex(File.ReadAllBytes(path)));
        Assert.NotEqual(untouched, editedHash);
        Assert.False(Directory.Exists(Path.Combine(_state, "backups")), "a refused save must not even create the backup folder");
    }

    [Fact]
    public void AChangeStagedForAnotherFileIsRefusedForThisFile()
    {
        string path = CopySample();
        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        var foreign = new DraftChange(new ChangeKey(@"C:\other\elsewhere.dta", target.DeclarationId, FieldKind.DefaultValue),
            target.DefaultValue.LogicalValue, "NOPE", ProposedFromDraft: false)
        {
            DeclarationName = target.DisplayName,
        };

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [foreign]));
        Assert.Equal(SaveOutcome.Rejected, result.Outcome);
        Assert.Contains("cannot be written to this file", result.Message, StringComparison.Ordinal);
        Assert.Equal(HashService.Sha256Hex(File.ReadAllBytes(path)), record.BaselineFingerprint);
    }

    [Fact]
    public void AFieldNameIsNeverWrittenEvenIfItWasStaged()
    {
        string path = CopySample();
        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations[0];

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [Change(record, target, FieldKind.Name, "renamed")]));

        Assert.Equal(SaveOutcome.Rejected, result.Outcome);
        // The capability check may pass (names parse as writable text) but the patcher keeps the rule
        // that a parameter name is never written, so either refusal is fine as long as it says so.
        Assert.True(result.Message.Contains("read-only", StringComparison.OrdinalIgnoreCase)
                    || result.Message.Contains("does not allow editing", StringComparison.Ordinal),
            $"unexpected refusal: {result.Message}");
        Assert.Equal(record.BaselineFingerprint, HashService.Sha256Hex(File.ReadAllBytes(path)));
    }

    [Fact]
    public void TheJournalAndTheBackupBothSayWhatHappened()
    {
        string path = CopySample();
        string originalHash = HashService.Sha256Hex(File.ReadAllBytes(path));

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [Change(record, target, FieldKind.DefaultValue, "JOURNALED")]));
        Assert.Equal(SaveOutcome.Written, result.Outcome);

        Assert.Equal(originalHash, HashService.Sha256Hex(File.ReadAllBytes(result.BackupPath!)));

        string journal = File.ReadAllText(Service().JournalPath);
        Assert.Contains(path.Replace("\\", "\\\\", StringComparison.Ordinal), journal, StringComparison.Ordinal);
        Assert.Contains("\"prepared\"", journal, StringComparison.Ordinal);
        Assert.Contains("\"backup-ready\"", journal, StringComparison.Ordinal);
        Assert.Contains("\"confirmed\"", journal, StringComparison.Ordinal);
        Assert.DoesNotContain("\"aborted\"", journal, StringComparison.Ordinal);
        Assert.Equal(3, journal.TrimEnd().Split(Environment.NewLine).Length);
    }

    [Fact]
    public void APlainXmlTemplateIsWrittenInPlaceWithoutAContainer()
    {
        string path = Path.Combine(_root, "plain.dta.xml");
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <template>
              <dataset>
                <variables>
                  <variable>
                    <feature tag="definition">
                      <property name="name" value="solo"></property>
                      <property name="description"><expression type="constant"></expression></property>
                      <property name="default" value="before"></property>
                      <property name="access"><expression type="constant">internal</expression></property>
                    </feature>
                  </variable>
                </variables>
              </dataset>
            </template>
            """;
        File.WriteAllText(path, Xml, new System.Text.UTF8Encoding(false));
        string before = File.ReadAllText(path);

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        Assert.Equal(TemplateReadState.Loaded, record.ReadState);
        VariableDeclaration target = record.Declarations[0];

        SaveFileResult result = Service().SaveOne(new SaveRequest(record, [
            Change(record, target, FieldKind.DefaultValue, "after"),
            Change(record, target, FieldKind.Description, "filled in"),
        ]));

        Assert.Equal(SaveOutcome.Written, result.Outcome);
        Assert.Equal(2, result.FieldsWritten);
        string after = File.ReadAllText(path);
        Assert.Contains("value=\"after\"", after, StringComparison.Ordinal);
        Assert.Contains("<expression type=\"constant\">filled in</expression>", after, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"before\"", after, StringComparison.Ordinal);

        // Everything the change did not touch is still character for character what it was.
        string expected = before
            .Replace("value=\"before\"", "value=\"after\"", StringComparison.Ordinal)
            .Replace("<expression type=\"constant\"></expression>", "<expression type=\"constant\">filled in</expression>", StringComparison.Ordinal);
        Assert.Equal(expected, after);
    }

    [Fact]
    public void ABatchReportsEachFileOnItsOwnTermsInsteadOfClaimingAtomicity()
    {
        string good = CopySample("example.dta");
        string stale = CopySample("requirements_specification_yps.dta");

        TemplateReaderService reader = new();
        TemplateRecord goodRecord = reader.Load(good, CancellationToken.None);
        TemplateRecord staleRecord = reader.Load(stale, CancellationToken.None);

        File.AppendAllText(stale, " "); // an outside edit
        string staleHashAfterEdit = HashService.Sha256Hex(File.ReadAllBytes(stale));

        VariableDeclaration goodTarget = goodRecord.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);
        VariableDeclaration staleTarget = staleRecord.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        List<SaveFileResult> results =
        [
            Service().SaveOne(new SaveRequest(goodRecord, [Change(goodRecord, goodTarget, FieldKind.DefaultValue, "batched")])),
            Service().SaveOne(new SaveRequest(staleRecord, [Change(staleRecord, staleTarget, FieldKind.DefaultValue, "batched")])),
        ];

        Assert.Equal(SaveOutcome.Written, results[0].Outcome);
        Assert.Equal(SaveOutcome.Rejected, results[1].Outcome);
        Assert.Equal(staleHashAfterEdit, HashService.Sha256Hex(File.ReadAllBytes(stale)));
        Assert.Contains("changed after it was read", results[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWriteThatCannotReplaceTheFileReportsTheFailureAndKeepsTheOriginal()
    {
        string path = CopySample();
        string originalHash = HashService.Sha256Hex(File.ReadAllBytes(path));

        TemplateRecord record = new TemplateReaderService().Load(path, CancellationToken.None);
        VariableDeclaration target = record.Declarations.First(d => d.DefaultValue.Capability == FieldCapability.Editable);

        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            SaveFileResult result = Service().SaveOne(new SaveRequest(record, [Change(record, target, FieldKind.DefaultValue, "LOCKED")]));

            // The file is writable-blocked, so the write is reported as a failure of that file alone.
            Assert.Equal(SaveOutcome.Failed, result.Outcome);
            Assert.True(result.Message.Contains("did not complete", StringComparison.Ordinal), result.Message);
            Assert.True(result.Message.Contains("target file is unchanged", StringComparison.Ordinal)
                        || result.Message.Contains("backup", StringComparison.Ordinal), result.Message);

            File.SetAttributes(path, FileAttributes.Normal);
            Assert.Equal(originalHash, HashService.Sha256Hex(File.ReadAllBytes(path)));
            // A failed write is journaled, and it is never marked as confirmed.
            string journal = File.ReadAllText(Service().JournalPath);
            Assert.Contains("aborted", journal, StringComparison.Ordinal);
            Assert.DoesNotContain("confirmed", journal, StringComparison.Ordinal);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void PreflightReportsEveryFileWithoutWritingAnything()
    {
        string first = CopySample();
        string second = Path.Combine(_root, "second.dta");
        File.Copy(Path.Combine(FindSamplesDir()!, "example.dta"), second);

        TemplateReaderService reader = new();
        TemplateRecord firstRecord = reader.Load(first, CancellationToken.None);
        TemplateRecord secondRecord = reader.Load(second, CancellationToken.None);

        List<SaveRequest> requests =
        [
            new(firstRecord, []),
            new(secondRecord, [Change(secondRecord, secondRecord.Declarations.First(
                d => d.DefaultValue.Capability == FieldCapability.Editable), FieldKind.DefaultValue, "x")]),
        ];

        IReadOnlyList<SaveFileResult> preflight = Service().Preflight(requests);

        Assert.Equal(SaveOutcome.Rejected, preflight[0].Outcome);
        Assert.Contains("No selected changes", preflight[0].Message, StringComparison.Ordinal);
        Assert.Equal(SaveOutcome.Ready, preflight[1].Outcome);
        Assert.Equal(firstRecord.BaselineFingerprint, HashService.Sha256Hex(File.ReadAllBytes(first)));
        Assert.Equal(secondRecord.BaselineFingerprint, HashService.Sha256Hex(File.ReadAllBytes(second)));
        Assert.False(File.Exists(Service().JournalPath), "preflight writes nothing at all");
    }
}
