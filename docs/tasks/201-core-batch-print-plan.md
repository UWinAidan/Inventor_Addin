# 201: Batch Print plan (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 3; open question 17)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 198

## Goal

Core orders the drawings for one PDF: the assembly's own first, then each component's, depth first in browser order, each file once.

## Scope

- `src/InventorAddin.Core/Drawing/BatchPrintPlan.cs`: input a tree of components (path, is library or Content Center) and a way to find a print (`PrintPlan.FindExistingPrint`); output the ordered drawing list, the models with no drawing, the skipped library parts, and the PDF path `<assembly name>.pdf` beside the assembly; the report text.
- Tests.

## Acceptance criteria

- [x] Order and de-duplication as above; a sub-assembly's print comes before its children's.
- [x] Library and Content Center parts are skipped and counted; models with no print are listed.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5717 passed, 0 failed (includes 10 new BatchPrintPlanTests cases). Core only; no add-in changes.
- **Not compiled (changed under `src/InventorAddin`):** none changed.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  Nothing checkable in Inventor until the Batch Print command (a later task) uses this plan.

## Defaults

- Added `BatchPrintComponent` (path, IsLibrary, children) and `BatchPrintPlan.Create(root, findPrint)`; `findPrint` is a delegate so the add-in passes `PrintPlan.FindExistingPrint` with a file-exists check.
- A file is keyed by path ignoring case and slash direction; the first occurrence wins and a repeated sub-assembly is not walked again.
- A model with no print is listed but its children are still walked. A library part's subtree is not walked.
- Report lists models with no drawing by file name only (no folders), and the library skip count.

## Follow-ups

- The add-in command (read the component tree, merge PDFs with PDFsharp, ask before replacing the PDF) is a later task.
