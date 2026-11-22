# 191: Ribbon icons for the M5 commands

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` and `docs/features/08-*.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** **Python with Pillow** (as task 073)
- **Parallel-safe with:** 189, 190

## Goal

Icons for every new M5 button, drawn by the existing icon tooling, with a preview PNG for Aidan.

## Scope

- `tools/icons/`: icons for Open Drawing, Place Initial Views, Check Out of Bounds, Update Notes, Part Marking, Batch Print, Overall REF Dimensions, Stock Size, Update Parts List; small and large, light and dark as the existing set.
- `src/InventorAddin/UI/Icons/` (generated files) and `src/InventorAddin.Core/Ribbon/RibbonIcons.cs` / `IconNames`.
- `docs/ui-mockups/m5-icons-preview.png`.

Out of scope: adding the buttons (each command's task does).

## Acceptance criteria

- [ ] Every icon follows the existing style and sizes; the preview shows them all at both sizes on light and dark.
- [ ] No icon copies another product's artwork.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan plans a polish of every icon later (TODO, 2026-10-25); match today's style, do not redesign.
- If Python is not available, stop and say so; later tasks then use an existing icon until this one runs.
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
