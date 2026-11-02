using System.IO;
using System.Text.Json;
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
        Assert.Contains("SHA-256", readme, StringComparison.Ordinal);

        // The released download is signed, and README says which key and how to check it. It must not
        // keep claiming the old unsigned state, and it must not promise anything about SmartScreen.
        Assert.Contains("Authenticode", readme, StringComparison.Ordinal);
        Assert.Contains("CertumSigning.settings.json", readme, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("no code signature", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not code-signed", readme, StringComparison.OrdinalIgnoreCase);

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
    public void NoSignerIsConfiguredInThePipelineAndTheRecordedOneIsNamedExactly()
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

        // A certificate is recorded, and the recording says plainly that CI cannot use it.
        using (Stream settings = File.OpenRead(Path.Combine(RepoRoot(), "packaging", "CertumSigning.settings.json")))
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(settings);
            Assert.Equal("Recorded", doc.RootElement.GetProperty("state").GetString());
            Assert.False(doc.RootElement.GetProperty("ciSigning").GetBoolean());
        }

        // The signing page states the state, the one key and the tools; it stays short.
        string signingPage = Read("docs", "CODE_SIGNING.md");
        Assert.Contains("Everything CI builds is unsigned", signingPage, StringComparison.Ordinal);
        Assert.Contains("One key", signingPage, StringComparison.Ordinal);
        Assert.Contains("CertumSigning.settings.json", signingPage, StringComparison.Ordinal);
        Assert.Contains("certum-sign.ps1", signingPage, StringComparison.Ordinal);
        Assert.Contains("verify-signature.ps1", signingPage, StringComparison.Ordinal);
        Assert.DoesNotContain("self-sign.ps1", signingPage, StringComparison.Ordinal);
        Assert.True(signingPage.Length < 2_400,
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
    public void OnlyTheRecordedCertificateSignsAndNoOtherKeyIsKeptOrOffered()
    {
        // The self-signed build-integrity key and its certificate are gone: a release of this project
        // is signed by one CA-issued certificate, or not at all.
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "tools", "signing", "self-sign.ps1")),
            "a self-signed signing tool invites a signed-looking download that vouches for nothing");
        foreach (string cer in Directory.GetFiles(Path.Combine(RepoRoot(), "tools", "signing"), "*.cer"))
        {
            Assert.EndsWith("code-signing.cer", cer, StringComparison.OrdinalIgnoreCase);
        }
        foreach (string doc in Directory.GetFiles(RepoRoot(), "*.md", SearchOption.AllDirectories)
                     .Where(f => Path.GetFileName(f) != "PLAN.md"   // the running log keeps its own history
                                 && !f.Contains(Path.DirectorySeparatorChar + ".reference" + Path.DirectorySeparatorChar)
                                 && !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)
                                 && !f.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar)))
        {
            Assert.False(File.ReadAllText(doc).Contains("self-sign.ps1", StringComparison.OrdinalIgnoreCase),
                Path.GetFileName(doc) + " still points at the self-signed key this project no longer keeps");
        }

        // The signing script is the only thing that signs, and it signs with the recorded key only.
        string script = Read("tools", "signing", "certum-sign.ps1");
        Assert.DoesNotContain("[string]$Thumbprint", script, StringComparison.Ordinal);
        Assert.Contains("1.3.6.1.5.5.7.3.3", script, StringComparison.Ordinal);   // id-kp-codeSigning
        Assert.DoesNotContain("1.3.6.1.5.5.7.3.2", script, StringComparison.Ordinal);
        Assert.Contains("self-issued", script, StringComparison.Ordinal);
        Assert.Contains("already signed by", script, StringComparison.Ordinal);
        Assert.Contains("Export-Certificate", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Export-PfxCertificate", script, StringComparison.Ordinal);

        // The public half of the recorded certificate is published, and it is a certificate rather
        // than a key bundle: PKCS#12 content (#12 is 1.2.840.113549.1.12) would be a private key.
        using JsonDocument document = JsonDocument.Parse(
            Read("packaging", "CertumSigning.settings.json"));
        JsonElement configuration = document.RootElement;
        string published = Path.Combine(RepoRoot(),
            configuration.GetProperty("publicCertificatePath").GetString()!);
        Assert.True(File.Exists(published), "the recorded certificate has no public half in the repository");
        byte[] der = File.ReadAllBytes(published);
        Assert.True(der.Length < 8192, "a public X.509 certificate is small; this one is not");
        Assert.Equal(0x30, der[0]); // DER SEQUENCE, not PEM text
        byte[] pkcs12Oid = [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x0C];
        Assert.False(ContainsSequence(der, pkcs12Oid), "that file carries PKCS#12 content: a private key would be committed");

        var issued = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(der);
        string recordedThumbprint = configuration.GetProperty("certificateThumbprint").GetString()!;
        Assert.Equal(recordedThumbprint, issued.Thumbprint);
        Assert.NotEqual("FB0885A4472AA277326B4C061893B64CB0A4FCBB", issued.Thumbprint);
        Assert.Equal("1.2.840.113549.1.1.11", issued.SignatureAlgorithm.Value); // sha256RSA
        Assert.Equal(issued.NotAfter.ToString("yyyy-MM-dd"), configuration.GetProperty("certificateNotAfter").GetString());
        Assert.Equal(configuration.GetProperty("certificateIssuer").GetString(), issued.Issuer);
        Assert.NotEqual(issued.Subject, issued.Issuer); // an authority issued it, so it vouches for something
        Assert.Equal(
            configuration.GetProperty("signerCommonName").GetString(),
            System.Text.RegularExpressions.Regex.Match(issued.Subject, "CN=([^,]+)").Groups[1].Value);
        // id-kp-codeSigning appears as an OID (6.8 = OID of 8 bytes) inside the extended key usage
        // extension; the chain's intermediates carry other purposes, so search the certificate for this one.
        Assert.True(
            ContainsSequence(der, [0x06, 0x08, 0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x03, 0x03]),
            "the published certificate does not carry the code-signing purpose");

        // The packaging notes describe the signed download, the unsigned CI build and the order of work.
        string packaging = Read("docs", "RELEASE_PACKAGING.md");
        Assert.Contains("Publishing a release", packaging, StringComparison.Ordinal);
        Assert.Contains("SHA-256 changes", packaging, StringComparison.Ordinal);
        Assert.Contains("NotSigned", packaging, StringComparison.Ordinal);
        Assert.Contains("certum-sign.ps1", packaging, StringComparison.Ordinal);
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
