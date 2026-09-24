param([string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\rotate.cur'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# The 8 px white dot retains a dark rim so it stays visible over light images.
$size = 32
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$rim = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 28, 28, 28))
$fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$graphics.FillEllipse($rim, [System.Drawing.RectangleF]::new(12, 12, 8, 8))
$graphics.FillEllipse($fill, [System.Drawing.RectangleF]::new(13, 13, 6, 6))
$rim.Dispose(); $fill.Dispose(); $graphics.Dispose()

$stream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $imageBytes = 40 + ($size * $size * 4) + ($size * 4)
    $writer.Write([uint16]0); $writer.Write([uint16]2); $writer.Write([uint16]1)
    $writer.Write([byte]$size); $writer.Write([byte]$size); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]16); $writer.Write([uint16]16); $writer.Write([uint32]$imageBytes); $writer.Write([uint32]22)
    $writer.Write([uint32]40); $writer.Write([int32]$size); $writer.Write([int32]($size * 2))
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
    $writer.Write([uint32]($size * $size * 4)); $writer.Write([int32]0); $writer.Write([int32]0)
    $writer.Write([uint32]0); $writer.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $pixel = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.G); $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.A)
        }
    }
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($group = 0; $group -lt 4; $group++) {
            [byte]$mask = 0
            for ($bit = 0; $bit -lt 8; $bit++) {
                if ($bitmap.GetPixel($group * 8 + $bit, $y).A -eq 0) { $mask = $mask -bor (1 -shl (7 - $bit)) }
            }
            $writer.Write($mask)
        }
    }
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
}
finally { $bitmap.Dispose(); $writer.Dispose(); $stream.Dispose() }
