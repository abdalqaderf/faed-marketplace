<#
.SYNOPSIS
    Regenerates the local flat-illustration product images used by the Development-only
    DemoDataSeeder (src/Faed.Web/Data/Seed/DemoDataSeeder.cs).

.DESCRIPTION
    Faed's real upload validator requires a genuine, structurally valid PNG (magic bytes and
    PNG chunk walk — see ListingImageValidator/VerificationDocumentValidator). This script
    draws simple, original flat-design product cards with System.Drawing (GDI+): a solid
    background, a plain silhouette icon for the item type, and a title/category footer.
    Nothing is downloaded or hotlinked — every pixel is generated locally, so there is no
    licensing concern and no dependency on network access at seed time.

    Output is written to src/Faed.Web/Data/Seed/Assets/Images/, which DemoDataSeeder reads
    from disk at seed time (see DemoAssets.LoadImage). Re-run this script any time the demo
    catalog changes to keep the two in sync.

.NOTES
    Windows-only (System.Drawing/GDI+). Not part of the shipped application — a dev-time
    content generator only. Uses [Type]::new(...) throughout rather than
    "New-Object Type(args)" — the latter is parsed unreliably by PowerShell when the
    arguments are arithmetic expressions (constructor overload resolution can silently
    receive a boxed array instead of scalars).
