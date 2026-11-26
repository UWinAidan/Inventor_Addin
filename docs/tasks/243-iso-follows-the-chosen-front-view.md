# 243: The iso follows the chosen front view

- **Milestone:** M5 (Aidan, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
