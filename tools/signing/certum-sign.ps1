<#
.SYNOPSIS
    Apply the CA-issued Authenticode signature to a built artifact, using a certificate that
    lives on the signing service's own cloud card.

.DESCRIPTION
    WHAT THIS IS FOR
    One certificate, one owner, one honest path: the private key never leaves the signing
    service's hardware, so it is never a file, never a repository secret and never available to a
    build agent. Signing therefore happens in a person's Windows session, on a machine where the
    vendor's desktop client is connected to that card. This script is the checked-in description
    of that step - the command line, the digest algorithms, the timestamp, and the verification
    that has to pass before a signed file may be published.

    CURRENT STATUS, RECORDED IN THE REPOSITORY
    packaging/CertumSigning.settings.json says "state": "Unconfigured": no certificate has been
    acquired. With that value this script refuses to run, which is the point - a repository that
    has not decided who signs must not be able to produce a signature that nobody can account for.
    Everything this project publishes is unsigned until the owner changes that state.

    WHAT A SIGNATURE FROM THIS CLASS OF CERTIFICATE DOES AND DOES NOT DO
      - it identifies a named individual as the publisher, and it proves the bytes have not changed
        since that person signed them;
      - the chain is issued under a root Windows trusts, so the "unknown publisher" state changes and
        SmartScreen reputation can start to accumulate. Reputation is earned by downloads and age;
        the first signed releases can still show a SmartScreen prompt, and antivirus products make
        their own decision either way;
      - it grants nothing to the publisher over the recipient's copy: the program stays
        GPL-3.0-only, no licence check or expiry is added, and a source rebuild is legitimately
        unsigned.

    WHY signtool AND NOT Set-AuthenticodeSignature
    The vendor documents signtool against the certificate the desktop client exposes, and signtool
    controls the digest algorithms and the RFC 3161 timestamp explicitly. A card-backed key asks
    for a PIN per signing session (batch several files in one call to pay it once), so the session
    model is visible here rather than hidden.

    WHAT IS ALWAYS TRUE IN THIS SCRIPT
      - the timestamp is mandatory. Without one the signature stops verifying the day the
        certificate expires, which for this class of certificate is about a year away;
      - the result is read back and must come back Valid, with a timestamp, before the script says
        anything resembling success;
      - the SHA-256 of the file CHANGES when the signature is appended, so the script prints the
        post-signature hash and warns about any hash file that still quotes the old one;
      - the private key cannot leave this machine through this script: there is no PFX export, only
        an optional export of the public certificate (-ExportCer) so a signature can be checked
        later without the card.

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
The configuration says ciSigning=true, but no pipeline here can hold this key: a card-backed
private key is not exportable and is not a build secret. Either fix the configuration or design
and build the (self-hosted, interactive) runner that this claim would need - and change the
release notes, the packaging notes and ChannelPolicyTests in the same commit.
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

    # Exit non-zero unless every file verifies, so this mode can be used as a gate and not only as a
    # printout. An unsigned or self-signed file is a true statement about this project's artifacts,
    # and the line below says which of the two it is rather than pretending to approve it.
    $bad = @($seen | Where-Object { $_.Status -ne 'Valid' })
    if ($bad) {
        Fail ("verify-only found a file that does not verify as Valid: " +
              (($bad | ForEach-Object { "$($_.Path) = $($_.Status)" }) -join ', ') +
              ". For every artifact this project has published so far that is the expected answer, because" +
              " nothing here is code-signed - but nothing may be published as though it were signed until it" +
              " reads Valid with a timestamp.")
    }
    return
}

# ---------------------------------------------------------------- the key must be recorded first
if ($settings.state -eq 'Unconfigured') {
    Fail @"
No certificate is recorded: $SettingsPath still says "state": "Unconfigured", and the project
publishes unsigned builds. This script will not invent a key.

To sign anything you have to decide to buy a code-signing certificate, pass the signing service's
identity and project verification, activate it on a cloud card and connect the vendor's desktop
client on this machine. Then record what you got in that file - state "Recorded", the real
certificateThumbprint, signerCommonName and certificateNotAfter - and commit that together with
the wording in README, SECURITY.md and docs/RELEASE_PACKAGING.md that currently says the opposite.
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
    if ($resolved -match '\.dta$') { Fail 'never sign a user template file: the program signs its own binaries, and a signature on a data file would be a lie about what it means' }
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
Say 'The file changed when the signature was appended, so these are the hashes to publish:'
foreach ($p in $published) { Say ("{0}  {1}" -f $p.Sha256, $p.Path) }

$sums = Get-ChildItem -Path (Join-Path $repoRoot 'artifacts') -Filter SHA256SUMS.txt -Recurse -ErrorAction SilentlyContinue
foreach ($file in $sums) {
    $stale = @($published | Where-Object { (Get-Content -Raw -LiteralPath $file.FullName) -match $_.Sha256 })
    if ($stale) {
        Say ''
        Say "WARNING: $($file.FullName) still quotes a pre-signature hash for $($stale[0].Path)."
        Say 'A reader checking the download against that file would get a mismatch: regenerate it from the signed bytes.'
    }
}
Say ''
Say 'Publishing order, because a hash taken before signing is a mismatch waiting to happen:'
Say '  1. sign (this script)          2. re-hash          3. attach the signed file'
Say '  4. restate the SHA-256 and the signer in the release notes, and keep the notes, README,'
Say '     SECURITY.md and docs/RELEASE_PACKAGING.md describing the file that is actually attached.'

if ($ExportCer) {
    $dir = Split-Path -Parent $ExportCer
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Export-Certificate -Cert $cert -FilePath $ExportCer | Out-Null
    Say ''
    Say "public certificate written to $ExportCer (Export-Certificate writes the certificate only;"
    Say 'this script contains no PFX export, and a card-backed key cannot be exported anyway)'
}
