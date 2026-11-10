# 082: Hole workbook: reader and models (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The workbook", "Tapped holes", "Blind holes", "Dowel and precision holes")
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 083, 086, 087

## Goal

Core reads `libraries/AWB_Holes.xlsx` into typed models, without Excel, and reports every problem the way the finish list does. Every option in the AWB Hole window comes from these models.

## Scope

- `src/InventorAddin.Core/Holes/` (new folder): an `.xlsx` package reader, the row models, `HoleWorkbook` (all tabs) and its load result with problems
- `tests/InventorAddin.Core.Tests/Holes/`

Out of scope: the window, ISO 286 limits (083), Settings (086).

## Acceptance criteria

- [x] An `.xlsx` reader using only `System.IO.Compression` and `System.Xml`: finds sheets by **tab name** through `xl/workbook.xml` and its relationships, reads shared strings, inline strings, numbers and booleans, and uses each cell's **cached value** (formula cells give the value Excel last showed). Cells are found by reference (`A4`), so blank cells in between are handled.
- [x] Each data tab is read by **header text** in row 4 (title row 1, note row 2), rows from 5 to the first fully blank row: Settings (Setting, Value, R, G, B by setting name), Clearance, Counterbore, Countersink, Tapped, Dowel, Precision Tol, Drill Point, with the headers exactly as in the starter workbook. "How to edit" is not read. Headers match ignoring case and surrounding spaces; nothing is renamed.
- [x] Row models carry units in the field names (`NominalMm` or `Nominal` with a `Units` value; pick one and say so) and keep the row's units (Metric mm, Imperial inches). `Show = N` rows are kept but marked hidden; the window leaves them out.
- [x] Settings: default units, thread depth multiplier, drill extra depth multiplier, and the three fit colours as RGB.
- [x] Tapped: a thread designation **as Inventor's thread table names it**: metric `M6` with pitch `1.0` gives `M6x1` (pitch written without trailing zeros: `M8x1.25`, `M10x1.5`); imperial drops a leading `#` (`#10-32 UNF` gives `10-32 UNF`); the thread type `ISO Metric profile` or `ANSI Unified Screw Threads` by units. Default thread and drill depths: the cell's value when present, else worked out from Settings (`round(nominal x thread multiplier, 3)`, drill `round(thread + nominal x drill multiplier, 3)`).
- [x] Precision Tol: fit (Slip, Normal, Press), name, type ISO (code) or Limits (upper, lower), default flag, note.
- [x] Drill Point: name, angle (blank for Flat), default flag, show.
- [x] Problems are collected, not thrown: a missing tab or header, a value that is not a number, a Units value that is neither Metric nor Imperial, an unknown fit or type. Each names the tab, row number and header, in the finish list's wording style (`FinishListCsv`). A row with a problem is left out; the rest load. A missing or unreadable file gives one problem and an empty workbook.
- [x] Tests build small workbooks in memory (a `ZipArchive` with the needed XML parts; invented values) and also read the committed starter `libraries/AWB_Holes.xlsx` and check a few known rows (M6 clearance 6.4/6.6/7.0, M6x1 tap drill 5, H7 Normal row default).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The starter workbook's layout is in spec 09 and in the workbook itself; read it (unzip it) before writing the reader. It was written by openpyxl and saved by LibreOffice, so expect both shared strings and inline strings.
- Follow `Materials/FinishListCsv.cs` and `Materials/CsvTable.cs` for the problem wording and the load-result shape.
- Spike 081 found Inventor's thread names (see its Results, question 1).

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows), `dotnet build src/InventorAddin.Core`: succeeded, no warnings. `dotnet test tests/InventorAddin.Core.Tests`: 2803 passed, 0 failed (59 of them in `Holes/`: `XlsxPackageTests`, `HoleWorkbookReaderTests`), rerun after the review fix. The starter `libraries/AWB_Holes.xlsx` reads with no problems.
- **Review fix (Excel's 17-digit numbers):** Excel writes a number cell with up to 17 significant digits (a typed 6.6 is stored as `6.5999999999999996`). `XlsxPackage` now rounds number cells through a double to 15 significant digits (`G15`, what Excel shows), so 6.6, 0.45 and 0.285 read as typed and a numeric pitch gives `M2.5x0.45`. Text cells are kept as written. Tested with raw cell values. A cell showing an Excel error (`#DIV/0!` and so on) is reported as "Default thread depth shows the error #DIV/0!; check its formula; row left out."
- **Not compiled (changed under `src/InventorAddin`):** none; this task changes Core and tests only.
- **Inventor API members not confirmed:** none used. The thread type names (`ISO Metric profile`, `ANSI Unified Screw Threads`) and the designation rules come from spike 081's results.
- **Manual checklist for Inventor:** nothing to check in Inventor yet; the reader has no caller until the AWB Hole window (085, 090).
- **Choices made (for the parent to log as Default if it agrees):**
  - Row lengths keep the row's units with no unit suffix (`Nominal`, `Close`, `CboreDiameter`, `TapDrill`) plus a `Units` value (Metric mm, Imperial inches); angles are `AngleDeg`. Values are `decimal`, as typed, so `round(..., 3)` matches Excel (half away from zero) and the pitch prints without trailing zeros.
  - **Settings headers are in row 3.** The starter workbook's Settings tab has its headers in **row 3**, not row 4 (no note row), unlike the brief's row-4 layout. The reader finds the Settings header row by its "Setting" cell instead of assuming row 4; the data tabs use row 4 as the brief says.
  - A Settings value that is missing or unreadable is reported and the starter value is used (Metric, 1.5, 0.5, the starter RGB), so one bad setting does not lose the workbook. Rows on data tabs with problems are left out, as the brief says.
  - A missing header leaves that whole tab out with one problem, as the finish list does for a missing column.
  - A blank Show cell shows the row; a blank Default cell is not default. Y, N, Yes, No and TRUE/FALSE (boolean cells) are accepted, ignoring case.
  - A metric thread already written with a pitch (`M8x1`) is kept as written; otherwise the Pitch / TPI value is added.
  - Number checks: sizes, diameters, depths, pitch and tap drill must be greater than 0; angles more than 0 and less than 180; RGB whole numbers 0 to 255; Upper and Lower any number, Upper not less than Lower. Numbers stored as text (the Pitch / TPI column is text in the starter) are read as numbers.

## Follow-ups

- Duplicate rows (the same Units and Size twice on a tab, or two Default = Y rows for one units and fit on Precision Tol or Drill Point) are not reported. The view-model (085) should decide which wins, or a later change can report them.
- Messages for a missing or unreadable workbook name the path but not whether it is the starter; task 086 may want wording like `MaterialLibraryMessages` (starter: "Reinstall the add-in, or set ... in Settings").
- A Counterbore or Countersink row whose Size has no Clearance row for its units is not checked here; task 084 checks "a size missing from the workbook".
