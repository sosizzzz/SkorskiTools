# Roadmap — post-1.0 features

The owner decided to implement **Full Auto** first. Skorski's Config (Firefox) is
deferred until a clean `user.js` is available; its plan is kept below unchanged.

## Current cycle: Full Auto

### Design goal

A single, generic engine with **no application names in C#**. Installing a new
program must require only a new `manifest.json`, as required by the fundamental
project rule. Manual installation stays the default and is never changed by Full Auto.

### Manifest contract

Full Auto is described by an optional `fullAuto` object in the application/browser
manifest. It is independent of the manual `installation` block:

```json
"fullAuto": {
  "enabled": true,
  "installArguments": ["/VERYSILENT", "/NORESTART"],
  "expectedExitCodes": [0],
  "restartExitCodes": [3010, 1641],
  "uninstall": {
    "useQuietUninstallString": true,
    "arguments": []
  }
}
```

Rules:

- `enabled` defaults to `false`.
- A manifest with `enabled: true` and no `installArguments` is **invalid**: Skorski
  Tools must not guess a vendor's silent switches.
- `expectedExitCodes` defaults to `[0]`; `restartExitCodes` defaults to `[3010, 1641]`.
- Arguments are always arrays, never concatenated strings.
- Uninstall uses only registry data matched during detection. `useQuietUninstallString`
  prefers `QuietUninstallString` over `UninstallString`.
- Reinstall never happens silently: the whole batch is confirmed first, naming every
  affected application.

### Engine

`FullAutoService` (Core, injectable and unit-tested):

1. `BuildPlanAsync` reuses `IInstallationDetector` for every item.
2. Not-installed + eligible → **Install**. Installed + eligible → **Reinstall**.
   Ineligible items are skipped with a reason in the plan.
3. `ExecuteAsync` verifies SHA-256 again, runs the original installer with the
   configured argument list, waits for it, checks exit codes, re-runs detection and
   reports progress.
4. Only `expectedExitCodes` count as success; `restartExitCodes` are reported as
   "needs restart", not as silent failures.

### UI and settings

- The *Full Auto* switch in Settings becomes usable and persists to
  `%USERPROFILE%\Skorski-Tools\settings.json`. It is off by default.
- Applications and Browsers get a **Full Auto** button, visible only while the switch
  is on. It shows a confirmation listing every planned action before running.
- Manual Install/Uninstall/Reinstall buttons remain unchanged.

### Acceptance criteria

- Adding a new installer with a valid `fullAuto` block requires no code change.
- No full-auto capability for a manifest without verified silent arguments.
- No destructive action without an explicit, named confirmation.
- All process arguments are passed as lists, never concatenated.
- Core logic is covered by unit tests with fake detector/installer.

### Known limitation

The owner must provide the actual silent switches for each installer. Skorski Tools
will not guess them or reverse-engineer the executables, so until those manifests are
updated, the engine will report those resources as not eligible.

---

## Deferred cycle: Skorski's Config (Firefox)

## Goal

Let the owner prepare a Firefox user profile with a single click, using files that
are shipped with Skorski Tools. The configuration is **user-level only**: no
enterprise policies, no `policies.json`, no machine-wide registry keys. The user
stays in control and sees exactly what will be applied.

## Scope

### In scope

- A `user.js` file exported by the owner, applied to the selected Firefox profile.
- Optional `search.json.mozlz4` (custom search engines).
- A set of signed `.xpi` extensions/themes installed into the profile and enabled.
- Profile discovery, profile selection, backup before applying, and revert.
- A manifest-driven catalog entry: adding/removing a preference or extension will
  not require changing C# code.

### Out of scope for this cycle

- Full Auto.
- Chrome / Edge configuration.
- Silent installation of browsers.
- Enterprise or organization-wide deployment.
- Automatic download of updates for extensions (Skorski Tools must never fetch a
  newer version of a bundled resource).

## Files needed from the owner

All files go under `resources/browsers/firefox/config/` (new folder). Every binary
or script file is verified with SHA-256 before being used, exactly like the other
resources.

