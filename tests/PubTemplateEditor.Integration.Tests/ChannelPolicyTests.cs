using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Keeps the documented distribution channel and signing status in step with the workflows, the
/// packaging configuration and the release notes: no document may claim a signature or a channel the
/// build does not have, and no workflow may reference a signer that does not exist.
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
            "docs/CODE_SIGNING.md",
            "docs/WINDOWS_PUBLISH.md",
            "docs/GPL_RELEASE.md",
            "docs/PRESERVATION_NOTES.md",
            "docs/PROJECT_SETUP.md",
            ".github/workflows/ci.yml",
            ".github/workflows/release.yml",
            "packaging/build-msix.ps1",
            "packaging/AppxManifest.template.xml",
            "tools/signing/certum-sign.ps1",
            "tools/signing/verify-signature.ps1",
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
        // the release itself has to state the signing status and how to check the download
        Assert.Contains("not code-signed", release, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", release, StringComparison.Ordinal);

        // Packaging is opt-in tooling, not a pipeline stage.
        Assert.Contains("buildMsix", release, StringComparison.Ordinal);
        string ci = Read(".github", "workflows", "ci.yml");
        Assert.DoesNotContain("build-msix.ps1", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSignerIsConfiguredInThePipelineAndNothingClaimsOne()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "docs", "CODE_SIGNING_POLICY.md")),
            "the signing policy page belonged to a signing subscription this project does not have");

        // No workflow may call a signing tool or reference a signing service.
        foreach (string workflow in Directory.GetFiles(Path.Combine(RepoRoot(), ".github", "workflows"), "*.yml"))
        {
            string text = File.ReadAllText(workflow);
            Assert.DoesNotContain("signpath", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("signing-request", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SIGNPATH_", text, StringComparison.Ordinal);
            Assert.DoesNotContain("sign-portable-exe", text, StringComparison.Ordinal);
            Assert.DoesNotContain("signed-portable", text, StringComparison.Ordinal);
            Assert.DoesNotContain("signtool", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Set-AuthenticodeSignature", text, StringComparison.Ordinal);
        }

        // Until a certificate is recorded, the settings file must keep saying there is none.
        using (Stream settings = File.OpenRead(Path.Combine(RepoRoot(), "packaging", "CertumSigning.settings.json")))
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(settings);
            Assert.Equal("Unconfigured", doc.RootElement.GetProperty("state").GetString());
            Assert.False(doc.RootElement.GetProperty("ciSigning").GetBoolean());
        }

        // The signing page states the status and points at the tools; it stays short.
        string signingPage = Read("docs", "CODE_SIGNING.md");
        Assert.Contains("not signed", signingPage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CertumSigning.settings.json", signingPage, StringComparison.Ordinal);
        Assert.Contains("certum-sign.ps1", signingPage, StringComparison.Ordinal);
        Assert.Contains("verify-signature.ps1", signingPage, StringComparison.Ordinal);
        Assert.True(signingPage.Length < 2_000,
            "the signing page is a pointer, not an essay: " + signingPage.Length + " characters");

        // Documents that readers and contributors act on must not point at a configured signer.
        string[] files =
        [
            "README.md", "SECURITY.md", "CONTRIBUTING.md", "CODE_OF_CONDUCT.md", "START_HERE.md",
            "REQUIREMENTS.md", ".github/PULL_REQUEST_TEMPLATE.md", ".github/CODEOWNERS",
            "docs/RELEASE_PACKAGING.md", "docs/PROJECT_SETUP.md", "docs/GPL_RELEASE.md",
            "docs/WINDOWS_PUBLISH.md", "docs/TEST_MATRIX.md", "docs/PRESERVATION_NOTES.md",
            "packaging/build-msix.ps1", "packaging/AppxManifest.template.xml",
            "docs/CODE_SIGNING.md", "tools/signing/certum-sign.ps1", "tools/signing/verify-signature.ps1",
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
        Assert.Contains("NonExportable", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Export-PfxCertificate", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Exportable", script.Replace("NonExportable", ""), StringComparison.Ordinal);
        // A broken signature is a hard failure, and the untrusted chain has to be named.
        Assert.Contains("HashMismatch", script, StringComparison.Ordinal);
        Assert.Contains("throw", script, StringComparison.Ordinal);
        Assert.Contains("SmartScreen", script, StringComparison.Ordinal);
        Assert.Contains("not trusted", script, StringComparison.Ordinal);

        // Only the public certificate is published: PKCS#12 content (#12 is 1.2.840.113549.1.12) in a
        // .cer would mean a private key had been committed.
        string cer = Path.Combine(RepoRoot(), "tools", "signing", "PUB-Template-Parameter-Editor-self-signed.cer");
        Assert.True(File.Exists(cer), "the public certificate of the signing key should be published next to the script");
        byte[] der = File.ReadAllBytes(cer);
        Assert.True(der.Length < 8192, "a public X.509 certificate is small; this one is not");
        Assert.Equal(0x30, der[0]); // DER SEQUENCE, not PEM text
        byte[] pkcs12Oid = [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x0C];
        Assert.False(ContainsSequence(der, pkcs12Oid), "that file carries PKCS#12 content: a private key would be committed");

        // Which key signed, and until when.
        string packaging = Read("docs", "RELEASE_PACKAGING.md");
        Assert.Contains("FB0885A4472AA277326B4C061893B64CB0A4FCBB", packaging, StringComparison.Ordinal);
        Assert.Contains("self-signed", packaging, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnknownError", packaging, StringComparison.Ordinal); // observed status
        // A signature changes the file, so publishing the pre-signature hash would break verification.
        Assert.Contains("SHA-256 changes", packaging, StringComparison.Ordinal);

        // A document offering a self-signed signature has to say the chain is not trusted and that
        // SmartScreen still warns.
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
            "the store-submission design document must not come back");
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "docs", "RELEASE_PACKAGING.md")));

        // Planning documents keep their original wording but carry the note that the channel changed.
        // docs/WINDOWS_PUBLISH.md was rewritten outright and names no other channel.
        foreach (string doc in new[] { "REQUIREMENTS.md", "START_HERE.md",
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
