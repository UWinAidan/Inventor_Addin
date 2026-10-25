# 125: A dowel seat starts at the last values used for its size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"; question 14)
- **Status:** todo
- **Depends on:** 106
- **Needs:** cloud or windows (Core first, then a look at the add-in's save path)
- **Parallel-safe with:** 122, 123

## Goal

PR #29 check 106-5 failed (Aidan, 2026-10-25): after OK on a 6 mm dowel with a counterbore of 9 x `d0/8` (= 3), a new 6 mm dowel counterbore started at 7 / 3 (the workbook values), not 9 / 3. An 8 mm dowel did start at its last values (9 / 4). The last values per units and dowel size (Aidan, question 14) must win over the workbook columns.

## Scope

- `src/InventorAddin.Core/Holes/HoleLastDowelSeats.cs`, `ViewModels/AwbHoleViewModel.cs`, and where the add-in records the last values on OK (`AwbHoleCommand` or the window code). Find which step loses the 6 mm values: not recorded (for example because the depth was an expression, or the key for the size differs between record and lookup, such as `6` and `6.0`), recorded but not saved to settings, or looked up after the workbook column has already filled the boxes.
- Fix and add Core tests that reproduce it: a counterbore depth given as an expression, record, then a new window for the same size starts at the recorded values; the expression is kept as the depth (task 100), the diameter as a number.

## Acceptance criteria

- [ ] The 106-5 case starts at 9 / `d0/8` for the next 6 mm dowel.
- [ ] The workbook columns are used only when there are no last values for that units and size.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows if the add-in changed.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. 6 mm dowel, Counterbore 9 x `d0/8`, OK. New AWB Hole, 6 mm dowel, Counterbore: starts at 9 and `d0/8`.
  2. Restart Inventor and repeat: still 9 / `d0/8`.
  3. 106 checks 6, 7 and 10, not tested yet, at the same time.

## Follow-ups
