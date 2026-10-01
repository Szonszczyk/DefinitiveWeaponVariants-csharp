# Validation — 2026-10-01

## Complete theme colors and asset deployment

Converted every explicit config-page color to a theme variable with an original
color fallback. Both theme files define all 31 variables. Added an installed-file
timestamp to the stylesheet URL so a page reload requests the updated theme.
The game deployment target now copies `wwwroot` and honors `SkipModDeploy`.

`dotnet build DefinitiveWeaponVariants.csproj --no-restore -p:SkipModDeploy=true
-p:SkipModPackaging=true` passed with zero errors and nine existing warnings.
A variable coverage check confirmed both themes define every referenced color,
and no explicit color remains outside a variable fallback. The installed game
CSS was inspected and contains the purple palette. The SPT Web assembly confirms
static assets map to `/<AssemblyName>/`. No live SPT page was available for a
browser check; no files were deployed to the game during this validation.

## Compact header and sidebar revision

The header now uses two rows: title with Reload/Save actions, then status with the
pending-change count. Save is the rightmost action. Successful saves reuse the
status row instead of inserting another banner. Validation and discard prompts
remain visible when necessary.

The sidebar has a divider after the final configuration section, an Advanced
options entry containing the empty-Unknown toggle, and a Back to SPT navigation link.

The solution build passed with zero errors and nine existing warnings using
`dotnet build DefinitiveWeaponVariants.sln --no-restore -p:SkipModDeploy=true -p:SkipModPackaging=true`.
The isolated browser preview confirmed the compact two-row layout, action order,
Advanced options panel, working visibility toggle and navigation to `/` through
Back to SPT. Core persistence logic was unchanged; no additional tests were added.
No deployment or packaging was performed for this revision.

## Previous UI revision

Implemented the requested removal of backups, per-mod `wwwroot` theme, hidden
empty Unknown editors, numeric sliders, removal of JSON-name captions, initially
expanded objects, smaller summaries, and a pending-setting count.

Validation:

```powershell
dotnet build DefinitiveWeaponVariants.sln --no-restore -p:SkipModDeploy=true -p:SkipModPackaging=true
dotnet run --project BlazorConfig/Checks/Checks.csproj -- config
```

Build passed with zero errors and the same nine warnings in existing DWV files.
All **54 checks passed**. The old backup-retention assertions were replaced with
assertions that neither an initial replacement nor repeated saves create backups.
New checks cover repeated edits, reverting values, nested fields, object resets,
invalid text, unknown additions/removals, raw JSON formatting, and list changes.

The isolated browser preview verified:

- Objects initially expanded, smaller toggle text, and no secondary JSON-name captions.
- Empty nested Unknown editors hidden; removing the last root unknown hides its
  navigation item; the optional toggle reveals empty editors again.
- Boolean edits count once and reverting clears the count.
- Numeric text moves the slider; moving the integer slider updates the text in
  whole steps. Out-of-range text remains visible and blocks Save.
- Repeated edits to the same setting remain one change; Save/Reload reset the count.
- Changing only the temporary theme's accent color changes the heading, save
  button, selected navigation styling, and slider after browser refresh.
- No backup files appeared in the temporary configuration folder.

The build output has `wwwroot/config-editor-theme.css` beside the DLL. A separate
package under `obj/BlazorUiValidation.zip` also contains that theme plus the DLL,
schema and defaults, with no EXE, check runner or personal config. The normal
project-root ZIP's SHA256 was checked before and after and was unchanged.

This pass preserved the user's version 5.0.0 and locale-packaging edits. No files
were deployed to SPT. The user had verified the initial page in SPT; this revision
was tested in an isolated preview, not by restarting the installed server.

## Initial implementation record (historical)

The record below describes the original version before the requested UI revision;
its backup behavior and 40-check count have been superseded by the current results.

Branch: `codex/schema-blazor-config` in DefinitiveWeaponVariants.

## Build

```powershell
dotnet build DefinitiveWeaponVariants.sln --no-restore -p:SkipModDeploy=true -p:SkipModPackaging=true
```

Passed against .NET SDK 10.0.401 and SPT 4.1.2 packages. Zero errors, nine warnings
in pre-existing DWV code (nullable references and unreachable code). No warnings
originated in `BlazorConfig`.

## Automated behavior checks

```powershell
dotnet run --project BlazorConfig/Checks/Checks.csproj -- config
```

All **40 checks passed**, including actual DWV schema/default round trips,
ordered aliases/current-name priority, nested unknown JSON, explicit null,
invalid types/ranges, typed lists and dictionaries, duplicate keys, malformed
objects, backup bytes and retention, failed writes, concurrent stale tabs and
external config/schema/default edits. All writes were to disposable temporary
directories; DWV's own config/default/schema were read-only test inputs.

## Browser verification

Used a separate temporary ASP.NET Core Blazor test host with the same components
and disposable config copies. The preview used a development-only administrator
identity; that host and identity are **not** in the mod repository or DLL.

Verified rendered desktop layout, section navigation, expandable recursive
objects, boolean edits, unsaved state and reload confirmation, numeric range
errors blocking Save, successful correction and Save, backup status, adding and
saving structured unknown JSON, and adding/saving a free-form list row. The
browser reported no warning/error console entries after the checks. The temporary
preview host was stopped when verification finished.

## Packaging

Exercised `PackageModForDistribution` with a verified staging directory under
`obj/BlazorPackageValidation` and output `obj/BlazorConfigValidation.zip`.
Confirmed the ZIP contains the mod DLL, `config/configSchema.json` and
`config/defaultConfig.jsonc`, and no EXE, check runner, preview host, or personal
`config.jsonc`.

An initial packaging attempt unexpectedly used the normal project-root ZIP path
and **overwrote the existing untracked `DefinitiveWeaponVariants.zip`**. The old
ZIP was not backed up. Packaging properties now honor nonempty overrides, and
the subsequent validation produced the intended separate ZIP. No files were
deployed to the installed SPT server.

## Remaining SPT-specific checks

The actual SPT server/game was not started or modified for verification. Before
publishing, check SPT's mod card discovery, administrator login/authorization,
coexistence with its layout/other mods, and applying the saved configuration after
a manual restart. The README provides the step-by-step checklist.

The original WPF project and APBS source were not changed. Existing uncommitted
DWV edits were retained. Only the project file and metadata were edited outside
the new `BlazorConfig` folder and copied `config/configSchema.json` (plus the ZIP
side effect described above). The full repository's whitespace check flags an
existing trailing-space line in `Main.cs`; it was left untouched.
