# 186: Print template paths in Settings

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5" (a starter template in the repo plus a Settings path to Aidan's copy))
- **Status:** done
- **Depends on:** none
- **Needs:** cloud (Core); add-in XAML not compiled
- **Parallel-safe with:** 185

## Goal

Settings carries a path to Aidan's own metric and imperial print templates; blank means the starter template shipped with the add-in, like the material library and hole workbook.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs`: `PrintTemplateMetricPath`, `PrintTemplateImperialPath` (blank by default, null stored as blank).
- `src/InventorAddin.Core/Materials/LibraryPaths.cs`: `PrintTemplate(settings, units, addinFolder)` resolving to `libraries/Templates/AWB Metric.idw` or `AWB Imperial.idw` when blank; constants for the folder, names and the `.idw` extension.
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs` and `SettingsSummaries.cs`: two rows in the libraries section, the same as the hole workbook row (browse, clear, summary).
- `src/InventorAddin/UI/SettingsWindow.xaml(.cs)`: the two rows (browse filter `*.idw`).
- Tests for the settings round trip, `LibraryPaths.PrintTemplate`, and the view-model rows.

Out of scope: Create Print (task 188); the template files themselves (task 187).

## Acceptance criteria

- [ ] Blank resolves to the starter file beside the add-in; a set path is trimmed and used as it is; whether it exists is not checked here.
- [ ] The settings file round-trips both paths; an old settings file without them loads with blanks.
- [ ] The Settings window shows both rows in the libraries section with the same look and behaviour as the hole workbook row.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Follow the hole workbook path end to end (`HoleWorkbookPath`, `LibraryPaths.HoleWorkbook`, `SettingsViewModel`, `SettingsWindow.xaml`).
- The build copies `libraries/` beside the add-in; check that a `Templates` subfolder is copied too (the csproj item), and add it if not.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran (Windows):** `dotnet test tests/InventorAddin.Core.Tests`: 5046 passed, 0 failed (includes the new print template tests and `Equality_ComparesEverySetting`, extended for the two new settings). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 8 warnings (existing CS8600 warnings elsewhere; none from this task's files). The add-in project, including `SettingsWindow.xaml(.cs)` and the csproj item, was compiled on Windows. Not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; everything changed there built on Windows. The XAML layout was not looked at on screen.
- **Inventor API members not confirmed:** none used; this task touches no Inventor API.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open AWB Addin Settings. Expand "Material and finish": under Finish list there are two new rows, "Metric print template" and "Imperial print template", each with a box, a Browse button and a muted help line, looking the same as the Finish list row. Box borders are not cut off at 150% display scale.
  2. Click Browse on the metric row: a file dialog opens filtered to Inventor drawings (*.idw). Pick any .idw; its path appears in the box and Save enables. Do the same for the imperial row.
  3. Type a relative name such as `Metric.idw`: the status line names "Metric print template" and Save is disabled. Type `D:\x.ipt`: it says the file must end in .idw.
  4. Type a full path to an .idw that does not exist and click Save: refused with "Metric print template: '...' was not found. Choose an existing file, or leave it blank to use the starter file."
  5. Choose real .idw files for both, Save, reopen Settings: both paths are still there and the collapsed section summary ends with the two file names. Clear both boxes and Save: the summary returns to "starter library, starter finish list".
  6. Once task 187 adds `libraries/Templates/*.idw`, check a build puts them in `libraries\Templates` beside the add-in.

## Follow-ups

- The print template rows sit in the "Material and finish" section (the libraries section the task names), so its heading no longer describes them fully. Rename to "Libraries" if Aidan wants; not done.
- `SettingsSummaries.MaterialFinish` names a print template only when one is set (blank adds nothing), so existing summaries and tests are unchanged. If Aidan wants "starter print templates" shown when blank, as the hole data row does, it is a small change.
- The csproj copies `libraries\Templates\*.idw` with a wildcard so it builds before task 187 adds the files; until then the starter path resolves to a file that does not exist.
