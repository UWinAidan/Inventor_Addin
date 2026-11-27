# 203: Overall REF dimensions plan (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 4; open question 18)
- **Status:** done
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

- [x] Width and height pairs are the leftmost/rightmost and lowest/highest geometry; arcs count by their extents.
- [x] Text points sit 10 mm outside the view's rectangle, below and to the right.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- Input is `RefCurve` (build with `RefCurve.Line` or `RefCurve.Arc`, which sets the extents from the quadrant points the sweep passes) plus a `ViewRectCm`; all sheet cm.
- Output `RefDimensionResult` has `Width` and `Height` (`RefDimension`): curve indices into the input, the extreme coordinates, `SpanCm`, and `TextPoint` (cm). Width text is centred below the view, height text centred to the right, each 10 mm outside the rectangle.
- On a tie at an extreme, a straight line (vertical for width, horizontal for height) beats an arc. `NeedsExtentFallback` is true when either end of a pair is not such a line (an arc, a sloped line, or a line along the axis); task 204 then uses the curve's `Evaluator2D` range.
- "Slightly larger" text is `TextHeightScale = 1.25` times the normal dimension text height (Default; Aidan gave no number). `FormattedText` is the spike-confirmed bold markup. Applying the scale needs a font-size override that is not confirmed; task 204 decides how.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5755 passed, 0 failed (15 in RefDimensionPlan).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** none for this Core task; checked through task 204.

## Follow-ups

- Task 204: the 1.25 text scale needs a confirmed way to set text size (style override or a dimension style), and a spike result for the arc-extreme fallback.
