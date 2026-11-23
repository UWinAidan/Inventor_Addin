# 192: Place Initial Views window and command; Create Print shows it

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views")
- **Status:** done (passed review round 2 after a parent fix; not run in Inventor)
- **Depends on:** 183, 188, 189, 190, 191
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Create Print shows the Initial Views window and places the confirmed views; a Place Initial Views button on a drawing does the same on the active sheet for the drawing's model.

## Scope

- `src/InventorAddin/UI/InitialViewsWindow.xaml(.cs)` bound to `InitialViewsViewModel`.
- `src/InventorAddin/Drawing/InitialViewsPlacer.cs`: base, projected, iso and flat pattern views by spike 183's calls, in one transaction.
- `src/InventorAddin/Commands/CreatePrintCommand.cs`: window before creating; Cancel creates nothing.
- `src/InventorAddin/Commands/PlaceInitialViewsCommand.cs` + ribbon button (drawing environment).

Out of scope: dimensions, notes.

## Acceptance criteria

- [x] Part: front, top, right and iso placed as the plan says; sheet metal: folded front and flat pattern; assembly: front and iso.
- [x] Cancel in Create Print's window creates no drawing.
- [x] Place Initial Views on a drawing with no views asks for the model (or uses the drawing's model when it has one) and places the views; one Undo removes them.
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 183, round 3):
  - Base view: `AddBaseView(doc, centre, scale, kFrontViewOrientation, kHiddenLineRemovedDrawingViewStyle, Missing, Missing, Missing)`; the point is the view's centre.
  - Projected views: `AddProjectedView(base, point, kFromBaseDrawingViewStyle, Missing)`. In the template's third angle standard, right of the base is the right view, above is the top view, and up-right is the iso (pass `kShadedDrawingViewStyle` for a shaded iso).
  - Flat pattern: `AddBaseView` with a `NameValueMap` holding `SheetMetalFoldedModel` = false as `AdditionalOptions`. Check `SheetMetalComponentDefinition.HasFlatPattern` first; a part without one was not tried.
  - Sizes: `DrawingView.Width`, `Height`, `Left`, `Top` and `Center` are in sheet cm.
