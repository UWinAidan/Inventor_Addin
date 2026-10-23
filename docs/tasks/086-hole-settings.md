# 086: Settings: Holes section and the shipped hole workbook

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Settings", "The workbook"); `docs/features/08-window-style.md` (Settings)
- **Status:** done
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

- [x] Settings shows a Holes section with the two rows; its collapsed summary reads like "starter hole data, AWB Hole" or "AWB_Holes.xlsx, Inventor's Hole".
- [x] A changed path that does not exist is refused on Save with the finish list's wording; a blank path uses the starter.
- [x] The workbook is copied to `libraries\` in the build output and deployed.
- [x] Access keys stay unique across the window.
- [x] Settings older files without the new values load with the defaults.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- The reader's missing or unreadable workbook messages (082) do not say whether the file is the starter one; word them as `MaterialLibraryMessages` does (task 082 follow-up).

- Task 070 is the model (Material library and Finish list rows).
- The switch only stores the choice here; task 091 acts on it.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows), `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests` (2902 passed, 0 failed; the count includes task 084's tests, which were being written in the same tree); `dotnet build InventorAddin.slnx -p:DeployToInventor=false` (succeeded, 0 errors; the only warnings are the existing CS8600s in other commands). `src/InventorAddin/bin/Debug/net8.0-windows/libraries/AWB_Holes.xlsx` is in the build output, so the deploy step (which copies the whole output folder) deploys it. Not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none. `InventorAddin.csproj`, `UI/SettingsWindow.xaml` and `UI/SettingsWindow.xaml.cs` were built on Windows.
- **Inventor API members not confirmed:** none; no Inventor API is used. The Browse button reuses the existing `PickFile` and `Microsoft.Win32.OpenFileDialog`.
- **Also done (task 082 follow-up, from the notes):** `HoleWorkbookMessages` (Core, `Holes/`) words a missing or unreadable workbook as `MaterialLibraryMessages` words the finish list's, naming the starter or the Settings workbook, and `HoleWorkbookReader.Read(LibraryPath)` uses them. `Read(string)` keeps its wording. Callers (085, 088) should resolve the path with `LibraryPaths.HoleWorkbook` and read with `Read(LibraryPath)`.
- **Manual checklist for Inventor:**
  1. Close Inventor, build `InventorAddin.slnx` (deploys), start Inventor. Check that `%APPDATA%\Autodesk\Inventor 2026\Addins\InventorAddin\libraries\AWB_Holes.xlsx` exists.
  2. AWB Addin tab, Settings. A **Holes** section sits between Material and finish and Developer, with Hole data (box, Browse...), a help line, the checkbox "Use Inventor's Hole command" (unticked) and a note under it.
  3. Collapse Holes: the summary reads "starter hole data, AWB Hole".
  4. Click Browse... beside Hole data: the picker shows only `*.xlsx` files. Pick a copy of `AWB_Holes.xlsx` and tick the checkbox. The collapsed summary reads "AWB_Holes.xlsx, Inventor's Hole". Save; reopen Settings and check both values were kept.
  5. Type `D:\nothere\holes.xlsx` (a path that does not exist) and Save: the window stays open with "Hole data: 'D:\nothere\holes.xlsx' was not found. Choose an existing file, or leave it blank to use the starter file."
  6. Type `holes.xlsx`: the error line reads "Hole data: enter a full path to the file, such as D:\Libraries\AWB_Holes.xlsx." and Save is disabled. Type a path ending `.xls`: "Hole data must be a file ending in .xlsx."
  7. Clear the box and Save: it saves (blank uses the starter).
  8. Press Alt in the window: H moves to the Hole data box, I toggles the checkbox, and no other control shares H or I.
  9. Ticking the checkbox changes nothing in the ribbon yet: task 091 acts on it.

## Follow-ups

- The note under the checkbox describes what task 091 will do ("Takes effect when you save"); until 091 lands the switch is stored only. 091 should check the wording still matches.
- `MaterialLibraryMessages.AddinFolderUnknown` says "the material library and finish list cannot be read"; if AWB Hole uses it when the add-in folder is unknown, it needs a hole workbook variant or wider wording.
