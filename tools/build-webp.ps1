param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$BuildDirectory
)
$ErrorActionPreference = 'Stop'
$destination = Join-Path (Split-Path $PSScriptRoot -Parent) 'third-party\libwebp'
$options = @('-S', $SourceDirectory, '-B', $BuildDirectory, '-G', 'Visual Studio 17 2022', '-A', 'x64',
    '-DBUILD_SHARED_LIBS=ON', '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded')
foreach ($option in @('ANIM_UTILS', 'CWEBP', 'DWEBP', 'GIF2WEBP', 'IMG2WEBP', 'VWEBP', 'WEBPINFO', 'LIBWEBPMUX', 'WEBPMUX', 'EXTRAS')) {
    $options += "-DWEBP_BUILD_$option=OFF"
}
& cmake @options
if ($LASTEXITCODE -ne 0) { throw 'libwebp configuration failed.' }
& cmake --build $BuildDirectory --config Release --target webp webpdemux --parallel 4
if ($LASTEXITCODE -ne 0) { throw 'libwebp compilation failed.' }
foreach ($name in @('libwebp.dll', 'libwebpdemux.dll', 'libsharpyuv.dll')) {
    Copy-Item -LiteralPath (Join-Path $BuildDirectory "Release\$name") -Destination $destination -Force
}
foreach ($name in @('COPYING', 'PATENTS', 'AUTHORS')) {
    Copy-Item -LiteralPath (Join-Path $SourceDirectory $name) -Destination $destination -Force
}
Get-FileHash (Join-Path $destination '*.dll') -Algorithm SHA256
