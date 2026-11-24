# Final verification (Phase 9)

Executed on Windows 11, .NET 10.0.404, non-elevated user session.

## Automated

| Check | Result |
|---|---|
| `dotnet build SkorskiTools.sln -c Debug` | 0 errors, 0 warnings |
| `dotnet test SkorskiTools.sln -c Debug` | 72/72 passed |
| Inno Setup compile (`ISCC.exe SkorskiTools.iss`) | Successful, 0 warnings |

Unit test coverage includes: path safety, SHA-256, manifest schema/rules,
discovery, invalid manifests, duplicate ids, registry/file/path detection,
installer argument parsing, auxiliary copying and portable deployment.

## Application (self-contained publish)

| Check | Result |
|---|---|
| Published app starts from its own folder | Yes, title "Skorski Tools" |
| Catalog discovered from published `resources/` | 6 applications, 3 browsers, 2 portables, 1 auxiliary, 2 scripts |
| Catalog issues | 0 |
| Navigation across all 7 sections | All responded, no crash, content visible |
| Initial detection | Applications 2/4, Browsers 1/2, Portables 0/2 |
| "Detect applications" button | Re-ran detection; new log entry and updated summary |
| Install button states | 2 enabled (not installed); Uninstall/Reinstall disabled for those 2 and enabled for the other 2 |
| Script copy button | Full code copied to clipboard |
| Auxiliary Download button | 2 files copied and SHA-256 verified into the user folder |
| Running as administrator | No (standard user session) |
| Full install switch off by default | Yes; settings.json holds `fullAutoEnabled: false` |
| Selection checkboxes | 4 in Applications, 2 in Portables when enabled |
| Full install button | Visible and enabled only while the switch is on |
| Empty selection | Shows "Select at least one ... first" banner |
| History page | Opens with "No operations recorded yet"; persistence covered by unit tests |
| Detail panel | Selecting Flow Launcher showed id, file, hash prefix, detection state and manifest path |
| New resources | VLC, Git and Brave loaded with 0 issues; Git detected as installed 2.56.1; Simulate showed its command and valid hash |
| Highlights | Selecting Brave showed exactly three benefits in the detail panel |
| Eligible checkboxes | Clear rounded checkboxes; Office disabled with tooltip, Firefox disabled in Browsers |
| Blocked checkbox | Firefox, Office and Acrobat disabled with a tooltip explaining each vendor limitation |
| Installer failure feedback | Exit codes and manifest hints surface in the result banner and history; zero-exit installs are verified by detection |
| Simulate button | Hidden unless both Full Auto and Developer mode are on; showed exact commands and `SHA-256: OK` for 4 applications without starting any installer |
| Executable icon resource | `RT_GROUP_ICON` present in `SkorskiTools.App.exe` |
| Portable desktop shortcut | Real `.lnk` created, `IconLocation` = shipped `SkorskiTools.ico`, and removed on uninstall |

## Distribution

| Check | Result |
|---|---|
| Self-contained publish | `hostfxr.dll`, `hostpolicy.dll`, `System.Private.CoreLib.dll`, `PresentationFramework.dll` present |
| Published resources | 38 files, 9 manifests, 5 schemas |
| Stray `bin`/`obj` directories in publish output | 0 |
| Silent install to a temporary directory | 519 files, exe + resources + uninstaller present |
| App launched from installed directory | Catalog loaded correctly |
| Silent uninstall | Destination directory completely removed |

## Known accepted limitations

- No code signing in 1.0: Windows SmartScreen will show a warning.
- Updates are manual: download and run the new installer.
- The desktop used for automated screenshots is shared, so UI verification was done
  with UI Automation and direct window capture rather than visual inspection alone.
