# 096: Removing a dowel's colour resets every face

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"; question 8)
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 093, 094, 098

## Goal

Aidan's answer to spec 09 question 8: when a dowel or precision hole becomes another kind, every face of the hole goes back to the part's colour, not only the faces that carry an AWB fit colour.

## Scope

- `src/InventorAddin/Holes/HoleFitWriter.cs` (the colour removal); its callers if they pass a filter
- Spec 09 already records the answer

## Acceptance criteria

- [x] Changing a dowel into a clearance or tapped hole sets `AppearanceSourceType = kPartAppearance` on every face of the hole, the bottom too, whatever appearance it had.
- [x] Holes that never were dowels keep any colour the user set on their faces: the reset runs only when a dowel or precision hole stops being one.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:** on Windows, `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: 0 errors, 8 warnings, all in files this task did not touch. `dotnet test tests/InventorAddin.Core.Tests`: 3246 passed, 0 failed, 0 skipped (11 new cases in `HoleSessionRulesTests.ResetsFaceAppearances`).
- **Not compiled (changed under `src/InventorAddin`):** none. `Holes/HoleFitWriter.cs` and `Holes/HoleSession.cs` compiled against the Inventor 2026 interop. Not run in Inventor yet.
- **Inventor API members not confirmed:** none new. `Face.AppearanceSourceType` and `AppearanceSourceTypeEnum.kPartAppearance` were already used in `HoleFitWriter`; the change only drops the filter on `Face.Appearance.DisplayName`.
- **How it works:** `HoleSessionRules.ResetsFaceAppearances(edit, openedKind, openedFitColoured, keptKind)` in Core decides. The session records, when the window opens on an existing hole, the kind it opened with (from the stored data, or read back from the geometry for an Inventor hole) and whether any face carried a fit appearance. On OK the faces are reset only when that hole was a dowel (either signal) and is kept as clearance or tapped. `HoleFitWriter.RemoveColour` now sets every face to the part's appearance with no filter; `RemoveDowel` takes the decision and still resets the tolerance and deletes the data either way. A new hole, or an edited hole that never was a dowel, keeps its face colours.
- **Manual checklist for Inventor:**
  1. Make a dowel, OK. Give one of its faces a colour by hand (Appearance on the face). Edit it (092) into a clearance hole, OK: every face is the part's colour, the hand-coloured one too. One Undo brings the dowel and both colours back.
  2. The same with a dowel edited into a tapped hole (the hole is made again, task 095): every face, the bottom too, is the part's colour. One Undo brings the dowel back.
  3. Make a clearance hole, OK. Give one of its faces a colour by hand. Edit it into a tapped hole, or change its size and keep it clearance, OK: the hand colour is still there.
  4. Edit a dowel and keep it a dowel (change the fit, say Normal to Press), OK: every face shows the new fit colour.
  5. In `awbaddin.log`, a dowel made into another kind logs "every face is back to the part's appearance"; the clearance hole in step 3 logs no such line.

## Follow-ups

- When a dowel becomes a tapped hole the hole is deleted and made again (task 095's recreate), so its faces are new. Whether Inventor gives a new hole's faces the part's appearance, or carries over a feature-level appearance, was not checked; step 2 of the checklist covers it, and the reset runs on the new faces either way.
- A hole made by Inventor's Hole that has an ISO fit on its diameter reads back as a dowel (task 088's read-back), so editing it into another kind resets its face colours too. That follows "a precision hole stops being one", but Aidan may want only AWB dowels covered.
