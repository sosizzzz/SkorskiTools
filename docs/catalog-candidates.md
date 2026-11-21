# Silent-install candidate catalog — analysis

Scope: apps that are commonly useful when setting up a personal Windows PC and whose
**silent switches are documented publicly**. No installer was downloaded, inspected or
added to `resources/`. Nothing here changes the catalog yet.

## Method

Sources used: vendor FAQ/docs where available (7-Zip, Git for Windows, VLC, VS Code),
and well-known deployment references (Silent Install HQ, winget manifests, enterprise
deployment guides). The switch list is a **proposal**; every new installer must be
provided by the owner and verified with SHA-256 before it is added.

## Tier 1 — strong candidates (documented by the vendor)

| Candidate | Category | Proposed `installArguments` | Scope | Installer | Confidence |
|---|---|---|---|---|---|
| **7-Zip** | utilities | `/S` | user or machine | NSIS EXE | High — official FAQ documents `/S` |
| **VLC media player** | multimedia | `/L=1033 /S` | machine | NSIS EXE | High — VideoLAN wiki documents `/L=<code> /S` |
| **Notepad++** | development | `/S /noUpdater` | user or machine | NSIS EXE | High — official manual documents `/S`, `/noUpdater` |
| **Git for Windows** | development | `/VERYSILENT /NORESTART /SP-` | machine | Inno Setup EXE | High — gitforwindows.org documents these flags |
| **Visual Studio Code** | development | `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /mergetasks=!runcode` | user or machine | Inno Setup EXE | High — official VS Code docs document `/mergetasks=!runcode` |
| **LibreOffice** | other | `/qn /norestart` **via `msiexec /i`** | machine | MSI | High — standard Windows Installer switches |

Notes:

- **VLC:** the `/L=1033` language argument is needed; without it the installer may
  still show a language prompt even in silent mode. Change the code for other locales.
- **VS Code:** there are two different installers. `VSCodeUserSetup-*.exe` installs per
  user without admin; `VSCodeSetup-*.exe` installs for all users and needs elevation.
  The owner must decide which one to bundle. `/mergetasks=!runcode` prevents the editor
  from opening at the end of an unattended install.
- **Git:** `/COMPONENTS=...` can customize shell integration. The default set is a
  reasonable starting point; keep it simple until the owner asks for more.
- **LibreOffice:** MSI needs special handling by the engine (see gaps below).

## Tier 2 — useful, lower confidence / extra handling

| Candidate | Proposed arguments | Installer | Notes |
|---|---|---|---|
| **OBS Studio** | `/S` | NSIS EXE | Widely documented; useful for recording tutorials |
| **Audacity** | `/VERYSILENT /NORESTART` | Inno Setup EXE | Common multimedia tool |
| **GIMP** | `/VERYSILENT /NORESTART` | Custom/Inno EXE | Verify the exact release package |
| **Node.js** | `/qn /norestart` via `msiexec /i` | MSI | Needs the same MSI handling as LibreOffice |
| **Python** | `/quiet InstallAllUsers=1 PrependPath=1` | Custom EXE | `PrependPath` changes PATH; decide deliberately |
| **Zoom** | `/qn /norestart` via `msiexec /i` | MSI | Needs MSI handling; often not needed on a home PC |

## Gaps found in the current engine

1. **MSI packages.** `ApplicationInstaller` currently starts the manifest file
   directly. `.msi` files must be started through
   `msiexec.exe /i "<file>" <arguments>`. Adding LibreOffice, Node.js or Zoom requires
   a generic change: detect `.msi` and prepend `msiexec /i` with the quoted path. This
   is a one-time engine improvement, not a per-app hack.

2. **User vs machine installers.** The manifest already has
   `fullAuto.scope: user|machine`. For VS Code and 7-Zip the owner must bundle the
   specific installer that matches the chosen scope; the app cannot convert a
   per-user installer into a per-machine one.

3. **Firefox remains blocked.** The bundled stub cannot be silent; only the full
   offline installer accepts `/S`. This is unchanged by the candidate analysis.

4. **Exit codes.** The engine already treats `3010`/`1641` as "installed, restart
   required" and other codes as failures, which matches the standard MSI/code
   conventions found in the sources.

## Recommendation

Start with **7-Zip, VLC, Notepad++, Git and VS Code** if the owner wants to extend the
catalog: they are high-value, have vendor-documented switches and need no engine
change. Add **LibreOffice** only after implementing generic MSI support.

Nothing is added automatically: for each chosen app the owner must provide the
original installer file, then a manifest with `id`, `name`, `files`, `detection`,
`installation` and the `fullAuto` block is created. Adding the file plus manifest is
enough — no C# changes, consistent with the project's fundamental rule.

## Sources

- [7-Zip FAQ — silent install](https://7-zip.org/faq.html)
- [VLC wiki — installing VLC](https://wiki.videolan.org/Documentation%3AInstalling_VLC)
- [Notepad++ manual — command line](https://npp-user-manual.org/docs/command-prompt)
- [Git for Windows — silent/unattended installation](https://gitforwindows.org/Silent-or-Unattended-Installation)
- [VS Code — installing on Windows](https://code.visualstudio.com/docs/setup/windows)
- [Silent Install HQ — LibreOffice](https://silentinstallhq.com/libreoffice-silent-install-how-to-guide)
- [Offline Installer Setup — silent switch cheat sheet](https://github.com/offlineinstallersetup/silent-install-cheatsheet)