#>

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$OutDir = Join-Path $PSScriptRoot '..\..\src\Faed.Web\Data\Seed\Assets\Images'
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function ColorFromHex([string]$hex) {
    $hex = $hex.TrimStart('#')
    $r = [Convert]::ToInt32($hex.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($hex.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($hex.Substring(4, 2), 16)
    return [System.Drawing.Color]::FromArgb($r, $g, $b)
}

function DrawIcon([System.Drawing.Graphics]$g, [string]$type, [System.Drawing.RectangleF]$bounds, [System.Drawing.Color]$fg) {
    $brush = [System.Drawing.SolidBrush]::new($fg)
    $penWidth = [Math]::Max(2.0, [double]($bounds.Width * 0.02))
    $pen = [System.Drawing.Pen]::new($fg, $penWidth)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $x = [double]$bounds.X; $y = [double]$bounds.Y; $w = [double]$bounds.Width; $h = [double]$bounds.Height

    function Pt([double]$px, [double]$py) {
        [System.Drawing.PointF]::new([single]($x + $w * $px), [single]($y + $h * $py))
    }

    switch ($type) {
        'tshirt' {
            $pts = @(
                (Pt 0.32 0.06), (Pt 0.5 0.14), (Pt 0.68 0.06), (Pt 0.90 0.24), (Pt 0.78 0.40),
                (Pt 0.68 0.32), (Pt 0.68 0.94), (Pt 0.32 0.94), (Pt 0.32 0.32), (Pt 0.22 0.40), (Pt 0.10 0.24)
            )
            $g.FillPolygon($brush, $pts)
            $g.DrawArc($pen, [single]($x + $w*0.32), [single]($y + $h*0.02), [single]($w*0.36), [single]($h*0.18), 0, 180)
        }
        'jacket' {
            $pts = @(
                (Pt 0.30 0.06), (Pt 0.50 0.16), (Pt 0.70 0.06), (Pt 0.92 0.24), (Pt 0.80 0.40),
                (Pt 0.70 0.32), (Pt 0.70 0.94), (Pt 0.30 0.94), (Pt 0.30 0.32), (Pt 0.20 0.40), (Pt 0.08 0.24)
            )
            $g.FillPolygon($brush, $pts)
            $g.DrawLine($pen, (Pt 0.5 0.16), (Pt 0.5 0.94))
            for ($i = 0; $i -lt 4; $i++) {
                $cy = 0.32 + $i * 0.14
                $g.FillEllipse($brush, [single]($x + $w*0.48), [single]($y + $h*$cy), [single]($w*0.05), [single]($w*0.05))
            }
        }
        'sweater' {
            $pts = @(
                (Pt 0.28 0.10), (Pt 0.50 0.04), (Pt 0.72 0.10), (Pt 0.94 0.26), (Pt 0.80 0.42),
                (Pt 0.72 0.34), (Pt 0.72 0.94), (Pt 0.28 0.94), (Pt 0.28 0.34), (Pt 0.20 0.42), (Pt 0.06 0.26)
            )
            $g.FillPolygon($brush, $pts)
            $rib = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(70, 0, 0, 0), 3.0)
            for ($i = 0; $i -lt 6; $i++) {
                $lx = 0.30 + $i * 0.08
                $g.DrawLine($rib, (Pt $lx 0.46), (Pt $lx 0.90))
            }
            $rib.Dispose()
        }
        'bag' {
            $g.FillRectangle($brush, [single]($x + $w*0.16), [single]($y + $h*0.34), [single]($w*0.68), [single]($h*0.56))
            $g.DrawArc($pen, [single]($x + $w*0.30), [single]($y + $h*0.08), [single]($w*0.40), [single]($h*0.42), 180, 180)
        }
        'backpack' {
            $rr = [System.Drawing.Drawing2D.GraphicsPath]::new()
            $rr.AddArc([single]($x + $w*0.18), [single]($y + $h*0.16), [single]($w*0.20), [single]($h*0.20), 180, 90)
            $rr.AddArc([single]($x + $w*0.62), [single]($y + $h*0.16), [single]($w*0.20), [single]($h*0.20), 270, 90)
            $rr.AddArc([single]($x + $w*0.62), [single]($y + $h*0.68), [single]($w*0.20), [single]($h*0.20), 0, 90)
            $rr.AddArc([single]($x + $w*0.18), [single]($y + $h*0.68), [single]($w*0.20), [single]($h*0.20), 90, 90)
            $rr.CloseFigure()
            $g.FillPath($brush, $rr)
            $pocket = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
            $g.FillRectangle($pocket, [single]($x + $w*0.34), [single]($y + $h*0.50), [single]($w*0.32), [single]($h*0.28))
            $g.DrawArc($pen, [single]($x + $w*0.30), [single]($y + $h*0.02), [single]($w*0.40), [single]($h*0.20), 180, 180)
            $pocket.Dispose(); $rr.Dispose()
        }
        'scarf' {
            $g.FillEllipse($brush, [single]($x + $w*0.10), [single]($y + $h*0.12), [single]($w*0.80), [single]($h*0.44))
            $inner = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
            $g.FillEllipse($inner, [single]($x + $w*0.30), [single]($y + $h*0.24), [single]($w*0.40), [single]($h*0.20))
            $g.FillRectangle($brush, [single]($x + $w*0.40), [single]($y + $h*0.50), [single]($w*0.14), [single]($h*0.44))
            $g.FillRectangle($brush, [single]($x + $w*0.50), [single]($y + $h*0.52), [single]($w*0.14), [single]($h*0.40))
            $inner.Dispose()
        }
        'belt' {
            $g.FillRectangle($brush, [single]($x + $w*0.05), [single]($y + $h*0.42), [single]($w*0.62), [single]($h*0.16))
            $g.DrawEllipse($pen, [single]($x + $w*0.62), [single]($y + $h*0.36), [single]($w*0.28), [single]($h*0.28))
            $g.FillEllipse($brush, [single]($x + $w*0.72), [single]($y + $h*0.46), [single]($w*0.08), [single]($w*0.08))
        }
        'shoe' {
            $pts = @(
                (Pt 0.08 0.62), (Pt 0.14 0.40), (Pt 0.30 0.30), (Pt 0.40 0.36), (Pt 0.52 0.28),
                (Pt 0.66 0.30), (Pt 0.80 0.42), (Pt 0.94 0.50), (Pt 0.94 0.68), (Pt 0.08 0.68)
            )
            $g.FillPolygon($brush, $pts)
            $lace = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(120, 255, 255, 255), 3.0)
            for ($i = 0; $i -lt 3; $i++) {
                $lx = 0.40 + $i * 0.10
                $g.DrawLine($lace, (Pt $lx 0.34), (Pt ($lx + 0.06) 0.46))
            }
            $lace.Dispose()
        }
        'sandal' {
            $g.FillEllipse($brush, [single]($x + $w*0.10), [single]($y + $h*0.46), [single]($w*0.80), [single]($h*0.34))
            $strapWidth = [single]($w*0.05)
            $strap = [System.Drawing.Pen]::new($fg, $strapWidth)
            $g.DrawArc($strap, [single]($x + $w*0.30), [single]($y + $h*0.12), [single]($w*0.40), [single]($h*0.50), 200, 140)
            $g.DrawLine($strap, (Pt 0.50 0.16), (Pt 0.50 0.50))
            $strap.Dispose()
        }
        'sock' {
            $pts = @(
                (Pt 0.36 0.06), (Pt 0.64 0.06), (Pt 0.64 0.52), (Pt 0.86 0.66), (Pt 0.90 0.86),
                (Pt 0.40 0.94), (Pt 0.36 0.70), (Pt 0.36 0.06)
            )
            $g.FillPolygon($brush, $pts)
            $cuff = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
            $g.FillRectangle($cuff, [single]($x + $w*0.36), [single]($y + $h*0.06), [single]($w*0.28), [single]($h*0.10))
            $cuff.Dispose()
        }
        default {
            $g.FillEllipse($brush, [single]($x + $w*0.2), [single]($y + $h*0.2), [single]($w*0.6), [single]($h*0.6))
        }
    }
    $brush.Dispose(); $pen.Dispose()
}

