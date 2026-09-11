param([string]$OutputDirectory = 'dist-arkboard-1.10.1')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler was not found.' }
$output = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
& (Join-Path $projectRoot 'tools\build-rotate-cursor.ps1') | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Runtime.Serialization.dll', 'System.Net.Http.dll')
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', "/out:$output\ArkBoard.exe", "/win32manifest:$projectRoot\app.manifest")
$arguments += "/win32icon:$projectRoot\assets\ArkBoard.ico"
$arguments += "/resource:$projectRoot\assets\ArkBoard.ico,ArkBoard.AppIcon"
$arguments += "/resource:$projectRoot\assets\rotate.cur,ArkBoard.RotateCursor"
foreach ($reference in $references) { $arguments += "/reference:$reference" }
foreach ($name in @('PresentationCore.dll', 'PresentationFramework.dll', 'WindowsBase.dll')) { $arguments += "/reference:$frameworkRoot\WPF\$name" }
$compression = Get-ChildItem -LiteralPath "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\System.IO.Compression" -Recurse -Filter 'System.IO.Compression.dll' | Select-Object -First 1
if (-not $compression) { throw 'System.IO.Compression was not found.' }
$arguments += "/reference:$($compression.FullName)"
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'ArkBoard.exe.config') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'Register-ArkBoard.ps1') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $output -Recurse -Force
Write-Output "Built: $output\ArkBoard.exe"

