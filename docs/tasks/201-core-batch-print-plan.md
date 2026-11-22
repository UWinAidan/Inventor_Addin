# 201: Batch Print plan (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 3; open question 17)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 198

## Goal

Core orders the drawings for one PDF: the assembly's own first, then each component's, depth first in browser order, each file once.

## Scope

- `src/InventorAddin.Core/Drawing/BatchPrintPlan.cs`: input a tree of components (path, is library or Content Center) and a way to find a print (`PrintPlan.FindExistingPrint`); output the ordered drawing list, the models with no drawing, the skipped library parts, and the PDF path `<assembly name>.pdf` beside the assembly; the report text.
- Tests.

## Acceptance criteria

- [ ] Order and de-duplication as above; a sub-assembly's print comes before its children's.
- [ ] Library and Content Center parts are skipped and counted; models with no print are listed.
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
