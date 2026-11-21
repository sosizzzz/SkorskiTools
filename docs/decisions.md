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

## Phase 4 — Browsers

Browsers are a separate catalog directory and navigation item, but they use the
same `ApplicationManifest` model and `ApplicationListViewModel` as Applications.
No detection, install, uninstall or reinstall logic was duplicated.

Firefox and Chrome are the initial entries. Any other browser can be added by
dropping a manifest into `resources/browsers/<id>/` with its installer.

"Skorski's Config" is explicitly out of scope for 1.0: there is no button,
placeholder or partial implementation for it.

## Phase 5 — Portables

Portables are deployed into `%USERPROFILE%\Skorski-Tools\portables\<id>\`. The
package is validated (path and SHA-256), extracted or copied into a temporary
directory, and only then moved into place. The desktop shortcut is built after the
move and points to the final executable, never to the temporary one.

Uninstall deletes only the portable's own folder and its shortcut. The confirmation
states that any data inside the portable's folder will be lost. Reinstall replaces
the folder with the bundled version; nothing is downloaded.

Shortcut creation goes through `IShortcutService`, so tests use a fake and no real
desktop shortcut is created during the test run.

## Phase 6 — Auxiliary and Scripts

Auxiliary files are copied into `%USERPROFILE%\Skorski-Tools\auxiliary\<id>\` after
SHA-256 verification. A per-resource subfolder avoids name collisions between
resources. There is no run button and no execution path for these files.

Scripts come only from `resources/scripts/scripts.json`. The editor is AvalonEdit in
read-only mode. AvalonEdit 6.3.1 does not expose `Text` as a dependency property, so
the code and the language are applied through attached properties
(`SyntaxHighlightingBehavior`). The only action is copying the full code to the
clipboard; scripts are never executed.

`AvalonEdit` was added to the dependency list because the specification requires
syntax highlighting in the Scripts view.

## Phase 7 — Settings

Settings is informational in 1.0: version, resources path, user files path, logs path
and the current installation mode (Manual). Visual preferences are not shown because
none are implemented yet.

The *Full Auto* option is visible but permanently disabled and labelled "Coming soon".
It has no command, no handler and no partial implementation behind it.

## Phase 8 — Distribution

`dotnet publish -r win-x64 --self-contained true` produces the executable, the .NET
runtime and the whole `resources/` tree next to it. `AppPaths.FindResourcesDirectory`
walks up from `AppContext.BaseDirectory`, so resources are found both in per-user and
all-users installations and the working directory is never used.

The Inno Setup script offers a scope choice. Selecting "All users" restarts the
installer with `runas` and the `/ALLUSERS` parameter, which fixes the target directory
as `C:\Program Files\Skorski Tools`; "Current user" installs to
`%LOCALAPPDATA%\Programs\Skorski Tools` with lowest privileges. The app itself never
requests elevation. No code signing is used in 1.0, so a SmartScreen warning is
expected. Updates are manual.

`Inno Setup 6.7.3` was installed per user to compile and verify the installer.

### Application icon
`Assets/app.ico` is set as `ApplicationIcon`, so it is embedded in `SkorskiTools.App.exe`.
The same file is copied next to the executable as `SkorskiTools.ico` so generated
portable shortcuts can point to it via `IconLocation`. Inno Setup icons inherit the
executable's icon.

`WScript.Shell` is a late-bound COM object: its properties must be set with
`Type.InvokeMember(BindingFlags.SetProperty, ...)`. Using `GetProperty().SetValue()`
silently created no shortcut at all. This was found and fixed after a real
end-to-end deployment check; a unit test now verifies that the `.lnk` file exists
and carries the requested icon.
