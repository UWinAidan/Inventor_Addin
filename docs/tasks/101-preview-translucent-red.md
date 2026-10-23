# 101: Preview holes in translucent red

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", live preview)
- **Status:** done (criterion 1 open: the red is opaque, spec 09 question 13)
- **Depends on:** 096, 099
- **Needs:** windows build
- **Parallel-safe with:** none (changes `HoleSession` and `HoleFitWriter`)

## Goal

Aidan, 090 check: while the window is open the preview holes are translucent red, like Inventor's Hole preview. On OK they go back to the part's colour, or to the fit colour for a dowel.

## Scope

- `src/InventorAddin/Holes/` (`HoleSession`, a preview colour writer beside `HoleFitWriter`); Core for the colour and the rule of what OK leaves, with tests

## Acceptance criteria

- [ ] After every change Inventor took, every face of the preview hole carries a preview appearance: red, partly see-through. Its colour and opacity are Core constants (Default: RGB 220, 40, 40 at about 50 percent).
- [x] OK: every face back to the part's colour (`kPartAppearance`), or the fit colour for a dowel; the preview appearance is removed from the part's assets, so nothing named after the preview is left in the part. One Undo still removes the whole hole.
- [x] Cancel and the other ways out: the part is as before (the outer transaction's abort); no preview appearance left.
- [x] An edited hole previews in red too and gets its own colour back on OK.
- [x] A change is still fast: log the time the colouring adds; if it is over 30 ms on a few holes, say so in Verification.
- [x] Unconfirmed members (the opacity asset value's name, for example `generic_transparency`, and whether Inventor draws a face appearance's transparency in shaded views) are tried with an invisible Inventor in a console app first (task 093's notes) and listed.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- Task 096's `HoleSessionRules.ResetsFaceAppearances` relies on fit colours being applied only on OK (`Finish`), never during the preview. The preview red changes that: make sure the red never counts as a fit colour, and that every face's colour on OK follows 096's rule plus the preview reset (task 096 review).
- `HoleFitWriter.RemoveColour` writes every face with no guard; if a face refuses, OK fails as a whole. Consider `ComSafe.Run` per face if the preview reset hits that (task 096 review).

## Verification

Filled in by the implementer.

- **Spike (throwaway console app in the scratchpad, its own invisible Inventor 2026, quit at the end; no Inventor left running):**
  - A Generic appearance's see-through value is `generic_transparency`: a float, 0 (opaque) to 1 (clear), `IsPercentage` true, limits 0..1. So the opacity 0.5 is written as transparency 0.5 (Core's `HolePreviewAppearance.Transparency`). The colour stays `generic_diffuse`.
  - **Inventor does not draw a face appearance's transparency in the shaded view.** `Camera.SaveAsBitmap` images of a block whose faces carry the red at transparency 0, 0.5 and 0.8 were identical, pixel for pixel; the same for the hole's feature appearance (`HoleFeature.Appearance`) and for a face override on the hole. The same appearance as the part's (`PartDocument.ActiveAppearance`) or the body's (`SurfaceBody.Appearance`) is drawn see-through. So the preview holes are red but solid. This was seen in images from an invisible Inventor (`kShadedWithEdgesRendering`), not in Inventor's own window; added to spec 09 as open question 13.
  - The preview runs in a third transaction, nested in the outer one and left open while the window waits, and aborted before anything else changes. Aborting it put every face back exactly as it was (a new hole's faces to `kFeatureAppearance` Default; an existing hole's user override colour kept) and removed the preview appearance from `doc.Assets`. Painting 2 holes, the appearance made new each time and the view updated: 8 to 28 ms; the abort: 0 to 3 ms. Preview steps (diameter changes) between paints stayed healthy. OK path (abort paint, fit colour in a step, end outer): fit colour on the faces, no preview appearance in the part, one `UndoTransaction` removed the hole and the fit appearance. Cancel path (abort paint, abort outer): the part as before, no preview appearance. Aborting the outer transaction while the paint was open also removed it, but the paint's own `Abort` then threw "Unspecified error", so the session always aborts the paint first.
  - Because the abort puts the faces back as they were, the red is never on a face when a writer, task 096's reset, `HoleFeatureReader.IsColoured` or OK reads or writes it; `HoleFitWriter.RemoveColour` is not used for the preview reset and was left as it is.
- **What changed:** Core `HolePreviewAppearance` (`src/InventorAddin.Core/Holes/HoleInventorNames.cs`: colour 220, 40, 40, opacity 0.5, names `AWB Hole preview` and `Awb_HolePreview`, the asset value names, `Transparency`, `Is`); `HoleSessionRules.PreviewTransactionName`, `SlowPaintMs` (30) and `IsSlowPaint`; tests in `HoleInventorNamesTests` and `HoleSessionRulesTests` (the preview name is never a fit appearance). Add-in: `src/InventorAddin/Holes/HolePreviewWriter.cs` (new) paints every face; `HoleSession` paints after every preview change (and at once for an edited hole), clears the paint in `StartStep`, before showing a sketch, in `RestoreSketches` and first in `Cancel`; OK clears it before `HoleFitWriter.Finish` and paints again if Finish fails. Painting that fails is logged as a warning and the preview goes on unpainted. The time is logged at Info when the colouring takes over 30 ms, and the existing slow-change line (over 500 ms) gives the colouring's share.
- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off` on Windows: succeeded, 0 errors, 8 warnings (all CS8600 in `Commands/` files this task did not touch). `dotnet test tests/InventorAddin.Core.Tests`: 3382 passed, 0 failed, 0 skipped.
- **Not compiled (changed under `src/InventorAddin`):** none; `Holes/HoleSession.cs` and `Holes/HolePreviewWriter.cs` were built on Windows. Not yet run inside the add-in in Inventor.
- **Inventor API members not confirmed:** none by name: `Assets.Add`, `Asset[name]`, `ColorAssetValue.Value`, `FloatAssetValue.Value` with `generic_transparency`, `Face.Appearance`, nested `TransactionManager.StartTransaction` left open, and `Transaction.Abort` were run in the spike. Not confirmed: that Inventor's own window draws face transparency the way the images do (see spec 09 question 13), and the colouring time inside the add-in on a real part.
- **Manual checklist for Inventor:**
  1. AWB Hole: the preview holes are red (expected solid, see question 13); the part around them is unchanged. Change size, seat, depth: still red. Type a value Inventor refuses: the hole stays red at its last good state.
  2. OK on a clearance hole: the part's colour. OK on a dowel: the fit colour. Manage > Appearances (or the appearance browser): no `AWB Hole preview` left. One Ctrl+Z removes the whole hole.
  3. Cancel: the part as before, no preview appearance.
  4. Edit a clearance hole that has a colour you set on one face: red while the window is open; OK keeps your colour. Edit a dowel: red while open; OK as a dowel gives the fit colour back; OK as a clearance hole gives the part's colour (task 096).
  5. `awbaddin.log`: a "the preview colour took N ms" line appears only if the colouring was slow (over 30 ms); note any.

## Follow-ups

- Spec 09 question 13: the preview red is drawn opaque, as Inventor does not draw a face's transparency. If Aidan wants it see-through, the choices are the part's appearance made see-through while the window is open, or client graphics as Inventor's Hole uses.
- In the spike, `TransactionManager.RedoTransaction()` after one Undo of the session did not bring the hole back (hole count stayed 1). Not part of this task; worth checking that Redo of an AWB Hole works in Inventor's window.
