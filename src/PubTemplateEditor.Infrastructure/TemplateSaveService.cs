using System.IO.Compression;
using System.Text.Json;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Infrastructure;

public enum SaveOutcome
{
    /// <summary>Nothing is wrong with this request; it may be written.</summary>
    Ready,

    /// <summary>The replacement happened and was read back from disk.</summary>
    Written,

    /// <summary>Nothing was written: the request was refused before any file was touched.</summary>
    Rejected,

    /// <summary>A write was attempted and did not complete; the message says what survives.</summary>
    Failed,
}

public sealed record SaveFileResult(
    string FilePath,
    SaveOutcome Outcome,
    string Message,
    int FieldsWritten,
    string BeforeFingerprint,
    string? AfterFingerprint = null,
    string? BackupPath = null);

/// <summary>
/// One selected change paired with the record it was staged against. Held by the caller; the service
/// never keeps a mutable draft of its own.
/// </summary>
public sealed record SaveRequest(TemplateRecord Record, IReadOnlyList<DraftChange> Changes);

/// <summary>
/// Writes selected fields back into template files while preserving everything else.
///
/// The order of operations is deliberate:
///   1. refuse anything that does not match what the editor believes is on disk,
///   2. build the complete replacement file in memory,
///   3. verify that in-memory file by parsing it back,
///   4. write it to a temporary file in the target folder and verify those bytes,
///   5. copy the original to a user-scoped backup and verify the copy,
///   6. journal the intent, then replace,
///   7. read the target back and verify field by field.
///
/// A batch is processed file by file and is not atomic across files: if the third of five templates
/// fails, the first two are already saved. Every file gets its own outcome, and the caller decides
/// which staged changes to drop.
/// </summary>
public sealed class TemplateSaveService
{
    private const string TemplateEntryName = "template.xml";

    private readonly string _stateDirectory;

    public TemplateSaveService(string? stateDirectory = null)
    {
        _stateDirectory = stateDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PubTemplateEditor");
    }

    public string JournalPath => Path.Combine(_stateDirectory, "recovery-journal.jsonl");
    public string BackupDirectory => Path.Combine(_stateDirectory, "backups");

    /// <summary>
    /// Checks every request without writing anything, so the caller can show the whole outcome before
    /// a single byte changes.
    /// </summary>
    public IReadOnlyList<SaveFileResult> Preflight(IReadOnlyList<SaveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        List<SaveFileResult> results = [];
        foreach (SaveRequest request in requests)
        {
            results.Add(Check(request));
        }

        return results;
    }

