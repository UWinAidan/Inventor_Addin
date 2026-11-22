# 214: Print template layout: the triad above the title block on portrait sheets

- **Milestone:** M5 (Aidan, 2026-11-22, spec 05 question 24)
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Template additions", question 24)
- **Status:** done
- **Depends on:** 185
- **Needs:** Core only
- **Parallel-safe with:** 213

## Goal

On A4 and ANSI A portrait, where the title block fills the border's width, the X/Y triad sits just above the title block at its left end instead of over it (Aidan, 2026-11-22). Other sizes keep the triad 5 mm in from the border's bottom-left corner.

## Scope

- `src/InventorAddin.Core/Drawing/PrintTemplateLayout.cs`: the triad's position per sheet, for example `SheetSizeSpec.TriadOriginMm` (border coordinates).
  - When the title block's left edge is at or left of the triad's extent (as on A4 and ANSI A portrait), place the origin 5 mm in from the border's left edge and 5 mm above the title block's top edge.
  - Otherwise keep (5, 5).
  - Decide by geometry, not by sheet name.
  - Keep `TriadSpec`'s sizes. Update its doc comment.
- Tests in `PrintTemplateLayoutTests`:
  - on every sheet, the triad's box (origin to origin + arrow length plus label offset) lies inside the border and does not overlap the title block;
  - on A4 and ANSI A it sits above the block at its left end;
  - on A3 it is unchanged.

## Acceptance criteria

- [x] The triad overlaps the title block on no sheet; A4 and ANSI A portrait have it just above the block's left end.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5110 passed, 0 failed on the second run (the first run had one failure in `RegisterStoreTests.RealEnvironment_ProbeFindsAnExistingFolderAndNotAMissingOne`, an environment probe unrelated to this task, passed on rerun). `PrintTemplateLayoutTests` alone: 59 passed.
- **Not compiled (changed under `src/InventorAddin`):** none; Core only.
- **Inventor API members not confirmed:** none.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Once task 187 draws the triad: create a print template drawing on A4 (metric) and ANSI A. The X/Y triad sits just above the title block's left end, 5 mm in from the border's left edge, not over the block.
  2. On A3 (and larger sheets) the triad is 5 mm in from the border's bottom-left corner, beside the block.

## Follow-ups

- Task 187 must place the triad with `SheetSizeSpec.TriadOriginMm` (border coordinates, from the border's bottom-left), not `PrintTemplateLayout.Triad.OriginXMm/OriginYMm`, which are now only the inset.
- The triad's extent (`PrintTemplateLayout.TriadExtentMm` = 17.5) assumes labels 14.5 mm out plus 3 mm label height; confirm against the drawn triad in task 187.