- Read `MassProperties.Mass` on the model before placing views, or the title block's mass field shows "N/A" (spike 183, round 3).

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (the existing CS8600 warnings only). `dotnet test tests/InventorAddin.Core.Tests`: 5305 passed, 0 failed (one earlier run showed a flaky Rename/SaveAsCopy pair that passed on rerun). New Core tests: `InitialViewsSetupTests` (model kind, default sheet, sheet matching); ribbon tests extended for the new command (name, icon, size, document row, drawing panel order).
- **Not compiled (changed under `src/InventorAddin`):** none; everything built on Windows. Nothing has been run in Inventor. Changed or new: `Drawing/InitialViewsPlacer.cs`, `UI/InitialViewsWindow.xaml(.cs)`, `Commands/PlaceInitialViewsCommand.cs`, `Commands/CreatePrintCommand.cs`, `UI/RibbonSetup.cs`.
- **Inventor API members not confirmed:**
  - Confirmed by spike 183 round 3 and by the build: `AddBaseView` (front, iso, flat pattern with the `SheetMetalFoldedModel` = false `NameValueMap`), `AddProjectedView(base, point, kFromBaseDrawingViewStyle)`, `kHiddenLineRemovedDrawingViewStyle`, `kShadedDrawingViewStyle`, `kIsoTopRightViewOrientation`, `HasFlatPattern`, `FlatPattern.Length`/`Width`, `TransientObjects.CreateNameValueMap`.
  - Not run in Inventor: the flat pattern is passed as the named argument `AdditionalOptions:` (the spike passed it positionally, argument 8); the build accepts the name.
  - Assumed, not tried: a flat pattern base view with `kFrontViewOrientation` lays Length across and Width up, as the plan sizes it.
  - Not covered by the spike: an iso base view placed at a planned centre beside projected views (the spike placed iso as a base view on its own, which is the same call); a sheet metal part with no flat pattern (the size is then not given, so the plan leaves the view out and the window says so); `DrawingDocument.ReferencedDocuments` to find the model (used by `DrawingExtractor`, but not on a drawing with no views); `Documents.Open(path, false)` for a model picked by the user.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a plain test part with some depth (not a cube) that has a Part Number set. Click Create Print. The Initial Views window opens before any drawing is made: Front, Top, Right and Isometric ticked, scale "Suggested (1:n)", the sheet shown as "A3, 420 x 297 mm" (or ANSI B for imperial settings).
  2. Click Cancel. No drawing is created or saved.
  3. Click Create Print again and OK. The new drawing shows front, top above it, right to its right and a shaded iso up and right, with a gap of about 15 mm between views, the group centred between the border and the title block. The weight field in the title block shows a mass, not "N/A".
  4. Check the scale: the views fill at most about 60 % of the space inside the border and above the title block, and a larger standard scale would not fit.
  5. Create Print on a part with a small box in a big sheet gives a larger scale than before (the rule is now the usable area, not the whole sheet).
  6. In the window untick Top: Right and Iso stay and the views re-layout with no gap. Untick Front: Top and Right untick, Iso stays. Tick Right: Front ticks. Pick 1:1 where the views cannot fit: a warning appears in the footer and OK still works.
  7. Sheet metal part with a flat pattern: Create Print shows Folded front and Flat pattern; both appear, the flat pattern unfolded. A sheet metal part with no flat pattern: the window says the flat pattern size is not known and leaves it out.
  8. Assembly: front and Isometric only; the iso sits to the right of the front view and is an isometric view, not the right view.
  9. Open a drawing with no views and no model reference (a new drawing from the AWB template). The Drawing panel shows Place Initial Views. Click it: a file picker asks for a part or assembly; choose one; the window opens and the views are placed on the active sheet. Press Ctrl+Z once: all the views are gone.
  10. Open a drawing that already has a view of a part (the print from step 3). Click Place Initial Views: the question "This sheet already has views" appears; No does nothing, Yes opens the window and places views (they overlap the old ones, that is expected).
  11. Click Place Initial Views with a part open (not a drawing): the button is not on the part ribbon.
  12. On a drawing with no model, click Place Initial Views and Cancel in the file picker: nothing is placed and Edit > Undo shows no new entry. Pick a part, then Cancel in the Initial Views window: nothing is placed. Then open that same part file normally from the Open dialog: it opens in its own window with nothing left over (the command closed the copy it opened). A part that was already open stays open after Cancel.
  13. Sheet metal with a flat pattern: the flat pattern view is Length across and Width up, matching the size in the window.
  14. Open the log (`awbaddin.log`): the Create Print and Place Initial Views lines list each view and its centre.

## Follow-ups

- `PrintPlan.ChooseScale` and its tests are no longer used by the add-in (Create Print takes its scale from `InitialViewsPlan`, task 188's follow-up). Remove it, or keep it only if another command wants a single-view scale.
- The front view now uses `kHiddenLineRemovedDrawingViewStyle` (spike 183) where Create Print used `kHiddenLineDrawingViewStyle` before; check on the first print that this is the look wanted.
- Place Initial Views uses the first part or assembly the drawing references. A drawing that references several models gets the first; a model chooser could come later.
- The actual size of each placed view (`DrawingView.Width`, `Height`) is not read back, so a view larger than planned (for example an iso estimated too small, or an assembly whose range box is much larger than its geometry) can overlap its neighbour. Reading the sizes and nudging is a later task.
- A drawing whose sheet is not an AWB sheet size is laid out with a 10 mm (metric) or 12.7 mm (imperial) border and a 55 mm title block band assumed; the sheet's real border and title block are not read.
