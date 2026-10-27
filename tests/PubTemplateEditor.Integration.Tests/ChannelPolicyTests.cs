using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The distribution channel and its signing story, checked where it is written down.
///
/// The owner's decision (2026-10-26) is that this application is distributed as a SignPath-signed
/// single-file executable on a GitHub Release, and not through the Microsoft Store. Half-true
/// sentences about who signs what are worse than an open gate: they make an unsigned build look
/// trusted. So the wording is asserted, not just documented.
/// </summary>
public class ChannelPolicyTests
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

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    [Fact]
    public void NothingClaimsThatMicrosoftSignsAnythingForThisProject()
    {
        string[] files =
        [
            "README.md",
            "CONTRIBUTING.md",
            "SECURITY.md",
            ".github/PULL_REQUEST_TEMPLATE.md",
            "docs/CODE_SIGNING_POLICY.md",
            "docs/RELEASE_PACKAGING.md",
            "docs/WINDOWS_PUBLISH.md",
            "docs/GPL_RELEASE.md",
            "docs/PRESERVATION_NOTES.md",
            "docs/PROJECT_SETUP.md",
            ".github/workflows/ci.yml",
            ".github/workflows/release.yml",
            "packaging/build-msix.ps1",
            "packaging/AppxManifest.template.xml",
        ];

        string[] forbiddenClaims =
        [
            "signed by Microsoft",
            "Microsoft signs",
            "Store signs",
            "Store-signed package",
            "Store signing happens",
            "after Partner Center certification",
            "Store submission",
        ];

        foreach (string file in files)
        {
            string text = Read(file);
            foreach (string claim in forbiddenClaims)
            {
                Assert.False(Regex.IsMatch(text, Regex.Escape(claim), RegexOptions.IgnoreCase),
                    $"{file} still claims '{claim}', which the current channel decision contradicts");
            }
        }
    }

    [Fact]
    public void TheChannelThatIsActuallyUsedIsNamedWhereItMatters()
    {
        string readme = Read("README.md");
        Assert.Contains("GitHub Release", readme, StringComparison.Ordinal);
        Assert.Contains("SignPath.io", readme, StringComparison.Ordinal);
        Assert.Contains("SignPath Foundation", readme, StringComparison.Ordinal);
        Assert.Contains("Code signing policy", readme, StringComparison.Ordinal);

        string release = Read(".github", "workflows", "release.yml");
        Assert.Contains("signpath/github-action-submit-signing-request", release, StringComparison.Ordinal);
        Assert.Contains("publish-github-release", release, StringComparison.Ordinal);
        Assert.Contains("gh release create", release, StringComparison.Ordinal);
        // publishing is refused unless the signing job of the same run succeeded
        Assert.Contains("needs.sign-portable-exe.result == 'success'", release, StringComparison.Ordinal);

        // Packaging still exists, but as opt-in tooling rather than a pipeline stage.
        Assert.Contains("buildMsix", release, StringComparison.Ordinal);
        string ci = Read(".github", "workflows", "ci.yml");
        Assert.DoesNotContain("build-msix.ps1", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOldStoreDesignIsGoneAndTheBaselineDocsSaySo()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "docs", "GITHUB_STORE_RELEASE.md")),
            "the Store submission design document must not survive the channel decision");
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "docs", "RELEASE_PACKAGING.md")));

        // The supplied planning documents keep their original wording as history, but they must say
        // up front that the channel changed, so nobody implements from a stale premise.
        foreach (string doc in new[] { "REQUIREMENTS.md", "START_HERE.md", "docs/WINDOWS_PUBLISH.md",
                                       "docs/TEST_MATRIX.md" })
        {
            Assert.Contains("Channel update, 2026-10-26", Read(doc), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryDocumentTheReadmeLinksToExists()
    {
        string readme = Read("README.md");
        var matches = Regex.Matches(readme, @"\]\((?<target>[^)#]+)(#[^)]*)?\)");

        foreach (Match match in matches)
        {
            string target = match.Groups["target"].Value;
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = Path.Combine(RepoRoot(), target.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"README links to {target}, which is not in the repository");
        }
    }
}
