<#
build-msix.ps1 - assembles the draft/self-contained MSIX payload with Windows SDK MakeAppx.

Design notes (docs/GITHUB_STORE_RELEASE.md):
- The MSIX payload is a normal self-contained win-x64 layout, not a single-file bundle.
- A package built here is for running the app as a packaged app during development. It is not a
  distribution channel (this project publishes a single-file executable on GitHub), it is built
  unsigned like everything else here, and if such a package were ever published it would have to be
  signed and trusted first - see docs/RELEASE_PACKAGING.md. Nothing here is, or may be called,
  store-signed.
  The produced .msix is therefore NOT trusted for arbitrary sideload; it is a submission artifact.
- -Strict rejects every unresolved REPLACE_* value in packaging/StoreRelease.settings.json,
  which is what a release/submission build must do (REL-04).

Only built-in tools are used: dotnet publish + Windows SDK MakeAppx.
#>
[CmdletBinding()]
param(
    [string]$Version = "0.1.0",
    [ValidateSet("Draft", "Strict")]
    [string]$Mode = "Draft",
    [string]$SettingsPath = "",
    [string]$LayoutDir = "",
    [string]$OutDir = "",
    [string]$PayloadDir = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$strict = ($Mode -eq "Strict")
$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
if (-not $SettingsPath) { $SettingsPath = Join-Path $scriptRoot "StoreRelease.settings.json" }
if (-not $LayoutDir)    { $LayoutDir    = Join-Path $repoRoot "artifacts\packaging\layout" }
if (-not $OutDir)       { $OutDir       = Join-Path $repoRoot "artifacts\packaging\out" }
if (-not $PayloadDir)   { $PayloadDir   = Join-Path $repoRoot "artifacts\publish\store-win-x64" }

function Fail([string]$message) { throw $message }

# ---------- 1. version ----------
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Fail "Version '$Version' must be MAJOR.MINOR.PATCH (the fourth package segment is derived as .0)."
}
$packageVersion = "$Version.0"

# ---------- 2. settings / identity ----------
if (-not (Test-Path $SettingsPath)) { Fail "Missing settings file: $SettingsPath" }
$settings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json

$tokens = [ordered]@{
    "PACKAGE_IDENTITY_NAME"    = $settings.packageIdentityName
    "PUBLISHER"                = $settings.publisher
    "PUBLISHER_DISPLAY_NAME"   = $settings.publisherDisplayName
    "DISPLAY_NAME"             = if ($settings.displayName) { $settings.displayName } else { "PUB Template Parameter Editor" }
    "PACKAGE_VERSION"          = $packageVersion
    "MIN_VERSION"              = $settings.minVersion
    "MAX_VERSION_TESTED"       = $settings.maxVersionTested
}

$unresolved = @($tokens.GetEnumerator() | Where-Object { -not $_.Value -or ($_.Value -like "REPLACE_*") })
if ($unresolved.Count -gt 0) {
    $names = ($unresolved | ForEach-Object { $_.Key }) -join ", "
    if ($strict) {
        Fail "Submission build rejected: unresolved identity values ($names) in $SettingsPath. REL-04 forbids invented identity values."
    }
    Write-Warning "Draft package uses placeholder identity values: $names"
    # Placeholders keep makeappx happy for shape validation only.
    $tokens["PACKAGE_IDENTITY_NAME"]  = if ($tokens["PACKAGE_IDENTITY_NAME"])  { $tokens["PACKAGE_IDENTITY_NAME"] }  else { "PUBTemplateEditorDraft" }
    if ($tokens["PACKAGE_IDENTITY_NAME"] -like "REPLACE_*") { $tokens["PACKAGE_IDENTITY_NAME"] = "PUBTemplateEditorDraft" }
    if ($tokens["PUBLISHER"] -like "REPLACE_*")            { $tokens["PUBLISHER"] = "CN=PUBTemplateEditorDraft" }
    if ($tokens["PUBLISHER_DISPLAY_NAME"] -like "REPLACE_*") { $tokens["PUBLISHER_DISPLAY_NAME"] = "PUB Template Editor (draft)" }
}

foreach ($key in @("architecture", "targetFramework", "licenseExpression", "minVersion", "maxVersionTested")) {
    if (-not $settings.$key) { Fail "Settings file is missing '$key'." }
    if ($settings.$key -like "REPLACE_*") { Fail "Settings value '$key' is unresolved." }
}
if ($settings.architecture -ne "x64") { Fail "Only x64 is supported for this target ($($settings.architecture))." }

