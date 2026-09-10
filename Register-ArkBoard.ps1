$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'ArkBoard.exe')).Path
$classes = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes')
try {
    $extension = $classes.CreateSubKey('.arkboard')
    try { $extension.SetValue('', 'ArkBoard.Project', [Microsoft.Win32.RegistryValueKind]::String) }
    finally { $extension.Dispose() }
    $openWith = $classes.CreateSubKey('.arkboard\OpenWithProgids')
    try { $openWith.SetValue('ArkBoard.Project', [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None) }
    finally { $openWith.Dispose() }
    $project = $classes.CreateSubKey('ArkBoard.Project')
    try {
        $project.SetValue('', 'ArkBoard Project', [Microsoft.Win32.RegistryValueKind]::String)
        $icon = $project.CreateSubKey('DefaultIcon')
        try { $icon.SetValue('', ('"' + $exePath + '",0'), [Microsoft.Win32.RegistryValueKind]::String) }
        finally { $icon.Dispose() }
        $command = $project.CreateSubKey('shell\open\command')
        try { $command.SetValue('', ('"' + $exePath + '" "%1"'), [Microsoft.Win32.RegistryValueKind]::String) }
        finally { $command.Dispose() }
    }
    finally { $project.Dispose() }
}
finally { $classes.Dispose() }

if (-not ('ArkBoard.AssociationRefresh' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace ArkBoard {
    public static class AssociationRefresh {
        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
'@
}
[ArkBoard.AssociationRefresh]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output ('Registered .arkboard for the current Windows user: ' + $exePath)
