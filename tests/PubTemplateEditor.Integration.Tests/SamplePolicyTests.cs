using System.Diagnostics;
using System.IO;
using PubTemplateEditor.TestSupport;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The native PUB .dta files in examples/ are the user's own template content. They are deliberately
/// outside the repository: publishing them would put third-party product data on a public source
/// host, and they are not the "source" that the GPL talks about. What the samples proved is written
/// up in docs/SAMPLE_FINDINGS.md and asserted by the evidence tests on the machine that has them,
/// while every other environment runs the same code against synthetic containers.
/// </summary>
public class SamplePolicyTests
{
    [Fact]
    public void TemplateContentIsKeptOutOfTheRepositoryOnPurpose()
    {
        string root = TestSamples.RepoRoot();

        string gitignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("examples/*.dta", gitignore, StringComparison.Ordinal);

        // The knowledge, not the bytes, is what travels with the source.
        Assert.True(File.Exists(Path.Combine(root, "docs", "SAMPLE_FINDINGS.md")),
            "the findings from the samples must be documented, since the files themselves are not shipped");

        Assert.Contains("user content",
            File.ReadAllText(Path.Combine(root, "tests", "TestSampleSupport.cs")), StringComparison.Ordinal);
        Assert.Contains(".gitignore keeps them out",
            File.ReadAllText(Path.Combine(root, "tests", "TestSampleSupport.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void NoTemplateFileIsTrackedByGit()
    {
        string root = TestSamples.RepoRoot();

        if (RunGit(root, "rev-parse", "--is-inside-work-tree")?.Trim() != "true")
        {
            return; // an extracted source archive: nothing tracked to check
        }

        string? listed = RunGit(root, "ls-files", "--", "examples", "tests", "src", "packaging");
        Assert.NotNull(listed);

        var templates = listed!.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.EndsWith(".dta", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(templates.Count == 0,
            "template content must not be tracked: " + string.Join(", ", templates));
    }

    private static string? RunGit(string workingDirectory, params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;    // no git on the machine: the tracked-tree check is simply not available
        }
    }
}
