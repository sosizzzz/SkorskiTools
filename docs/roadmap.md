# Roadmap — next cycle: Skorski's Config (Firefox)

This document proposes the first post-1.0 feature. It is a plan only: no code is
changed until the owner confirms the open decisions at the end.

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

## Risks and decisions to confirm

1. **Which profiles?** Recommended: show all profiles from `profiles.ini`, preselect
   `*.default-release`, let the owner choose; never apply to all silently.
2. **Existing `user.js` content:** recommended to preserve the user's lines and only
   manage a delimited block.
3. **Other installed extensions:** recommended to leave them untouched.
4. **Extension updates:** set `applyBackgroundUpdates: 0`; Skorski Tools must not
   download a different version.
5. **Unsigned add-ons:** skip with a clear message; do not modify signature prefs.
6. **`newtab@mozilla.org.xpi`:** exclude from the first cycle unless the owner
   explicitly wants it.

## Open questions for the owner

- Please provide the clean `user.js` (or confirm option 2 above).
- Confirm which of the existing `.xpi` files should be enabled, and whether to add
  more.
- Confirm whether `search.json.mozlz4` should be applied.
- Confirm the profile policy (per-profile choice vs. default profile only).
