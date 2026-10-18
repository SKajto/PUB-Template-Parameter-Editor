[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [string]$Thumbprint = $env:PUB_EDITOR_SIGNING_THUMBPRINT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedPath = (Resolve-Path -LiteralPath $ExePath).Path
if ([System.IO.Path]::GetExtension($resolvedPath) -ne '.exe') {
    throw 'The signing target must be an .exe file.'
}

$storePath = 'Cert:\CurrentUser\My'
$codeSigningEku = '1.3.6.1.5.5.7.3.3'
$now = Get-Date
$certificate = $null

function Test-CodeSigningCertificate {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )
    $extension = $Certificate.Extensions |
        Where-Object { $_.Oid.Value -eq '2.5.29.37' } |
        Select-Object -First 1
    if ($null -eq $extension) {
        return $false
    }
    return $extension.Format($true) -match [regex]::Escape($codeSigningEku)
}

if ($Thumbprint) {
    $normalizedThumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    $certificate = Get-ChildItem -Path $storePath |
        Where-Object {
            $_.Thumbprint -eq $normalizedThumbprint -and
            $_.HasPrivateKey -and
            $_.NotBefore -le $now -and
            $_.NotAfter -gt $now -and
            (Test-CodeSigningCertificate -Certificate $_)
        } |
        Select-Object -First 1
    if ($null -eq $certificate) {
        throw 'The requested certificate is missing, expired, lacks a private key, or is not valid for code signing.'
    }
} else {
    $subject = $env:PUB_EDITOR_SIGNING_SUBJECT
    $certificate = Get-ChildItem -Path $storePath |
        Where-Object {
            (-not $subject -or $_.Subject -eq $subject) -and
            $_.HasPrivateKey -and
            $_.NotBefore -le $now -and
            $_.NotAfter -gt $now -and
            (Test-CodeSigningCertificate -Certificate $_)
        } |
        Sort-Object -Property NotAfter -Descending |
        Select-Object -First 1

    if ($null -eq $certificate) {
        throw 'No usable local code-signing certificate is installed in the current-user Personal store. Set PUB_EDITOR_SIGNING_THUMBPRINT or PUB_EDITOR_SIGNING_SUBJECT to select a valid certificate.'
    }
}

if (-not $certificate.HasPrivateKey) {
    throw 'The selected code-signing certificate has no private key.'
}

$signingResult = Set-AuthenticodeSignature `
    -FilePath $resolvedPath `
    -Certificate $certificate `
    -HashAlgorithm SHA256

$verification = Get-AuthenticodeSignature -FilePath $resolvedPath
if ($null -eq $verification.SignerCertificate -or
    $verification.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'Authenticode verification did not identify the selected signing certificate.'
}
$chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
$chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
$chainBuilt = $chain.Build($verification.SignerCertificate)
$chainErrors = @(
    $chain.ChainStatus |
        Where-Object { $_.Status -ne [System.Security.Cryptography.X509Certificates.X509ChainStatusFlags]::NoError } |
        ForEach-Object { $_.Status.ToString() }
)
$untrustedSelfSigned = -not $chainBuilt -and
    $chainErrors.Count -eq 1 -and
    $chainErrors[0] -eq 'UntrustedRoot'

if ($verification.Status -ne 'Valid' -and
    -not ($verification.Status -in @('UnknownError', 'NotTrusted') -and $untrustedSelfSigned)) {
    throw "Authenticode verification failed: $($verification.Status) - $($verification.StatusMessage)"
}

Write-Output "Signed: $resolvedPath"
Write-Output "Signer: $($certificate.Subject)"
Write-Output "Thumbprint: $($certificate.Thumbprint)"
Write-Output "Verification status: $($verification.Status)"
if ($untrustedSelfSigned) {
    Write-Warning 'The signature is cryptographically present, but this self-signed certificate is not trusted by Windows. This is for local testing, not public distribution.'
}
