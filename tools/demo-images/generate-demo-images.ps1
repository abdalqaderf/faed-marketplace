#requires -Version 5.1
<#
.SYNOPSIS
    Regenerates the Development-only demo product photography used by DemoDataSeeder.

.DESCRIPTION
    Faed's demo seed needs a product photo (and, for the conditions that disclose a physical
    imperfection, a defect photo) for every listing it publishes. Rather than download or
    hotlink stock imagery - which carries a licensing question and a broken-link risk - the
    demo set ships a handful of small, original, locally generated PNGs.

    Each file is a flat placeholder: a category-tinted panel, a neutral appliance glyph, the
    product name, the shop name, and a condition chip. Defect frames add a marker and a
    "Disclosed defect" caption so the buyer-facing disclosure flow has something to show.

    Nothing here is photorealistic and nothing needs to be - the point of the demo seed is the
    journey, not the studio shot. Run this only when the product line in DemoDataSeeder
    changes; the output is committed so a normal checkout never has to run it.

.EXAMPLE
    powershell -File tools/demo-images/generate-demo-images.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $OutputDirectory) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $OutputDirectory = Join-Path $scriptRoot '..\..\src\Faed.Web\Data\Seed\Assets\Images'
}

Add-Type -AssemblyName System.Drawing

$size = 920

# Category tints - deliberately muted so the product name stays the loudest thing on the card.
$palette = @{
    'small-kitchen-appliances' = [System.Drawing.Color]::FromArgb(255, 26, 76, 73)
    'home-cleaning'            = [System.Drawing.Color]::FromArgb(255, 37, 58, 94)
    'power-tools'              = [System.Drawing.Color]::FromArgb(255, 122, 46, 30)
}

