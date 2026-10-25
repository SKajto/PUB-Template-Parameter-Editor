using System.IO;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// FILE-02/FILE-04: the folder listing is a non-recursive .dta scan that only stats files.
/// It never parses, and one entry that cannot be stat'ed must not remove the others.
/// </summary>
public sealed class FolderScannerTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "pubte-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void ListsOnlyTemplateFilesInTheFolderItself()
    {
        string root = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "b_second.dta"), "x");
            File.WriteAllText(Path.Combine(root, "a_first.dta"), "xx");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "not a template");
            File.WriteAllText(Path.Combine(root, "template.dta.bak"), "not a template");
            File.WriteAllText(Path.Combine(root, "UPPER.DTA"), "extension check is case-insensitive");

            string nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "deep.dta"), "must not be listed (non-recursive)");
            File.WriteAllText(Path.Combine(root, "dir.dta.txt"), "not a template");

            var entries = new TemplateFolderScanner().Scan(root);

            Assert.Equal(
                new[] { "a_first.dta", "b_second.dta", "UPPER.DTA" },
                entries.Select(e => Path.GetFileName(e.FilePath)).ToArray());
            Assert.DoesNotContain(entries, e => Path.GetFileName(e.FilePath) == "deep.dta");

            // Sizes come from the directory entry only; the content is never opened for parsing.
            Assert.Equal(2, entries.Single(e => Path.GetFileName(e.FilePath) == "a_first.dta").SizeBytes);
            Assert.All(entries, e => Assert.Null(e.ListingError));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BrokenEntryIsListedWithAnExplanationAndTheRestSurvive()
    {
        string root = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "good1.dta"), "1");
            File.WriteAllText(Path.Combine(root, "broken.dta"), "2");
            File.WriteAllText(Path.Combine(root, "good2.dta"), "3");

            // The stat seam stands in for a file that is locked or vanishes mid-listing.
            var scanner = new TemplateFolderScanner(path =>
                Path.GetFileName(path) == "broken.dta"
                    ? throw new IOException("The process cannot access the file because it is being used by another process.")
                    : new FileInfo(path).Length);

            var entries = scanner.Scan(root);

            Assert.Equal(3, entries.Count);
            var broken = entries.Single(e => Path.GetFileName(e.FilePath) == "broken.dta");
            Assert.NotNull(broken.ListingError);
            Assert.Equal(0, broken.SizeBytes);
            Assert.All(entries.Where(e => Path.GetFileName(e.FilePath) != "broken.dta"),
                e => Assert.Null(e.ListingError));

            // Listing order stays deterministic and complete despite the failure.
            Assert.Equal(
                new[] { "broken.dta", "good1.dta", "good2.dta" },
                entries.Select(e => Path.GetFileName(e.FilePath)).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingFolderIsReportedInsteadOfReturningAnEmptyListing()
    {
        string missing = Path.Combine(TempDir(), "does-not-exist");
        Assert.ThrowsAny<IOException>(() => new TemplateFolderScanner().Scan(missing));
    }
}
