param([string]$AssetsDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets'))

$ErrorActionPreference = 'Stop'
$sizes = @(32, 64, 128, 256)
$images = @()

foreach ($size in $sizes) {
    $path = Join-Path $AssetsDirectory "arkboard_icon_$size.png"
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 24 -or $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) {
        throw "$path is not a valid PNG file."
    }
    $width = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
    $height = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
    if ($width -ne $size -or $height -ne $size) {
        throw "$path must be ${size}x${size}; found ${width}x${height}."
    }
    $images += ,$bytes
}

$outputPath = Join-Path $AssetsDirectory 'ArkBoard.ico'
$stream = [System.IO.File]::Create($outputPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$images.Count)
    $offset = 6 + (16 * $images.Count)
    for ($index = 0; $index -lt $images.Count; $index++) {
        $size = $sizes[$index]
        $dimension = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$images[$index].Length)
        $writer.Write([UInt32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "Built: $outputPath"
