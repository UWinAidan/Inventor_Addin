# 086: Settings: Holes section and the shipped hole workbook

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Settings", "The workbook"); `docs/features/08-window-style.md` (Settings)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build (Settings XAML)
- **Parallel-safe with:** 082, 083, 084, 085, 087

## Goal

Settings gets a collapsible "Holes" section with the hole workbook path and the "Use Inventor's Hole command" switch, and the starter workbook ships beside the DLL like the finish list.

## Scope

- Core: `AddinSettings` (`HoleWorkbookPath`, blank uses the starter; `UseInventorHoleCommand`, default false), `LibraryPaths` (starter `libraries\AWB_Holes.xlsx`), `SettingsViewModel` (rows, validation like the finish list: full path, ends in `.xlsx`, existence checked on Save only for a changed path), `SettingsSummaries` (the Holes summary), tests
- Add-in: `UI/SettingsWindow.xaml` (a `HolesSection` CollapsibleSection after Material and finish: Hole data with Browse for `*.xlsx` and help text; the checkbox with a note), `SettingsWindow.xaml.cs` (Browse), `InventorAddin.csproj` (copy `libraries\AWB_Holes.xlsx` to the output)
- Spec 08's Settings section

## Acceptance criteria

- [ ] Settings shows a Holes section with the two rows; its collapsed summary reads like "starter hole data, AWB Hole" or "AWB_Holes.xlsx, Inventor's Hole".
- [ ] A changed path that does not exist is refused on Save with the finish list's wording; a blank path uses the starter.
- [ ] The workbook is copied to `libraries\` in the build output and deployed.
- [ ] Access keys stay unique across the window.
- [ ] Settings older files without the new values load with the defaults.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- Task 070 is the model (Material library and Finish list rows).
- The switch only stores the choice here; task 091 acts on it.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
