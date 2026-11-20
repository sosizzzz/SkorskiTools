# Technical decisions

This file records important decisions and deviations from the specification.

## Phase 1 — Foundation

### .NET 10 SDK installation
The environment had only the .NET 8 runtime and no SDK, and the session is not
elevated. `winget install` was cancelled by UAC (exit code 1602), so the official
`dotnet-install.ps1` script was used with `-Channel 10.0 -Version 10.0.404` and
`-InstallDir %LOCALAPPDATA%\Microsoft\dotnet`. The SDK is installed per user,
which is consistent with the rule that Skorski Tools never runs as administrator.
Build commands in this workspace prepend that directory to `PATH`. A user-level
`DOTNET_ROOT` environment variable was also set so the compiled `.exe` resolves
the .NET 10 runtime instead of the system-wide .NET 8 installation.

### UI library
`WPF-UI` 4.3.0 was selected, as specified. Version 4.3.0 ships a native
`net10.0-windows7.0` assembly, so no compatibility fallback was necessary.
The dark Fluent theme is applied through `ThemesDictionary Theme="Dark"` and
`ControlsDictionary`.

### Dependency injection
No DI container was added: it is not part of the approved dependency list.
Services are composed manually in `App.OnStartup` and passed to view models.

### Solution format
`dotnet new sln` defaults to the new `.slnx` format on .NET 10. The classic
`SkorskiTools.sln` was generated instead (`--format sln`) to match the specified
project structure.

### FluentWindow title
WPF-UI's `FluentWindow` resets `Window.Title` while applying its template. The
final title is therefore assigned in `App.OnStartup` after `MainWindow.Show()`.

### Owner-provided resources
The original package in `Utilidades 1.0\` was left untouched. The owner confirmed
the files should be copied, so their contents now also live in `resources/`:

- `Instaladores` → `resources/installers/`
- `Navegadores\Chrome` → `resources/browsers/chrome/`
- `Navegadores\Firefox` → `resources/browsers/firefox/`
- `Portables` → `resources/portables/`
- `Activation` → `resources/auxiliary/activation/`
- `Scripts` → `resources/scripts/`

Original file names are preserved. Binary payloads are ignored by Git to avoid
committing large third-party installers; manifests, schemas and lists stay tracked.
Each resource now has its own subdirectory containing `manifest.json` plus its files,
matching the discovery rule in the specification.

## Phase 2 — Dynamic catalog

### JSON Schema dependency
`JsonSchema.Net` 9.7.0 was added to Core. It is not in the original dependency list,
but the specification explicitly requires validating manifests against the shipped
JSON Schemas. Custom rules (unique ids, path safety, file existence, SHA-256,
categories) are implemented in C# because they cannot be expressed in JSON Schema.

### SHA-256 timing
Hashes are verified during discovery, not only at install time. The catalog is loaded
on a background thread so hashing bundled files does not block the UI.

### Provisional detection values
The `detection.displayName` values in the manifests are based on the published product
names. They will be checked against real registry entries in Phase 3 and corrected if
needed. Detection is not executed in Phase 2.

### Scripts
`resources/scripts/scripts.json` was created from the owner's `scripts.md` content.
Skorski Tools only displays and copies this code; it never runs it.

### Main window rendering fix
The WPF template left a second, empty `MainWindow` in the project root. Because
`App.xaml.cs` lives in `SkorskiTools.App`, unqualified `new MainWindow()` resolved
to that template class instead of `SkorskiTools.App.Views.MainWindow`, so the app
showed a blank 800x450 window. The template files were deleted.

WPF-UI's Mica backdrop also requires `ExtendsContentIntoTitleBar="True"`; otherwise
it throws at runtime. A `ui:TitleBar` was added to the window.

`WindowStartupLocation="CenterScreen"` chose the secondary monitor in this
multi-monitor setup, and a 1200x760 window exceeded the primary work area at 125%
DPI scaling. The window is now explicitly centered on the primary monitor and sized
to 1100x640.

## Phase 3 — Applications

### Target framework
Core and Tests now target `net10.0-windows` instead of `net10.0`. The project only
supports Windows, and the registry APIs produced CA1416 warnings on the portable TFM.

### Detection
Detection reads all three uninstall locations (HKLM, HKLM WOW6432Node and HKCU)
through `IUninstallEntryProvider`, so unit tests use a fake provider instead of the
real registry. `file-exists` and `path` strategies expand `%VARIABLES%`. The detected
version comes from `DisplayVersion` or from the file version.

### Reinstall
Reinstall runs the original installer in repair/upgrade mode. It never uninstalls
the application first, so no destructive step is performed without confirmation.

### Uninstall safety
Uninstall commands come only from the registry entry matched during detection.
They are split into executable plus an argument list and never concatenated.
Program folders are never deleted by Skorski Tools.
