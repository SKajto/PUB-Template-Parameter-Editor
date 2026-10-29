<#
.SYNOPSIS
    Sign a built artifact with the Authenticode certificate recorded in
    packaging/CertumSigning.settings.json.

.DESCRIPTION
    The private key lives on the signing service's card, so it is never a file, never a repository
    secret and never available to a build agent: this script runs in a Windows session where the
    vendor's client is connected to that card. It reads the key's thumbprint from the settings file
    and refuses while the file records none.

    Rules the script enforces:
      - SHA-256 file and timestamp digests, and a mandatory RFC 3161 timestamp: without one the
        signature stops verifying when the certificate expires;
      - the result is read back and must report Valid with a timestamp, otherwise the script fails;
      - signing appends to the file, so the post-signature SHA-256 is printed and any hash file that
        still quotes the old one is flagged;
      - no private-key export. -ExportCer writes the public certificate only.

    signtool is used rather than Set-AuthenticodeSignature because the vendor documents it, it
    controls the digests and timestamp explicitly, and a card-backed key asks for a PIN once per
    signing session - batch the files in one call to pay it once.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/signing/certum-sign.ps1

.EXAMPLE
    ... -FilePath artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe `
        -ExportCer tools/signing/PUB-Template-Parameter-Editor-code-signing.cer

.EXAMPLE
    ... -VerifyOnly          # read back the signature without touching the file
#>
[CmdletBinding()]
param(
    [string[]]$FilePath = @('artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe'),
    [string]$SettingsPath = '',
    [string]$Thumbprint = '',
    [string]$TimestampUrl = '',
    [string]$Description = '',
    [string]$ExportCer = '',
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

function Say([string]$m) { Write-Host $m }
function Pair([string]$k, $v) { Say ("{0,-22} {1}" -f $k, $v) }
function Fail([string]$m) { throw $m }

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot   = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
if (-not $SettingsPath) { $SettingsPath = Join-Path $repoRoot 'packaging\CertumSigning.settings.json' }

# ---------------------------------------------------------------- the recorded configuration
if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {
    Fail "no signing configuration at $SettingsPath - the project records no signer"
}
$settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json

if ($settings.ciSigning -eq $true) {
    Fail @"
The configuration says ciSigning=true, but a card-backed private key is not exportable and is not a
build secret. Unset ciSigning, or add the interactive self-hosted runner that claim needs - and
update the release notes, README, SECURITY.md and ChannelPolicyTests in the same commit.
"@
}

function Resolve-Signtool {
    $kitsRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not (Test-Path $kitsRoot)) { Fail "Windows SDK not found at $kitsRoot (signing needs signtool.exe)" }
    $tool = Get-ChildItem -Path $kitsRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $tool) { Fail "signtool.exe not found under $kitsRoot - install the Windows SDK" }
    return $tool
}

function Show-Signature([string]$path) {
    $verify = Get-AuthenticodeSignature -LiteralPath $path
    Pair 'path'              $verify.Path
    Pair 'signature type'    $verify.SignatureType
    Pair 'status'            $verify.Status
    Pair 'status message'    $verify.StatusMessage
    if ($verify.SignerCertificate) {
        Pair 'signer'            $verify.SignerCertificate.Subject
        Pair 'issuer'            $verify.SignerCertificate.Issuer
        Pair 'thumbprint'        $verify.SignerCertificate.Thumbprint
        Pair 'valid from/to'    "$($verify.SignerCertificate.NotBefore.ToString('yyyy-MM-dd')) until $($verify.SignerCertificate.NotAfter.ToString('yyyy-MM-dd'))"
    }
    Pair 'timestamp'         $(if ($verify.TimeStamperCertificate) { $verify.TimeStamperCertificate.Subject } else { 'NONE - the signature stops verifying when the certificate expires' })
    Pair 'sha256 of file'    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    return $verify
}

function Explain([string]$status) {
    switch ($status) {
        'Valid'        { 'the chain this machine trusts leads back to the certificate; on another machine the same file is judged by the same chain, so a failure here is a reason not to publish' }
        'NotTrusted'   { 'the chain is not trusted here. For a certificate under a root Windows trusts this normally means the desktop client is not connected to the card, the intermediate certificates are missing, or the machine has no way to fetch them' }
        'UnknownError' { 'a chain problem this trust provider cannot resolve. If the chain ends in a self-signed certificate, the wrong certificate was used - the self-signed local key (tools/signing/self-sign.ps1) is integrity only and is never a substitute' }
        'HashMismatch' { 'STOP: the content no longer matches the signature. This file was modified after it was signed - do not publish it, find out what changed' }
        'NotSigned'    { 'no signature on this file. Correct for every artifact this project builds in CI' }
        default        { 'read the status message above before publishing anything' }
    }
}

# ---------------------------------------------------------------- verify-only path
if ($VerifyOnly) {
    $signtool = Resolve-Signtool
    $seen = @()
    foreach ($file in $FilePath) {
        $resolved = (Resolve-Path -LiteralPath $file -ErrorAction SilentlyContinue).Path
        if (-not $resolved) { Fail "not a file: $file" }
        Say ''
        Say ('-' * 72)
        $verify = Show-Signature $resolved
        Say ('-' * 72)
        Explain ([string]$verify.Status)
        Say ''
        Say 'chain as the platform verifies it:'
        & $signtool verify /pa /all $resolved | Out-Host
        $seen += [pscustomobject]@{ Path = $resolved; Status = [string]$verify.Status }
    }
    Say ''
    Say 'verify-only: nothing was written.'

    # Non-zero unless every file verifies, so this mode works as a gate and not only as a printout.
    $bad = @($seen | Where-Object { $_.Status -ne 'Valid' })
    if ($bad) {
        Fail ("verify-only: at least one file does not verify as Valid: " +
              (($bad | ForEach-Object { "$($_.Path) = $($_.Status)" }) -join ', ') +
              ". Nothing may be published as signed until it reads Valid with a timestamp.")
    }
    return
}

# ---------------------------------------------------------------- the key must be recorded first
if ($settings.state -eq 'Unconfigured') {
    Fail @"
No certificate is recorded: $SettingsPath still says "state": "Unconfigured". This script will not
invent a key. After a certificate is issued, set state to "Recorded" and put the real
certificateThumbprint, signerCommonName and certificateNotAfter in that file, and update the
unsigned/signed wording in README, SECURITY.md and docs/RELEASE_PACKAGING.md in the same commit.
"@
}

if (-not $Thumbprint) { $Thumbprint = $settings.certificateThumbprint }
$Thumbprint = ($Thumbprint -replace '[^0-9A-Fa-f]', '')
if ($Thumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
    Fail "certificateThumbprint '$($settings.certificateThumbprint)' is not a 40-digit hex thumbprint - record the real one in $SettingsPath before signing"
}
if (-not $TimestampUrl) { $TimestampUrl = $settings.timestampUrl }
if (-not $TimestampUrl) { Fail 'a timestamp url is required: an untimestamped signature stops verifying the day the certificate expires' }

# ---------------------------------------------------------------- the certificate, on this machine
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Thumbprint -eq $Thumbprint.ToUpper() } |
    Select-Object -First 1
if (-not $cert) {
    Fail @"
No certificate with thumbprint $Thumbprint in Cert:\CurrentUser\My.
A cloud card appears there only while the vendor's desktop client is connected to the service:
check that it is running and logged in, and that the thumbprint in $SettingsPath matches the
certificate the client lists.
"@
}

if ($cert.Subject -eq $cert.Issuer) {
    Fail "the certificate found is self-signed ($($cert.Subject)). That is the local build-integrity key, not a publisher certificate; the recorded thumbprint must point at the CA-issued one"
}
$codeSigningEku = '1.3.6.1.5.5.7.3.2'
if (-not ($cert.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningEKU })) {
    Fail 'that certificate is not valid for code signing (no Code Signing EKU)'
}
$now = Get-Date
if ($now -lt $cert.NotBefore -or $now -gt $cert.NotAfter) {
    Fail "the certificate is outside its validity window ($($cert.NotBefore.ToString('yyyy-MM-dd')) - $($cert.NotAfter.ToString('yyyy-MM-dd')))"
}
if ($cert.NotAfter -lt $now.AddDays(30)) {
    Say "note: this certificate expires on $($cert.NotAfter.ToString('yyyy-MM-dd')). Renew it, and keep the timestamp on everything already signed - the timestamp is what keeps old releases verifiable."
}

if (-not $Description) { $Description = 'PUB Template Parameter Editor' }
$signtool = Resolve-Signtool

Say ''
Pair 'configuration'     $SettingsPath
Pair 'state'             $settings.state
Pair 'signer'            $cert.Subject
Pair 'issuer'            $cert.Issuer
Pair 'thumbprint'        $cert.Thumbprint
Pair 'expires'           $cert.NotAfter.ToString('yyyy-MM-dd')
Pair 'signtool'          $signtool
Pair 'timestamp'         "$TimestampUrl (sha256)"

# ---------------------------------------------------------------- sign, then read it back
$targets = @()
foreach ($file in $FilePath) {
    $resolved = (Resolve-Path -LiteralPath $file -ErrorAction SilentlyContinue).Path
    if (-not $resolved) { Fail "not a file: $file" }
    if ($resolved -match '\.dta$') { Fail "refusing to sign a template data file: only the application's own binaries are signed" }
    $targets += $resolved
    Pair 'file'           $resolved
    Pair 'size bytes'     (Get-Item -LiteralPath $resolved).Length
    Pair 'sha256 before'  (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
}

Say ''
Say 'signing (a card-backed key may ask for the PIN once for this batch)'
Say ('-' * 72)
$arguments = @('sign', '/sha1', $cert.Thumbprint, '/fd', 'sha256', '/td', 'sha256', '/tr', $TimestampUrl, '/d', $Description, '/v') + $targets
& $signtool @arguments | Out-Host
$exit = $LASTEXITCODE
Say ('-' * 72)
if ($exit -ne 0) { Fail "signtool exited with $exit. Nothing is published: check whether the desktop client is connected, whether the PIN was accepted, and whether the timestamp service is reachable" }

$published = @()
foreach ($resolved in $targets) {
    Say ''
    Say 'verifying after signing'
    Say ('-' * 72)
    $verify = Show-Signature $resolved
    Say ('-' * 72)
    Explain ([string]$verify.Status)

    & $signtool verify /pa /all $resolved | Out-Host

    if ($verify.Status -ne 'Valid') {
        Fail "refusing to report success: the signature on $resolved reads '$($verify.Status)'. A CA-issued chain has to verify as Valid on a machine that can reach the revocation and intermediate endpoints, or it will not verify on a user's either"
    }
    if (-not $verify.TimeStamperCertificate) {
        Fail "the signature on $resolved carries no timestamp. Publishing it would tie the release's lifetime to the certificate's, and every later renewal would make old downloads look broken"
    }
    $after = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
    $published += [pscustomobject]@{ Path = $resolved; Sha256 = $after }
}

# ---------------------------------------------------------------- what to publish now
Say ''
Say ('-' * 72)
Say 'publish these hashes (signing appends to the file, so the hash changes):'
foreach ($p in $published) { Say ("{0}  {1}" -f $p.Sha256, $p.Path) }

$sums = Get-ChildItem -Path (Join-Path $repoRoot 'artifacts') -Filter SHA256SUMS.txt -Recurse -ErrorAction SilentlyContinue
foreach ($file in $sums) {
    $stale = @($published | Where-Object { (Get-Content -Raw -LiteralPath $file.FullName) -match $_.Sha256 })
    if ($stale) {
        Say ''
        Say "WARNING: $($file.FullName) still quotes a pre-signature hash for $($stale[0].Path) - regenerate it from the signed bytes."
    }
}

if ($ExportCer) {
    $dir = Split-Path -Parent $ExportCer
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Export-Certificate -Cert $cert -FilePath $ExportCer | Out-Null
    Say ''
    Say "public certificate written to $ExportCer (Export-Certificate writes the certificate only;"
    Say 'this script contains no PFX export, and a card-backed key cannot be exported anyway)'
}
