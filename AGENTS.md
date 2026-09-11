# ArkBoard project notes

Read this file before changing ArkBoard in a new session.

ArkBoard is a portable x64 WPF application for Windows 10/11, built with the .NET Framework 4.x compiler included with Windows. The public repository is https://github.com/MaxPuliero/ArkBoard and the project uses the MIT license.

## Structure

- `src/MainWindow.cs`: programmatic WPF shell, menus, inspector, commands, import and persistence UI.
- `src/BoardSurface.cs`: canvas rendering, hit testing and pointer gestures.
- `src/BoardCore.cs`: document model, undo/redo, embedded ZIP project format and layout helpers.
- `src/BeeImporter.cs`: read-only BeeRef `.bee` v1/v2 SQLite migration importer using the Windows `winsqlite3.dll` system library.
- `src/PsdSupport.cs`: intentionally basic 8-bit RGB PSD raster-layer decoder/compositor.
- `src/TextEditing.cs`: canvas text creation and editing overlay.
- `src/Localization.cs`: runtime English, Italian and Japanese translations.
- `src/DarkDialog.cs`: ArkBoard-styled modal windows.
- `src/SelfTests.cs`: integration tests and screenshot generation.
- `build.ps1`: portable release build. `Register-ArkBoard.ps1` registers the `.arkboard` icon and file association for the current user.

## Workflow

Build with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory dist-arkboard-X.Y.Z`. Run the executable with `--self-test test-output-arkboard-X.Y.Z`; every check must pass. Inspect generated screenshots before release. BeeRef coverage uses the embedded `assets/TestBeeRefV1.bee` and `assets/TestBeeRefV2.bee` fixtures and must continue to verify that source databases are unchanged. Package the contents of the dist folder at the ZIP root, commit, push `main`, and publish the matching GitHub release with the portable ZIP.

Keep the assembly version, build default directory, README version and release filename aligned. Preserve existing `.arkboard` manifests unless a feature requires a new schema version. Projects are ZIP archives with embedded source assets; PSD visibility is stored per canvas item.

## Product constraints

The UI is dark, square-edged and desaturated. The canvas must stay responsive at large window sizes. Window opacity uses native layered-window alpha only below 100%. Locked overlay mode forces a native layered click-through main window and shows a separate control window for opacity, Always on Top and unlock; that strip clamps to 50% opacity. Text objects support move and proportional scale; images also support rotate, flip and non-destructive masks. PSD support excludes blend modes, effects, Photoshop masks, adjustments, smart objects, 16/32-bit data, ZIP layer compression and PSB.

BeeRef support is intentionally an import-only migration path. Open `.bee` files with SQLite read-only flags, never migrate or overwrite them, leave the resulting document dirty with no native path so Save uses a new `.arkboard`, and keep malformed-row and size-limit handling defensive. Versions 1 and 2 import embedded images, text, stacking, transforms and crop masks; per-image opacity, grayscale, compressed SQLAR data and unknown item types remain unsupported and must be reported or skipped without affecting valid rows.
