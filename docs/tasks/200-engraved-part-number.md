# 200: Engraved part number feature in the model

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2 (optional engraved part number))
- **Status:** built, not run in Inventor
- **Depends on:** 184, 199
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

With the option ticked, Part Marking also adds an engraved text feature on a picked planar face of the part, its text linked to Part Number.

## Scope

- `src/InventorAddin/Drawing/EngravedPartNumberWriter.cs` (spike 184, question 3), called from the marking command on the drawing's part.

## Acceptance criteria

- [ ] The part gets an engrave feature 0.2 mm deep with the part number at 3 mm, centred on the picked face.
- [ ] Changing the Part Number in Part Properties changes the engraved text after an update.
- [ ] One Undo in the part removes it; the feature is named so it can be found ("AWB Part Number").
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 2):
  - Face: the largest planar face (`SurfaceType = kPlaneSurface`, `Evaluator.Area`). Sketch: `Sketches.Add(face, false)`, and `ModelToSketchSpace(face.PointOnFace)` for the text point.
  - Text: `TextBoxes.AddFitted(point, "<StyleOverride FontSize='0.3'><Property Document='model' PropertySet='Design Tracking Properties' Property='Part Number' FormatID='{32853F0F-3444-11D1-9E93-0060B03C1CA6}' PropertyID='5'>PART NUMBER</Property></StyleOverride>", Missing)`. FontSize is the height in cm.
  - Engrave: `Profiles.AddForSolid(false, ObjectCollection{textBox}, Missing)`, then `Features.EmbossFeatures.AddEngraveFromFace(profile, depthCm, kNegativeExtentDirection, Missing, Missing)`.
  - A new Part Number plus `PartDocument.Update()` updates the text, and the feature stays healthy.
  - Check that `MassProperties.Volume` drops; if not, delete the feature and use `kPositiveExtentDirection`.

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.


## Defaults

- The face is the largest planar face of the part's first body (spike 184), not a face the user picks: the window and the command have no face pick. The text goes at `Face.PointOnFace`, which is a point on the face, not necessarily its centroid (spike 184 used it).
- The text is 3 mm (0.12 in) high and the cut 0.2 mm (0.008 in) deep, from `PartMarking` (Core).
- The command runs the drawing marking in one transaction on the drawing, and only after it ends the engraving in its own transaction on the part (not nested). So Part Marking makes two Undo steps, one per document. A failed engraving aborts the part transaction (the part is left as it was) and shows a warning with the reasons; the drawing marking stays.
- The tick box ("Engrave part number in the part", access key N) is hidden for an assembly and for method None. The part is the first part the drawing references (`PrintUnitRefresher.FindModel`); a drawing of an assembly skips it.
- Negative direction first; if the volume does not drop (by more than 1e-9 cm3) the feature is deleted with its sketch kept (`Delete(true, false, false)`) and the positive direction is tried on a fresh profile; if neither works the command throws, so the part transaction aborts.
- Command type stays kNonShapeEditCmdType: the command's document is the drawing; whether kShapeEditCmdType suits the part edit is not known.
- The part is updated after the engraving; it is left modified and unsaved.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5707 passed, 0 failed (after review changes). New Core pieces: `PartMarking.EngraveFeatureName`, `EngraveFormattedText`, `LargestAreaIndex`, `EngraveRemovedMaterial` (volume must drop by more than 1e-9 cm3), with tests. No COM probe was run; Inventor was not started.
- **Not compiled (changed under `src/InventorAddin`):** none; built on Windows. Built does not mean it works: nothing has run in Inventor. Changed: `Drawing/EngravedPartNumberWriter.cs` (new), `Commands/PartMarkingCommand.cs`, `UI/PartMarkingWindow.xaml`.
- **Inventor API members not confirmed by existing code or spike 184 (all compile against the interop):** `EmbossFeature.Name` (set) and `EmbossFeature.Delete(true, false, false)` (argument meaning from the coordinator), `PartComponentDefinition.MassProperties.Volume` (spike 184 read it), `SurfaceBodies[1]` indexer. The probe in spike 184 used `TextBoxes.AddFitted` with a FontSize wrapper around the field and it worked, so that is confirmed.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a drawing of a plain part (a copy). Click Part Marking. Below Extra lines there is a tick box "Engrave part number in the part", off by default.
  2. Set Method to None: the tick box disappears. Set it back to Laser engrave: it returns. Open a drawing of an assembly: no tick box.
  3. Tick it, Leader to an edge, OK, pick an edge. The leader note appears as before. Open the part: the browser has a feature "AWB Part Number" and a sketch under it; the text is the part number, 3 mm high, on the largest flat face, cut 0.2 mm deep (check with Measure or the feature's edit dialog). Check the cut really goes into the material, not out of it (Mass Properties volume is lower).
  4. In the part, change Part Number in iProperties, then Update (or Ctrl+Update). The engraved text changes to the new number and the feature stays healthy (no warning icon).
  5. In the part, Ctrl+Z once: the engraving and its sketch are gone. (Ctrl+Z in the drawing removes the marking separately.)
  6. Imperial part: the text is 0.12 in high and the cut 0.008 in deep.
  7. Untick and run again: the part is not changed.
  8. Positive-direction fallback: on a part where the negative direction cuts into air (if the first test did not show it, try a part whose largest face points the other way), the result is still one sketch plus one feature "AWB Part Number" in the browser, and the volume drops.
  9. After one run with the tick on, open Undo in the part: exactly one "Part Marking" entry. In the drawing: exactly one "Part Marking" entry.
  10. Failure: on a part with no planar face (or a face too small for the text) the drawing marking is still placed, a warning says why the engraving failed, and the part has no new sketch or feature.

## Follow-ups

- The engraving goes on the largest planar face, not a picked one, and at the face's `PointOnFace`. If Aidan wants to choose the face (or have it truly centred), add a face pick to the command: spec 05 question 16.
- Two Undo steps (drawing, part). One step might be possible with `TransactionManager.StartGlobalTransaction`, which exists in the interop but has not been tried.
