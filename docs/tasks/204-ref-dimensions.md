# 204: Overall REF Dimensions command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 4)
- **Status:** todo
- **Depends on:** 184, 191, 203
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that adds bold overall REF dimensions to the selected view (or the sheet's first base view).

## Scope

- REF text (Aidan, 2026-11-23): REF dimensions read "260 REF", the number and REF in bold, the text slightly larger than normal dimensions.

- `src/InventorAddin/Drawing/RefDimensionWriter.cs` (spike 184, question 5); `src/InventorAddin/Commands/RefDimensionsCommand.cs` + ribbon button.

## Acceptance criteria

- [ ] On a part's front view: a horizontal and a vertical dimension across the part, text "<value> REF" in bold; they update when the part changes size.
- [ ] One Undo removes both.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 1):
  - Curves: walk `DrawingView.DrawingCurves(Missing)` by `Count`/`Item` (in C#, `foreach` is fine). Each curve has `StartPoint`, `EndPoint` (sheet cm) and `ProjectedCurveType`.
  - Extremes: the vertical lines with the least and greatest X (horizontal lines for Y).
  - Dimension: `GeneralDimensions.AddLinear(textPoint, Sheet.CreateGeometryIntent(c1, Missing), Sheet.CreateGeometryIntent(c2, Missing), kHorizontalDimensionType, Missing, Missing, Missing)`.
  - Text: `Text.FormattedText = "<StyleOverride Bold='True'><DimensionValue/> REF</StyleOverride>"` prints bold "<value> REF". `ModelValue` is in cm.
  - Not tried: a view with only arcs at the extremes. Fall back to the curves' `Evaluator2D` range, and list it under Verification.

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
