<#
PROVISIONAL packaging artwork generator.

Creates placeholder PNG tiles so the MSIX layout is complete and certifiable in shape.
These are NOT final branding: they are solid-tile placeholders with the letters "PUB".
Replace them with designed assets before any packaged channel is used; the designed artwork now lives in tools/brand/generate-brand-assets.ps1.

Uses only Windows built-in PowerShell 5.1 + WPF imaging; nothing is downloaded.
#>
[CmdletBinding()]
param(
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $OutDir) { $OutDir = Join-Path $scriptRoot "assets" }

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

# name => width,height in device-independent pixels (100% target scale)
$tiles = [ordered]@{
    "StoreLogo.png"          = @(48, 48)
    "Square44x44Logo.png"    = @(44, 44)
    "Square71x71Logo.png"    = @(71, 71)
    "Square150x150Logo.png"  = @(150, 150)
    "Square310x310Logo.png"  = @(310, 310)
    "Wide310x150Logo.png"    = @(310, 150)
    "SplashScreen.png"       = @(620, 300)
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

foreach ($name in $tiles.Keys) {
    $size = $tiles[$name]
    $width = [int]$size[0]
    $height = [int]$size[1]

    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()

    $backdrop = New-Object System.Windows.Media.SolidColorBrush(
        [System.Windows.Media.Color]::FromArgb(255, 15, 108, 189))
    $dc.DrawRectangle($backdrop, $null, (New-Object System.Windows.Rect(0, 0, $width, $height)))

    $fontSize = [Math]::Max(9.0, [Math]::Min($width, $height) * 0.28)
    $typeface = New-Object System.Windows.Media.Typeface(
        "Segoe UI", [System.Windows.FontStyles]::Normal,
        [System.Windows.FontWeights]::SemiBold, [System.Windows.FontStretches]::Normal)

    $text = New-Object System.Windows.Media.FormattedText(
        "PUB", [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Windows.FlowDirection]::LeftToRight, $typeface, $fontSize,
        [System.Windows.Media.Brushes]::White, 1.0)
    $originX = ($width - $text.WidthIncludingTrailingWhitespace) / 2.0
    $originY = ($height - $text.Height) / 2.0
    $origin = New-Object System.Windows.Point($originX, $originY)
    $dc.DrawText($text, $origin)
    $dc.Close()

    $target = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $target.Render($visual)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($target))

    $path = Join-Path $OutDir $name
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }

    Write-Output "generated $path ${width}x${height}"
}
