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
2. Dynamic catalog — pending.
3. Applications — pending.
4. Browsers — pending.
5. Portables — pending.
6. Auxiliary and Scripts — pending.
7. Settings — pending.
8. Distribution — pending.
9. Final testing — pending.

Each phase is built, verified and committed separately.
