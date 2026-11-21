# 181: Revert 180: descriptions use exactly their table's decimals

- **Milestone:** M4 part 2 (from Aidan's check of 177 to 180, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` (open question 14)
- **Status:** done
- **Depends on:** 180
- **Needs:** Core only
- **Parallel-safe with:** none (same files as 180)

## Goal

Aidan (2026-11-20): the "diameters keep extra decimals" rule in 180 was never his decision. In the Tap and Clearance table every number in DESCRIPTION uses the table's decimals, like XDIM and YDIM: in millimetres 1 decimal, so Ø2.25 prints as Ø2.3. The Precision table keeps 2. A hole that needs x.xx belongs in the Precision table; the wizard's Swap moves it there.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableDescription.cs` and `tests/InventorAddin.Core.Tests/HoleTable/HoleTableDescriptionTests.cs`: restored to their state before 180 (`Len` uses `Number(value, decimals)`; the `Length` helper and 180's tests removed; `Inch_tapped` expects `0.16` again).
- Spec 04 question 14 reworded. `DECISIONS.md`: 180's entry corrected (it was not Aidan's) and Aidan's rule logged.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 4964 passed, 1 failed (`SaveAsReplacerTests`, a known flaky test: temp folder not reachable; unrelated). 4965 tests, 12 fewer than 180's 4977. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 8 warnings (existing).
- **Not compiled (changed under `src/InventorAddin`):** none changed.
- **Inventor API members not confirmed:** none (Core only).
- **Manual checklist for Inventor:**
  1. Update the drawing's tables (a stored table drawn by 180 may first report "descriptions changed"; expected once). D1 reads `Ø2.3 THRU`. Swap a hole to the Precision table and Update: its description shows 2 decimals.

## Follow-ups

None.
