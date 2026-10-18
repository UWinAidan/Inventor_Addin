# 101: Preview holes in translucent red

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", live preview)
- **Status:** todo
- **Depends on:** 096, 099
- **Needs:** windows build
- **Parallel-safe with:** none (changes `HoleSession` and `HoleFitWriter`)

## Goal

Aidan, 090 check: while the window is open the preview holes are translucent red, like Inventor's Hole preview. On OK they go back to the part's colour, or to the fit colour for a dowel.

## Scope

- `src/InventorAddin/Holes/` (`HoleSession`, a preview colour writer beside `HoleFitWriter`); Core for the colour and the rule of what OK leaves, with tests

## Acceptance criteria

- [ ] After every change Inventor took, every face of the preview hole carries a preview appearance: red, partly see-through. Its colour and opacity are Core constants (Default: RGB 220, 40, 40 at about 50 percent).
- [ ] OK: every face back to the part's colour (`kPartAppearance`), or the fit colour for a dowel; the preview appearance is removed from the part's assets, so nothing named after the preview is left in the part. One Undo still removes the whole hole.
- [ ] Cancel and the other ways out: the part is as before (the outer transaction's abort); no preview appearance left.
- [ ] An edited hole previews in red too and gets its own colour back on OK.
- [ ] A change is still fast: log the time the colouring adds; if it is over 30 ms on a few holes, say so in Verification.
- [ ] Unconfirmed members (the opacity asset value's name, for example `generic_transparency`, and whether Inventor draws a face appearance's transparency in shaded views) are tried with an invisible Inventor in a console app first (task 093's notes) and listed.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- Task 096's `HoleSessionRules.ResetsFaceAppearances` relies on fit colours being applied only on OK (`Finish`), never during the preview. The preview red changes that: make sure the red never counts as a fit colour, and that every face's colour on OK follows 096's rule plus the preview reset (task 096 review).
- `HoleFitWriter.RemoveColour` writes every face with no guard; if a face refuses, OK fails as a whole. Consider `ComSafe.Run` per face if the preview reset hits that (task 096 review).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. AWB Hole: the preview holes are translucent red; the part around them is unchanged. Change size, seat, depth: still red.
  2. OK on a clearance hole: the part's colour. OK on a dowel: the fit colour. Manage > Appearances (or the appearance browser): no preview appearance left.
  3. Cancel: the part as before, no preview appearance.

## Follow-ups
