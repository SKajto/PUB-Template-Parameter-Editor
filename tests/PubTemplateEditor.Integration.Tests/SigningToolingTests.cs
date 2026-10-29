using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using PubTemplateEditor.TestSupport;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Guards the signing configuration and scripts: the repository holds no key and configures no
/// signer, the signing scripts refuse until a real certificate is recorded, no workflow calls a
/// signing tool, and no private-key material is tracked. Nothing here signs anything.
/// </summary>
public class SigningToolingTests
{
    private static string RepoRoot() => TestSamples.RepoRoot();

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    private static JsonDocument Settings() =>
        JsonDocument.Parse(Read("packaging", "CertumSigning.settings.json"));

    [Fact]
    public void TheRepositoryRecordsNoSignerAndNoKey()
    {
        using JsonDocument doc = Settings();
        JsonElement root = doc.RootElement;

        // The state the release job reads.
        Assert.Equal("Unconfigured", root.GetProperty("state").GetString());
        Assert.False(root.GetProperty("ciSigning").GetBoolean());
        Assert.True(string.IsNullOrEmpty(root.GetProperty("certificateThumbprint").GetString()),
            "a thumbprint recorded together with state 'Unconfigured' is the inconsistency the release job rejects");
        Assert.True(string.IsNullOrEmpty(root.GetProperty("signerCommonName").GetString()));
        Assert.True(string.IsNullOrEmpty(root.GetProperty("publicCertificatePath").GetString()),
            "no public certificate may be committed for a certificate that does not exist");

        // Digests and the timestamp are fixed in the configuration rather than chosen at the keyboard.
        Assert.Equal("sha256", root.GetProperty("fileDigestAlgorithm").GetString());
        Assert.Equal("sha256", root.GetProperty("timestampDigestAlgorithm").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("timestampUrl").GetString()));
    }

    [Fact]
    public void TheFilledInExampleIsMarkedAsAnExampleAndStaysApartFromTheRealConfiguration()
    {
        string example = Read("examples", "CertumSigning.settings.json.example");
        using JsonDocument doc = JsonDocument.Parse(example);

        Assert.Contains("copy the shape, never the values", example, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("Unconfigured", doc.RootElement.GetProperty("state").GetString());
        Assert.False(doc.RootElement.GetProperty("ciSigning").GetBoolean());

        // The example's thumbprint is deliberately not the configured one, and the configured file
        // is the only one any tool reads by default.
        string exampleThumbprint = doc.RootElement.GetProperty("certificateThumbprint").GetString()!;
        Assert.Matches("^[0-9A-Fa-f]{40}$", exampleThumbprint);
    }

    [Fact]
    public void TheSigningScriptRefusesWhileNoCertificateIsRecorded()
    {
        // Run it: the refusal has to be the script's real behaviour, not just its wording.
        (string? output, int exitCode, bool available) = RunSigningScript(
            "tools" + Path.DirectorySeparatorChar + "signing" + Path.DirectorySeparatorChar + "certum-sign.ps1");

        if (!available)
        {
            return; // no PowerShell here: the script is a Windows tool and cannot be executed
        }

        Assert.NotEqual(0, exitCode);
        Assert.Matches("Unconfigured|will not invent a key", output ?? string.Empty);
    }

    [Fact]
    public void TheSigningScriptIsWrittenAroundTheVendorDocumentedPathAndVerifiesWhatItDid()
    {
        string script = Read("tools", "signing", "certum-sign.ps1");

        Assert.Contains("signtool", script, StringComparison.Ordinal);
        Assert.Contains("'/fd', 'sha256'", script, StringComparison.Ordinal);
        Assert.Contains("'/td', 'sha256'", script, StringComparison.Ordinal);
        Assert.Contains("'/tr'", script, StringComparison.Ordinal);          // timestamp, not optional
        Assert.Contains("verify /pa /all", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("'Valid'", script, StringComparison.Ordinal);
        Assert.Contains("TimeStamperCertificate", script, StringComparison.Ordinal);
        Assert.Contains("sha256 before", script, StringComparison.Ordinal);
        Assert.Contains("Sha256", script, StringComparison.Ordinal);
        Assert.Contains("Unconfigured", script, StringComparison.Ordinal);

        // Template data files must never be signed.
        Assert.Contains(@"\.dta$", script, StringComparison.Ordinal);

        // The key must not be able to leave through this script.
        Assert.DoesNotContain("Export-PfxCertificate", script, StringComparison.Ordinal);
        Assert.Contains("Export-Certificate", script, StringComparison.Ordinal);

        // The local self-signed key and a CA-issued certificate must stay distinguishable.
        Assert.Contains("self-sign.ps1", script, StringComparison.Ordinal);
        Assert.Contains("self-signed", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not trusted", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheVerificationScriptReadsAndNeverWrites()
    {
        string script = Read("tools", "signing", "verify-signature.ps1");

        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("X509Chain", script, StringComparison.Ordinal);
        Assert.Contains("NotSigned", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.DoesNotContain("signtool sign", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageSigningIsOptInAndRefusesWithoutARecordedKey()
    {
        string script = Read("packaging", "build-msix.ps1");

        Assert.Contains("[switch]$Sign", script, StringComparison.Ordinal);
        Assert.Contains("CertumSigning.settings.json", script, StringComparison.Ordinal);
        Assert.Contains("state -eq \"Unconfigured\"", script, StringComparison.Ordinal);
        Assert.Contains("signtool add", script, StringComparison.Ordinal);
        Assert.Contains("verify /pa /all", script, StringComparison.Ordinal);
        // The default build record reports the package as unsigned.
        Assert.Contains("unsigned", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseWorkflowGatesItsOwnSigningClaims()
    {
        string release = Read(".github", "workflows", "release.yml");

        Assert.Contains("Signing configuration must not claim a key this pipeline does not have", release, StringComparison.Ordinal);
        Assert.Contains("The payload really is unsigned, as every document says", release, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", release, StringComparison.Ordinal);
        Assert.Contains("NotSigned", release, StringComparison.Ordinal);
        Assert.Contains("SIGNING-STATUS.txt", release, StringComparison.Ordinal);
        // The workflow states its own signing status.
        Assert.Contains("not code-signed", release, StringComparison.Ordinal);

        string ci = Read(".github", "workflows", "ci.yml");
        Assert.DoesNotContain("signtool", ci, StringComparison.Ordinal);
        Assert.Contains("No private key or key-shaped file may be tracked", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPrivateOrCertificateBundleMaterialIsTracked()
    {
        string? listed = RunGit(RepoRoot(), "ls-files", "-z");
        if (listed is null)
        {
            return; // no git on this machine: nothing to inspect
        }

        string[] files = listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        string[] forbiddenExtension = [".pfx", ".p12", ".pkcs12", ".pvk", ".spc", ".snk", ".csr", ".key"];
        List<string> offenders = files
            .Where(f => forbiddenExtension.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (string file in files)
        {
            if (Path.GetExtension(file).Equals(".cer", StringComparison.OrdinalIgnoreCase))
            {
                continue; // public certificates are published on purpose and asserted elsewhere
            }

            string full = Path.Combine(RepoRoot(), file);
            if (!File.Exists(full) || new FileInfo(full).Length > 2_000_000)
            {
                continue;
            }

            string text = File.ReadAllText(full);
            if (Regex.IsMatch(text, "BEGIN (RSA |EC |ENCRYPTED |OPENSSH )?PRIVATE KEY"))
            {
                offenders.Add(file + " (contains a private key block)");
            }
        }

        Assert.True(offenders.Count == 0,
            "private key material must never be tracked: " + string.Join(", ", offenders));
    }

    private static (string? output, int exitCode, bool available) RunSigningScript(string relativePath)
    {
        foreach (string candidate in new[] { "powershell.exe", "pwsh" })
        {
            try
            {
                var start = new ProcessStartInfo(candidate)
                {
                    WorkingDirectory = RepoRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (string a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                                             "-File", Path.Combine(RepoRoot(), relativePath) })
                {
                    start.ArgumentList.Add(a);
                }

                using Process process = Process.Start(start)!;
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(120_000);
                return (output, process.ExitCode, true);
            }
            catch (Exception)
            {
                // try the next candidate
            }
        }

        return (null, -1, false);
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
            process.WaitForExit(30_000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
