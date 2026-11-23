# 227: Initial Views fit check matches what is placed

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 189, 190, 192)
- **Status:** done, not run in Inventor
- **Depends on:** 192
- **Needs:** Core, plus reading placed view sizes on Windows
- **Parallel-safe with:** none

## Goal

The Initial Views window says 1:2 does not fit A3 although the views fit fine at 1:2 after OK, and an assembly on A3 got 1:20 with the views filling a small part of the sheet. The fit estimate (probably the iso size and the assembly range box) is too cautious.

## Scope

- `InitialViewsPlan` (Core): estimate each view from the model box seen along that view direction, the iso from the box's projected outline (not the 3D diagonal or a padded guess); tests with boxes whose real placed sizes are known from spike 183 C13 or from reading placed views.
- Check the iso view is placed at the sheet scale (Aidan saw it look a different scale from the front view on the assembly); fix if not.
- Optionally read placed view sizes back after placing and log planned vs placed.

## Acceptance criteria

- [ ] On Aidan's test part, 1:2 is offered as fitting A3 when the placed views fit.
- [ ] The assembly test gets the largest standard scale whose placed views fit within the fill fraction.
- [ ] The iso view's scale equals the sheet scale.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

- **Finding:** the old estimate was already the exact isometric outline of the range box (`0.7071 (W+D)` by `0.8165 H + 0.4082 (W+D)`), so a "tighter formula from the box" cannot help: a box is the worst case for the real silhouette, and an assembly's range box can be far larger than its geometry. The cure is to measure, not estimate.
- **Change:** `InitialViewsPlan.Plan` and `InitialViewsViewModel` take optional measured view sizes at scale 1 (`ViewSizeCm`), which replace the estimates (the estimate stays as the fallback). `InitialViewsPlacer.Measure` places the planned views, at the estimate's own scale on an A3-sized area so a big assembly stays near the sheet, in a scratch drawing made from the AWB template (not shown, closed without saving) and reads each `DrawingView.Width`/`Height` divided by its `Scale` (the scratch run skips the scale check and its warnings, and shows the wait cursor); Create Print and Place Initial Views call it before opening the window, so the "Suggested" scale and the does-not-fit warning use real sizes. `Place` now reads placed sizes back and logs placed vs planned per view, and every base view (iso included) has its `Scale` checked against the plan and set when it differs (logged as a warning).
- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5363 passed, 0 failed (5 new tests in `InitialViewsPlanTests`: measured iso gives 1:2 where the estimate gives 1:4, measured sizes at a fixed scale, a huge assembly box with measured views, null/empty keeps the estimate, non-positive sizes rejected).
- **Not compiled in the cloud:** none (built on Windows). Changed under `src/InventorAddin`: `Drawing/InitialViewsPlacer.cs`, `Commands/CreatePrintCommand.cs`, `Commands/PlaceInitialViewsCommand.cs`. Not run in Inventor.
- **Inventor API members not confirmed by existing use or a spike:** `Documents.Add(kDrawingDocumentObject, template, false)` (invisible; the visible form is in Create Print); setting `DrawingView.Scale` (read is used in `DrawingExtractor`; the set is from the API reference); `DrawingView.Width/Height` of a projected view at scale 1 (C13 confirmed them on a base view).
- **Manual checklist for Inventor:**
  1. Deploy (close Inventor first), open Aidan's test part, run Create Print. The Initial Views window opens a second or two later than before. The Suggested scale should now be 1:2 on A3 (it said 1:2 did not fit before). Click OK; the views fit and no scale was changed.
  2. Open `%APPDATA%\AwbAddin\awbaddin.log`: an "Initial Views: measured at scale 1" line lists each view's real size and the old estimate; the "Create Print" line lists "placed W x H, planned W x H" per view. Placed should be within a few percent of the measured size times the scale.
  3. Make a print of the test assembly (A3). The suggested scale should be much larger than 1:20 and the views should fill about 60 percent of the usable area, not a small part.
  4. Check the iso view's scale: its scale label (or Edit View) should equal the front view's. No "placed at scale ..." warning should appear in the log; if one does, the iso was not at the sheet scale and was corrected.
  5. On an open drawing with no views, run Place Initial Views: same suggested scale as step 1.
  6. A sheet metal part (folded front and flat pattern) still places both views.
  7. No scratch drawing stays open or appears in the document tabs after the window opens.

## Follow-ups

- If measuring is slow on a large assembly (it places the views twice), measure only the front and iso, or cache the sizes per model.
- The measured sizes come from the shaded iso and hidden-line front; task 228 changes the iso style, so re-check the measured iso against the placed one after it.
- A measured size is an outline of visible geometry; dimensions, centre marks and tags added after placing can still make a view larger than measured.
