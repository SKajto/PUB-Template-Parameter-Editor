using System.IO;
using System.IO.Compression;

namespace PubTemplateEditor.TestSupport;

/// <summary>
/// Sample handling for tests.
///
/// The native PUB .dta files in examples/ are user content and are deliberately not part of the
/// repository (.gitignore keeps them out; see docs/SAMPLE_FINDINGS.md for what was learned from
/// them). They stay on a maintainer's disk, where the evidence tests verify against their real
/// bytes. Everywhere else - a fresh clone, CI, a contributor without the samples - these helpers
/// write a synthetic container of the same shape, so the same code path still runs and the suite
/// never depends on content that cannot be distributed.
/// </summary>
public static class TestSamples
{
    public static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubTemplateEditor.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    public static string SamplesDirectory => Path.Combine(RepoRoot(), "examples");

    public static string NativePath(string fileName) => Path.Combine(SamplesDirectory, fileName);

    public static bool HasNative(string fileName) => File.Exists(NativePath(fileName));

    /// <summary>Structural stand-in: same element shape as a native template, no user content.</summary>
    public const string SyntheticTemplateXml = """
        <?xml version="1.0" encoding="UTF-8"?><template><dataset><variables>
        <variable><feature tag="definition">
        <property name="name" value="ProjectName"></property>
        <property name="description"><expression type="constant">Name of the project</expression></property>
        <property name="type" value="user"></property>
        <property name="access" value="external"></property>
        <property name="default" value="Sample project"></property>
        <property name="central variable" value=""></property>
        </feature></variable>
        <variable><feature tag="definition">
        <property name="name" value="Budget"></property>
        <property name="description"><expression type="constant">Budget in EUR</expression></property>
        <property name="type" value="user"></property>
        <property name="access" value="internal"></property>
        <property name="default" value="ID(10)|Name(15)|Contents=Description(38)"></property>
        <property name="central variable" value=""></property>
        </feature></variable>
        <variable><feature tag="definition">
        <property name="name" value="EmptyDefault"></property>
        <property name="description"><expression type="constant"></expression></property>
        <property name="type" value="user"></property>
        <property name="access" value="external"></property>
        <property name="default" value=""></property>
        <property name="central variable" value=""></property>
        </feature></variable>
        </variables></dataset></template>
        """;

    /// <summary>
    /// Writes a .dta-shaped ZIP: the template XML plus a schema folder with a non-XML mapping
    /// entry, which is what the container round trip has to carry unchanged.
    /// </summary>
    public static string WriteSyntheticContainer(string directory, string fileName, string? xml = null)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        using FileStream stream = File.Create(path);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        AddText(archive, "template.xml", xml ?? SyntheticTemplateXml);
        AddText(archive, "schemas/synthetic.xsd/synthetic.xsd",
            "<?xml version=\"1.0\"?><schema xmlns=\"http://www.w3.org/2001/XMLSchema\"></schema>");
        AddText(archive, "schemas/synthetic.xsd/__RPE_MAPPINGS__", "synthetic mapping entry");

        return path;
    }

    /// <summary>
    /// Copies the native sample of that name when the disk has it, otherwise writes the synthetic
    /// container. Either way the returned path is a real .dta-shaped file inside the caller's
    /// directory, safe to modify: tests never touch the authoritative examples.
    /// </summary>
    public static string CopyNativeOrSynthetic(string directory, string nativeFileName, string? targetName = null)
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, targetName ?? nativeFileName);
        if (HasNative(nativeFileName))
        {
            File.Copy(NativePath(nativeFileName), target, overwrite: true);
        }
        else
        {
            WriteSyntheticContainer(directory, targetName ?? nativeFileName);
        }

        return target;
    }

    /// <summary>A plain .dta-shaped folder listing: one container per name, all synthetic.</summary>
    public static string[] WriteSyntheticFolder(string directory, params string[] fileNames)
    {
        Directory.CreateDirectory(directory);
        return fileNames.Select(name => WriteSyntheticContainer(directory, name)).ToArray();
    }

    private static void AddText(ZipArchive archive, string entryName, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open());
        writer.Write(text);
    }
}
