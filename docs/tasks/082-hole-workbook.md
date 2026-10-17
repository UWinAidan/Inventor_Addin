# 082: Hole workbook: reader and models (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The workbook", "Tapped holes", "Blind holes", "Dowel and precision holes")
- **Status:** todo
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

- [ ] An `.xlsx` reader using only `System.IO.Compression` and `System.Xml`: finds sheets by **tab name** through `xl/workbook.xml` and its relationships, reads shared strings, inline strings, numbers and booleans, and uses each cell's **cached value** (formula cells give the value Excel last showed). Cells are found by reference (`A4`), so blank cells in between are handled.
- [ ] Each data tab is read by **header text** in row 4 (title row 1, note row 2), rows from 5 to the first fully blank row: Settings (Setting, Value, R, G, B by setting name), Clearance, Counterbore, Countersink, Tapped, Dowel, Precision Tol, Drill Point, with the headers exactly as in the starter workbook. "How to edit" is not read. Headers match ignoring case and surrounding spaces; nothing is renamed.
- [ ] Row models carry units in the field names (`NominalMm` or `Nominal` with a `Units` value; pick one and say so) and keep the row's units (Metric mm, Imperial inches). `Show = N` rows are kept but marked hidden; the window leaves them out.
- [ ] Settings: default units, thread depth multiplier, drill extra depth multiplier, and the three fit colours as RGB.
- [ ] Tapped: a thread designation **as Inventor's thread table names it**: metric `M6` with pitch `1.0` gives `M6x1` (pitch written without trailing zeros: `M8x1.25`, `M10x1.5`); imperial drops a leading `#` (`#10-32 UNF` gives `10-32 UNF`); the thread type `ISO Metric profile` or `ANSI Unified Screw Threads` by units. Default thread and drill depths: the cell's value when present, else worked out from Settings (`round(nominal x thread multiplier, 3)`, drill `round(thread + nominal x drill multiplier, 3)`).
- [ ] Precision Tol: fit (Slip, Normal, Press), name, type ISO (code) or Limits (upper, lower), default flag, note.
- [ ] Drill Point: name, angle (blank for Flat), default flag, show.
- [ ] Problems are collected, not thrown: a missing tab or header, a value that is not a number, a Units value that is neither Metric nor Imperial, an unknown fit or type. Each names the tab, row number and header, in the finish list's wording style (`FinishListCsv`). A row with a problem is left out; the rest load. A missing or unreadable file gives one problem and an empty workbook.
- [ ] Tests build small workbooks in memory (a `ZipArchive` with the needed XML parts; invented values) and also read the committed starter `libraries/AWB_Holes.xlsx` and check a few known rows (M6 clearance 6.4/6.6/7.0, M6x1 tap drill 5, H7 Normal row default).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The starter workbook's layout is in spec 09 and in the workbook itself; read it (unzip it) before writing the reader. It was written by openpyxl and saved by LibreOffice, so expect both shared strings and inline strings.
- Follow `Materials/FinishListCsv.cs` and `Materials/CsvTable.cs` for the problem wording and the load-result shape.
- Spike 081 found Inventor's thread names (see its Results, question 1).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
