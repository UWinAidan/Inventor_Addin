# 186: Print template paths in Settings

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5" (a starter template in the repo plus a Settings path to Aidan's copy))
- **Status:** todo
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

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
