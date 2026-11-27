# 204: Overall REF Dimensions command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 4)
- **Status:** review
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

- Core (task 203): pass an Inventor arc's start angle and signed sweep straight into `RefCurve.Arc` (negative sweeps are accepted). Confirm the Arc2d member names against the Inventor API reference and list them under Verification. `NeedsExtentFallback` is true for arcs, sloped lines and lines along the axis; do not assume an arc.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- Text height: the dimension style's text height (`Style.TextStyle.FontSize`) times `RefDimensionPlan.TextHeightScale` (1.25), set as `FontSize` in the formatted text (task 204 probe: style 0.35 cm, printed 0.4375 cm bold). Logged in DECISIONS by the parent if wanted.
- View choice: a `DrawingView` in the drawing's select set; else the active sheet's first view with no parent view; else its first view.
- An overall dimension whose end is an arc, circle or sloped line is not placed (see Verification, probe finding), and the command says so in a warning box.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5763 passed, 1 failed (RenamePlanTests slow-listing, known flaky; an earlier run also failed SaveAsReplacer, also known flaky). Added Core tests for `FormattedTextFor`, `ValueMatchesPlan`, `NotPlacedWarning` and the ribbon rows. Probe on Windows (copies in `C:\AwbSpike\204\`, Inventor 2026 started and quit by the script): a slot part (two straight edges, two end arcs) with a base front view.
- **Probe findings (task 204):** arcs read as `kCircularArcCurve` (5125) with `Segments[1].Geometry` an `Arc2d` (Center, Radius, StartAngle, SweepAngle; the right-hand arc gave start 3 pi/2, sweep pi, the left start pi/2, sweep pi). A linear dimension to an arc measures to its **centre**: with an intent of `Missing`, a point at the extreme, a point at the arc end, a type of aligned, or with a straight edge as the other end, the value was 6 cm where the overall width is 9 cm. A `DrawingCurveSegment` intent and a drawing-sketch point intent both threw E_FAIL. So the width of a slot-ended part cannot be dimensioned to its extent. The writer makes the dimension, compares `ModelValue` with the planned span over the view scale (`RefDimensionPlan.ValueMatchesPlan`) when `NeedsExtentFallback` is true, and deletes it with a warning when they differ. Bold text with `FontSize` set through `FormattedText` read "60.00 REF" (two bold runs).
- **Not compiled (changed under `src/InventorAddin`):** none; the add-in built on Windows (0 errors). Not run inside Inventor: `RefDimensionsCommand` and `RefDimensionWriter` have not been run by the add-in (only the equivalent calls in the probe).
- **Inventor API members not confirmed:** `DrawingView.DrawingCurves` enumerated with `foreach` in C# (as `HoleTableSource` does; the probe used `DrawingCurves(Missing)`); `DrawingCurveSegments` indexer `[1]` in C#; `DrawingDocument.SelectSet` holding a `DrawingView` when a view is selected (the property exists; not tried); `LinearGeneralDimension.Style.TextStyle.FontSize` and `Delete` (exist; used in the probe by late binding); the C# default `ArrowheadsInside = true` of `AddLinear` against the spike's `Missing` (assumed the same). Circle and spline extremes use `Evaluator2D.RangeBox`, which the probe confirmed on arcs and lines only.
- **Manual checklist for Inventor:**
  1. Open a drawing with a front view of a plain rectangular part (straight edges all round), nothing selected. Click Overall REF Dimensions on the AWB Drawing panel. Expect a horizontal dimension below and a vertical one to the right of the view, each reading "<value> REF" in bold, slightly larger than other dimensions, no warning box.
  2. Press Undo once. Expect both dimensions gone.
  3. Change the part's size and update the drawing. Expect the dimensions to follow.
  4. Select a second view (for example a side view) and click the button. Expect the dimensions on that view.
  5. Open a drawing whose view has a rounded end (a slot or a round bar) and click the button. Expect the straight-ended dimension placed and a warning box for the other; no wrong-valued dimension left on the sheet.
  6. Click the button on a part or assembly window. Expect "Open a drawing first".

## Follow-ups

- Overall width or height of a view whose extreme is an arc or circle (slot ends, round bars) is not placed: the API dimensions arcs to their centre. Options for Aidan: dimension centre to centre and add the radii as a non-associative value; draw a sketch line between the extreme points and dimension that; or leave it manual. Added to spec 05's open questions.
- The `NeedsExtentFallback` check also rejects sloped extremes whose dimension would be right; harmless, but untested.
- Only `Segments[1]` of an arc is read; an arc split into several segments could give a short extent. The value check guards against a wrong dimension being left (it is removed with a warning). If that removal fails, the warning says a wrong dimension may remain.
