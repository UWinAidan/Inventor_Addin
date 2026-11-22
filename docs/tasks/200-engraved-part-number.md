# 200: Engraved part number feature in the model

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2 (optional engraved part number))
- **Status:** todo
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

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
