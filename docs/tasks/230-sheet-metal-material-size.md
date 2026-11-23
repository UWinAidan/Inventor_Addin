# 230: Sheet metal Material Size: thickness x flat pattern size

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/03-material-and-finish.md`
- **Status:** todo
- **Depends on:** 192
- **Needs:** Core, plus reading thickness and the flat pattern on Windows
- **Parallel-safe with:** none

## Goal

For sheet metal, MATERIAL SIZE shows the thickness and the flat pattern size, for example "1.5 x 60 x 120 mm" (inches on imperial parts).

## Scope

- Core: the text rule (thickness first, then the flat pattern's two sides smaller first, part units, number format as the stock size text), tests.
- Where Material Size is written today for parts (find the writer); sheet metal parts with a flat pattern get this text, written in Create Print with the other backfills. Sheet metal without a flat pattern: leave the property alone (Default).

## Acceptance criteria

- [ ] A sheet metal part prints "T x W x L" in its units.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
