# 205: Stock size: workbook, calculation and window model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 5; open question 19)
- **Status:** done (Core tests pass; the add-in project built on Windows, 0 errors; not run in Inventor)
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 203

## Goal

Core turns a part's box size into a standard stock size text, from a workbook, and backs a small window.

## Scope

- `src/InventorAddin.Core/Materials/StockWorkbook*.cs`: tabs How to edit, Plate (thickness, units), Round (diameter, units), Settings (allowance per side, width and length step, per units); `libraries/AWB_Stock.xlsx` starter with invented common sizes; `LibraryPaths.StockWorkbook` and a Settings row.
- `src/InventorAddin.Core/Materials/StockSize.cs`: plate thickness = smallest dimension + 2 × allowance, up to the next listed thickness; width and length + 2 × allowance, up to the next step; round: diameter from the two smaller dimensions, length from the longest. Text "PL 12 x 65 x 130", "Ø30 x 85", imperial "PL .500 x 2.50 x 5.00".
- `PartPropertyStorage.MaterialSize` (custom "Material Size"); `src/InventorAddin.Core/ViewModels/StockSizeViewModel.cs` (plate or round, the result, editable).
- Tests.

## Acceptance criteria

- [ ] Plate 10.2 x 61 x 123 mm with 1 mm allowance and a list 6, 8, 10, 12, 16: "PL 16 x 65 x 125" (or as the steps say; tests state the numbers).
- [ ] A size larger than the biggest listed thickness says so instead of guessing.
- [ ] Editing the text in the window writes the edited text.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- Number format is task 230's, reused, not copied: the formatter moved out of `SheetMetalSizeText` into `PartProperties/MaterialSizeNumber.cs` (`FromCm`, `FromUnits`), which both texts call. So imperial reads "PL 0.5 x 2.5 x 5", not the spec's ".500 x 2.50 x 5.00" (spec question 41). No unit suffix on the stock text, per the spec examples (230's text ends " mm" or " in").
- Workbook (`libraries/AWB_Stock.xlsx`, starter values invented): tabs How to edit, Plate (Thickness, Units), Round (Diameter, Units), Settings (Units, Allowance, Step); headers in row 4, columns found by name; Units read Metric, Imperial, mm or in. One allowance and one step per units system; the step serves width and length. A missing Settings row gives 1 mm / 5 mm (metric) or 0.04 in / 0.25 in (imperial).
- Plate: sorted box, thickness = smallest + 2 x allowance up to the next listed, width = middle and length = largest, each + 2 x allowance up to the next step. Round: diameter = the larger of the two smaller sides + 2 x allowance up to the next listed diameter; length = largest + 2 x allowance up to the next step. A value within 0.0001 over a listed size or step rounds to it, so float noise does not jump a step.
- Too big for the list (or an empty list): no text, a problem saying so; the window starts blank and the user may type a size.
- Settings: `AddinSettings.StockWorkbookPath`, `LibraryPaths.StockWorkbook`, "Stock data" row in `SettingsViewModel` and the Settings window (Browse button).

- Review fixes: the Settings heading names a chosen stock workbook; width, length and diameter never round to less than one step; tests for just-over cases and a duplicate Settings units row.

## API for task 206

- Read: `StockWorkbookReader.Read(LibraryPaths.StockWorkbook(settings, addinFolder))` gives `StockWorkbookReadResult` (`.Workbook`, `.Problems`, `.CouldNotRead`). `StockWorkbookMessages.AddinFolderUnknown(reason)` for when the add-in folder cannot be found.
- Window model: `new StockSizeViewModel(boxCm, printUnits, workbook, result.Problems, storedMaterialSize)`; `boxCm` is the three lengths of `OrientedMinimumRangeBox` in cm, any order. Bind `IsPlate`/`IsRound`, `Text` (editable), `Message`, `ResultProblem`, `WorkbookProblemsText`, `CanWrite`. On OK call `WriteFor()`: a `PropertyWrite` for Material Size, or null when blank or unchanged.
- Without the window: `StockSize.Plate/Round(boxCm, units, workbook)` gives `StockSizeResult`.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors (14 warnings, none from this task). `dotnet test tests/InventorAddin.Core.Tests`: 5809 to 5811 passed after the review fixes (5 new tests); the 0 to 4 failures per run were only the known flaky RenamePlan, SaveAsReplacer, NewPartCreator and SaveAsCopyCreator tests; earlier runs failed only the known flaky RenamePlan, SaveAsReplacer, NewPartCreator and SaveAsCopyCreator tests, plus `SettingsStoreTests.Equality_ComparesEverySetting`, which I fixed by adding the new setting to it. 39 new tests in `tests/InventorAddin.Core.Tests/Materials/StockSizeTests.cs`.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: the new Stock data row in `UI/SettingsWindow.xaml(.cs)` and the `AWB_Stock.xlsx` copy in `InventorAddin.csproj`.
- **Inventor API members not confirmed:** none; no Inventor API is used.
- **Starter workbook:** generated with a one-off script (not kept); shared string count 72, XML declarations and a minimal styles part, as AWB_Notes.xlsx. Check that Excel opens it.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open Settings. Under Material and finish, a "Stock data" row with Browse sits under "Notes data". Type a path ending in .csv: the window refuses it. Clear it and save.
  2. Open `libraries/AWB_Stock.xlsx` in Excel: tabs How to edit, Plate, Round, Settings open without a repair prompt and read clearly.
  (The command itself is task 206.)

## Follow-ups

- Task 206 should skip or agree with sheet metal parts, which get their Material Size from task 230 (spec 05 question 34 says the computed value replaces a hand-typed one).
- Task 206 maps the part's `UnitsSystem` to `PrintUnits` with `PrintPlan.PrintUnitsFor`.
- Question 41 added to spec 05 (imperial format and unit suffix).
