<#
.SYNOPSIS
    Apply a self-signed Authenticode signature to a built executable.

.DESCRIPTION
    What a self-signed signature IS:
      - proof that these exact bytes were produced on this machine by whoever holds this key;
      - integrity: any later change breaks the hash, so a build can be told apart from a tampered copy;
      - a signature you can re-check years later against the exported public certificate.

    What a self-signed signature is NOT, and must never be described as:
      - a trusted signature. Windows does not trust the chain, so SmartScreen still shows
        "Windows protected your PC" and antivirus products may still warn;
      - a substitute for a certificate from a public certificate authority, or for a signing service;
      - a reason to change anything on a user's machine. Trusting a self-signed certificate is a
        decision that makes *everything* it has ever signed run without a prompt on that machine -
        it belongs on a build machine, not on a user's PC.

    The private key stays in the current user's certificate store and is created non-exportable;
    only the public certificate is ever written to disk (-ExportCer).

    Timestamping is off by default: without a timestamp the signature is only verifiable while the
    certificate is valid. -TimestampUrl contacts a timestamping authority, which needs network
    access and does not make the chain trusted.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/signing/self-sign.ps1

.EXAMPLE
    ... -FilePath artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe `
        -ExportCer tools/signing/PUB-Template-Parameter-Editor-self-signed.cer

.EXAMPLE
    ... -CreateCertIfMissing          # generate a local code-signing certificate first
#>
[CmdletBinding()]
param(
    [string]$FilePath = 'artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe',
    [string]$CertName  = 'PUB Template Parameter Editor Local Self-Signed',
    [string]$Thumbprint = '',
    [string]$TimestampUrl = '',
    [string]$ExportCer = '',
    [switch]$CreateCertIfMissing
)

$ErrorActionPreference = 'Stop'

function Say([string]$m) { Write-Host $m }
function Pair([string]$k, $v) { Say ("{0,-22} {1}" -f $k, $v) }

# ---------------------------------------------------------------- the certificate
$cert = $null
if ($Thumbprint) {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Thumbprint -eq $Thumbprint.Replace(' ', '') } |
        Select-Object -First 1
} else {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.FriendlyName -eq $CertName -or $_.Subject -eq "CN=$CertName" } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if (-not $cert) {
    if (-not $CreateCertIfMissing) {
        throw @"
No code-signing certificate named '$CertName' in Cert:\CurrentUser\My.
Re-run with -CreateCertIfMissing to create one (private key stays in this user's store,
non-exportable), or point at an existing one with -Thumbprint.
"@
    }
    Say "creating a local code-signing certificate (non-exportable private key)"
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=$CertName" `
        -FriendlyName $CertName `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -KeyLength 3072 `
        -NotAfter (Get-Date).AddYears(5) `
        -CertStoreLocation Cert:\CurrentUser\My
}

# ---------------------------------------------------------------- the file
$resolved = (Resolve-Path -LiteralPath $FilePath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "not a file: $resolved" }
if ($resolved -match '\\(bin|obj|artifacts)\\' -or $resolved -match '\.dta$') {
    Say "note: signing a build output; nothing under bin/obj/artifacts is published from the repository"
}

$beforeHash = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
Pair "file"            $resolved
Pair "size bytes"      (Get-Item -LiteralPath $resolved).Length
Pair "sha256 before"   $beforeHash

# ---------------------------------------------------------------- sign it
$signArgs = @{ FilePath = $resolved; Certificate = $cert }
if ($TimestampUrl) { $signArgs['TimestampServer'] = $TimestampUrl }
$result = Set-AuthenticodeSignature @signArgs

Say ''
Say 'verifying after signing'
Say ('-' * 72)
$verify = Get-AuthenticodeSignature -LiteralPath $resolved
Pair "file"            $verify.Path
Pair "signature type"  $verify.SignatureType
Pair "status"          $verify.Status
Pair "status message"  $verify.StatusMessage
Pair "signer"          $verify.SignerCertificate.Subject
Pair "signer thumbprint" $verify.SignerCertificate.Thumbprint
Pair "valid from/to"   "$($verify.SignerCertificate.NotBefore.ToString('yyyy-MM-dd')) until $($verify.SignerCertificate.NotAfter.ToString('yyyy-MM-dd'))"
Pair "timestamp"       $(if ($verify.TimeStamperCertificate) { $verify.TimeStamperCertificate.Subject } else { 'none (signature expires with the certificate)' })
Pair "sha256 after"    (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
Say ('-' * 72)

$gloss = switch ($verify.Status) {
    'Valid'        { 'the chain is trusted ON THIS MACHINE; on any other machine the same file is still untrusted, because a self-signed certificate says nothing about who vouches for it' }
    'NotTrusted'   { 'the certificate chain is not trusted here, so SmartScreen and antivirus products treat this file exactly as they treat an unsigned one' }
    'UnknownError' { 'the chain ends in a certificate that no trust provider on this machine trusts - the normal result for a self-signed signature, not a fault in the build' }
    'HashMismatch' { 'STOP: the content no longer matches the signature. This file was changed after it was signed - do not publish it, find out what changed' }
    'NotSigned'    { 'STOP: the file carries no signature; signing did not happen' }
    default        { 'read the status message above before publishing anything' }
}
Say "what the status means: $gloss"

if ($beforeHash -ne (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash) {
    Say 'note: signing appends to the file, so its SHA-256 changes. Publish the hash printed above,'
    Say 'never the one from before signing - a reader checking the download would get a mismatch.'
}

if ($verify.Status -in 'HashMismatch', 'NotSigned', 'Error', 'Suspicious', 'PayloadNotSigned') {
    throw "refusing to report success: the signature on $resolved is $($verify.Status)"
}

if ($ExportCer) {
    $dir = Split-Path -Parent $ExportCer
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Export-Certificate -Cert $cert -FilePath $ExportCer | Out-Null
    Say ''
    Say "public certificate (no private key) written to $ExportCer"
    Say "so a signature made with it can be checked without the key"
}
