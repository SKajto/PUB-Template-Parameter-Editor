using System.IO.Compression;
using System.Text;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Infrastructure;

/// <summary>One entry of a DTA ZIP container, captured for faithful rebuild.</summary>
public sealed record ContainerEntry(
    string Name,
    DateTimeOffset LastWrite,
    int CompressionMethod,
    string Comment,
    byte[] Content);

/// <summary>
/// Adapter for the observed native .dta container layout: a ZIP archive whose
/// 'template.xml' entry holds the template plus schemas/ and locale property
/// entries. Reads capture every entry's logical content; rebuilds write the
/// same names, order, timestamps, compression method and contents back.
///
/// Documented preservation guarantee: entry-level logical identity (name, order,
/// LastWriteTime, Deflate method, content bytes, comment). The raw compressed
/// byte stream and some ZIP header fields (general-purpose flags, external
/// attributes) are rewritten by System.IO.Compression and are not bit-identical.
/// </summary>
public sealed class DtaContainer : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly bool _leaveOpen;

    public IReadOnlyList<ContainerEntry> Entries { get; }

    public DtaContainer(Stream stream, ZipArchiveMode mode, bool leaveOpen = false)
    {
        _archive = new ZipArchive(stream, mode, leaveOpen);
        _leaveOpen = leaveOpen;
        Entries = mode == ZipArchiveMode.Read
            ? _archive.Entries.Select(Capture).ToList()
            : [];
    }

    /// <summary>Reads all entries; returns the template.xml content when present.</summary>
    public TemplateXmlContent? ReadTemplateXml()
    {
        ContainerEntry? entry = Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "template.xml", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var source = SourceText.FromBytes(entry.Content);
        return new TemplateXmlContent
        {
            EntryName = entry.Name,
            EntryBytes = entry.Content,
            Source = source,
            EntryFingerprint = HashService.Sha256Hex(entry.Content),
        };
    }

    /// <summary>
    /// Writes a new container with the same entries, replacing the content of the
    /// named entry. Never mutates the original stream in place.
    /// </summary>
    public static void Rebuild(
        Stream output,
        IReadOnlyList<ContainerEntry> entries,
        string replacedEntryName,
        byte[] replacedContent)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var entry in entries)
        {
            bool isReplaced = string.Equals(entry.Name, replacedEntryName, StringComparison.OrdinalIgnoreCase);
            var newEntry = archive.CreateEntry(entry.Name,
                entry.CompressionMethod == 0 ? CompressionLevel.NoCompression : CompressionLevel.SmallestSize);
            newEntry.LastWriteTime = entry.LastWrite;
            if (entry.Comment.Length > 0)
            {
                newEntry.Comment = entry.Comment;
            }

            using var target = newEntry.Open();
            byte[] content = isReplaced ? replacedContent : entry.Content;
            target.Write(content, 0, content.Length);
        }
    }

    private static ContainerEntry Capture(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        byte[] content = ms.ToArray();
        return new ContainerEntry(
            entry.FullName,
            entry.LastWriteTime,
            DeriveCompressionMethod(content, content.Length),
            entry.Comment ?? string.Empty,
            content);
    }

    /// <summary>
    /// System.IO.Compression does not expose the stored method of a read entry.
    /// Every entry of every observed native .dta sample uses Deflate, so rebuild
    /// uses Deflate by default and stores only empty entries.
    /// </summary>
    private static int DeriveCompressionMethod(byte[] content, int length) => length == 0 ? 0 : 8;

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _archive.Dispose();
        }
    }
}
