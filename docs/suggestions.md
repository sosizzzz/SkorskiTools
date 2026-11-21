# Feature suggestions and future directions

This document collects ideas that are **not scheduled**. It is a discussion aid: each
idea has value, approximate cost, dependencies and any constraint from the spec.
Nothing here is implemented unless the owner chooses it in a later cycle.

## Constraints that still apply

- Skorski Tools never runs as administrator. Elevation only for a single operation.
- Scripts and auxiliary files are never executed.
- No downloads of newer versions of bundled resources; the owner provides files.
- Adding a resource must not require source code changes.
- No dependencies outside the approved list without documenting the reason.
- Full Auto is currently parked; do not resume it without an explicit request.

## Priority shortlist (best value / effort)

| Idea | Value | Cost | Notes |
|---|---|---|---|
| **In-app operation history** — done | High | Low–Medium | `History` page reads `operation-history.json` with up to 200 results. Manual and full-install actions append entries. |
| **Resource detail panel** — done | High | Low | Shared control shows files, hashes, detection source, installed version, category and manifest path. |
| **Developer: manifest doctor** | High (portfolio) | Medium | Validate every manifest, recompute hashes, list issues with path and line, and export a report. Great for demonstrating the validation design. |
| **Dashboard summary with last detection** | Medium | Low | Show counts, last detection time and catalog issues at a glance. |
| **Log viewer (developer mode)** | Medium | Low | Read the Serilog files from the app, with filters by level. No external dependency needed. |
| **Light theme + accent colour** | Medium | Medium | WPF-UI already supports theme swapping; needs settings persistence, which now exists. |

## Usability

### In-app notifications

Replace the remaining `MessageBox` calls with WPF-UI `Snackbar`/`InfoBar` presentations.
Benefit: fewer blocking popups and a more modern feel. Risk: low, but confirmation
dialogs must remain explicit for destructive actions.

### Search and navigation

- Instant fuzzy search instead of plain `Contains`.
- Keyboard shortcuts: `Ctrl+F` focus search, `Esc` clear, arrow keys move between items.
- Category filter chips instead of a combo box.
- Sorting by name, category, installed state or version.
- Favourites/pinned resources stored in `settings.json`.

### Resource detail panel

A right-hand pane with: name, description, version, detected version, detection source,
category, file list, SHA-256, manifest path and last operation result. This makes the
transparency requirement visible without opening raw files.

## Catalog and validation

### Manifest doctor (developer mode)

A page that runs the same validation as startup and shows:

- resources loaded, skipped and why;
- hash mismatches with file path;
- schema errors with instance location;
- duplicate ids;
- categories referenced but not defined.

Include a **Copy report** button. This is a strong portfolio feature because it
demonstrates the validation and path-safety design.

### Live manifest reload

Instead of restarting the app, watch `resources/` with `FileSystemWatcher` and reload
when a manifest changes. Must debounce and never reload while an operation is running.

### Version comparison

Read file/product versions and show whether the bundled version is newer than the
installed one. Useful for deciding reinstall. Must not download anything.

### Detection inspector (developer mode)

For a selected application, show which registry keys/values matched and which strategy
was used. This makes detection auditable and easier to fix.

## Operations

### Operation history

Persist a small JSON array in the user folder with timestamp, resource id, action,
result, exit code and message. Show it as a read-only timeline. This complements the
Serilog files without adding a visual history in 1.0-style UI.

### Queue with continue-on-error

For Full Auto (if ever resumed): allow the batch to continue after an individual
failure, collect all results, and play one sound at the end. The current engine already
stops per item on failure, so this is mostly a policy/UI change.

### Pre-flight checks

Before a batch: close applications that lock files, verify free disk space, and check
that no installer is already running. Careful: killing user processes is destructive
and needs explicit consent.

### Restore point before batch

Creating a system restore point is a system-wide change and needs elevation. Only
consider this as an opt-in developer/power-user feature.

## Portables

### Backup before reinstall or uninstall

Copy the portable's data folders to `%USERPROFILE%\Skorski-Tools\backups\<id>\<timestamp>\`
before replacing/removing it. Reinstall currently warns about data loss; a backup would
remove that risk. Needs a rule for what counts as "data" vs "program files".

### Portable version tracking

Read the executable's file version and show it in the list, with a "bundled version"
field in the manifest. No downloads; the owner still provides updates.

## Scripts and auxiliary

### Script metadata

Add `tags`, `author` and `updated` fields to `scripts.json`, plus tag-based filtering.
Pure data/schema change.

### Copy all / download all

Buttons to copy every visible script or copy all auxiliary files at once. Must keep the
"never execute" rule.

### Editor theme

Expose AvalonEdit highlight themes as a setting (dark/light). Low risk.

## Settings and developer tools

### Diagnostics bundle

A developer-mode button that packages settings, catalog issues and the last N log lines
into a zip under the user folder. Useful for support and for a portfolio showcase.

### Reset settings

Restore defaults for Full Auto and Developer mode without touching user files.

### Localization

Move UI strings to resource files and add Spanish/English. The project is currently
English-only by rule, but a Spanish UI option could be useful for the owner; this would
require changing the documented language policy.

## Distribution and portfolio

### GitHub Actions

Build, test and publish on tag:

- `dotnet build` + `dotnet test` on Windows;
- self-contained `win-x64` publish;
- Inno Setup compile;
- upload the installer to a GitHub Release.

This proves the project can be reproduced and is a strong portfolio signal.

### Release automation notes

The release asset is ~205 MB. Consider splitting resources into a separate archive or
compressing installers further, while keeping the catalog verifiable with SHA-256.

### Code signing

Post-1.0 improvement to remove the SmartScreen warning. Requires a certificate and
documenting the trust decision.

## Explicitly out of scope for now

- Enterprise/GPO deployment or machine-wide browser policies.
- Auto-updating bundled apps from the internet.
- Executing scripts or auxiliary files from the UI.
- Running the whole app as administrator.
- Resuming Full Auto development without a new owner request.

## Suggested next cycle after the three new installers arrive

1. Add VLC, Git and LibreOffice manifests once the owner provides the files.
2. Verify them with **Simulate** (developer mode).
3. Add the **resource detail panel** and **operation history** — both are contained,
   visible improvements that reuse existing data.
4. Then, if desired, the **manifest doctor** as the portfolio-centred feature.
