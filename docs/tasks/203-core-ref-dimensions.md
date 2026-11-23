# 203: Overall REF dimensions plan (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 4; open question 18)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 201

## Goal

Core picks the extreme edges of a view and where its overall REF dimensions go.

## Scope

- REF text (Aidan, 2026-11-23): REF dimensions read "260 REF", the number and REF in bold, the text slightly larger than normal dimensions.

- `src/InventorAddin.Core/Drawing/RefDimensionPlan.cs`: input the view's curves as 2D segments (sheet cm) and its rectangle; output the two pairs of extremes, the text point 10 mm outside, and the formatted text "<value> REF" bold.
- Tests.

## Acceptance criteria

- [ ] Width and height pairs are the leftmost/rightmost and lowest/highest geometry; arcs count by their extents.
- [ ] Text points sit 10 mm outside the view's rectangle, below and to the right.
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
