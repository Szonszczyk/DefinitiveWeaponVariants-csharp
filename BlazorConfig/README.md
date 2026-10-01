# Schema-driven Blazor configuration editor

This folder adapts Config File Configurator's schema idea to an SPT 4.1.2 server
mod. It is independently implemented; it does not copy APBS components, assets,
styles, or live configuration behavior. The original WPF application is unchanged.

The editor runs inside SPT's existing Blazor host and is compiled into the mod DLL.
Players do not install an editor executable, another web server, or another mod.
No restart button or live-apply operation is included.

## Try it in Definitive Weapon Variants

1. Build DWV and install the resulting DLL with its normal mod data.
2. Put `configSchema.json` beside `defaultConfig.jsonc` and `config.jsonc` in the
   mod's `config` directory. DWV normally creates `config.jsonc` at server startup.
3. Start SPT, sign in to its web interface with an administrator account, and open
   the DWV card. The page route is `/definitive-weapon-variants/config` on the
   **same host and port** as SPT. Do not add a second port or web host.
4. Edit settings, press **Save configuration**, and restart SPT manually before
   expecting item generation or other startup behavior to use the changes.

The build packages `config/configSchema.json` alongside `defaultConfig.jsonc`.
It does not package a player's `config.jsonc`. Ship `wwwroot/` alongside the DLL
as required by SPT's web-page hosting. The included `config-editor-theme.css`
contains editable per-mod colors; the reusable layout styles remain in the DLL.
All config-page colors (including text, borders, controls, and feedback) use the
theme's `--mc-*` variables. SPT's surrounding navigation keeps its own styling.
The stylesheet URL includes the installed file's modification time to bypass
cached copies after edits. Reload the page after editing the installed CSS.
Editing the source project's CSS requires copying it to the installed mod or
building with deployment enabled; deployment now copies the complete `wwwroot`.
To try the purple palette, copy `config-editor-theme-test.css` over the installed
`wwwroot/config-editor-theme.css`. Keep the original filename for the active theme.

For a compile check that does not deploy files or overwrite your existing ZIP:

```powershell
dotnet build DefinitiveWeaponVariants.sln --no-restore -p:SkipModDeploy=true -p:SkipModPackaging=true
```

Run without `--no-restore` once after adding/changing the SPT Web package.
DWV's post-build deployment remains project-specific; use the skip flags above
when you only want to compile. It now copies the actual `$(TargetPath)` instead
of a hardcoded Debug DLL path. Review `ModDeployDirectory` in the project before
using deployment on another machine, or override it with `-p:ModDeployDirectory=...`.

## Code tour: read these files in order

| File | Responsibility |
| --- | --- |
| `ModEditorSettings.cs` | Per-mod route, labels, file names, policy, and DLL-relative config directory. |
| `Core/Schema.cs` | Deserializes the existing schema vocabulary and validates types/names. |
| `Core/EditorDraft.cs` | A browser's editable working copy, recursive objects, validation and migration. |
| `Core/ConfigFileStore.cs` | Reads snapshots, detects stale edits, and atomically replaces the config. |
| `Core/DraftChangeTracker.cs` | Counts settings changed since loading, including nested and unknown values. |
| `ConfigPage.razor` | SPT route, authorization, section navigation, Save/Reload and unsaved state. |
| `Components/PropertyEditor.razor` | Chooses a control from a field's schema type; recurses for objects. |
| `Components/NumericEditor.razor` | Synchronizes a bounded slider with its editable numeric text. |
| `Components/UnknownEditor.razor` | Adds, edits and removes raw unknown JSON at one object level. |
| `Components/EditorStyles.razor` | Local, scoped CSS embedded in the compiled component. |
| `../wwwroot/config-editor-theme.css` | Per-mod accent and background colors, editable without rebuilding. |
| `_Imports.razor` | Razor imports shared by this folder and its descendants. |
| `Checks/` | Developer-only automated regression checks. Never ship this folder's output. |

Razor combines HTML and C#. `@if`/`@foreach` choose which HTML to render;
`@bind` updates a draft property; `@onclick` calls a method; `[Parameter]` accepts
data from a parent component; `EventCallback` notifies that parent of an edit.
Blazor handles these interactions through SPT's server connection. `@key` gives
rows stable identities so removing an earlier row does not scramble input focus.

The equivalent of WPF's view model is `ObjectDraft`/`FieldDraft`. They have no WPF
dependencies and no reference to DWV's `ConfigData`. The page owns one session;
the store shares only a per-file save lock. Two browser tabs never share a draft.

## Add this editor to another mod

You do **not** need to rewrite the components or create one page per setting.

1. Copy the complete `BlazorConfig` folder and `wwwroot/config-editor-theme.css`
   into the other mod's project. The code must
   compile into that mod's DLL. Do not reference the DWV DLL as an editor library.
   You can omit `Checks` when copying, or keep it with the exclusion below.
2. Edit `ModEditorSettings.cs`:
   - Set a unique `Route`, for example `/my-mod/config`.
   - Change `Title` and `Description`.
   - Match `ConfigFile` and `DefaultFile` to that mod's actual filenames.
   - Keep the DLL-relative `config` folder unless your mod uses another layout.
   - Keep the `Administrator` policy when targeting the same SPT web host.
