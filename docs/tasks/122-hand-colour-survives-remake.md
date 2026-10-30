# 122: A hand colour on a hole face survives the hole being remade

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes")
- **Status:** done
- **Depends on:** 096
- **Needs:** windows build
- **Parallel-safe with:** 123, 124

## Goal

PR #29 check 096-3 failed (Aidan, 2026-10-25): a face of a clearance hole coloured by hand kept its colour on a size change, but lost it when the hole was changed to tapped, because the hole is recreated for the thread (task 088). A hand colour must be kept whenever the hole is remade.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs` (the recreate path), `HoleFitWriter.cs`: before recreating, read each hole face's appearance override that is not an AWB fit appearance; after recreating, put it back on the matching face of the new hole.
- Matching faces between the old and the new hole is the hard part. Core rule, in `src/InventorAddin.Core/Holes/` with tests: describe each face by its role (bore, seat wall, seat floor, countersink cone, bottom, drill point) and its centre index, and map old roles to new. Where the new hole has no face of that role (for example a seat removed), the colour is dropped. Where every face of the old hole had the same hand colour, the whole new hole takes it.
- An AWB fit colour is not a hand colour: task 096's rules for fit colours are unchanged.

## Acceptance criteria

- [x] A clearance hole with one bore face coloured by hand, changed to tapped, keeps that colour on its bore; changed back, keeps it again.
- [x] A hole coloured by hand all over keeps it on every face across a remake.
- [x] Fit colours still follow task 096.
- [x] Core tests for the face mapping; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Notes for the implementer

- Face roles: see how task 096 found every face of the hole, and `HoleGeometry` for the parts of each hole.
- Check every Inventor member against existing usage or the API reference.

## How it works

- **Core:** `src/InventorAddin.Core/Holes/HoleFaceColours.cs`. `Classify` gives each face of a hole a key: its role (Bore, SeatWall, SeatFloor, CountersinkCone, Bottom, DrillPoint, Other) and the index of the centre whose axis it lies on. At each centre: with a counterbore seat, a cylinder wider than the narrowest is the seat wall and the plane nearest the sketch plane is the seat floor; with a countersink seat, the cone nearest the sketch plane is the countersink; every other cylinder is the bore, plane the bottom, cone the drill point. `Carry` maps the old hole's hand colours to the new hole's faces by key; when every old face had the same hand colour, every new face takes it.
- **Add-in:** `HoleFitWriter.ReadHandColours` reads, before the delete, each face's appearance when its source is `kOverrideAppearance` and it is not an AWB fit appearance or the preview appearance, plus the feature's own appearance when its source is `kFeatureAppearance`. Face shapes come from `Face.SurfaceType`, `Face.Geometry` (`Cylinder`, `Cone`, `Plane`) and `Face.Evaluator.RangeBox`. Old and new faces are both keyed against the new hole's centres (`SketchPoint.Geometry3d`), so a centre removed in the same change drops its colours. `HoleFeatureWriter.Recreate` reads the colours before `old.Delete`. After the new hole is made, named and the end of part is back at the bottom, `ApplyHandColours` updates the document and puts the colours back, all inside the caller's step transaction. A failure to read or apply a colour is logged and never refuses the change.
- Fit colours are untouched. A dowel's faces carry fit appearances, which are not hand colours, so nothing is carried, and task 096's reset on OK still runs as before.

## Defaults

