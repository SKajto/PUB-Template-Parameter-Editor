<#
.SYNOPSIS
    Draws the application's own artwork: the icon (.ico and PNG) and the MSIX store assets.

.DESCRIPTION
    Everything here is hand-authored in this repository - no third-party icon set and no downloaded
    asset. The mark is a template sheet with an adjustable value line, which is what the application
    actually edits, plus a written-change check on the large tiles. Two variants are drawn: a detailed
    one for large surfaces, and a simplified one for the small tile sizes where thin details merge
    into grey.

    Rendering uses WPF itself (PresentationCore/PresentationFramework) from Windows PowerShell, so each
    file is an accurate raster of the vector artwork at its own size. Run this after changing the
    artwork; the generated files are committed so ordinary builds and CI do not need to run it.

    Files written:
        src/PubTemplateEditor.App/Assets/app.ico      window and executable icon (PNG frames)
        src/PubTemplateEditor.App/Assets/app-256.png  the same mark, single frame
        packaging/assets/*.png                        MSIX tiles, splash image, store logo

.EXAMPLE
        powershell -NoProfile -ExecutionPolicy Bypass -File tools\brand\generate-brand-assets.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = ""
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    # tools/brand/<this file> -> repository root
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase

# The same palette the interface uses, so the mark and the window agree with each other.
$accent    = [System.Windows.Media.Color]::FromArgb(255, 0x31, 0x5F, 0xCA)  # interface accent
$accentInk = [System.Windows.Media.Color]::FromArgb(255, 0x24, 0x44, 0x8C)  # accent on a light surface
$page      = [System.Windows.Media.Color]::FromArgb(255, 0xFF, 0xFF, 0xFF)
$pageLine  = [System.Windows.Media.Color]::FromArgb(255, 0x8B, 0x96, 0xA6)
$pageRule  = [System.Windows.Media.Color]::FromArgb(255, 0xD8, 0xE0, 0xEB)

function New-Brush([System.Windows.Media.Color]$color) {
    $brush = New-Object System.Windows.Media.SolidColorBrush $color
    $brush.Freeze()
    return $brush
}

function Rect-Of([double]$x, [double]$y, [double]$w, [double]$h) {
    return New-Object System.Windows.Rect($x, $y, $w, $h)
}

function Draw-Mark([System.Windows.Media.DrawingContext]$dc, [double]$u, [bool]$detailed) {
    $accentBrush = New-Brush $accent
    $pageBrush = New-Brush $page
    $lineBrush = New-Brush $pageLine
    $ruleBrush = New-Brush $pageRule
    $inkBrush = New-Brush $accentInk

    # Field: a rounded square, on the corner ratio the interface panels use.
    $fieldRadius = [Math]::Max(2, $u * 0.22)
    $dc.DrawRoundedRectangle($accentBrush, $null, (Rect-Of 0 0 $u $u), $fieldRadius, $fieldRadius)

    # The template sheet.
    $pageX = $u * 0.24
    $pageW = $u * 0.52
    $pageY = $u * 0.16
    $pageH = $u * 0.68
    $pageRadius = [Math]::Max(1, $u * $(if ($detailed) { 0.035 } else { 0.06 }))
    $dc.DrawRoundedRectangle($pageBrush, $null, (Rect-Of $pageX $pageY $pageW $pageH), $pageRadius, $pageRadius)

    $barX = $pageX + $pageW * 0.14
    $barRoom = $pageW * 0.72

    if (-not $detailed) {
        # Small tiles: sheet, two muted lines, one dark value line. Nothing thinner than a pixel.
        $barHeight = [Math]::Max(2, [Math]::Round($u * 0.09))
        $dc.DrawRectangle($lineBrush, $null, (Rect-Of $barX ($u * 0.34) ($barRoom * 0.72) $barHeight))
        $dc.DrawRectangle($lineBrush, $null, (Rect-Of $barX ($u * 0.50) ($barRoom * 0.52) $barHeight))
        $dc.DrawRectangle($inkBrush, $null, (Rect-Of $barX ($u * 0.66) ($barRoom * 0.66) $barHeight))
        return
    }

    # Lines of template text.
    $barHeight = [Math]::Max(2, $u * 0.045)
    $barRadius = $barHeight / 2
    foreach ($row in @(@(0.30, 0.72), @(0.39, 0.58), @(0.48, 0.66))) {
        $dc.DrawRoundedRectangle($lineBrush, $null,
            (Rect-Of $barX ($u * $row[0]) ($barRoom * $row[1]) $barHeight), $barRadius, $barRadius)
    }

    # The value line: a track with a knob, i.e. a parameter being adjusted.
    $trackY = $u * 0.60
    $trackHeight = [Math]::Max(3, $u * 0.055)
    $trackRadius = $trackHeight / 2
    $dc.DrawRoundedRectangle($ruleBrush, $null,
        (Rect-Of $barX $trackY $barRoom $trackHeight), $trackRadius, $trackRadius)

    $filled = $barRoom * 0.62
    $dc.DrawRoundedRectangle($inkBrush, $null,
        (Rect-Of $barX $trackY $filled $trackHeight), $trackRadius, $trackRadius)

    $knob = [Math]::Max(5, $u * 0.10)
    $centreX = $barX + $filled
    $centreY = $trackY + ($trackHeight / 2)
    $dc.DrawEllipse($pageBrush, $null, (New-Object System.Windows.Point($centreX, $centreY)), ($knob / 2), ($knob / 2))
    $dc.DrawEllipse($inkBrush, $null, (New-Object System.Windows.Point($centreX, $centreY)), ($knob * 0.30), ($knob * 0.30))

    # Written-change badge, only from 150 px up where it still reads.
    if ($u -ge 150) {
        $badge = $u * 0.30
        $badgeX = $u * 0.56
        $badgeY = $u * 0.60
        $dc.DrawRoundedRectangle($inkBrush, $null,
            (Rect-Of $badgeX $badgeY $badge $badge), ($badge * 0.28), ($badge * 0.28))

        $pen = New-Object System.Windows.Media.Pen -ArgumentList $pageBrush, ($u * 0.040)
        $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
        $pen.Freeze()

        # The check itself, in path mini-language. Numbers are formatted invariantly: the path parser
        # always reads "12.5", never the current locale's "12,5".
        $culture = [System.Globalization.CultureInfo]::InvariantCulture
        function N([double]$v) { return $v.ToString($culture) }
        $points = @(
            @(($badgeX + $badge * 0.28), ($badgeY + $badge * 0.54)),
            @(($badgeX + $badge * 0.43), ($badgeY + $badge * 0.69)),
            @(($badgeX + $badge * 0.72), ($badgeY + $badge * 0.32))
        )
        $path = "M {0},{1} L {2},{3} L {4},{5}" -f (N $points[0][0]), (N $points[0][1]),
                                                 (N $points[1][0]), (N $points[1][1]),
                                                 (N $points[2][0]), (N $points[2][1])
        $geometry = [System.Windows.Media.Geometry]::Parse($path)
    }
}

function Render-Png([string]$path, [int]$width, [int]$height) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    Draw-Mark $dc ([Math]::Min($width, $height)) ([Math]::Min($width, $height) -ge 48)
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))

    $directory = Split-Path -Parent $path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Host ("  {0}  {1}x{2}" -f (Split-Path -Leaf $path), $width, $height)
}

function Write-Icon([string]$path, $frames) {
    # ICO container with PNG-encoded frames: header, one directory entry per image, then the images.
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([UInt16]0)                 # reserved
        $writer.Write([UInt16]1)                 # resource type: icon
        $writer.Write([UInt16]$frames.Count)

        $offset = 6 + (16 * $frames.Count)
        foreach ($frame in $frames) {
            $bytes = [System.IO.File]::ReadAllBytes($frame.Path)
            $dim = if ($frame.Size -ge 256) { [Byte]0 } else { [Byte]$frame.Size }
            $writer.Write($dim)                  # width  (0 means 256)
            $writer.Write($dim)                  # height (0 means 256)
            $writer.Write([Byte]0)               # palette entries
            $writer.Write([Byte]0)               # reserved
            $writer.Write([UInt16]1)             # colour planes
            $writer.Write([UInt16]32)            # bits per pixel
            $writer.Write([UInt32]$bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $bytes.Length
        }

        foreach ($frame in $frames) {
            $writer.Write([System.IO.File]::ReadAllBytes($frame.Path))
        }
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
    Write-Host ("  {0}  ({1} frames)" -f (Split-Path -Leaf $path), $frames.Count)
}

$appAssets = Join-Path $RepoRoot "src/PubTemplateEditor.App/Assets"
$msixAssets = Join-Path $RepoRoot "packaging/assets"
$temp = Join-Path $env:TEMP ("pubte-brand-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp -Force | Out-Null

try {
    Write-Host "Application icon"
    $frames = @()
    foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
        $path = Join-Path $temp ("icon-{0}.png" -f $size)
        Render-Png $path $size $size
        $frames += [pscustomobject]@{ Size = $size; Path = $path }
    }
    Render-Png (Join-Path $appAssets "app-256.png") 256 256
    Write-Icon (Join-Path $appAssets "app.ico") $frames

    Write-Host "MSIX and store assets"
    Render-Png (Join-Path $msixAssets "Square44x44Logo.png") 44 44
    Render-Png (Join-Path $msixAssets "Square71x71Logo.png") 71 71
    Render-Png (Join-Path $msixAssets "Square150x150Logo.png") 150 150
    Render-Png (Join-Path $msixAssets "Square310x310Logo.png") 310 310
    Render-Png (Join-Path $msixAssets "Wide310x150Logo.png") 310 150
    Render-Png (Join-Path $msixAssets "StoreLogo.png") 50 50
    Render-Png (Join-Path $msixAssets "SplashScreen.png") 620 300
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "done"