3. Use `Microsoft.NET.Sdk.Web`, `net10.0`, and `OutputType` `Library` in the mod
   project. Add `SPTarkov.Server.Web` at the **same compatible SPT version** as the
   mod's other SPT packages. This implementation was built against **4.1.2**.

   ```xml
   <Project Sdk="Microsoft.NET.Sdk.Web">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <OutputType>Library</OutputType>
     </PropertyGroup>
     <ItemGroup>
       <!-- Keep your existing compatible SPT package references too. -->
       <PackageReference Include="SPTarkov.Server.Web" Version="4.1.2" />
       <Compile Remove="BlazorConfig\Checks\**\*.cs" />
       <Content Remove="BlazorConfig\Checks\**\*" />
       <Content Update="wwwroot\**\*" CopyToOutputDirectory="PreserveNewest" />
     </ItemGroup>
   </Project>
   ```

4. On that mod's **existing** metadata class, add `IModBlazorMetadata` and the
   following members. Retain its own name, GUID, version, dependencies and license.

   ```csharp
   using SPTarkov.Server.Web;
   using ModConfigEditor;

   // Add IModBlazorMetadata to your existing interface list.
   public string? WWWRootUrl { get; init; }
   public string? HomePage { get; init; } = ModEditorSettings.Route;
   public string? HomePageDescription { get; init; } = ModEditorSettings.Description;
   ```

   The page uses `[Route(ModEditorSettings.Route)]`, so there is only one route
   string to change. If your mod already implements this interface or has a home
   page, keep its existing members and add a link to the editor instead of adding
   duplicate members or replacing a home page you want to keep.
5. Write `config/configSchema.json` describing the JSON names that mod actually
   loads. Put its read-only defaults beside it. The supported schema is described
   below. Its keys must match the config's JSON keys, including capitalization.
6. Update that mod's own packaging/deployment steps to copy the schema and default
   file alongside the DLL in `config/`, plus the complete `wwwroot/` folder beside
   the DLL. Edit `wwwroot/config-editor-theme.css` to choose distinct colors: start
   with `--mc-accent`, then adjust the background/panel/highlight variables. SPT
   serves this file at `/<AssemblyName>/config-editor-theme.css`; `ThemeUrl` derives
   the assembly name automatically. If your metadata specifies a custom
   `WWWRootUrl`, change `ThemeUrl` to use that same URL prefix. Copy the DLL and mod data for players;
   do not ship developer check/preview outputs. Do not overwrite player configs.
7. Keep that mod's startup config loader. No editor DI registration, MVC controller,
   `Program.cs`, separate host, or `IConfigEditorConfigProvider` is needed. The
   editor intentionally does not inject or mutate the running config instance.
8. Build and run the checks, then perform the SPT checks below before publishing.

The `ModConfigEditor` namespace can stay the same in separate mod DLLs: each DLL
contains its own types. Each mod still needs a **unique URL route**. If you combine
multiple editor copies into a single DLL, give them distinct namespaces instead.
When maintaining several mods, keep a shared source template and copy updates to
each mod; a separate runtime dependency is not required.