- **A face's colour is a hand colour** when Inventor reports its appearance source as an override (`kOverrideAppearance`) and the appearance is neither an AWB fit appearance nor the preview appearance. **A colour set on the whole hole feature** (feature appearance, `kFeatureAppearance`) is kept too: it is put back as the new feature's appearance. Body, part and material appearances are not hand colours; the new faces get them from Inventor anyway.
- **A through hole with no seat has one face**, so colouring its bore colours "every face". The new hole then takes the colour on every face, the drill point and any new seat included (the task's all-over rule). A blind hole whose bore alone is coloured keeps the colour on the bore only.
- **When old faces with the same role and centre carry different hand colours** (a bore split in two by another feature), the new faces of that role take the first coloured one in Inventor's face order.
- **A face belongs to a centre** when its axis passes within 0.5 mm of it (`HoleFaceColours.CentreToleranceCm`). Planes use the middle of their range box as the point on the axis. A face of another surface type (torus, spline) belongs to no centre and keeps no colour of its own, except under the all-over rule.
- **A spotface** counts as a counterbore for roles (seat wall and seat floor).
- **Old and new faces are matched against the new hole's centres.** A colour on a centre that the same change removed is dropped.

## Verification

- **Ran:** On Windows with Inventor closed, `dotnet build InventorAddin.slnx -tl:off`: build succeeded, 0 errors, 8 warnings (CS8600 in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`, files this task did not touch), add-in deployed. `dotnet test tests/InventorAddin.Core.Tests`: 3849 passed, 0 failed, 0 skipped, including 22 new cases in `Holes/HoleFaceColoursTests.cs`.
- **Not compiled (changed under `src/InventorAddin`):** none. `Holes/HoleFitWriter.cs` and `Holes/HoleFeatureWriter.cs` compiled against the Inventor 2026 interop. Not yet run in Inventor.
- **Inventor API members not confirmed:** All are new to the repo. Their names and types were checked by reflection on `Autodesk.Inventor.Interop.dll` (Inventor 2026): `Face.SurfaceType`, `Face.Geometry`, `Face.Evaluator`, `SurfaceEvaluator.RangeBox`, `Box.MinPoint`/`MaxPoint`, `Point.X/Y/Z`, `UnitVector.X/Y/Z`, `Cylinder.BasePoint`/`AxisVector`/`Radius`, `Cone.BasePoint`/`AxisVector`, `Plane.Normal`, `SurfaceTypeEnum.kCylinderSurface`/`kConeSurface`/`kPlaneSurface`, `SketchPoint.Geometry3d`, `Asset.Name`, `HoleFeature.AppearanceSourceType` and `HoleFeature.Appearance` (both have setters), `AppearanceSourceTypeEnum.kOverrideAppearance`/`kFeatureAppearance`. Their behaviour has not been seen in Inventor. Not confirmed:
  - that a face coloured by hand reports `kOverrideAppearance`;
  - that a colour set in Feature Properties makes the feature report `kFeatureAppearance`;
  - that an appearance asset stays in `doc.Assets` and can be set on a face after the only hole using it is deleted;
  - that `Face.Evaluator.RangeBox` is centred on the axis for the annular seat floor.
- **Manual checklist for Inventor:**
  1. Make a blind clearance hole (no seat). Colour its bore face by hand (select the face, Appearance override). Edit it, change Type to Tapped, OK: the bore keeps the colour and the drill point cone does not. `awbaddin.log` shows "made again" and "hand colours put back on 1 of N face(s)" (N being the new hole's face count).
  2. Edit it again, back to Clearance, OK: the bore is still coloured.
  3. Make a counterbored clearance hole. Colour every face by hand with the same colour (wall, floor, bore, bottom). Change it to tapped, OK: every face keeps the colour.
  4. Make a counterbored clearance hole. Colour only the counterbore wall red and the bore blue. Change it to tapped, OK: the wall is red and the bore blue. Change it to tapped with no seat: the bore stays blue and the red is gone.
  5. A hole made with two centres, only the second centre's bore coloured: after a change to tapped, only the second centre's bore is coloured.
  6. Set a colour on the whole hole through the hole's Feature Properties (Appearance). Change it to tapped: the hole keeps that colour.
  7. Make a dowel and OK (fit colour on every face). Edit it into a clearance or tapped hole: every face is back to the part's colour (task 096), and the log has no "hand colours put back" line.
  8. (Reviewer) A through clearance hole with no seat, its bore coloured by hand, changed to a counterbored tapped hole: a one-face hole counts as coloured all over (Default), so the new seat and the bore all take the colour. Say if that is not what you want. Also try a spotface hole (treated as a counterbore, untested).
  9. One Undo after any of the above brings the hole back as it was, colours included.

## Follow-ups

- Spec 09 says "Only AWB fit colours are removed by the rules below", but task 096 resets every face, hand colours included, when a dowel becomes another kind (Aidan's answer to question 8). This task leaves 096 as it was, as the brief says. Aidan may want a hand colour on one of a dowel's faces kept when the dowel becomes a clearance or tapped hole. Today only a dowel face with a hand colour over its fit colour would be affected.
- The preview hole made again during the window (several type changes before OK) carries the colours at each step, so a colour survives Tapped, Clearance, Tapped in one session. If a step makes a hole whose role is missing (a seat removed) and the next step adds it back, the colour on that role is gone. The window does not remember the colours from when it opened.
