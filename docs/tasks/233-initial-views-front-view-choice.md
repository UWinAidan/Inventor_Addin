# 233: Initial Views: the Front view choice and Auto

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 225, 229
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The window gets a "Front view" dropdown (Auto, Front, Back, Top, Bottom, Left, Right), default Auto. Auto for ordinary parts and assemblies picks the direction with the largest projected area, longest side horizontal; sheet metal uses the base face rule (task 229). This also fixes the assembly print whose front view was edge-on with no proper top view.

## Scope

- Core: the Auto rule from the model's extents, tests; the plan's view boxes follow the chosen direction.
- Adapter: the base view orientation from spike 225.

## Acceptance criteria

- [ ] Aidan's test assembly prints with a full front view and a proper top view on Auto.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Built on Windows with Inventor 2026 installed.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (the warnings are the ones already there). `dotnet test tests/InventorAddin.Core.Tests`: 5452 passed, 0 failed (30 new: `FrontViewRuleTests`, `InitialViewsFrontViewTests`).
- **What changed:** Core `FrontViewRule` (the choices, Auto, the box for a direction); `InitialViewsResult.FrontOrientation`; the view-model's `FrontOptions` and `SelectedFront`, which re-measures once per units and choice; the adapter places the front view from a camera and measures for the chosen direction; a "Front view" list in the window (both Create Print and Place Initial Views).
- **Not compiled:** nothing; all changed add-in files were built. They are not run in Inventor: `Drawing/InitialViewsPlacer.cs`, `Commands/CreatePrintCommand.cs`, `Commands/PlaceInitialViewsCommand.cs`, `UI/InitialViewsWindow.xaml`.
- **Inventor API members not confirmed:** none new. The camera and `AddBaseView(... kArbitraryViewOrientation ..., camera)` are the calls task 229 and spike 225 confirmed; `PartDocument.ComponentDefinition.RangeBox` and `AssemblyDocument.ComponentDefinition.RangeBox` are used in `Read`. Not confirmed in Inventor: that the six standard directions in `FrontViewRule` (front +Z up +Y, top +Y up -Z, right +X up +Y, and their opposites) match Inventor's own Back, Bottom and Left views on a model; Front, Top and Right follow spike 225.
- **Manual checklist for Inventor:**
  1. Open Aidan's test assembly (the one whose print had an edge-on front view) and click Create Print. The Initial Views window shows Front view: Auto. The preview and suggested scale fit; click OK. The print has a full front view (the long side horizontal, looking along the assembly's shortest side) and a proper top view above it.
  2. In the window, change Front view to Front, then Back, Top, Bottom, Left and Right. Each change takes a moment (the views are measured again), and the scale and sheet size update. Click OK on, say, Left: the front view looks from the left of the model, with Y up.
  3. Open an ordinary part and do the same: Auto looks at the largest face with the longer side horizontal.
  4. Open a sheet metal part: Auto is the base face rule from task 229 (unchanged); choose Top and the folded front looks from above instead. The flat pattern view does not change.
  5. Open a drawing, click Place Initial Views, and check the Front view list is there and works the same.
  6. Look in the log for "Initial Views: front view ..." lines, and for "front view's camera could not be built" (should not appear).

## Follow-ups

- Auto uses the axis-aligned range box. Spike 225 found that for a part lying at an angle in the XY plane `OrientedMinimumRangeBox` gives a tighter view (10 x 2 against 9.66 x 6.73). Using it (a camera along its shortest direction) is a possible later improvement; it would make the projected views off-axis.
- The Top and Right tick boxes keep their names, but they are the views above and to the right of whichever front view is chosen, so after choosing Left the "Top" view is not the model's top. Renaming them (for example "Above" and "Beside") was not done.
- Defaults to log in DECISIONS.md: a sheet metal part on any choice other than Auto looks along that direction and the flat pattern view is unchanged; Auto breaks an area tie for the standard front, then top, then right; where the horizontal is not the standard view's, up is the positive direction of the third axis; the Front view list also shows in Place Initial Views.
