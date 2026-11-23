# 228: Every view black and white, and an iso on every print

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (waiting on Aidan's check in Inventor)
- **Depends on:** 192
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan overturned the Default "the iso is its own shaded view": every view, iso included, is black and white (visible lines, no shading). Sheet metal prints get no iso today; every print gets one, sheet metal included.

## Scope

- `InitialViewsPlan`/`InitialViewsViewModel` (Core): sheet metal offers and ticks Iso; layout tests.
- `InitialViewsPlacer`: no shaded style; the iso uses the same black-and-white style as the other views.

## Acceptance criteria

- [ ] A part, a sheet metal part and an assembly each get an iso, unshaded.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 13 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 5364 passed, 0 failed.
- **Changes:** sheet metal now offers Iso (ticked by default; order folded front, flat pattern, iso) and lays it out above the folded front, with the flat pattern on the right. Iso uses `kHiddenLineRemovedDrawingViewStyle`, the style every other view already uses; the scratch measuring in `Measure` goes through the same `PlaceViews`, so it measures that style. Tests updated and added in `InitialViewsPlanTests` and `InitialViewsViewModelTests`.
- **Not compiled (changed under `src/InventorAddin`):** none; `InitialViewsPlacer.cs` built on Windows (0 errors). Not run in Inventor.
- **Inventor API members not confirmed:** none new (`kHiddenLineRemovedDrawingViewStyle` is already used in this file).
- **Manual checklist for Inventor:**
  1. Open a saved part, click Place Initial Views on a drawing: Front, Top, Right and Isometric are all ticked; the iso has no shading, only lines like the others.
  2. Open a sheet metal part with a flat pattern: Folded front, Flat pattern and Isometric are offered and ticked; the iso sits above the folded front, flat pattern on the right; all three are black and white. Check the iso is not clipped by the sheet.
  3. Open an assembly: Front and Isometric, iso unshaded.
  4. Check the log line "Initial Views: measured" now lists Iso for the sheet metal part, and the iso's measured size matches the placed one.

## Follow-ups

- The iso of a sheet metal part is its folded model; if Aidan wants it showing the flat pattern or a different position (for example three in a row), that is a layout choice to revisit after his check.
