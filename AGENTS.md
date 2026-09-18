# ArkBoard project notes

Read this file before changing ArkBoard in a new session.

ArkBoard is a portable x64 WPF application for Windows 10/11, built with the .NET Framework 4.x compiler included with Windows. The public repository is https://github.com/MaxPuliero/ArkBoard and the project uses the MIT license.

## Structure

- `src/MainWindow.cs`: programmatic WPF shell, menus, inspector, commands, import and persistence UI.
- `src/BoardSurface.cs`: canvas rendering, hit testing and pointer gestures.
- `src/BoardCore.cs`: document model, undo/redo, embedded ZIP project format and layout helpers.
- `src/BeeImporter.cs`: read-only BeeRef `.bee` v1/v2 SQLite migration importer using the Windows `winsqlite3.dll` system library.
- `src/PureRefImporter.cs`: experimental read-only PureRef `.pur` legacy 1.10/1.11 migration importer for embedded PNG assets and basic item transforms.
- `src/PsdSupport.cs`: intentionally basic 8-bit RGB PSD raster-layer decoder/compositor.
- `src/TextEditing.cs`: canvas text creation and editing overlay.
- `src/Localization.cs`: runtime English, Italian and Japanese translations.
- `src/DarkDialog.cs`: ArkBoard-styled modal windows.
- `src/UpdateChecker.cs`: GitHub release discovery, trusted asset selection and streamed update downloads.
- `src/SelfTests.cs`: integration tests and screenshot generation.
- `build.ps1`: portable release build. `Register-ArkBoard.ps1` registers the `.arkboard` icon and file association for the current user.

## Workflow

Build with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory dist-arkboard-X.Y.Z`. Run the executable with `--self-test test-output-arkboard-X.Y.Z`; every check must pass. Inspect generated screenshots before release. BeeRef coverage uses the embedded `assets/TestBeeRefV1.bee` and `assets/TestBeeRefV2.bee` fixtures and must continue to verify that source databases are unchanged. Package the contents of the dist folder at the ZIP root, commit, push `main`, and publish the matching GitHub release with the portable ZIP.

Normal saves use `BoardDocument.SaveAsync`: capture an immutable manifest/asset snapshot on the UI thread, write it atomically on a worker task, and report byte progress to the shared bottom activity indicator. Project loading also runs on a worker task and reports through the same indicator. The indicator is an independent root overlay so it remains visible in minimal UI mode. Already-compressed image formats use ZIP `NoCompression`; other assets use `Fastest`. If the document revision changes while writing, completion must leave `Dirty` set.

Keep **Open** and **Open Recent** as separate commands. Open Recent appears in both the File menu and the canvas context menu. Keep the most recent ten valid paths, most recent first, in `%APPDATA%\ArkBoard\recent-projects.txt`; tests must not read or write this user file.

The canvas context menu mirrors all five top-level menu-bar sections and their commands. Keep its checkable View and Settings entries synchronized with the main menu so minimal UI mode never hides functionality.

The startup update check queries GitHub's latest-release API asynchronously with a short timeout and fails silently when offline. It must never delay the main window. The synchronized **Auto-Check for Updates** setting defaults on and persists as `checkForUpdates` in `%APPDATA%\ArkBoard\settings.ini`; its adjacent **Check** button performs a manual check, reports when the app is current, and downloads the trusted release asset through a Windows Save dialog. Self-tests must not access the real preference file.

Keep the assembly version, build default directory, README version and release filename aligned. Preserve existing `.arkboard` manifests unless a feature requires a new schema version. Projects are ZIP archives with embedded source assets; PSD visibility is stored per canvas item.

## Product constraints

The UI is dark, square-edged and desaturated. The canvas must stay responsive at large window sizes. Window opacity uses native layered-window alpha only below 100%. Locked overlay mode forces a native layered click-through main window and shows a separate control window for opacity, Always on Top and unlock; that strip clamps to 50% opacity. Text objects support move and proportional scale; images also support rotate, flip and non-destructive masks. Multiple selected images use one axis-aligned group box: corner scaling preserves proportions and relative spacing, while rotation changes every image angle and position around the common center in one undoable gesture. PSD support excludes blend modes, effects, Photoshop masks, adjustments, smart objects, 16/32-bit data, ZIP layer compression and PSB.

Rotation anchors use inset corner positions while the selected on-screen bounds are at least 90 pixels on both axes. Below that threshold they move 28 screen pixels outside the centers of the four sides and render with a 4-pixel radius instead of 8, while the 14-pixel hit radius remains unchanged. Apply the same adaptive rule to group controls. Show the movement cursor only for selected objects.

Align Width and Align Height operate on selected images only, use the selected images' arithmetic mean for the target side, preserve each aspect ratio and are undoable. Clipboard paste resolves the current mouse position through `Board.ToWorld`; use the viewport center only when the pointer is outside the canvas.

Quick Controls starts collapsed. Tab toggles a minimal canvas mode that hides the menu, status controls, text button and Quick Controls while leaving the selection inspector available; the canvas context menu must retain a localized Show UI escape hatch. PSD layer controls use the square dark UI style. Alt-clicking a layer isolates it or restores the other layers, while Page Up/Page Down first isolate the topmost visible layer and then cycle exclusively through the layer stack.

Optional edge snapping is implemented in `BoardSurface` and starts disabled. Move and proportional-scale snapping use the transformed visible bounds, so masks define the active edge. The eight-pixel screen threshold is converted to world units using the current zoom, and active matches render as constant-width white screen-space guides. `BoardSurface.ImagePadding` defaults to 4 canvas pixels and is shared by adjacent-edge snapping and `ImageLayout.Pack`; Settings exposes it through an inline localized numeric field with triangle step controls. Holding Ctrl during move or scale temporarily inverts the persistent Snapping setting.

BeeRef support is intentionally an import-only migration path. Open `.bee` files with SQLite read-only flags, never migrate or overwrite them, leave the resulting document dirty with no native path so Save uses a new `.arkboard`, and keep malformed-row and size-limit handling defensive. Versions 1 and 2 import embedded images, text, stacking, transforms and crop masks; per-image opacity, grayscale, compressed SQLAR data and unknown item types remain unsupported and must be reported or skipped without affecting valid rows.

PureRef support is also import-only and experimental. Only legacy `.pur` 1.10/1.11 files are recognized; the format was reverse engineered from the MIT-licensed FyorDev/PureRef-format project. Import embedded PNG images plus basic transforms and simple text, but reject PureRef 2.x. External/duplicate images, image-attached text and non-rectangular crops are intentionally skipped. Text and transform fidelity must be treated as best effort until fixtures from real PureRef projects cover it.