| File | Required | Notes |
|---|---|---|
| `config.json` | yes | Describes the config version, prefs file, optional search file and extensions. Schema shipped in `resources/schemas/`. |
| `user.js` | yes | The desired preferences. Must contain only intended, stable prefs. |
| `extensions/*.xpi` | no | Signed extension/theme packages. One file per add-on. Metadata (id/version/name) is read from each package at load time. |
| `search.json.mozlz4` | no | Custom search engine database. Optional; applied only when listed in `config.json`. |

### Important: `prefs.js` is not `user.js`

The existing file `Ajustes Firefox ( copiar en raiz )\prefs.js` is a **generated
Firefox profile dump**. It contains machine-specific values (telemetry IDs,
installation timestamps, update timers, profile UUIDs) and Firefox overwrites it on
exit. It must not be copied into a profile.

Two options:

1. **Recommended:** the owner provides a clean `user.js` containing only the
   preferences they actually want to enforce (for example privacy, homepage,
   toolbar and performance settings).
2. Skorski Tools derives a first `user.js` from the existing `prefs.js` by filtering
   out machine-specific/runtime prefs, and the owner reviews it before release.

### Existing candidates and their SHA-256

These files are already in the repository and can be reused if the owner confirms:

| File | SHA-256 |
|---|---|
| `Ajustes Firefox ( copiar en raiz )\search.json.mozlz4` | `888380b383ac70317fd9126c4294a0bf556b8085599067825216f1f840e797eb` |
| `Ajustes Firefox ( copiar en raiz )\extensions\uBlock0@raymondhill.net.xpi` | `66d0e3f21ce3b32b1bc0a9399b93cb2c9aa598ef18805d92118c76f66e266c1e` |
| `Ajustes Firefox ( copiar en raiz )\extensions\nova-flame@mozilla.org.xpi` | `3e0280637d76785aad707d4a9b14b649b307e0f684c88bd62c2e31288fee4616` |
| `Ajustes Firefox ( copiar en raiz )\extensions\newtab@mozilla.org.xpi` | `6f92ff848f8b0f7a5f0ea4302f32fddac44c08dbdc1d75c5b13ee8af2bb05419` |

Notes:

- **uBlock Origin** and **Flame** are ordinary signed add-ons and are good
  candidates.
- **`newtab@mozilla.org.xpi`** is a Firefox system add-on. Replacing it is fragile
  and version-dependent; recommended to leave it out of the first cycle.
- Release Firefox only installs **signed** extensions. The feature must detect an
  unsigned `.xpi`, report it clearly and skip it, rather than trying to disable
  signature checks (that pref is locked in release builds).

### Proposed `config.json`

```json
{
  "schemaVersion": 1,
  "version": "1.0",
  "preferences": { "file": "user.js", "sha256": "<hash>" },
  "search": { "file": "search.json.mozlz4", "sha256": "<hash>" },
  "extensions": [
    {
      "file": "extensions/uBlock0@raymondhill.net.xpi",
      "sha256": "<hash>",
      "enabled": true
    },
    {
      "file": "extensions/nova-flame@mozilla.org.xpi",
      "sha256": "<hash>",
      "enabled": true
    }
  ]
}
```

Add/remove an item here and press **Reload catalog** — no source code change.

## Implementation plan

### Step 1 — Core model and validation

- Models: `BrowserConfig`, `BrowserConfigFile`, `BrowserExtension`.
- JSON Schema: `resources/schemas/browser-config.schema.json`.
- Validate: `schemaVersion`, hashes, existing files, safe relative paths, unique
  extension ids, `enabled` flag, duplicate ids across the catalog.
- Unit tests mirroring the existing manifest tests.

Acceptance: an invalid config is reported as a catalog issue and does not stop the
rest of the catalog.

### Step 2 — Firefox profile service (Core, testable)

