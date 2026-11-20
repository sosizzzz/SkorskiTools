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