# One row per demo image. 'kind' is product or defect; 'category' picks the tint.
$images = @(
    @{ file = 'kettle.png';                 title = 'Rapid-Boil Electric Kettle';   shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Sealed';                 kind = 'product' }
    @{ file = 'hand-mixer.png';             title = '5-Speed Hand Mixer';           shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Sealed';                 kind = 'product' }
    @{ file = 'toaster.png';                title = '2-Slice Toaster';              shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Box opened or damaged';  kind = 'product' }
    @{ file = 'toaster-defect.png';         title = '2-Slice Toaster';              shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Box opened or damaged';  kind = 'defect' }
    @{ file = 'blender.png';                title = 'Countertop Blender';           shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Box opened or damaged';  kind = 'product' }
    @{ file = 'blender-defect.png';         title = 'Countertop Blender';           shop = 'Amman Kitchen Co.';   category = 'small-kitchen-appliances'; grade = 'Box opened or damaged';  kind = 'defect' }
    @{ file = 'steam-iron.png';             title = 'Steam Iron';                   shop = 'Amman Kitchen Co.';   category = 'home-cleaning';            grade = 'Customer return';        kind = 'product' }
    @{ file = 'upright-vacuum.png';         title = 'Bagless Upright Vacuum';       shop = 'Amman Kitchen Co.';   category = 'home-cleaning';            grade = 'Ex-display';             kind = 'product' }
    @{ file = 'upright-vacuum-defect.png';  title = 'Bagless Upright Vacuum';       shop = 'Amman Kitchen Co.';   category = 'home-cleaning';            grade = 'Ex-display';             kind = 'defect' }
    @{ file = 'cordless-drill.png';         title = 'Cordless Drill Driver';        shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Box opened or damaged';  kind = 'product' }
    @{ file = 'cordless-drill-defect.png';  title = 'Cordless Drill Driver';        shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Box opened or damaged';  kind = 'defect' }
    @{ file = 'handheld-vacuum.png';        title = 'Handheld Vacuum';              shop = 'Petra Power Tools';   category = 'home-cleaning';            grade = 'Customer return';        kind = 'product' }
    @{ file = 'circular-saw.png';           title = 'Circular Saw';                 shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Sealed';                 kind = 'product' }
    @{ file = 'angle-grinder.png';          title = 'Angle Grinder';                shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Ex-display';             kind = 'product' }
    @{ file = 'angle-grinder-defect.png';   title = 'Angle Grinder';                shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Ex-display';             kind = 'defect' }
    @{ file = 'screwdriver-set.png';        title = 'Precision Screwdriver Set';    shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Sealed';                 kind = 'product' }
    @{ file = 'tool-bag.png';               title = 'Canvas Tool Bag';              shop = 'Petra Power Tools';   category = 'power-tools';              grade = 'Customer return';        kind = 'product' }
)

function New-DemoImage {
    param([hashtable]$Spec, [string]$Path)

    $tint = $palette[$Spec.category]
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.Clear($tint)

        # Soft inner disc so the card has depth without a gradient.
        $disc = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 255, 255, 255))
        $g.FillEllipse($disc, 90, 40, $size - 180, $size - 260)
        $disc.Dispose()

        # Neutral appliance glyph: a body with a control dial. Not a real product - a stand-in.
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $bodyRect = New-Object System.Drawing.Rectangle 300, 250, 320, 300
        $g.FillRectangle($white, $bodyRect)
        $dial = New-Object System.Drawing.SolidBrush $tint
        $g.FillEllipse($dial, 410, 470, 100, 100)
        $dial.Dispose()

        if ($Spec.kind -eq 'defect') {
            # A ring and a scuff stroke over the glyph, plus a caption.
            $markPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 214, 69, 65)), 12
            $g.DrawEllipse($markPen, 360, 300, 150, 150)
            $g.DrawLine($markPen, 370, 430, 500, 320)
            $markPen.Dispose()
        }
        $white.Dispose()

        # Grade chip, top-right.
        $chipFont = New-Object System.Drawing.Font 'Segoe UI', 22, ([System.Drawing.FontStyle]::Bold)
        $chipText = $Spec.grade
        $chipSize = $g.MeasureString($chipText, $chipFont)
        $chipRect = New-Object System.Drawing.RectangleF ($size - $chipSize.Width - 76), 56, ($chipSize.Width + 36), ($chipSize.Height + 20)
        $chipBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 22, 28, 38))
        $g.FillRectangle($chipBrush, $chipRect)
        $chipBrush.Dispose()
        $chipInk = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.DrawString($chipText, $chipFont, $chipInk, ($chipRect.X + 18), ($chipRect.Y + 10))
        $chipInk.Dispose()
        $chipFont.Dispose()

        # Caption band along the bottom.
        $band = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 12, 16, 22))
        $g.FillRectangle($band, 0, ($size - 210), $size, 210)
        $band.Dispose()

        $titleSize = if ($Spec.title.Length -gt 22) { 34 } else { 40 }
        $titleFont = New-Object System.Drawing.Font 'Segoe UI', $titleSize, ([System.Drawing.FontStyle]::Bold)
        $subFont = New-Object System.Drawing.Font 'Segoe UI', 22, ([System.Drawing.FontStyle]::Regular)
        $ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $subInk = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 255, 255, 255))

        $subtitle = "$($Spec.shop)  |  Faed demo placeholder"
        if ($Spec.kind -eq 'defect') { $subtitle = "Disclosed defect  |  $($Spec.shop)  |  Faed demo placeholder" }
        $g.DrawString($Spec.title, $titleFont, $ink, 60, ($size - 180))
        $g.DrawString($subtitle, $subFont, $subInk, 62, ($size - 104))

        $ink.Dispose(); $subInk.Dispose(); $titleFont.Dispose(); $subFont.Dispose()

        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

# Remove any previously generated demo PNG so a renamed product line does not leave orphans.
Get-ChildItem -Path $OutputDirectory -Filter '*.png' -File | Remove-Item -Force

foreach ($spec in $images) {
    $path = Join-Path $OutputDirectory $spec.file
    New-DemoImage -Spec $spec -Path $path
    Write-Host "wrote $($spec.file)"
}

Write-Host "Done - $($images.Count) files in $OutputDirectory"
