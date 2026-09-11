param([string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\rotate.cur'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$size = 32
$bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 28, 28, 28)), 5
$stroke = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 210, 210, 210)), 2
$outline.StartCap = $outline.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$stroke.StartCap = $stroke.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$arc = New-Object System.Drawing.RectangleF 5, 5, 22, 22
$graphics.DrawArc($outline, $arc, -58, 280)
$graphics.DrawArc($stroke, $arc, -58, 280)
$arrow = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF 4, 17),
    (New-Object System.Drawing.PointF 4, 25),
    (New-Object System.Drawing.PointF 12, 23)
)
$graphics.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 28, 28, 28))), $arrow)
$innerArrow = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF 6, 19),
    (New-Object System.Drawing.PointF 6, 23),
    (New-Object System.Drawing.PointF 10, 22)
)
$graphics.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 210, 210, 210))), $innerArrow)
$graphics.Dispose(); $outline.Dispose(); $stroke.Dispose()

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
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
            $x = $group * 8 + $bit
            if ($bitmap.GetPixel($x, $y).A -eq 0) { $mask = $mask -bor (1 -shl (7 - $bit)) }
        }
        $writer.Write($mask)
    }
}
$bitmap.Dispose(); $writer.Flush()
$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
[System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
$writer.Dispose(); $stream.Dispose()
