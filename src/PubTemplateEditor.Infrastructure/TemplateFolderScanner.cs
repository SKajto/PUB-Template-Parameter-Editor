using System.IO;

namespace PubTemplateEditor.Infrastructure;

public sealed record TemplateFolderEntry(string FilePath, long SizeBytes, string? ListingError);

/// <summary>
/// FILE-02: lists candidate template files in the chosen folder without reading or
/// parsing them. Listing is non-recursive over *.dta (the native container extension);
/// files that cannot be stat'ed are still listed with an explanation so one broken
/// entry never hides the rest.
/// </summary>
public sealed class TemplateFolderScanner
{
    public const string TemplateExtension = ".dta";

    private readonly Func<string, long> _sizeReader;

    /// <summary>
    /// <paramref name="sizeReader"/> is a test seam: a failing stat must be reported per
    /// entry instead of aborting the listing. Production code uses the default.
    /// </summary>
    public TemplateFolderScanner(Func<string, long>? sizeReader = null) =>
        _sizeReader = sizeReader ?? (path => new FileInfo(path).Length);

    public IReadOnlyList<TemplateFolderEntry> Scan(string folderPath)
    {
        ArgumentNullException.ThrowIfNull(folderPath);

        var entries = new List<TemplateFolderEntry>();
        foreach (string path in Directory.EnumerateFileSystemEntries(folderPath))
        {
            string name = Path.GetFileName(path);
            if (!Directory.Exists(path) && name.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    entries.Add(new TemplateFolderEntry(path, _sizeReader(path), null));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
                {
                    entries.Add(new TemplateFolderEntry(path, 0, ex.Message));
                }
            }
        }

        return entries
            .OrderBy(e => Path.GetFileName(e.FilePath), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
