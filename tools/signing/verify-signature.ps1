<#
.SYNOPSIS
    Print what the signature on a file actually says. Read-only.

.DESCRIPTION
    Reports the Authenticode status the platform computes, the signer and issuer, the certificate's
    validity window, whether a timestamp is present, the chain this machine builds, and the file's
    SHA-256. Nothing is written and nothing is signed.

    An unsigned file reports NotSigned - correct for every build this project's CI produces. A file
    whose chain ends somewhere this machine does not accept reports UnknownError or NotTrusted, which
    for a published download means do not run it and report the file.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/signing/verify-signature.ps1 `
        -FilePath "$env:USERPROFILE\Downloads\PUB_Template_Parameter_Editor.exe"
#>
[CmdletBinding()]
param(
    [string[]]$FilePath = @('artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe'),
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Say([string]$m) { Write-Host $m }
function Pair([string]$k, $v) { Say ("{0,-22} {1}" -f $k, $v) }

function Describe([string]$path) {
    $verify = Get-AuthenticodeSignature -LiteralPath $path
    $signer = $verify.SignerCertificate
    $chainStatus = ''
    if ($signer) {
        # The chain as the local platform builds it, including whatever revocation state it can see.
        $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
        $chain.ChainPolicy.RevocationMode = 'Online'
        [void]$chain.Build($signer)
        $chainStatus = ($chain.ChainElements | ForEach-Object { $_.Certificate.Subject }) -join ' <- '
    }
    [pscustomobject]@{
        path         = $verify.Path
        sizeBytes    = (Get-Item -LiteralPath $path).Length
        sha256       = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        signature    = $verify.SignatureType.ToString()
        status       = $verify.Status.ToString()
        statusMessage = $verify.StatusMessage
        signer       = if ($signer) { $signer.Subject } else { '' }
        issuer       = if ($signer) { $signer.Issuer } else { '' }
        thumbprint   = if ($signer) { $signer.Thumbprint } else { '' }
        validFrom    = if ($signer) { $signer.NotBefore.ToString('yyyy-MM-dd') } else { '' }
        validTo      = if ($signer) { $signer.NotAfter.ToString('yyyy-MM-dd') } else { '' }
        selfSigned   = if ($signer) { $signer.Subject -eq $signer.Issuer } else { $false }
        timestamp    = if ($verify.TimeStamperCertificate) { $verify.TimeStamperCertificate.Subject } else { '' }
        chain        = $chainStatus
    }
}

$results = foreach ($file in $FilePath) {
    $resolved = (Resolve-Path -LiteralPath $file -ErrorAction SilentlyContinue).Path
    if (-not $resolved) { Say "not a file: $file"; continue }
    Describe $resolved
}

if ($Json) {
    $results | ConvertTo-Json -Depth 3
    return
}

foreach ($r in $results) {
    Say ('-' * 72)
    Pair 'path'            $r.path
    Pair 'size bytes'      $r.sizeBytes
    Pair 'sha256'          $r.sha256
    Pair 'signature type'  $r.signature
    Pair 'status'          $r.status
    Pair 'status message'  $r.statusMessage
    if ($r.signer) {
        Pair 'signer'       $r.signer
        Pair 'issuer'       $r.issuer
        Pair 'thumbprint'   $r.thumbprint
        Pair 'valid from'   $r.validFrom
        Pair 'valid until'  $r.validTo
        Pair 'self-signed'  $(if ($r.selfSigned) { 'yes - subject equals issuer, so nobody outside that key vouches for it, and no release of this project may be signed that way' } else { 'no - a certification authority issued it' })
        Pair 'timestamp'    $(if ($r.timestamp) { $r.timestamp } else { 'none - stops verifying when the certificate expires' })
        Pair 'chain'        $r.chain
    }
}
Say ('-' * 72)
Say 'status is what THIS machine computes from the chain it can build; a different machine with a'
Say 'different root store or no network for revocation can report differently. Compare the sha256'
Say 'above with the hash published for this release before running anything.'