# ---------- 3. payload ----------
if (-not $SkipPublish) {
    Write-Output " Publishing self-contained payload -> $PayloadDir"
    dotnet publish (Join-Path $repoRoot "src\PubTemplateEditor.App\PubTemplateEditor.App.csproj") `
        -c Release -p:PublishProfile=StoreMsixWin_x64 -o $PayloadDir | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed with exit code $LASTEXITCODE" }
}
$payload = Resolve-Path $PayloadDir -ErrorAction Stop
$appExe = Join-Path $payload "PUB_Template_Parameter_Editor.exe"
if (-not (Test-Path $appExe)) { Fail "Payload has no PUB_Template_Parameter_Editor.exe: $payload" }

$loosePdb = @(Get-ChildItem -Path $payload -Filter *.pdb -Recurse)
if ($loosePdb.Count -gt 0) {
    Fail "Payload must not contain loose .pdb files (DebugType=embedded): $($loosePdb[0].Name) found"
}

# ---------- 4. layout ----------
if (Test-Path $LayoutDir) { Remove-Item -Recurse -Force $LayoutDir }
New-Item -ItemType Directory -Force -Path $LayoutDir | Out-Null
Copy-Item -Path (Join-Path $payload "*") -Destination $LayoutDir -Recurse -Force

$assetsDir = Join-Path $scriptRoot "assets"
if (-not (Test-Path (Join-Path $assetsDir "Square44x44Logo.png"))) {
    Write-Output " Generating provisional tile assets"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptRoot "generate-placeholder-assets.ps1") -OutDir $assetsDir | Out-Host
}
Copy-Item -Path $assetsDir -Destination (Join-Path $LayoutDir "assets") -Recurse -Force

# Offline licence/notices beside the payload so the package itself carries them
# (GPL distribution: the licence travels with the binary, not only with the source).
$noticesDir = Join-Path $LayoutDir "notices"
New-Item -ItemType Directory -Force -Path $noticesDir | Out-Null
Copy-Item -Path (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $noticesDir "LICENSE.txt") -Force
Copy-Item -Path (Join-Path $repoRoot "NOTICE") -Destination (Join-Path $noticesDir "NOTICE.txt") -Force
Copy-Item -Path (Join-Path $repoRoot "THIRD-PARTY-NOTICES.txt") -Destination (Join-Path $noticesDir "THIRD-PARTY-NOTICES.txt") -Force

$manifestPath = Join-Path $LayoutDir "AppxManifest.xml"
$manifest = Get-Content -Raw -Path (Join-Path $scriptRoot "AppxManifest.template.xml")
foreach ($entry in $tokens.GetEnumerator()) {
    $manifest = $manifest.Replace("{{" + $entry.Key + "}}", $entry.Value)
}
$leftover = [regex]::Matches($manifest, "\{\{[A-Z_]+\}\}") | ForEach-Object { $_.Value } | Sort-Object -Unique
if ($leftover) { Fail "Unresolved manifest tokens: $($leftover -join ', ')" }
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

# ---------- 5. makeappx ----------
$kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
if (-not (Test-Path $kitsRoot)) { Fail "Windows SDK not found at $kitsRoot (MSIX packaging needs MakeAppx)." }
$makeappx = Get-ChildItem -Path $kitsRoot -Filter makeappx.exe -Recurse |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx) { Fail "makeappx.exe not found under $kitsRoot" }
$sdkVersion = [version]($makeappx -replace '^.*\\bin\\([^\\]+)\\.*$', '$1')
Write-Output " Using $makeappx (Windows SDK $sdkVersion)"

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
$msixName = "PUB_Template_Parameter_Editor_${Version}_win-x64.msix"
$msixPath = Join-Path (Resolve-Path $OutDir) $msixName
if (Test-Path $msixPath) { Remove-Item -Force $msixPath }

& $makeappx pack /d $LayoutDir /p $msixPath /o | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "makeappx pack failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $msixPath)) { Fail "makeappx reported success but produced no package" }

# ---------- 6. inspect the produced package ----------
$inspectDir = Join-Path $env:TEMP ("msix-inspect-" + [Guid]::NewGuid().ToString("N"))
& $makeappx unpack /p $msixPath /d $inspectDir /o | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "makeappx unpack verification failed" }
foreach ($required in @("AppxManifest.xml", "PUB_Template_Parameter_Editor.exe", "assets\Square44x44Logo.png", "notices\LICENSE.txt", "notices\THIRD-PARTY-NOTICES.txt")) {
    if (-not (Test-Path (Join-Path $inspectDir $required))) { Fail "Package is missing '$required'" }
}
if (@(Get-ChildItem -Path $inspectDir -Filter *.pdb -Recurse).Count -gt 0) { Fail "Package contains loose .pdb files" }
$unpackedManifest = Get-Content -Raw -Path (Join-Path $inspectDir "AppxManifest.xml")
[xml]$manifestXml = $unpackedManifest
$identity = $manifestXml.Package.Identity
if ($identity.Version -ne $packageVersion) { Fail "Packaged identity version $($identity.Version) != $packageVersion" }
if ($identity.ProcessorArchitecture -ne "x64") { Fail "Packaged architecture is not x64" }
Remove-Item -Recurse -Force $inspectDir

# ---------- 7. provenance record ----------
$gitCommit = (& git -C $repoRoot rev-parse HEAD)
$gitDirty  = if ((& git -C $repoRoot status --porcelain)) { "yes" } else { "no" }
$msixHash  = (Get-FileHash -Path $msixPath -Algorithm SHA256).Hash
$exeHash   = (Get-FileHash -Path $appExe -Algorithm SHA256).Hash
$dotnetSdk = (& dotnet --version)

$record = [ordered]@{
    appVersion              = $Version
    packageVersion          = $packageVersion
    mode                    = $Mode
    sourceCommit            = $gitCommit
    sourceDirty             = $gitDirty
    dotnetSdk               = $dotnetSdk
    windowsSdkMakeAppx      = "$sdkVersion"
    makeappx                = $makeappx
    runtimeIdentifier       = "win-x64"
    selfContained           = $true
    publishSingleFile       = $false
    package                 = (Split-Path $msixPath -Leaf)
    packageSha256           = $msixHash
    payloadExeSha256        = $exeHash
    packageSizeBytes        = (Get-Item $msixPath).Length
    identityName            = $identity.Name
    publisher               = $identity.Publisher
    signedBy                = "none (nothing in this project is code-signed; see docs/RELEASE_PACKAGING.md)"
    generatedUtc            = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$recordPath = Join-Path (Split-Path $msixPath -Parent) "build-info.json"
$record | ConvertTo-Json -Depth 4 | Set-Content -Path $recordPath -Encoding UTF8

Write-Output ""
Write-Output "MSIX      : $msixPath"
Write-Output "SHA-256   : $msixHash"
Write-Output "Build info: $recordPath"
if (-not $strict) {
    Write-Output "NOTE: draft mode - identity placeholders are not real and the package is unsigned."
}
