# 152: New faces take the bore's hand colour when a hole is changed in place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 140
- **Needs:** windows build
- **Parallel-safe with:** 153, 154 (disjoint files; run one after another anyway, they share the build)

## Goal

Aidan's checklist 140-2 failed (2026-11-10): a blind clearance hole with only its bore coloured by hand, edited to add a counterbore: only the bore wall keeps the colour; the new counterbore wall and floor come out uncoloured. They must take the bore's colour (Aidan's Option A decision, `DECISIONS.md` 2026-11-07 and 2026-11-09), and so must a new drill point.

Cause, from `awbaddin.log` (2026-11-09 21:55): adding a seat to an untapped hole is done **in place** by `HoleFeatureWriter.Change` (`SetSeat`), not by `Recreate`. Task 140's carry (`HoleFitWriter.ReadHandColours` / `ApplyHandColours`, `HoleFaceColours.Carry`) only runs in `Recreate`. Inventor keeps the bore face's override across an in-place change, and the faces it adds come out Default.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `Change`: on the in-place path, read the hand colours before the change (as `Recreate` does) and, after it, give the faces that are new (a role and centre the old hole did not have) the bore's colour by the existing `HoleFaceColours.Carry` rule. Faces that existed keep whatever they have: do not clear or rewrite an existing face's colour, and do not re-apply the feature's own colour on this path (Inventor keeps it in place). Skip the work when the old hole had no hand colour. A failure to read or apply is logged and never stops the change.
- `src/InventorAddin/Holes/HoleFitWriter.cs`: whatever small change `ApplyHandColours` needs to colour only new faces (for example a flag, or a separate method sharing the per-face code). Log at Info how many new faces took the bore's colour.
- `src/InventorAddin.Core/Holes/HoleFaceColours.cs` and tests, only if the in-place rule needs a Core helper (for example "the carry for new keys only"). Any rule lives in Core.

Out of scope:

- The remade-hole path (task 140), apart from sharing code.
- AWB fit colours.

## Acceptance criteria

- [ ] An in-place change that adds a counterbore, countersink or spotface to a hole whose bore has a hand colour gives the new seat faces the bore's colour; an in-place change that makes a through hole blind gives the new drill point or flat bottom the bore's colour.
- [ ] Faces that existed before an in-place change keep their own colour or lack of one.
- [ ] The window applies changes live (the preview is the real feature, `Change` runs on each change), so the colour follows each step: adding a seat, removing it and adding it again leaves the seat faces in the bore's colour. One Undo after OK reverts the command, as before.
- [ ] Core tests for any new Core rule; `dotnet build InventorAddin.slnx` on Windows, 0 errors; `dotnet test tests/InventorAddin.Core.Tests` passes (the `Numbering` timing tests can fail under load and pass alone).

## Notes for the implementer

Read task 140's brief and Verification first, and `Recreate` in `HoleFeatureWriter.cs` for the read and apply calls. Inventor may be running on this PC: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs. Use no Inventor API member that is not already used in `HoleFitWriter.cs` or `Extraction/` without listing it.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows (Inventor running): build succeeded, 0 errors, 9 warnings, all pre-existing (CS8600 in the save-as and new-part commands, xUnit2012 in `HoleDiagramTests`). `dotnet test tests/InventorAddin.Core.Tests`: 4550 passed, 0 failed, 0 skipped (8 new `CarryToNewFaces` tests).
- **What changed:** Core `HoleFaceColours.CarryToNewFaces` is the `Carry` rule filtered to faces whose role and centre the hole did not have before; existing faces and faces with no key get null. `HoleFitWriter.ApplyHandColoursToNewFaces` classifies the faces after the change and colours only those, logging at Info "N new face(s) of M on <hole> took the bore's hand colour after the change in place". It shares a new private `ColourFaces` with `ApplyHandColours` (whose behaviour is unchanged apart from the warning text "put on" instead of "put back on"). `HoleFeatureWriter.Change`, on the in-place path only, reads the hand colours before the change (with the new centres, as `Recreate` does), and after a successful change, when any face had a hand colour, updates the document and applies them to the new faces. Read and apply failures are logged as warnings and never stop the change. The feature's own colour is not written on this path.
- **Not compiled (changed under `src/InventorAddin`):** none. `HoleFeatureWriter.cs` and `HoleFitWriter.cs` compiled in the Windows build above. Not yet run in Inventor.
- **Inventor API members not confirmed:** none new. Everything used (`Face.Appearance` set, `HoleFeature.Faces`, `PartDocument.Update`, `SketchPoint.Geometry3d`, the face geometry reads) was already used by task 140's `ReadHandColours` / `ApplyHandColours`.
- **Manual checklist for Inventor:**
  1. Open a part with a blind clearance hole made with AWB Hole (a drill point, no seat). Right-click only its bore (cylinder) face, Properties, and give it a colour by hand (for example blue). Leave the drill point uncoloured.
  2. Edit the hole with AWB Hole and add a counterbore. In the preview, the counterbore wall and floor should turn blue straight away. The bore stays blue and the drill point stays uncoloured. Click OK.
  3. `awbaddin.log` should show "2 new face(s) of 4 on <hole> took the bore's hand colour after the change in place" (the face count may differ).
  4. Ctrl+Z once. The hole should go back to no seat, with only the bore blue.
  5. Edit again: add a counterbore, then set the seat to none, then add the counterbore again, then OK. The wall and floor should be blue.
  6. Repeat step 2 with a countersink, then with a spotface. The new cone, or the spotface wall and floor, should be blue.
  7. Through to blind: make a through clearance hole, colour its bore by hand, edit it and set a distance depth with a drill point. The new drill point should be blue. Repeat with a flat bottom; the bottom face should be blue.
  8. Existing faces: on a counterbored hole whose wall is red by hand and bore blue, change only the diameter. The wall should stay red, the floor stay as it was, and the bore stay blue. The log line should report 0 new faces.
  9. No hand colour: edit an uncoloured hole and add a seat. Nothing should be coloured, and there should be no "new face(s)" log line.

## Follow-ups

- In-place change with moved centres: the old faces are classified against the new centres (the same choice `Recreate` makes). If a centre moves more than 0.5 mm in the same change that adds a seat, the old bore is not found at that centre and the new seat takes no colour. When every old face was one colour, the moved hole's faces take that colour as "new" faces, which matches what the hole looked like but is a write the brief did not ask for. Check this in Inventor if it matters; otherwise read the old hole's own `HoleCenterPoints` for the before-read.
- `ReadHandColours` logs one Info line about the feature's appearance source each time it runs, and it now runs on every in-place change, so a live preview session writes it once per edit. This is harmless but noisy; consider moving it to Debug or logging it only on a remake.
