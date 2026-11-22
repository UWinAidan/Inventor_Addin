# 189: Initial views: which views and where (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views", "Core logic: Initial view layout")
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 185, 186

## Goal

Core decides the initial views for a part, a sheet metal part and an assembly, one scale for them all, and each view's centre on the sheet in third angle, clear of the title block.

## Scope

- `src/InventorAddin.Core/Drawing/InitialViewsPlan.cs` (+ small records): input the model kind, its range box size in cm, the sheet size and the usable area (inside the border, minus the title block and its two new rows, from `PrintTemplateLayout`); output the views (front, top, right, iso; folded front and flat pattern; front and iso), a standard scale by `PrintPlan.ChooseScale`'s rule, and each view's centre.
- Tests.

Out of scope: the window (190), Inventor (192).

## Acceptance criteria

- [x] Part: front bottom-left, top above it, right to its right (third angle), iso in the top-right; all four fit inside the usable area at the chosen scale with a gap between views (Default 15 mm, say so).
- [x] Sheet metal: folded front and flat pattern side by side; when the flat pattern size is not known, the plan says so and the flat pattern is left out.
- [x] Assembly: front and iso.
- [x] The scale is the largest standard scale at which every chosen view fits; the iso view's size is estimated from the box (document the estimate).
- [x] A subset of views (the user unticks some) is laid out again without gaps where the missing views were.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Reuse `PrintPlan.ChooseScale`'s list of standard scales; do not make a second list.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: the 27 new `InitialViewsPlanTests` pass; full suite 5073 passed, 0 failed on the final run. Earlier full runs showed 2 unrelated, different failures each time (timing and file-system tests in Numbering), gone on rerun with no changes.
- **Not compiled (changed under `src/InventorAddin`):** none changed.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Nothing to check for this task alone (Core only). Task 192 checks the placement: open a part, run Place Initial Views, and see front bottom-left, top above it, right beside it, iso top-right, all inside the border and clear of the title block, with 15 mm between views.

Scale rule (Aidan, 2026-11-20, Create Print's rule): the largest of `PrintPlan.StandardScales` at which the arranged views, with their gaps, fit within `PrintPlan.SheetFillFraction` (60%) of the usable area's width and height. The check is on the whole layout, not one view. `Fits` is false and `DoesNotFitMessage` is added when even the smallest scale does not fit that share.

Defaults chosen (for the parent to log in `DECISIONS.md`): view gap 15 mm (`InitialViewsPlan.ViewGapCm`); the usable area is the border minus the full-width band of the 180 by 55 mm title block; the view group is centred in that area; the iso size is estimated as the true isometric outline of the box, 0.7071 (W + D) wide and 0.8165 H + 0.4082 (W + D) high.

## Follow-ups

- Task 190 (window): right and top views are projected from the front, so unticking Front while keeping Top or Right cannot work in Inventor. The plan lays them out anyway; the window or task 192 should force Front on.
- Task 192: map Inventor's range box to `InitialViewsBoxCm` (front width, height, depth along the viewing direction) and pass the flat pattern size from `FlatPattern.Length` and `Width`, or null.
- Task 192: check that the flat pattern view's `Width` and `Height` match the assumed orientation (Length wide, Width high); if the view is rotated, swap them in `FlatPatternSizeCm`.
- The usable area ignores the triad, parts list and NOTES list; a later task that places those above the title block should shrink `UsableArea`.
