# 194: Out-of-bounds check (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Core logic: Out-of-bounds check")
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** any Core task

## Goal

Core reports which views and annotations fall outside the border or over the title block.

## Scope

- `src/InventorAddin.Core/Drawing/OutOfBoundsCheck.cs`: input the border's inner rectangle, the title block's rectangle (with its new rows) and named rectangles of items; output each item that is outside, partly outside, or over the title block, with a message.
- Tests.

## Acceptance criteria

- [x] Inside: not reported. Partly outside or over the title block: reported with which edge. Wholly outside: reported.
- [x] A tolerance (Default 0.5 mm) so an item touching the border is not reported.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5578 passed, 0 failed (one unrelated flaky test failed once and passed on rerun; 14 new tests in `OutOfBoundsCheckTests`).
- **Not compiled (changed under `src/InventorAddin`):** none; Core only.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. None for this task alone; it has no Inventor caller yet. The command that feeds it view and annotation ranges is checked with that task.

## Defaults

- Rectangles are millimetres, Y up (`SheetRect`). An item is "wholly outside" only when it crosses an edge beyond the tolerance and has no part inside the border; anything within the tolerance of the border, or straddling its line by less than that, is not reported. It counts as over the title block when it overlaps it by more than the tolerance in both directions; a zero-thickness item (line or leader) in one direction counts when it lies within the block in that direction (overlap of 0 or more). An item both partly outside and over the title block is one finding with `OverTitleBlock` set. The title block rectangle may be null (no check).

## Follow-ups

- The add-in side must convert Inventor's centimetre ranges to millimetres and build the rectangles (task for the command, not this one).