- Discover profiles from `%APPDATA%\Mozilla\Firefox\Profiles\` by reading
  `profiles.ini` (not by guessing folder names).
- Expose `IFirefoxProfileProvider` so tests use fake profiles.
- Refuse to apply anything while `firefox.exe` is running; show a clear message.
- Read extension metadata from each `.xpi` `manifest.json` (id, version, name, type,
  target Firefox versions).
- Apply operation:
  1. Create a timestamped backup of `user.js`, `extensions.json` and the search file.
  2. Write `user.js` as a clearly delimited **Skorski-managed block**. If the file
     already exists, keep the user's own lines and only replace the managed block;
     if it does not exist, create it.
  3. Copy `.xpi` files into `<profile>\extensions\`.
  4. Add/update entries in `extensions.json` with `location: "app-profile"`,
     `path`, `rootURI`, `userDisabled: false` and `applyBackgroundUpdates: 0`.
     Do not remove or disable extensions that are not in the config.
  5. Optionally copy `search.json.mozlz4`.
- Revert operation restores the backup exactly.

Acceptance: applying to a fake profile produces the expected files and JSON entries;
reverting restores the original bytes. No real Firefox profile is modified by tests.

### Step 3 — UI

- New page **Firefox config** (or a section inside Browsers) showing config version,
  discovered profiles, detected extensions with name/version/id and their state.
- Profile selector, **Apply configuration** and **Revert** buttons.
- Confirmation before applying, naming the selected profile and listing what will
  change. Progress and a result summary in the UI.

Acceptance: the page reads the same catalog the rest of the app uses; no hardcoded
extension or preference list in C# or XAML.

### Step 4 — Documentation and verification

- README section, decisions entry, verification report update.
- End-to-end manual check on a **copy** of a Firefox profile, never the owner's real
  profile.
- New Release (1.1.0) only after the owner approves the result.

## Confirmed decisions

1. **Preferences file:** the owner will provide a clean `user.js`. Skorski Tools will
   not derive it from the existing `prefs.js`.
2. **Extensions:** only **uBlock Origin** (`uBlock0@raymondhill.net.xpi`) in the first
   cycle.
3. **Theme:** dark mode is mandatory, with the **Flame** colour theme
   (`nova-flame@mozilla.org.xpi`).
4. **Search:** `search.json.mozlz4` is included, and **Google must be the only search
   engine enabled**.
5. **New tab / startup:** opening Firefox or a new tab must show a blank page.
6. **Profiles:** all profiles are discovered from `profiles.ini`; the UI preselects
   `*.default-release` and requires explicit confirmation. Never apply to all
   silently.
7. **`newtab@mozilla.org.xpi`:** excluded; it is a Firefox system add-on and is
   fragile to replace.
8. **Existing `user.js` content:** preserve the user's own lines and only manage a
   delimited Skorski block.
9. **Other installed extensions:** leave them untouched.
10. **Extension updates:** `applyBackgroundUpdates: 0`; Skorski Tools never downloads
    a different version.
11. **Unsigned add-ons:** skip with a clear message; do not touch signature prefs.

## Preference checklist for the owner-provided `user.js`

The provided file should contain, at minimum, the preferences that enforce the
confirmed behaviour. Skorski Tools will validate their presence at load time and
report a clear catalog issue if any are missing:

| Requirement | Indicative preference(s) |
|---|---|
| Dark mode | `ui.systemUsesDarkTheme` / active dark theme pref (to be confirmed empirically) |
| Flame colour theme | `extensions.activeThemeID` = `nova-flame@mozilla.org` |
| Only Google search | `browser.search.defaultenginename` = `Google` and hide/disable the other engines |
| Blank startup | `browser.startup.page` = `0`, `browser.startup.homepage` = `about:blank` |
| Blank new tab | `browser.newtabpage.enabled` = `false` |

> Firefox themes are exclusive: activating Flame replaces the built-in dark theme.
> Flame ships a `dark_theme` variant, so the final combination must be verified on a
> **copy** of a profile. The exact pref names will be confirmed during Step 2 and
> recorded in `docs/decisions.md`.

## Still needed from the owner

- The clean `user.js` file, saved as
  `resources/browsers/firefox/config/user.js` (or tell me its path so I can copy it).
- Confirm that the provided `search.json.mozlz4` contains **only Google**; it will be
  decompressed and checked during implementation.
- If any additional prefs beyond the checklist matter, include them in `user.js`.
