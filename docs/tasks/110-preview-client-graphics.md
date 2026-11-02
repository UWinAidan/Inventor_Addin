# 110: The AWB Hole preview drawn with client graphics, no face paint

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": live 3D preview; question 13)
- **Status:** todo (completed from spike 107's final result, confirmed by Aidan 2026-10-28)
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
- [ ] Opacity 0.25, colour RGB 255, 0, 0, drawn over the model (`BurnThrough`).
- [ ] Core tests cover a cylinder, a cone to its apex (tip radius 0.001 cm), a partial face whose samples are arcs, a cone whose samples sit at one height, and a flipped hole.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

## Follow-ups