function New-ProductImage {
    param(
        [string]$FileName,
        [string]$Title,
        [string]$Subtitle,
        [string]$BackHex,
        [string]$FgHex = '#FFFFFF',
        [string]$Icon,
        [string]$BadgeText,
        [string]$BadgeHex = '#1F2937',
        [switch]$Flaw,
        [string]$FlawLabel
    )

    $size = 900.0
    $bmp = [System.Drawing.Bitmap]::new(900, 900)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $back = ColorFromHex $BackHex
    $fg = ColorFromHex $FgHex

    $rect = [System.Drawing.Rectangle]::new(0, 0, 900, 900)
    $darker = [System.Drawing.Color]::FromArgb(255,
        [Math]::Max(0, $back.R - 28), [Math]::Max(0, $back.G - 28), [Math]::Max(0, $back.B - 28))
    $gradBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $back, $darker, 45.0)
    $g.FillRectangle($gradBrush, $rect)

    $panel = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(28, 255, 255, 255))
    $g.FillEllipse($panel, [single]($size*0.08), [single]($size*0.06), [single]($size*0.84), [single]($size*0.84))

    $iconBounds = [System.Drawing.RectangleF]::new([single]($size*0.24), [single]($size*0.14), [single]($size*0.52), [single]($size*0.52))
    DrawIcon -g $g -type $Icon -bounds $iconBounds -fg $fg

    if ($Flaw) {
        $flawPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(230, 220, 38, 38), 6.0)
        $fx = $size*0.62; $fy = $size*0.50; $fr = $size*0.13
        $g.DrawEllipse($flawPen, [single]$fx, [single]$fy, [single]$fr, [single]$fr)
        $g.DrawLine($flawPen, [single]($fx + $fr), [single]($fy + $fr), [single]($fx + $fr*1.5), [single]($fy + $fr*1.5))
        $flawBoxBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235, 220, 38, 38))
        $flawRect = [System.Drawing.RectangleF]::new([single]($fx + $fr*1.4), [single]($fy + $fr*1.3), [single]($size*0.30), [single]($size*0.07))
        $g.FillRectangle($flawBoxBrush, $flawRect)
        $flawFont = [System.Drawing.Font]::new('Segoe UI', [single]($size*0.026), [System.Drawing.FontStyle]::Bold)
        $sf1 = [System.Drawing.StringFormat]::new()
        $sf1.Alignment = [System.Drawing.StringAlignment]::Center
        $sf1.LineAlignment = [System.Drawing.StringAlignment]::Center
        $g.DrawString($FlawLabel, $flawFont, [System.Drawing.Brushes]::White, $flawRect, $sf1)
        $flawPen.Dispose(); $flawBoxBrush.Dispose(); $flawFont.Dispose(); $sf1.Dispose()
    }

    if ($BadgeText) {
        $badge = ColorFromHex $BadgeHex
        $badgeBrush = [System.Drawing.SolidBrush]::new($badge)
        $badgeRect = [System.Drawing.RectangleF]::new([single]($size*0.68), [single]($size*0.06), [single]($size*0.26), [single]($size*0.09))
        $g.FillRectangle($badgeBrush, $badgeRect)
        $badgeFont = [System.Drawing.Font]::new('Segoe UI', [single]($size*0.026), [System.Drawing.FontStyle]::Bold)
        $sf2 = [System.Drawing.StringFormat]::new()
        $sf2.Alignment = [System.Drawing.StringAlignment]::Center
        $sf2.LineAlignment = [System.Drawing.StringAlignment]::Center
        $g.DrawString($BadgeText, $badgeFont, [System.Drawing.Brushes]::White, $badgeRect, $sf2)
        $badgeBrush.Dispose(); $badgeFont.Dispose(); $sf2.Dispose()
    }

    $footerRect = [System.Drawing.RectangleF]::new(0.0, [single]($size*0.80), [single]$size, [single]($size*0.20))
    $footerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(150, 17, 24, 39))
    $g.FillRectangle($footerBrush, $footerRect)

    $titleFont = [System.Drawing.Font]::new('Segoe UI', [single]($size*0.038), [System.Drawing.FontStyle]::Bold)
    $subFont = [System.Drawing.Font]::new('Segoe UI', [single]($size*0.024), [System.Drawing.FontStyle]::Regular)
    $sfLeft = [System.Drawing.StringFormat]::new()
    $sfLeft.Alignment = [System.Drawing.StringAlignment]::Near
    $titleRect = [System.Drawing.RectangleF]::new([single]($size*0.05), [single]($size*0.815), [single]($size*0.90), [single]($size*0.09))
    $subRect = [System.Drawing.RectangleF]::new([single]($size*0.05), [single]($size*0.90), [single]($size*0.90), [single]($size*0.08))
    $g.DrawString($Title, $titleFont, [System.Drawing.Brushes]::White, $titleRect, $sfLeft)
    $g.DrawString($Subtitle, $subFont, [System.Drawing.Brushes]::WhiteSmoke, $subRect, $sfLeft)

    $outPath = Join-Path $OutDir $FileName
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $g.Dispose(); $bmp.Dispose(); $gradBrush.Dispose(); $panel.Dispose()
    $titleFont.Dispose(); $subFont.Dispose(); $sfLeft.Dispose(); $footerBrush.Dispose()
    Write-Output "Wrote $outPath"
}

