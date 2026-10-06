# 002: Settings model and JSON store

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Settings)
- **Status:** done
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

- [x] `AddinSettings` has `SchemaVersion` (int, current value 1) and `ShowDeveloperTools` (bool, default `false`)
- [x] `SettingsStore` takes the settings folder in its constructor and uses `settings.json` in it. It does not read environment variables or `%APPDATA%` itself
- [x] `Load()` on a missing file returns defaults and does not create the file
- [x] `Load()` on a file that is not valid JSON returns defaults, renames the bad file to `settings.json.bad` (overwriting an older one), and reports a warning. It does not throw
- [x] `Load()` ignores unknown properties and fills missing properties with defaults
- [x] `Save()` creates the folder if needed and writes through a temporary file in the same folder that then replaces `settings.json`, so a crash mid-write leaves the old file intact
- [x] Saved JSON is indented and uses camelCase property names
- [x] Round trip: `Save(x)` then `Load()` gives equal values
- [x] Tests use a temporary folder per test and clean it up
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.Settings`. Use `System.Text.Json`, no new packages.
- Keep the serializer options private to the store. Do not reuse `ModelJson.Options`: model dumps and settings are different files with different needs.
- Warnings: return them rather than logging (the log arrives in task 003 and Core must not depend on how the add-in reports). A simple shape such as `SettingsLoadResult { AddinSettings Settings; IReadOnlyList<string> Warnings }` is fine.
- `ShowDeveloperTools` defaults to `false` so end users do not see the developer buttons. Aidan turns it on in the Settings window (task 008) or by editing the file.
- `SchemaVersion` is there for future migrations. Do not write migration code now; just read and write the number.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 34 passed, 0 failed, 0 skipped (23 of them new in `SettingsStoreTests`). Run in a cloud session on Linux.
- **Not compiled (changed under `src/InventorAddin`):** none. No add-in files were changed.
- **Inventor API members not confirmed:** none. No Inventor API used.
- **Manual checklist for Inventor:** none, Core only.

Notes on behaviour the criteria left open:

- `AddinSettings` is a `sealed record` with settable properties, so round-trip equality is value equality and new fields need no test changes to stay comparable.
- `Load()` treats JSON that parses but is not a settings object (`null`, an array, a wrong value type such as `"showDeveloperTools": "yes"`) the same as invalid JSON: defaults, rename to `.bad`, one warning. Property names are read case-insensitively so a hand-edited `ShowDeveloperTools` still loads.
- If the file exists but cannot be read (locked, no access), `Load()` returns defaults with a warning and leaves the file alone. If renaming a bad file fails, it still returns defaults with a warning.
- `Save()` writes to `settings.json.<guid>.tmp` in the same folder, flushes it to disk, then `File.Move(..., overwrite: true)` onto `settings.json`. It throws on I/O failure (the caller reports it) and deletes the temp file in that case. `SchemaVersion` is written as given, not forced to the current value.
- The "crash mid-write leaves the old file intact" guarantee comes from the temp-file-then-replace design. The tests check that no temp file is left behind and that a failed replace cleans up; they cannot simulate a process crash. The failed-replace test accepts `IOException` (what Linux throws) or `UnauthorizedAccessException` (what Windows throws for a move onto a directory). Only the Linux case has been run.

## Follow-ups

- Task 005: the add-in should report `SettingsLoadResult.Warnings` through the log from task 003, and decide how to surface a `Save()` exception to the user.