Official hosting reference:
[SPT 4.1 mod web pages](https://github.com/sp-tarkov/wiki/blob/main/SPT_41/modding/server/Mod_Web_Pages.md).

## Schema behavior

This is Config File Configurator's custom format, not the JSON Schema standard.
Top-level `Sections` group the UI. Each section has `Name`, `DisplayName`, optional
`Description`, and `Properties`. Sections do **not** add nesting to the saved JSON.

Properties use `Property`, `DisplayName`, `Description`, `Type`, and optional
`Default`. Use lowercase type names:

| Type | Editor and persisted value |
| --- | --- |
| `bool` | Checkbox; JSON boolean. |
| `int` | Whole-number input with slider when both bounds exist; signed 64-bit integer. Fractions are rejected. |
| `float` | Numeric input with slider when both bounds exist; finite JSON number. |
| `string` | Text input; JSON string. |
| `list`, no `AllowedValues` | Add/remove rows; typed JSON array. `ItemType` defaults to `string`. |
| `list`, `AllowedValues`, `AllowMultiple: false` | Dropdown; selected JSON scalar. |
| `list`, `AllowedValues`, `AllowMultiple: true` | Checkboxes; JSON array of selected values. |
| `object` | Recursive `Properties` plus unknown children; JSON object. |
| `dictionary` | Add/remove named rows; JSON object with a single scalar `ValueType`. |

Numeric properties accept `Minimum` and `Maximum`; these also validate numeric
dictionary/list entries, whose numeric editors also get sliders when both bounds
are present. Integer sliders move in whole steps; float sliders allow fractional
values. Text fields preserve invalid/out-of-range input for validation rather than
silently clamping it; use a decimal point in numeric JSON values. A one-sided bound
keeps a text field because an unbounded range has no second slider endpoint.
Dictionary `ValueType` must be `bool`, `string`, `int`
or `float`. Free-form list `ItemType` supports the same scalar types. This format
does not describe lists of objects; they remain editable as unknown raw JSON.

`RenamedFrom` is an ordered array of old keys. The current key wins, otherwise
the first existing alias wins. Save writes the current name and removes aliases.
Schema names and aliases must not overlap another field at the same object level.

Missing fields use their schema `Default`, then the corresponding default-file
value. Missing objects can be assembled from their children's defaults. Reset
uses these same defaults and affects the draft only. Resetting a whole object
replaces its draft subtree, including any unknown children; use field-level resets
if you want to retain custom children. Existing explicit null/wrong-shaped values
are shown for repair and block saving until resolved. They are never silently
converted to empty objects or replaced by defaults.

All edits, including unknown removal and resets, stay in memory until Save.
Unknown root properties have their own section; unknown nested properties stay
inside their object. Names must be nonempty and unique; unknown names cannot
collide with known keys or migration aliases. Raw JSON allows arrays, objects,
scalars and null. Empty Unknown editors and the empty root Unknown navigation item
are hidden by default. Open **Advanced options** in the sidebar and enable **Show empty unknown-property editors** to add a
property where none exists. Nonempty Unknown editors remain visible. All object
editors start expanded, and the smaller summary text still allows collapsing them.
Only display labels are shown; the secondary JSON property-name captions are removed.

**Unsaved changes: N** counts settings different from the loaded draft. Editing
one field repeatedly counts once; restoring its original value clears that change.
Nested fields count individually; a list/dictionary counts as one setting. Each
unknown addition, removal or edited value counts individually; renaming an unknown
key counts as removing the old setting and adding the new one. Invalid text still
counts without triggering validation. Saving or reloading establishes a new baseline.
Reload asks before discarding unsaved changes. This tracks user edits rather than
implicit migrations/default insertion which may occur on Save.

## Saving and operational limits

- Config, schema and defaults accept JSON comments and trailing commas. Saving
  writes formatted standard JSON while retaining the configured `.jsonc` filename.
  Comments/formatting in the player file are not retained in the new file.
  Schema and default files are never rewritten.
- The config root must be an object. Invalid configs are reported without writing.
- A missing config is displayed from the default file and created only on Save
  by the editor. A missing/malformed schema or default file produces a load error.
- The server filesystem must allow writes to the mod's config directory.
- Each replacement uses a same-directory temporary file, flushes it, then performs
  `File.Replace` without a backup. Unsupported/failed replacement does not fall back
  to overwriting the original in place.
- No backup creation, retention, or cleanup system is included. Existing backups
  from earlier versions are left alone. To restore factory settings, stop SPT and
  replace `config.jsonc` with a copy of the shipped `defaultConfig.jsonc` (adjust
  filenames for another mod). Defaults restore factory values, not personal edits.
- Another editor tab's save or a detected external change to config/schema/defaults
  rejects a stale save. Reload before retrying. This is optimistic conflict
  detection, not a filesystem lock that arbitrary external programs must obey;
  avoid editing the same file externally at the instant Save is running.
- Every load/save checks SPT's administrator policy, in addition to guarding the
  route and rendering. File paths come only from the mod's compiled settings.
- An SPT stop/restart disconnects Blazor and loses unsaved edits. The page only
  works while SPT is running. The mod does not apply saved settings until its
  normal startup loading occurs again.
- Generic schema validation cannot express all of a mod's domain rules (for
  example, a valid Tarkov item ID or a relationship between two settings). Retain
  startup validation and extend validation deliberately if needed.

## Automated and manual verification

From the mod's project root:

```powershell
dotnet run --project BlazorConfig/Checks/Checks.csproj -- config
```

The check runner links the exact core source and uses only temporary files. It
checks migrations, unknown preservation, invalid values, list/dictionary semantics,
backup-free saves, change counting, failed writes, stale sessions, and a round trip of the supplied mod's
actual schema/defaults. It does not start SPT or modify player files. Its DLL and
runtime metadata are developer outputs; `UseAppHost=false` avoids a check EXE.

Before publishing, verify in the target SPT build:

1. The DWV card opens the page and an unauthenticated/non-admin user cannot edit.
2. Section navigation, expanded nested objects, booleans, linked numeric sliders
   and fields, numeric limits, list rows, and change counts work. Check that the
   editable `wwwroot` theme is served and changes the page's colors.
3. Save one setting and inspect `config.jsonc`; no backup should be created. Confirm the running
   mod still uses its previous state; manually restart and check the new behavior.
4. Reveal empty Unknown editors and add an unknown nested object containing an array and null, save, reload, and
   confirm it survives. Remove it explicitly and confirm the deletion is saved.
5. Introduce a `RenamedFrom` key in a disposable config. Confirm it migrates only
   on a successful Save and unrelated nested data remains.
6. Open two tabs, save in the first, and confirm the second rejects its stale Save.
7. Try invalid numbers/raw JSON, then Reload; confirm validation and discard flow.
8. Check that the distribution ZIP contains the schema/defaults, `wwwroot` theme, and no editor EXE,
   check runner, preview host, or personal config.

Read `VALIDATION.md` for what was actually verified during this implementation.