# ---- Amman Threads (clothing / bags & accessories) -----------------------------------

New-ProductImage -FileName 'tee-front.png' -Title 'Cotton Crew Tee' -Subtitle 'Amman Threads - Clothing' `
    -BackHex '#1F6F5C' -Icon 'tshirt' -BadgeText 'Grade A'

New-ProductImage -FileName 'tote-front.png' -Title 'Leather Tote' -Subtitle 'Amman Threads - Bags & Accessories' `
    -BackHex '#7A4B2A' -Icon 'bag' -BadgeText 'Grade D'

New-ProductImage -FileName 'tote-corner-scuff.png' -Title 'Leather Tote - Detail' -Subtitle 'Corner scuff, disclosed' `
    -BackHex '#7A4B2A' -Icon 'bag' -BadgeText 'Defect' -Flaw -FlawLabel 'Scuff'

New-ProductImage -FileName 'denim-jacket-front.png' -Title 'Indigo Denim Jacket' -Subtitle 'Amman Threads - Nova Basics' `
    -BackHex '#2B4C7E' -Icon 'jacket' -BadgeText 'Grade B'

New-ProductImage -FileName 'denim-jacket-detail.png' -Title 'Indigo Denim Jacket - Detail' -Subtitle 'Button placket close-up' `
    -BackHex '#233F68' -Icon 'jacket' -BadgeText 'Grade B'

New-ProductImage -FileName 'denim-jacket-box.png' -Title 'Indigo Denim Jacket - Packaging' -Subtitle 'Retail box opened for a photo shoot' `
    -BackHex '#233F68' -Icon 'jacket' -BadgeText 'Packaging' -Flaw -FlawLabel 'Box'

