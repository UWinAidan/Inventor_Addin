# 110: The AWB Hole preview drawn with client graphics, no face paint

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": live 3D preview; question 13)
- **Status:** done
- **Depends on:** 107
- **Needs:** windows build
- **Parallel-safe with:** 111, 112, 113, 119

## Goal

The preview looks like Inventor's own Hole preview (Aidan, 2026-10-24): a faint, see-through red copy of the hole volume drawn over the model with client graphics, end caps included, so a through hole looks red seen straight down the bore. Task 101's face paint is removed.

## Scope

- Remove task 101's preview paint: the paint transaction in `HoleSession` (`_paint`, `ClearPaint`), `HolePreviewWriter`'s face painting, and Core's `HolePreviewAppearance` where nothing else uses it. This also closes the `TODO.md` follow-ups "Preview red (task 101 review)" (a failed clear leaving the paint transaction open; "translucent" comments), which are folded in here.
- Draw the preview as spike 107's final result (round 4, `f0a2f33` on `spike/108-hole-types`, `src/InventorAddin/Spike107.cs`, read it with `git show spike/108-hole-types:src/InventorAddin/Spike107.cs`; confirmed by Aidan 2026-10-28): a solid of revolution (`TransientBRep.CreateSolidCylinderCone`) for each of the hole's cylindrical and conical faces, the full 360° over the face's extent along its axis. The extent comes from points sampled along each of the face's edges (`Edge.Evaluator`, `CurveEvaluator.GetParamExtents`, `GetPointAtParam` with `ref` arrays the caller allocates; 25 points per edge), not from `Face.CalculateFacets` (DISP_E_TYPEMISMATCH from C#) and not from circle edges only (a seat off the edge has arcs). An edge that cannot be sampled (a cone's apex) falls back to its `StartVertex` and `StopVertex` points; a cone whose points all sit at one height runs to its apex; each end radius is at least 0.001 cm, since `CreateSolidCylinderCone` refuses 0. The solids are unioned into one `SurfaceGraphics` with `BurnThrough`, colour RGB 255, 0, 0 and opacity 0.25 (Aidan), made with `AddNonTransacting`, redrawn after each successful change and removed on OK, Cancel, a refused change that keeps the last good hole (the graphics follow the last good hole), and when the session ends any other way.
- The geometry the graphics need (each solid's end points and radii, from the face's axis, radius or cone apex and half-angle, and the sampled edge points read in `Extraction/`) is worked out in Core, with tests; the add-in only reads the faces and turns the result into Inventor objects.
- Draw on the part's `ComponentDefinition.ClientGraphicsCollection` only, with no occurrence transform: spike 103 question 8 showed such graphics land exactly on the part in a part edited in place too. `SurfaceGraphics` has `Color` (`TransientObjects.CreateColor(r, g, b, opacity)`), used in spike 107; `LineGraphics` and triangle graphics have none and take a colour set. If task 105 is done, check the preview in place as well.

Out of scope:

- The fit colours on OK and task 096's reset: unchanged.

## Acceptance criteria

- [ ] No face of the part is painted while the window is open; nothing of the preview is left in the part after OK, Cancel, Undo or closing the part.
- [ ] A through hole looks red seen down the bore; a blind hole's drill point is red seen from the side; every seat is drawn, and a seat running off the edge of the part is drawn whole (spike 107 round 4, Aidan).
- [x] Opacity 0.25, colour RGB 255, 0, 0, drawn over the model (`BurnThrough`).
- [x] Core tests cover a cylinder, a cone to its apex (tip radius 0.001 cm), a partial face whose samples are arcs, a cone whose samples sit at one height, and a flipped hole.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

- **What changed:**
  - Core: `src/InventorAddin.Core/Holes/HolePreviewSolids.cs` (new): `HolePreviewGraphics` (RGB 255, 0, 0; opacity 0.25; `BurnThrough`; depth priority 0; client graphics id `Awb_HolePreview` from `Branding.IdPrefix`), the face model `HolePreviewFace`, the result `HolePreviewSolid`, and `HolePreviewSolids` (each face's solid from its axis, radius or cone half-angle and the sampled edge points; a cone with no length runs to its apex; every end radius at least 0.001 cm; `SampleParams` gives the 25 evaluator parameters per edge, `PointsOf` reads the flat coordinate array). `HolePreviewAppearance` is cut down to `DisplayName` and `Is`, which `HoleFitWriter.IsHandColour` still uses (see Follow-ups); its colour, opacity, name, asset value names and `Transparency` are gone. `HoleSessionRules.PreviewTransactionName` is gone; `SlowPaintMs`/`IsSlowPaint` are renamed `SlowDrawMs`/`IsSlowDraw` (still 30 ms).
  - Add-in: `src/InventorAddin/Extraction/HolePreviewFaceReader.cs` (new) reads the hole's cylindrical and conical faces and samples their edges as spike 107 round 4 did (vertices when an edge cannot be sampled). `src/InventorAddin/Holes/HolePreviewWriter.cs` is rewritten: no face paint; `Draw` builds a `CreateSolidCylinderCone` for each Core solid, unions them with `DoBoolean` (a solid that will not union is drawn on its own and logged), and adds one `SurfaceGraphics` on one node of `ClientGraphicsCollection.AddNonTransacting`; `Clear` deletes it by id. `HoleSession`: the paint transaction (`_paint`, `PaintPreview`, `ClearPaint`) is replaced by `DrawPreview`/`ClearPreview` with a `_drawn` flag. The graphics are cleared at the start of every step (`StartStep`), so they never sit over a change, and drawn again at the end of each `Preview()`, from the hole the session holds: the new hole after a successful change, the last good hole after a refused one, nothing when the hole was removed. That is the same call path task 130's debounced `PreviewNeeded` already goes through, so typing redraws once per applied change. OK clears them in its step (and draws them again if `HoleFitWriter.Finish` fails); Cancel, `RestoreSketches` and `Finish` (every way out, including the part closing and a document switch) clear them. `ShowSketch` no longer needs to clear anything, as the graphics are not in a transaction.
  - This also closes task 101's review follow-ups "Preview red": a failed clear can no longer leave a paint transaction open (there is none), and the "translucent" paint comments are gone.
- **Ran on Windows (Inventor not running):**
  - `dotnet build InventorAddin.slnx -tl:off`: succeeded, 0 errors, 8 warnings (all CS8600 in `Commands/CreatePrintCommand.cs`, `NewPartCommand.cs`, `SaveAsCopyCommand.cs`, `SaveAsReplaceCommand.cs`, untouched by this task). The build deployed the add-in to `%APPDATA%\Autodesk\Inventor 2026\Addins`.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4185 passed, 0 failed, 0 skipped (16 new tests in `HolePreviewSolidsTests`; 3 test methods removed with the paint members).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not yet run inside Inventor.
- **Inventor API members:** every member used is one spike 107's confirmed code used, the same way: `Face.Geometry`, `Cylinder.BasePoint/AxisVector/Radius`, `Cone.BasePoint/AxisVector/Radius/HalfAngle/IsExpanding`, `Face.Edges` (iterated, not typed), `Edge.Evaluator`, `CurveEvaluator.GetParamExtents(out, out)`, `GetPointAtParam(ref, ref)` with caller-allocated arrays, `Edge.StartVertex/StopVertex`, `Vertex.Point`, `TransientGeometry.CreatePoint`, `TransientBRep.CreateSolidCylinderCone(bottom, top, r, r, rTop, Type.Missing)`, `TransientBRep.DoBoolean(..., kBooleanTypeUnion)`, `ClientGraphicsCollection.AddNonTransacting`, `ClientGraphicsCollection[id]`, `ClientGraphics.AddNode`, `ClientGraphics.Delete`, `GraphicsNode.AddSurfaceGraphics`, `GraphicsNode.OverrideOpacity`, `SurfaceGraphics.Color/BurnThrough/DepthPriority`, `TransientObjects.CreateColor(r, g, b, opacity)`, `View.Update`. `HoleFeature.Faces` typed as `Faces` is used the same way in `HoleFitWriter`. Not confirmed by the spike: that a crash with the window open leaves nothing saved in the file (spike 107 question 5 confirmed OK, Cancel and Undo only).
- **Manual checklist for Inventor:**
  1. Open a part with a sketch of hole centres on a block. AWB Hole, a through clearance hole: the hole volume is faint red (about a quarter opaque) over the model; no face of the part turns red. Orbit to look straight down the bore: the bore looks red (end caps).
  2. Change to Blind with a drill point: from the side, the cylinder and the drill point cone are red, through the part's faces.
  3. Add a counterbore, then a countersink: each seat is drawn. Move a centre near the edge of the block (or pick one on the edge) so the seat runs off the part: the seat is drawn whole, outside the part too.
  4. Type in the Diameter box: the red follows each change about 300 ms after you stop typing (task 130). Type a value Inventor refuses (for example a seat smaller than the hole, if the window lets it through): the red stays on the last good hole.
  5. Pick and unpick centres: the red follows; with no centres picked, no red is left.
  6. OK: the red goes; the hole has the part's colour (or the fit colour for a dowel). One Ctrl+Z removes the hole and leaves no red. Run it again and Cancel: no red and no hole. Run it again and close the part with the window open: reopen it, no red.
  7. Edit an existing AWB hole (Edit Feature): it is drawn red at once; OK and Cancel leave no red; a hand colour on one of its faces is kept.
  8. In an assembly, edit the part in place and run AWB Hole: the red lands on the part, not offset. Switch back to the assembly with the window open: the window closes and no red is left.
  9. `awbaddin.log`: no "preview solid could not be made", "could not be joined" or "could not be drawn" warnings; a "drawing the preview took N ms" line only when a draw was over 30 ms (note any).

## Follow-ups

- `HolePreviewAppearance` (`DisplayName`, `Is`) is kept only because `HoleFitWriter.IsHandColour` checks it. Task 101's appearance was always aborted with its transaction, so no part should hold it; if Aidan agrees, delete the class and that check (and its two tests in `HoleInventorNamesTests`).
- Overlapping see-through solids drawn on their own (a union Inventor refuses) are darker where they overlap. The log says when it happens; nothing to do unless it shows.
