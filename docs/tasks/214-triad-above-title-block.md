# 214: Print template layout: the triad above the title block on portrait sheets

- **Milestone:** M5 (Aidan, 2026-11-22, spec 05 question 24)
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Template additions", question 24)
- **Status:** todo
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

- [ ] The triad overlaps the title block on no sheet; A4 and ANSI A portrait have it just above the block's left end.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
