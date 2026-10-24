# 111: Hole workbook: Spotface and Taper Tapped tabs

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The workbook", "Taper tapped holes", "Types and seats")
- **Status:** todo
- **Depends on:** 106
- **Needs:** cloud or windows (Core and the starter workbook)
- **Parallel-safe with:** 110, 119

## Goal

The workbook reader knows two new tabs, Spotface and Taper Tapped (Aidan, 2026-10-24), and the starter workbook has them, so tasks 112 and 114 can offer spotfaces and taper tapped holes.

## Scope

- `src/InventorAddin.Core/Holes/HoleWorkbookReader.cs`, `HoleWorkbookRows.cs`, `HoleWorkbook.cs`, `HoleWorkbookMessages.cs`: the two tabs, laid out like the others (title row 1, note row 2, headers row 4).
  - **Spotface**: Units, Size, Spotface dia, Spotface depth, Show (Default, 2026-10-24).
  - **Taper Tapped**: Units, Size, Thread type, Tap drill, Default thread depth, Default drill depth, Show, like the Tapped tab (Default, 2026-10-24). The default depth columns may be formulas; a blank cell is worked out from the Settings tab's multipliers, as for Tapped.
- **Both tabs are optional**: a workbook without them (Aidan's own copy until he adds them) loads with no problem for a missing tab; Spotface rows are then none, and Taper Tapped has no sizes. A tab that is present with a missing header is reported as today.
- `libraries/AWB_Holes.xlsx` (the starter): add the two tabs with **invented** starter rows (`CLAUDE.md`: no copied data): a handful of NPT sizes as Imperial rows and BSPT sizes as Metric rows, and Spotface rows for the sizes the Counterbore tab has. Update the How to edit tab's text for them. Follow how tasks 097 and 106 edited the package (shared strings, the autofilter note in `TODO.md`).
- `tests/InventorAddin.Core.Tests/Holes/`: reader tests for both tabs, a workbook without them, bad values, `Show = N`, and the starter workbook loading with no problems.

Out of scope:

- Using the rows in the window (112, 114).

## Acceptance criteria

- [ ] Both tabs are read into typed rows in the row's units, as `decimal`, like the Tapped and Counterbore rows.
- [ ] A workbook without either tab loads with no problem reported for it.
- [ ] The starter workbook opens in Excel and loads in Core with no problems.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
