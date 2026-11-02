using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using PubTemplateEditor.TestSupport;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Guards the signing configuration and scripts: the repository records one issued code-signing
/// certificate and holds no key, the signing scripts refuse every other key, no workflow calls a
/// signing tool, and no private-key material is tracked. Nothing here signs anything: every executed
/// signing script is given a target that does not exist, so it can only refuse.
/// </summary>
public class SigningToolingTests
{
    private static string RepoRoot() => TestSamples.RepoRoot();

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    private static JsonDocument Settings() =>
        JsonDocument.Parse(Read("packaging", "CertumSigning.settings.json"));

    [Fact]
    public void TheConfigurationRecordsOneIssuedCertificateAndNoKey()
    {
        using JsonDocument doc = Settings();
        JsonElement root = doc.RootElement;

        // The state the release job reads: a certificate exists, and CI may not use it.
        Assert.Equal("Recorded", root.GetProperty("state").GetString());
        Assert.False(root.GetProperty("ciSigning").GetBoolean());
        Assert.Matches("^[0-9A-Fa-f]{40}$", root.GetProperty("certificateThumbprint").GetString()!);
        Assert.False(string.IsNullOrEmpty(root.GetProperty("signerCommonName").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("certificateIssuer").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("publicCertificatePath").GetString()));
        Assert.True(DateTime.TryParse(root.GetProperty("certificateNotAfter").GetString(), out _),
            "the expiry is what makes the mandatory timestamp matter, so it has to be a date");

        // The file is a record, never a key: no private-key field of any shape may appear in it.
        string raw = Read("packaging", "CertumSigning.settings.json");
        Assert.DoesNotContain("-----BEGIN", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateKey", raw, StringComparison.Ordinal);

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
        using JsonDocument recorded = Settings();
        Assert.NotEqual(recorded.RootElement.GetProperty("certificateThumbprint").GetString(), exampleThumbprint);
    }

    [Fact]
    public void TheSigningScriptRefusesAnythingOtherThanTheRecordedCertificate()
    {
        // Every run here gets a target file that does not exist, so a script that failed to refuse
        // could not sign anything real. The refusals have to be the script's behaviour, not its wording.
        string nowhere = Path.Combine(Path.GetTempPath(), "pubte-signing-refusal-check", "nothing-here.exe");
        string real = Read("packaging", "CertumSigning.settings.json");

        var cases = new (string Field, string Value, string Pattern)[]
        {
            // no certificate recorded at all
            ("state", "\"Unconfigured\",", "Unconfigured|will not invent a key"),
            // a state that is neither of the two the configuration is allowed to carry
            ("state", "\"Almost\",", "only when the state is Recorded"),
            // a well-formed configuration naming a key no signing client presents
            ("certificateThumbprint", "\"0000000000000000000000000000000000000000\",", "No certificate with thumbprint"),
        };

        foreach ((string field, string value, string pattern) in cases)
        {
            string path = Path.Combine(Path.GetTempPath(), "pubte-signing-refusal-check",
                                       field + "-" + value.Trim('"', ',') + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, WithField(real, field, value));

            (string? output, int exitCode, bool available) = RunSigningScript(
                "tools" + Path.DirectorySeparatorChar + "signing" + Path.DirectorySeparatorChar + "certum-sign.ps1",
                "-SettingsPath", path, "-FilePath", nowhere);

            if (!available)
            {
                return; // no PowerShell here: the script is a Windows tool and cannot be executed
            }

            Assert.True(exitCode != 0, $"the script exited 0 for {field}={value}");
            Assert.True(Regex.IsMatch(output ?? string.Empty, pattern),
                        $"the script did not refuse {field} = {value} (exit {exitCode}):{Environment.NewLine}{output}");
        }
    }

    /// <summary>Replaces the line of one top-level field of the settings document.</summary>
    private static string WithField(string json, string field, string value)
    {
        string pattern = @"(?m)^(\s*)""" + field + @"""[^\n]*$";
        return Regex.Replace(json, pattern, m => m.Groups[1].Value + "\"" + field + "\": " + value);
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

        // The code-signing purpose is id-kp-codeSigning (1.3.6.1.5.5.7.3.3); a self-issued certificate
        // never signs a release, and a file signed by another key is never re-signed silently.
        Assert.Contains("1.3.6.1.5.5.7.3.3", script, StringComparison.Ordinal);
        Assert.Contains("self-issued", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already signed by", script, StringComparison.Ordinal);
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
        Assert.Contains("The CI build carries no signature, and cannot", release, StringComparison.Ordinal);
        Assert.Contains("CI may only publish a build it can describe", release, StringComparison.Ordinal);
        Assert.Contains("signingState", release, StringComparison.Ordinal);
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

    private static (string? output, int exitCode, bool available) RunSigningScript(string relativePath, params string[] extraArguments)
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
                foreach (string a in extraArguments)
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
