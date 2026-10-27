using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The distribution channel and the signing status, checked where they are written down.
///
/// Owner decisions of 2026-10-26: this application is distributed as a single-file executable on a
/// GitHub Release rather than through the Microsoft Store, and it is **not code-signed** - nothing
/// signs these binaries and no certificate has been acquired. Half-true sentences about who signs
/// what are worse than an open gate: they make an unsigned build look trusted, and a leftover
/// signing job in a workflow would fail every release run. So the wording, and the absence of any
/// signing configuration, are both asserted.
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
            "trusted signature",
            "signed by a trusted",
            "signature anyone should trust",
            "no SmartScreen warning",
            "SmartScreen no longer warns",
            "SmartScreen warning disappears",
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

        // The documents a reader or contributor acts on must not point at a signer either. The dated
        // technical record stays in PLAN.md, the project log, where superseded decisions are marked as
        // superseded instead of being rewritten after the fact.
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
    public void SelfSigningIsWiredAndWordedAsIntegrityAndNeverAsTrust()
    {
        string script = Read("tools", "signing", "self-sign.ps1");

        // Real signing through the platform, with the result read back rather than assumed.
        Assert.Contains("Set-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        // The private key must not be able to leave the store through this script.
        Assert.Contains("NonExportable", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Export-PfxCertificate", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Exportable", script.Replace("NonExportable", ""), StringComparison.Ordinal);
        // A broken signature is a hard failure, and the untrusted-chain reality is spelled out.
        Assert.Contains("HashMismatch", script, StringComparison.Ordinal);
        Assert.Contains("throw", script, StringComparison.Ordinal);
        Assert.Contains("SmartScreen", script, StringComparison.Ordinal);
        Assert.Contains("not trusted", script, StringComparison.Ordinal);

        // What is published is the public certificate only - the byte pattern of a PKCS#12 bundle
        // (PKCS #12 is 1.2.840.113549.1.12) in a .cer would mean a private key had been committed.
        string cer = Path.Combine(RepoRoot(), "tools", "signing", "PUB-Template-Parameter-Editor-self-signed.cer");
        Assert.True(File.Exists(cer), "the public certificate of the signing key should be published next to the script");
        byte[] der = File.ReadAllBytes(cer);
        Assert.True(der.Length < 8192, "a public X.509 certificate is small; this one is not");
        Assert.Equal(0x30, der[0]); // DER SEQUENCE, not PEM text
        byte[] pkcs12Oid = [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x0C];
        Assert.False(ContainsSequence(der, pkcs12Oid), "that file carries PKCS#12 content: a private key would be committed");

        // The provenance record: which key signed, and until when.
        string packaging = Read("docs", "RELEASE_PACKAGING.md");
        Assert.Contains("FB0885A4472AA277326B4C061893B64CB0A4FCBB", packaging, StringComparison.Ordinal);
        Assert.Contains("self-signed", packaging, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnknownError", packaging, StringComparison.Ordinal); // the honest observed status
        // A signature changes the file, so publishing the pre-signature hash would break verification.
        Assert.Contains("SHA-256 changes", packaging, StringComparison.Ordinal);

        // Any document that mentions self-signing has to say in the same breath that the chain is not
        // trusted and that SmartScreen still warns - the pairing is what keeps it honest.
        string[] docs = Directory.GetFiles(RepoRoot(), "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".reference" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            .Where(f => File.ReadAllText(f).Contains("self-sig", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(docs.Length >= 2, "the README and the packaging notes should both describe the local signature");
        foreach (string doc in docs)
        {
            string text = File.ReadAllText(doc);
            Assert.True(
                text.Contains("not trusted", StringComparison.OrdinalIgnoreCase)
                || text.Contains("untrusted", StringComparison.OrdinalIgnoreCase),
                Path.GetFileName(doc) + " offers a self-signed signature without saying the chain is untrusted");
            Assert.True(text.Contains("SmartScreen", StringComparison.OrdinalIgnoreCase),
                Path.GetFileName(doc) + " offers a self-signed signature without saying the warning stays");
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool run = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { run = false; break; }
            }

            if (run) return true;
        }

        return false;
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
