#requires -Version 5.1
<#
.SYNOPSIS
    Regenerates the Development-only demo photography used by DemoDataSeeder.

.DESCRIPTION
    Faed's demo seed needs a product photo for every listing it publishes, and a defect
    photo for the conditions that disclose a physical imperfection. Phase 13 replaced the
    earlier flat placeholders with real, openly-licensed stock photography, treated two
    different ways (DESIGN-BRIEF.md sec 8):

      * Product shots  - cut out with the remove.bg API and composited onto the
        --faed-card-bg card colour (#E7E5DE), so a catalogue sourced from many
        photographers still reads as one consistent grid.
      * Defect shots   - never cut out. They keep their real background - a shelf, a
        doorstep, a torn carton - and are only square-cropped. Six of them are reused
        across every defect-disclosing listing.

    Every source file lives in tools/demo-images/sources/ and every one is reproduced
    with author and licence in docs/CREDITS.md. tools/demo-images/manifest.json is the
    single list this script and that credits file are both built from.

    The output PNGs are committed, so a normal checkout never runs this script. Run it
    only when the product line in DemoDataSeeder, or a source photo, changes.

    This script renders the master 1000px canvases. The committed files are then optimised
    for the demo (the defence machine may be on slow Wi-Fi): downscaled to 880px for
    products / 760px for defects and reduced to a 256-colour palette PNG with dithering,
    which keeps every file under ~300 KB with no visible loss at display size. After
    regenerating, re-run that optimisation before committing.

.PARAMETER ApiKey
    A remove.bg API key. Falls back to $env:REMOVE_BG_API_KEY. Only the product cut-outs
    need it; -DefectsOnly skips it entirely.

.EXAMPLE
    $env:REMOVE_BG_API_KEY = 'xxxxxxxx'
    powershell -File tools/demo-images/generate-demo-images.ps1
#>
[CmdletBinding()]
param(
    [string]$ApiKey = $env:REMOVE_BG_API_KEY,
    [string]$OutputDirectory,
    [switch]$DefectsOnly,
    [switch]$ProductsOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sourcesDir = Join-Path $scriptRoot 'sources'
$manifest   = Get-Content (Join-Path $scriptRoot 'manifest.json') -Raw | ConvertFrom-Json
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $scriptRoot '..\..\src\Faed.Web\Data\Seed\Assets\Images'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$size        = [int]$manifest.canvas.size
$cardColor   = [System.Drawing.ColorTranslator]::FromHtml($manifest.canvas.background)
$productFrac = [double]$manifest.canvas.productMaxFraction

$curlCmd = Get-Command curl.exe -ErrorAction SilentlyContinue
if (-not $curlCmd) { throw "curl.exe is required for the remove.bg upload and was not found on PATH." }
$curl = $curlCmd.Source

# --- helpers ----------------------------------------------------------------

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Get-OpaqueBounds([System.Drawing.Bitmap]$bmp, [int]$alphaThreshold = 12) {
    # Fast alpha bounding box via LockBits (32bpp ARGB).
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $bytes  = New-Object byte[] ($stride * $bmp.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    } finally {
        $bmp.UnlockBits($data)
    }
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        $row = $y * $stride
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bytes[$row + $x * 4 + 3] -gt $alphaThreshold) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { return $rect }  # fully transparent - shouldn't happen
    New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
}

function New-Canvas {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear($cardColor)
    return @($bmp, $g)
}

function Invoke-RemoveBg([string]$sourcePath, [string]$destPath) {
    if (-not $ApiKey) {
        throw "No remove.bg API key. Set `$env:REMOVE_BG_API_KEY or pass -ApiKey (or run with -DefectsOnly)."
    }
    $err = [System.IO.Path]::GetTempFileName()
    & $curl -s --show-error --fail-with-body `
        -H "X-Api-Key: $ApiKey" `
        -F "image_file=@$sourcePath" `
        -F "size=auto" -F "format=png" -F "type=product" `
        -o $destPath `
        'https://api.remove.bg/v1.0/removebg' 2> $err
    $exit = $LASTEXITCODE
    $header = if (Test-Path $destPath) { [System.IO.File]::ReadAllBytes($destPath)[0..7] } else { @() }
    $isPng  = ($header.Count -ge 8) -and $header[0] -eq 0x89 -and $header[1] -eq 0x50
    if ($exit -ne 0 -or -not $isPng) {
        $body = if (Test-Path $destPath) { Get-Content $destPath -Raw -ErrorAction SilentlyContinue } else { '' }
        Remove-Item $destPath, $err -ErrorAction SilentlyContinue
        throw "remove.bg failed for '$([System.IO.Path]::GetFileName($sourcePath))' (curl $exit): $body"
    }
    Remove-Item $err -ErrorAction SilentlyContinue
}

# --- products: cut out, then centre on the card --------------------------------

function Build-Product($entry) {
    $src = Join-Path $sourcesDir $entry.source
    if (-not (Test-Path $src)) { throw "Missing source '$($entry.source)' for $($entry.file)." }

    # Optional pre-crop (fractions [x, y, w, h] of the source) to keep foreground clutter
    # out of what remove.bg sees.
    $upload = $src
    $cropTmp = $null
    if ($entry.PSObject.Properties['crop']) {
        $c = $entry.crop
        $orig = [System.Drawing.Image]::FromFile($src)
        try {
            $cx = [int]($orig.Width * $c[0]); $cy = [int]($orig.Height * $c[1])
            $cw = [int]($orig.Width * $c[2]); $ch = [int]($orig.Height * $c[3])
            $crop = New-Object System.Drawing.Bitmap $cw, $ch
            $cg = [System.Drawing.Graphics]::FromImage($crop)
            $cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $cg.DrawImage($orig, (New-Object System.Drawing.Rectangle 0, 0, $cw, $ch), $cx, $cy, $cw, $ch, [System.Drawing.GraphicsUnit]::Pixel)
            $cropTmp = [System.IO.Path]::GetTempFileName() + '.png'
            $crop.Save($cropTmp, [System.Drawing.Imaging.ImageFormat]::Png)
            $cg.Dispose(); $crop.Dispose()
            $upload = $cropTmp
        } finally { $orig.Dispose() }
    }

    $cut = [System.IO.Path]::GetTempFileName() + '.png'
    Invoke-RemoveBg $upload $cut
    if ($cropTmp) { Remove-Item $cropTmp -ErrorAction SilentlyContinue }
    try {
        $fg = New-Object System.Drawing.Bitmap $cut
        $box = Get-OpaqueBounds $fg
        $maxEdge = $size * $productFrac
        $scale = [Math]::Min($maxEdge / $box.Width, $maxEdge / $box.Height)
        $dw = [int]($box.Width * $scale); $dh = [int]($box.Height * $scale)
        $dx = [int](($size - $dw) / 2); $dy = [int](($size - $dh) / 2)

        $canvas, $g = New-Canvas
        $dest = New-Object System.Drawing.Rectangle $dx, $dy, $dw, $dh
        $g.DrawImage($fg, $dest, $box.X, $box.Y, $box.Width, $box.Height, [System.Drawing.GraphicsUnit]::Pixel)
        Save-Png $canvas (Join-Path $OutputDirectory $entry.file)
        $g.Dispose(); $canvas.Dispose(); $fg.Dispose()
    } finally {
        Remove-Item $cut -ErrorAction SilentlyContinue
    }
    Write-Host "  product  $($entry.file)"
}

# --- defects: square crop only, real background kept ---------------------------

function Build-Defect($entry) {
    $src = Join-Path $sourcesDir $entry.source
    if (-not (Test-Path $src)) { throw "Missing source '$($entry.source)' for $($entry.file)." }
    $img = [System.Drawing.Image]::FromFile($src)
    try {
        $edge = [Math]::Min($img.Width, $img.Height)
        $anchorX = if ($entry.PSObject.Properties['anchorX']) { [double]$entry.anchorX } else { 0.5 }
        $anchorY = if ($entry.PSObject.Properties['anchorY']) { [double]$entry.anchorY } else { 0.5 }
        $sx = [int](($img.Width - $edge) * $anchorX); $sy = [int](($img.Height - $edge) * $anchorY)
        $canvas = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dst = New-Object System.Drawing.Rectangle 0, 0, $size, $size
        $g.DrawImage($img, $dst, $sx, $sy, $edge, $edge, [System.Drawing.GraphicsUnit]::Pixel)
        Save-Png $canvas (Join-Path $OutputDirectory $entry.file)
        $g.Dispose(); $canvas.Dispose()
    } finally {
        $img.Dispose()
    }
    Write-Host "  defect   $($entry.file)"
}

# --- run ---------------------------------------------------------------------

# Clear previously generated PNGs so a renamed product line leaves no orphans.
Get-ChildItem -Path $OutputDirectory -Filter '*.png' -File | Remove-Item -Force

if (-not $DefectsOnly) {
    Write-Host "Products (remove.bg cut-out onto $($manifest.canvas.background)):"
    foreach ($p in $manifest.products) { Build-Product $p }
}
if (-not $ProductsOnly) {
    Write-Host "Defects (square crop, background kept):"
    foreach ($d in $manifest.defects) { Build-Defect $d }
}

$count = (Get-ChildItem -Path $OutputDirectory -Filter '*.png' -File).Count
Write-Host "Done - $count files in $OutputDirectory"
