# 194: Out-of-bounds check (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Core logic: Out-of-bounds check")
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** any Core task

## Goal

Core reports which views and annotations fall outside the border or over the title block.

## Scope

- `src/InventorAddin.Core/Drawing/OutOfBoundsCheck.cs`: input the border's inner rectangle, the title block's rectangle (with its new rows) and named rectangles of items; output each item that is outside, partly outside, or over the title block, with a message.
- Tests.

## Acceptance criteria

- [ ] Inside: not reported. Partly outside or over the title block: reported with which edge. Wholly outside: reported.
- [ ] A tolerance (Default 0.5 mm) so an item touching the border is not reported.
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
