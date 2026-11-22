# 205: Stock size: workbook, calculation and window model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 5; open question 19)
- **Status:** todo
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

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
