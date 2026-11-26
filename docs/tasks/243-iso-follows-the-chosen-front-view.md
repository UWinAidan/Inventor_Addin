# 243: The iso follows the chosen front view

- **Milestone:** M5 (Aidan, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 233, 242
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The iso is always Inventor's standard top-right iso, whatever is picked as Front, so after choosing Left or Back it shows the part from an unrelated corner. Aidan decided (2026-11-26) that the iso follows the chosen front view: it looks at the front-top-right corner relative to whatever is picked as Front.

## Scope

- **Core** (new rule beside `FrontViewRule`, with tests): from the front view's orientation (the direction toward the viewer `f` and up `u`, model space), the iso looks from `f + u + r`, normalised, where `r = u x f` is the sheet's right; its up is `u` with the part along the viewing direction removed. For the standard Front (`f` +Z, `u` +Y in Inventor's default front) this gives the same corner as `kIsoTopRightViewOrientation`; a test proves it.
- **Add-in** (`InitialViewsPlacer`, the scratch measurement and the plan/preview inputs that size the iso): the iso is placed with an arbitrary camera from the rule whenever Front view is not the standard front, including Auto when it picks a non-standard front for a part or an assembly. When the front is the standard one, keep `kIsoTopRightViewOrientation` (no behaviour change).
- Sheet metal on Auto keeps today's iso (Aidan: "flat pattern and iso stay as they are"); an explicit Front view choice on sheet metal makes the iso follow it (Default).
- Spec 05: one clause saying the iso follows the chosen front.

Out of scope: the Top and Right tick box labels (TODO follow-up).

## Acceptance criteria

- [ ] Front view Left (or Back, Top, Bottom, Right): the iso shows the corner at the top right of that front view, upright.
- [ ] Front view Front or Auto picking Front: the iso is unchanged from today.
- [ ] The fit check and preview size the iso the same way it is placed (scratch measurement uses the same camera).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Notes for the implementer

- 229 and 242 already place a base view with an arbitrary camera (`CreateCamera`, `kArbitraryViewOrientation`); the iso is a base view too, so reuse that path.
- Check how 233 turns each choice into a standard orientation, and derive `f` and `u` for each from the same table so the iso and the front never disagree.
- A PowerShell COM check on a copy in `C:\AwbSpike\243\` is allowed (see task 242's notes); never touch `C:\CAD_TEST` or the repo templates.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5534 passed, 0 failed (includes the new `IsoViewRuleTests`, among them the standard front giving the (+X, +Y, +Z) corner of `kIsoTopRightViewOrientation`). No COM probe was run; the iso uses the same `BuildCamera` / `kArbitraryViewOrientation` path as the front view (spike 225, tasks 229/233/242).
- **Not compiled (changed under `src/InventorAddin`):** none; `InitialViewsPlacer.cs` (new `AddIso`) was built on Windows. It has not been run in Inventor.
- **Inventor API members not confirmed:** none new. `AddBaseView` with `kArbitraryViewOrientation` and a `Camera`, and `CreateCamera`, are existing usage in the same file.
- **How it works:** Core `IsoViewRule.Choose(front)` gives the iso orientation (eye on f + u + r, up = u minus its part along the eye). `InitialViewsPlacer.PlaceViews` calls `AddIso` with `plan.FrontOrientation`: null (sheet metal on Auto) or the standard front keeps `kIsoTopRightViewOrientation`; anything else uses the camera. The scratch measurement uses `PlaceViews`, so it measures the same camera. The estimate `IsoSizeCm(box)` needed no change: the box is already width/height/depth as the front sees it, and the same corner formula applies. Spec 05 already carries the clause (line 45).
- **Manual checklist for Inventor:**
  1. Open a part that is clearly not symmetric (for example an L bracket) and click Create Print (or Place Initial Views).
  2. Front view Front: the iso looks as it did before.
  3. Front view Left: the iso shows the corner at the top right of the Left view (the left face on the left of the iso, the top visible), upright. Repeat for Back, Top, Bottom and Right.
  4. Front view Auto on a part that picks a non-standard front: the iso matches that front's top-right corner.
  5. A sheet metal part on Auto: the iso is the standard one; on Left it follows Left.
  6. After changing Front view, check the preview and the placed views: the iso fits the sheet at the same size the preview showed.

## Follow-ups

Things noticed but not done.

- The Top and Right tick box labels (already a TODO item) are out of scope here.
