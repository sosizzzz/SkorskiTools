# Skorski Tools

A personal, extensible Windows desktop application that prepares, maintains and
configures PCs from a single interface. It centralizes the installers, browsers,
portable apps, auxiliary files and scripts provided by the owner.

Target: **Windows 10 and Windows 11**. Skorski Tools never runs as administrator.

## Requirements

- .NET 10 SDK (10.0.404 or newer)
- Windows 10 / 11 with WPF support

> In this development environment the .NET 10 SDK is installed per user at
> `%LOCALAPPDATA%\Microsoft\dotnet`. The `DOTNET_ROOT` user environment variable
> points there so the compiled `.exe` finds the .NET 10 runtime. Add that directory
> to `PATH` before running `dotnet` commands, or use a shell that already includes it.

## Build and test

```powershell
dotnet restore SkorskiTools.sln
dotnet build SkorskiTools.sln -c Debug
dotnet test SkorskiTools.sln -c Debug
dotnet run --project src/SkorskiTools.App/SkorskiTools.App.csproj
```

To just launch the already-built Debug app, double-click **`run-app.cmd`** in the
repository root. It sets `DOTNET_ROOT` and `PATH` for the current process and starts
`SkorskiTools.App.exe`. Alternatively, from PowerShell:

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& '.\src\SkorskiTools.App\bin\Debug\net10.0-windows\SkorskiTools.App.exe'
```

## Solution layout

| Path | Purpose |
|---|---|
| `src/SkorskiTools.Core` | Models, validation, detection and services (no UI) |
| `src/SkorskiTools.App` | WPF application: Views, ViewModels, Controls, Themes |
| `src/SkorskiTools.Tests` | xUnit tests for Core |
| `resources/` | Manifests, schemas and original resource files |
| `docs/decisions.md` | Technical decisions and deviations |
| `installer/` | Inno Setup script (Phase 8) |

## User folders

On first launch the app creates, even when empty:

```
%USERPROFILE%\Skorski-Tools\
├── auxiliary/
├── installers/
├── portables/
├── browsers/
└── scripts/
```

Logs are written to `%LOCALAPPDATA%\Skorski Tools\logs\` with daily rolling files.

## Development phases

1. **Foundation** — in progress: solution, .NET 10 + WPF, MVVM, navigation, dark theme, user folders, logging.
2. **Dynamic catalog** — done: JSON Schemas, manifest validation, SHA-256, path safety, discovery, categories, icons, search and category filters.
3. **Applications** — done: registry/MSI/file/path detection, manual install, uninstall with confirmation, reinstall (repair/upgrade), button states and *Detect applications* button. Browsers reuse the same code.
4. **Browsers** — done: independent catalog entry and navigation item using the same application model and actions. Firefox and Chrome are included; adding another browser only needs a manifest. No browser configuration actions in 1.0.
5. **Portables** — done: atomic deploy to `%USERPROFILE%\Skorski-Tools\portables\<id>\`, SHA-256 check, desktop shortcut, reinstall and uninstall with a data-loss warning.
6. **Auxiliary and Scripts** — done: auxiliary files are copied after SHA-256 verification and never executed; scripts are shown read-only with AvalonEdit syntax highlighting and a clipboard copy button. No execution mechanism exists in either section.
7. **Settings** — done: version, resources/user files/logs paths, manual installation mode, and a disabled *Full Auto* option marked "Coming soon".
8. Distribution — pending.
9. Final testing — pending.

Each phase is built, verified and committed separately.

## Catalog

Resources are discovered automatically from `resources/<section>/<resource>/manifest.json`.
Each manifest is validated against the matching JSON Schema in `resources/schemas/`,
plus project rules (unique ids, known categories, safe relative paths, existing files
and matching SHA-256). An invalid resource is reported and skipped; it never stops the
rest of the catalog. The **Reload catalog** button in the sidebar re-runs discovery.

Scripts live in `resources/scripts/scripts.json` and are shown read-only. They are
never executed, and neither are auxiliary files.
