param([string]$ProjectRoot = 'F:\codex\LoglikeCap', [string]$ImageRoot = '', [string]$OutputPath = '', [int]$Columns = 7)
Add-Type -AssemblyName System.Drawing
$iconRoot = if ($ImageRoot) { $ImageRoot } else { Join-Path $ProjectRoot 'Assets\RuinRunner\Resources\Art\Pixel\Items' }
$files = @(Get-ChildItem -LiteralPath $iconRoot -Filter '*.png' -Recurse | Sort-Object FullName)
$columns = [Math]::Max(1, $Columns)
$cell = 128
$sheet = [Drawing.Bitmap]::new($columns * $cell, [int][Math]::Ceiling($files.Count / $columns) * $cell)
$graphics = [Drawing.Graphics]::FromImage($sheet)
$graphics.Clear([Drawing.Color]::FromArgb(57, 79, 64))
$graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$font = [Drawing.Font]::new('Consolas', 8)
try {
    for ($i = 0; $i -lt $files.Count; $i++) {
        $icon = [Drawing.Bitmap]::new($files[$i].FullName)
        try {
            $x = ($i % $columns) * $cell
            $y = [int][Math]::Floor($i / $columns) * $cell
            $graphics.DrawImage($icon, [Drawing.Rectangle]::new($x + 16, $y + 4, 96, 96))
            $label = $files[$i].Directory.Name + '/' + $files[$i].BaseName
            $graphics.DrawString($label, $font, [Drawing.Brushes]::White, [single]($x + 3), [single]($y + 105))
        } finally { $icon.Dispose() }
    }
    $output = if ($OutputPath) { $OutputPath } else { Join-Path $ProjectRoot 'Logs\Validation\individual-items-contact-sheet.png' }
    $sheet.Save($output, [Drawing.Imaging.ImageFormat]::Png)
    Write-Output $output
} finally { $font.Dispose(); $graphics.Dispose(); $sheet.Dispose() }
