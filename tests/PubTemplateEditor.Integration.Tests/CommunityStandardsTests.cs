using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The repository's own contribution surface: the files GitHub shows under "Community Standards",
/// plus the identity the commits carry. A template with an unfilled placeholder is worse than no
/// template at all, because it tells a contributor that nobody reads this part of the project.
/// </summary>
public class CommunityStandardsTests
{
    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubTemplateEditor.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), "community file is missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void TheCommunityFilesExistAndSaySomethingUseful()
    {
        string conduct = Read("CODE_OF_CONDUCT.md");
        string contributing = Read("CONTRIBUTING.md");
        string security = Read("SECURITY.md");
        string pullRequest = Read(".github", "PULL_REQUEST_TEMPLATE.md");
        string owners = Read(".github", "CODEOWNERS");

        Assert.Contains("Code of Conduct", conduct, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contributor Covenant", conduct, System.StringComparison.OrdinalIgnoreCase);

        // Contributing has to carry the constraints that actually bite, not generalities.
        Assert.Contains("dotnet test", contributing, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPL-3.0-only", contributing, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("examples/", contributing, System.StringComparison.Ordinal);

        // The security policy must be about this program: untrusted template files and no network.
        Assert.Contains("Report a vulnerability", security, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DtdProcessing", security, System.StringComparison.Ordinal);
        Assert.Contains("do not attach your real templates", security, System.StringComparison.OrdinalIgnoreCase);

        Assert.Contains("What could not be verified", pullRequest, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".github/workflows", pullRequest, System.StringComparison.Ordinal);

        Assert.Contains("@SKajto", owners, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NoCommunityFileStillCarriesATemplatePlaceholder()
    {
        string[] files =
        [
            "CODE_OF_CONDUCT.md", "CONTRIBUTING.md", "SECURITY.md", "README.md",
            ".github" + Path.DirectorySeparatorChar + "PULL_REQUEST_TEMPLATE.md",
            ".github" + Path.DirectorySeparatorChar + "CODEOWNERS",
            ".github" + Path.DirectorySeparatorChar + "ISSUE_TEMPLATE" + Path.DirectorySeparatorChar + "config.yml",
        ];

        foreach (string file in files)
        {
            string text = File.ReadAllText(Path.Combine(RepoRoot(), file));
            foreach (string marker in new[] { "INSERT ", "[TODO", "<your@", "FIXME", "<INSERT" })
            {
                Assert.False(text.Contains(marker, System.StringComparison.OrdinalIgnoreCase),
                    file + " still contains the placeholder marker '" + marker + "'");
            }
        }
    }

    [Fact]
    public void IssueFormsAreWellFormedAndOfferTheCasesThisProjectNeeds()
    {
        string dir = Path.Combine(RepoRoot(), ".github", "ISSUE_TEMPLATE");
        string[] forms = Directory.GetFiles(dir, "*.yml");

        Assert.Contains(forms, f => Path.GetFileName(f) == "config.yml");
        string[] names = forms.Select(static f => Path.GetFileName(f)!)
            .Where(n => n != "config.yml")
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("bug_report.yml", names);
        Assert.Contains("template_format_problem.yml", names);   // the format is where the risk lives
        Assert.Contains("feature_request.yml", names);

        foreach (string form in names)
        {
            string text = File.ReadAllText(Path.Combine(dir, form));
            Assert.Contains("name:", text, System.StringComparison.Ordinal);
            Assert.Contains("description:", text, System.StringComparison.Ordinal);
            Assert.Contains("attributes:", text, System.StringComparison.Ordinal);
        }

        // The two forms that are about a file must tell the reporter not to hand one over: the
        // templates are business data, and the shape is what the maintainer needs.
        string bug = File.ReadAllText(Path.Combine(dir, "bug_report.yml"));
        string format = File.ReadAllText(Path.Combine(dir, "template_format_problem.yml"));
        Assert.Contains("not attach your real templates", bug, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not** attach your template", format, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hand-made", format, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommunityGuidancePointsWhereItShouldAndNotAtUnwrittenPages()
    {
        string readme = Read("README.md");
        foreach (string file in new[] { "CODE_OF_CONDUCT.md", "CONTRIBUTING.md", "SECURITY.md",
                                        "docs/CODE_SIGNING_POLICY.md", "docs/SAMPLE_FINDINGS.md" })
        {
            Assert.True(File.Exists(Path.Combine(RepoRoot(), file)), "the README refers to a page that is not there: " + file);
        }

        Assert.Contains("Code signing policy", readme, System.StringComparison.Ordinal);
        Assert.Contains("SignPath.io", readme, System.StringComparison.Ordinal);
        Assert.Contains("SignPath Foundation", readme, System.StringComparison.Ordinal);
    }
}
