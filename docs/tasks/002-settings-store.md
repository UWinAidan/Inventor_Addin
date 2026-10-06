# 002: Settings model and JSON store

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Settings)
- **Status:** todo
- **Depends on:** 001
- **Needs:** cloud
- **Parallel-safe with:** 003, 004

## Goal

Core can load and save per-user settings as JSON. Later features add their own fields to the model without touching the load/save logic.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs` (new)
- `src/InventorAddin.Core/Settings/SettingsStore.cs` (new)
- `tests/InventorAddin.Core.Tests/Settings/SettingsStoreTests.cs` (new)

Out of scope:

- Anything under `src/InventorAddin` (task 005 wires the store into the add-in)
- Any setting other than the ones listed below. Spec 01 open question 3 decides the rest.
- A settings window (task 008)

## Acceptance criteria

- [ ] `AddinSettings` has `SchemaVersion` (int, current value 1) and `ShowDeveloperTools` (bool, default `false`)
- [ ] `SettingsStore` takes the settings folder in its constructor and uses `settings.json` in it. It does not read environment variables or `%APPDATA%` itself
- [ ] `Load()` on a missing file returns defaults and does not create the file
- [ ] `Load()` on a file that is not valid JSON returns defaults, renames the bad file to `settings.json.bad` (overwriting an older one), and reports a warning. It does not throw
- [ ] `Load()` ignores unknown properties and fills missing properties with defaults
- [ ] `Save()` creates the folder if needed and writes through a temporary file in the same folder that then replaces `settings.json`, so a crash mid-write leaves the old file intact
- [ ] Saved JSON is indented and uses camelCase property names
- [ ] Round trip: `Save(x)` then `Load()` gives equal values
- [ ] Tests use a temporary folder per test and clean it up
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.Settings`. Use `System.Text.Json`, no new packages.
- Keep the serializer options private to the store. Do not reuse `ModelJson.Options`: model dumps and settings are different files with different needs.
- Warnings: return them rather than logging (the log arrives in task 003 and Core must not depend on how the add-in reports). A simple shape such as `SettingsLoadResult { AddinSettings Settings; IReadOnlyList<string> Warnings }` is fine.
- `ShowDeveloperTools` defaults to `false` so end users do not see the developer buttons. Aidan turns it on in the Settings window (task 008) or by editing the file.
- `SchemaVersion` is there for future migrations. Do not write migration code now; just read and write the number.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none, Core only.

## Follow-ups