New-ProductImage -FileName 'wool-scarf.png' -Title 'Wool-Blend Scarf' -Subtitle 'Amman Threads - Bags & Accessories' `
    -BackHex '#4B4238' -Icon 'scarf' -FgHex '#F2EBDD' -BadgeText 'Grade A'

New-ProductImage -FileName 'leather-belt.png' -Title 'Leather Belt' -Subtitle 'Amman Threads - Bags & Accessories' `
    -BackHex '#5B3A29' -Icon 'belt' -BadgeText 'Grade C'

New-ProductImage -FileName 'canvas-backpack.png' -Title 'Canvas Backpack' -Subtitle 'Amman Threads - Bags & Accessories' `
    -BackHex '#3E5641' -Icon 'backpack' -BadgeText 'Grade B'

New-ProductImage -FileName 'canvas-backpack-box.png' -Title 'Canvas Backpack - Packaging' -Subtitle 'Crushed retail box' `
    -BackHex '#3E5641' -Icon 'backpack' -BadgeText 'Packaging' -Flaw -FlawLabel 'Box'

# ---- Petra Footwear (shoes / bags & accessories) --------------------------------------

New-ProductImage -FileName 'court-low-pair.png' -Title 'Court Low Sneakers' -Subtitle 'Petra Footwear - Shoes' `
    -BackHex '#8A3324' -Icon 'shoe' -BadgeText 'Grade B'

New-ProductImage -FileName 'court-low-box.png' -Title 'Court Low Sneakers - Packaging' -Subtitle 'Crushed shoe box' `
    -BackHex '#8A3324' -Icon 'shoe' -BadgeText 'Packaging' -Flaw -FlawLabel 'Box'

New-ProductImage -FileName 'merino-half-zip.png' -Title 'Merino Half-Zip' -Subtitle 'Petra Footwear - Clothing' `
    -BackHex '#5C5142' -Icon 'sweater' -BadgeText 'Grade C'

New-ProductImage -FileName 'running-shoes-pair.png' -Title 'TrailHead Runner' -Subtitle 'Petra Footwear - TrailHead' `
    -BackHex '#C25B1E' -Icon 'shoe' -BadgeText 'Grade A'

New-ProductImage -FileName 'leather-sandals-front.png' -Title 'Leather Sandals' -Subtitle 'Petra Footwear - Shoes' `
    -BackHex '#9B7653' -Icon 'sandal' -BadgeText 'Grade D'

New-ProductImage -FileName 'leather-sandals-scuff.png' -Title 'Leather Sandals - Detail' -Subtitle 'Display-stand mark' `
    -BackHex '#9B7653' -Icon 'sandal' -BadgeText 'Defect' -Flaw -FlawLabel 'Mark'

New-ProductImage -FileName 'sports-socks.png' -Title 'Sports Socks 3-Pack' -Subtitle 'Petra Footwear - Bags & Accessories' `
    -BackHex '#2E4756' -Icon 'sock' -BadgeText 'Grade A'

New-ProductImage -FileName 'shoe-bag-set.png' -Title 'Travel Shoe Bag Set' -Subtitle 'Petra Footwear - Bags & Accessories' `
    -BackHex '#44546B' -Icon 'backpack' -BadgeText 'Grade C'

New-ProductImage -FileName 'shoe-bag-set-logo.png' -Title 'Travel Shoe Bag Set - Detail' -Subtitle 'Off-centre printed logo' `
    -BackHex '#44546B' -Icon 'backpack' -BadgeText 'Defect' -Flaw -FlawLabel 'Logo'

Write-Output "Done: generated images in $OutDir"
