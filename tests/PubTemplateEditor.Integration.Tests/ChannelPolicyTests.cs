using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The distribution channel and its signing story, checked where it is written down.
///
/// The distribution channel and the signing status, checked where they are written down.
///
/// Owner decisions of 2026-10-26: this application is distributed as a single-file executable on a
/// GitHub Release, not through the Microsoft Store, and it is **not code-signed** - the free
/// subscriptions for open-source projects were declined because a new repository has no public trust
/// record, and no paid certificate has been acquired. Half-true sentences about who signs what are
/// worse than an open gate: they make an unsigned build look trusted, and a leftover signing job in
/// a workflow would fail every release run. So the wording and the absence are both asserted.
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
        Assert.Contains("no code signature", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA-256", readme, StringComparison.Ordinal);

        string release = Read(".github", "workflows", "release.yml");
        Assert.Contains("publish-github-release", release, StringComparison.Ordinal);
        Assert.Contains("gh release create", release, StringComparison.Ordinal);
        // the release itself has to say that the binary is unsigned and how to check it
        Assert.Contains("not code-signed", release, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", release, StringComparison.Ordinal);

        // Packaging still exists, but as opt-in tooling rather than a pipeline stage.
        Assert.Contains("buildMsix", release, StringComparison.Ordinal);
        string ci = Read(".github", "workflows", "ci.yml");
        Assert.DoesNotContain("build-msix.ps1", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSigningServiceIsConfiguredOrNamedAnywhereButTheProjectLog()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "docs", "CODE_SIGNING_POLICY.md")),
            "the signing policy page belonged to a signing subscription this project does not have");

        // No workflow may submit anything to a signer: a job that cannot run would fail every
        // release, and a leftover secret/variable pair invites someone to re-enable it by accident.
        foreach (string workflow in Directory.GetFiles(Path.Combine(RepoRoot(), ".github", "workflows"), "*.yml"))
        {
            string text = File.ReadAllText(workflow);
            Assert.DoesNotContain("signpath", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("signing-request", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SIGNPATH_", text, StringComparison.Ordinal);
            Assert.DoesNotContain("sign-portable-exe", text, StringComparison.Ordinal);
            Assert.DoesNotContain("signed-portable", text, StringComparison.Ordinal);
        }

        // The documents a reader or contributor acts on must not point at a signer either. Only
        // PLAN.md, the project log, records which service was approached and when.
        string[] files =
        [
            "README.md", "SECURITY.md", "CONTRIBUTING.md", "CODE_OF_CONDUCT.md", "START_HERE.md",
            "REQUIREMENTS.md", ".github/PULL_REQUEST_TEMPLATE.md", ".github/CODEOWNERS",
            "docs/RELEASE_PACKAGING.md", "docs/PROJECT_SETUP.md", "docs/GPL_RELEASE.md",
            "docs/WINDOWS_PUBLISH.md", "docs/TEST_MATRIX.md", "docs/PRESERVATION_NOTES.md",
            "packaging/build-msix.ps1", "packaging/AppxManifest.template.xml",
            "src/PubTemplateEditor.App/Properties/PublishProfiles/PortableWin_x64.pubxml",
        ];
        foreach (string file in files)
        {
            Assert.False(Regex.IsMatch(Read(file), "signpath", RegexOptions.IgnoreCase),
                file + " still mentions a signing service that this project does not use");
        }
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