    public SaveFileResult SaveOne(SaveRequest request, CancellationToken token = default)
    {
        SaveFileResult check = Check(request);
        if (check.Outcome == SaveOutcome.Rejected)
        {
            return check;
        }

        TemplateRecord record = request.Record;
        string path = record.FilePath;
        byte[] originalBytes = File.ReadAllBytes(path);
        string beforeHash = HashService.Sha256Hex(originalBytes);

        TemplatePatch patch = TemplateXmlPatcher.Patch(record, request.Changes);
        if (!patch.Verified || patch.IsEmpty)
        {
            string why = patch.VerificationProblems.Count > 0
                ? "The patched source did not pass its own verification: " + string.Join(" ", patch.VerificationProblems)
                : patch.Rejected.Count > 0
                    ? string.Join(" ", patch.Rejected.Select(r => r.Reason).Distinct())
                    : "There was nothing left to write for this file.";
            return new SaveFileResult(path, SaveOutcome.Rejected, why, 0, beforeHash);
        }

        byte[] newBytes;
        try
        {
            newBytes = record.Format == TemplateFormat.DtaZipContainer
                ? RebuildContainer(originalBytes, patch, record)
                : TemplateXmlText.Encode(patch.PatchedText, record.XmlContent!.Source.EncodingName);
        }
        catch (Exception ex)
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                $"The replacement file could not be assembled: {ex.Message}", 0, beforeHash);
        }

        string expectedHash = HashService.Sha256Hex(newBytes);
        string tempPath = Path.Combine(
            Path.GetDirectoryName(path) ?? ".",
            $"~{Path.GetFileNameWithoutExtension(path)}.new-{Guid.NewGuid():N}.tmp");
        string? backupPath = null;
        string journalId = Guid.NewGuid().ToString("N");

        try
        {
            Directory.CreateDirectory(_stateDirectory);
            Directory.CreateDirectory(BackupDirectory);
            // The guid keeps two saves of the same file inside one second from colliding; a collision
            // would turn a perfectly good save into a failure.
            backupPath = Path.Combine(BackupDirectory,
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Path.GetFileName(path)}-{journalId[..8]}.bak");

            AppendJournal(journalId, path, "prepared", beforeHash, expectedHash, backupPath);

            using (FileStream temp = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       1 << 16, FileOptions.WriteThrough))
            {
                temp.Write(newBytes);
                temp.Flush(true);
            }

            byte[] tempBytes = File.ReadAllBytes(tempPath);
            if (HashService.Sha256Hex(tempBytes) != expectedHash)
            {
                throw new IOException("The temporary file does not match the bytes that were prepared for it.");
            }

            File.Copy(path, backupPath, overwrite: false);
            if (HashService.Sha256Hex(File.ReadAllBytes(backupPath)) != beforeHash)
            {
                throw new IOException("The backup copy does not match the file that is currently on disk.");
            }

            AppendJournal(journalId, path, "backup-ready", beforeHash, expectedHash, backupPath);

            File.Replace(tempPath, path, destinationBackupFileName: null);

            byte[] onDisk = File.ReadAllBytes(path);
            string afterHash = HashService.Sha256Hex(onDisk);
            if (afterHash != expectedHash)
            {
                AppendJournal(journalId, path, "mismatch-after-replace", beforeHash, afterHash, backupPath);
                return new SaveFileResult(path, SaveOutcome.Failed,
                    $"The file was replaced, but the bytes on disk are not the bytes that were prepared. The original copy is in '{backupPath}'.",
                    patch.Applied.Count, beforeHash, afterHash, backupPath);
            }

            string? verificationProblem = VerifyOnDisk(record, patch, onDisk);
            AppendJournal(journalId, path, verificationProblem is null ? "confirmed" : "verified-failed",
                beforeHash, afterHash, backupPath);

            return verificationProblem is null
                ? new SaveFileResult(path, SaveOutcome.Written,
                    $"{patch.Applied.Count} field(s) written; the original copy is in '{backupPath}'.",
                    patch.Applied.Count, beforeHash, afterHash, backupPath)
                : new SaveFileResult(path, SaveOutcome.Failed,
                    $"The file was replaced but did not read back as expected: {verificationProblem} The original copy is in '{backupPath}'.",
                    patch.Applied.Count, beforeHash, afterHash, backupPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendJournal(journalId, path, "aborted", beforeHash, expectedHash, backupPath, ex.Message);
            string state = File.Exists(path) && HashService.Sha256Hex(File.ReadAllBytes(path)) == beforeHash
                ? "The target file is unchanged."
                : backupPath is not null ? $"The original copy is in '{backupPath}'." : "No backup was made.";
            return new SaveFileResult(path, SaveOutcome.Failed,
                $"The save did not complete: {ex.Message} {state}", 0, beforeHash, null, backupPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // Left behind rather than deleting something that may be the replacement.
                }
            }
        }
    }

    /// <summary>Everything that could be refused before a write is attempted, in one pass.</summary>
    private static SaveFileResult Check(SaveRequest request)
    {
        TemplateRecord record = request.Record;
        string path = record.FilePath;

        if (record.ReadState != TemplateReadState.Loaded || record.XmlContent is null)
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                record.ReadError ?? "This template was not read successfully, so nothing can be written back to it.", 0, string.Empty);
        }

        if (request.Changes.Count == 0)
        {
            return new SaveFileResult(path, SaveOutcome.Rejected, "No selected changes for this file.", 0,
                record.BaselineFingerprint ?? string.Empty);
        }

        SaveFileResult? mismatch = request.Changes
            .Where(c => !string.Equals(c.Key.FilePath, path, StringComparison.Ordinal))
            .Select(c => new SaveFileResult(path, SaveOutcome.Rejected,
                $"A change staged for '{c.Key.FilePath}' cannot be written to this file.", 0, record.BaselineFingerprint ?? string.Empty))
            .FirstOrDefault();
        if (mismatch is not null)
        {
            return mismatch;
        }

        if (request.Changes.Any(c => c.Key.DeclarationId < 0))
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                "A change belongs to a template that is no longer open and cannot be written.", 0,
                record.BaselineFingerprint ?? string.Empty);
        }

        SaveFileResult? unreadableField = request.Changes
            .Where(c => record.Declarations.FirstOrDefault(d => d.DeclarationId == c.Key.DeclarationId) is null
                        || record.Declarations.First(d => d.DeclarationId == c.Key.DeclarationId)
                            .GetField(c.Key.Field).Capability != FieldCapability.Editable)
            .Select(c => new SaveFileResult(path, SaveOutcome.Rejected,
                $"'{c.DeclarationName}' does not allow editing of that field any more; nothing was written.", 0,
                record.BaselineFingerprint ?? string.Empty))
            .FirstOrDefault();
        if (unreadableField is not null)
        {
            return unreadableField;
        }

        if (record.BaselineFingerprint is null)
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                "The editor has no fingerprint for the file it read, so it will not overwrite it.", 0, string.Empty);
        }

        if (!File.Exists(path))
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                "The template file is no longer in its folder; nothing was written.", 0, record.BaselineFingerprint);
        }

        string currentHash = HashService.Sha256Hex(File.ReadAllBytes(path));
        if (currentHash != record.BaselineFingerprint)
        {
            return new SaveFileResult(path, SaveOutcome.Rejected,
                "The file changed after it was read. Re-read it and stage the change again before saving.",
                0, currentHash);
        }

        return new SaveFileResult(path, SaveOutcome.Ready, "Ready to write.", request.Changes.Count,
            record.BaselineFingerprint);
    }

    /// <summary>
    /// Rebuilds the .dta container with the patched template.xml entry. Every other entry keeps its
    /// name, order, timestamp, comment and content bytes; the ZIP framing itself is rewritten by
    /// System.IO.Compression and is documented as not bit-identical.
    /// </summary>
    private static byte[] RebuildContainer(byte[] originalBytes, TemplatePatch patch, TemplateRecord record)
    {
        string entryName = record.XmlContent!.EntryName.Length == 0 ? TemplateEntryName : record.XmlContent.EntryName;
        byte[] patchedEntry = TemplateXmlText.Encode(patch.PatchedText, record.XmlContent.Source.EncodingName);

        using var input = new MemoryStream(originalBytes, writable: false);
        using var read = new DtaContainer(input, ZipArchiveMode.Read, leaveOpen: false);
        IReadOnlyList<ContainerEntry> entries = read.Entries;
        if (!entries.Any(e => string.Equals(e.Name, entryName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"The container has no '{entryName}' entry to replace.");
        }

        using var output = new MemoryStream();
        DtaContainer.Rebuild(output, entries, entryName, patchedEntry);
        return output.ToArray();
    }

    /// <summary>Reads the finished file back from disk and checks the written fields one by one.</summary>
    private static string? VerifyOnDisk(TemplateRecord record, TemplatePatch patch, byte[] fileBytes)
    {
        TemplateRecord reread = new TemplateReaderService().LoadFromBytes(record.FilePath, fileBytes);
        if (reread.ReadState != TemplateReadState.Loaded)
        {
            return reread.ReadError ?? "the saved file did not parse";
        }

        foreach (PatchedField field in patch.Applied)
        {
            VariableDeclaration? declaration = reread.Declarations.FirstOrDefault(d => d.DeclarationId == field.DeclarationId);
            if (declaration is null)
            {
                return $"the parameter stored at position {field.DeclarationId + 1} is missing after the save";
            }

            if (declaration.GetField(field.Field).LogicalValue != field.NewValue)
            {
                return $"'{declaration.DisplayName}' does not hold the value that was written";
            }
        }

        if (reread.Declarations.Count != record.Declarations.Count)
        {
            return $"the saved file declares {reread.Declarations.Count} parameters instead of {record.Declarations.Count}";
        }

        return null;
    }

    private void AppendJournal(string id, string target, string stage, string before, string after,
        string? backup, string? error = null)
    {
        try
        {
            var line = new JournalEntry(id, target, stage, before, after, backup, error, DateTimeOffset.Now);
            File.AppendAllText(JournalPath, JsonSerializer.Serialize(line) + Environment.NewLine);
        }
        catch (IOException)
        {
            // The journal is recovery aid, not a precondition for the write; a failure here must not
            // be reported as if the save itself had failed.
        }
    }

    private sealed record JournalEntry(
        string Id, string Target, string Stage, string Before, string After,
        string? Backup, string? Error, DateTimeOffset At);
}
