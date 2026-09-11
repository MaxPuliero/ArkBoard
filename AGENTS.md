# ArkBoard project notes

Read this file before changing ArkBoard in a new session.

ArkBoard is a portable x64 WPF application for Windows 10/11, built with the .NET Framework 4.x compiler included with Windows. The public repository is https://github.com/MaxPuliero/ArkBoard and the project uses the MIT license.

## Structure

- `src/MainWindow.cs`: programmatic WPF shell, menus, inspector, commands, import and persistence UI.
- `src/BoardSurface.cs`: canvas rendering, hit testing and pointer gestures.
- `src/BoardCore.cs`: document model, undo/redo, embedded ZIP project format and layout helpers.
- `src/PsdSupport.cs`: intentionally basic 8-bit RGB PSD raster-layer decoder/compositor.
- `src/TextEditing.cs`: canvas text creation and editing overlay.
- `src/Localization.cs`: runtime English, Italian and Japanese translations.
- `src/DarkDialog.cs`: ArkBoard-styled modal windows.
- `src/SelfTests.cs`: integration tests and screenshot generation.
- `build.ps1`: portable release build. `Register-ArkBoard.ps1` registers the `.arkboard` icon and file association for the current user.

## Workflow

Build with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory dist-arkboard-X.Y.Z`. Run the executable with `--self-test test-output-arkboard-X.Y.Z`; every check must pass. Inspect generated screenshots before release. Package the contents of the dist folder at the ZIP root, commit, push `main`, and publish the matching GitHub release with the portable ZIP.

Keep the assembly version, build default directory, README version and release filename aligned. Preserve existing `.arkboard` manifests unless a feature requires a new schema version. Projects are ZIP archives with embedded source assets; PSD visibility is stored per canvas item.

## Product constraints

The UI is dark, square-edged and desaturated. The canvas must stay responsive at large window sizes. Window opacity uses native layered-window alpha only below 100%. Text objects support move and proportional scale; images also support rotate, flip and non-destructive masks. PSD support excludes blend modes, effects, Photoshop masks, adjustments, smart objects, 16/32-bit data, ZIP layer compression and PSB.
