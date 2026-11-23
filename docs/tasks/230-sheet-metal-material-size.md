# 230: Sheet metal Material Size: thickness x flat pattern size

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/03-material-and-finish.md`
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 192
- **Needs:** Core, plus reading thickness and the flat pattern on Windows
- **Parallel-safe with:** none

## Goal

For sheet metal, MATERIAL SIZE shows the thickness and the flat pattern size, for example "1.5 x 60 x 120 mm" (inches on imperial parts).

## Scope

- Core: the text rule (thickness first, then the flat pattern's two sides smaller first, part units, number format as the stock size text), tests.
- Where Material Size is written today for parts (find the writer); sheet metal parts with a flat pattern get this text, written in Create Print with the other backfills. Sheet metal without a flat pattern: leave the property alone (Default).

## Acceptance criteria

- [x] A sheet metal part prints "T x W x L" in its units.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors, 13 warnings (none from this task). `dotnet test tests/InventorAddin.Core.Tests`: 5391 passed, 0 failed (no flaky reruns needed).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `Extraction/SheetMetalSizeReader.cs` (new) and `Extraction/FinishSpecBackfiller.cs` (calls it from `WritesFor`).
- **Inventor API members not confirmed:** none new. `SheetMetalComponentDefinition.HasFlatPattern`, `.Thickness.Value`, `.FlatPattern.Length` and `.Width` are used the same way in `InitialViewsPlacer.Read` and `PartExtractor`.
- **Manual checklist for Inventor:**
  1. Open a saved metric sheet metal part with a flat pattern (say 1.5 mm thick, flat pattern 60 x 120 mm). Clear any Material Size custom property.
  2. Click Create Print and confirm Initial Views. Open the part's iProperties, Custom tab: Material Size reads "1.5 x 60 x 120 mm" (thickness, then the smaller flat side, then the larger). The log says "Filled in Material Size".
  3. The new print's MATERIAL SIZE cell shows the same text.
  4. Repeat on an imperial sheet metal part: the text is in inches with " in" (for example "0.063 x 2.362 x 4.724 in").
  5. Open a sheet metal part with no flat pattern and a plain part: Create Print leaves Material Size alone (unchanged or absent).
  6. Edit the thickness, run Create Print again: the property is refreshed to the new text.

Notes: the size text is written by `FinishSpecBackfiller.WritesFor`, which Material and Finish and Mass Update also call, so those write it too, not only Create Print.

## Follow-ups

Things noticed but not done.

- Defaults chosen (log in DECISIONS.md as "Default" if kept): numbers are 2 decimals in mm and 3 in inches with trailing zeros dropped, unit suffix " mm" or " in"; a stored Material Size that differs is overwritten (a refresh, like Mass), so a hand-typed value on a sheet metal part with a flat pattern is replaced. Ask Aidan whether a hand-typed value should be kept.
- The future Stock Size command (spec 05 question 19) will also write Material Size; it should skip or agree with sheet metal parts.
