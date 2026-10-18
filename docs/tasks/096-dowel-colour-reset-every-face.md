# 096: Removing a dowel's colour resets every face

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"; question 8)
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 093, 094, 098

## Goal

Aidan's answer to spec 09 question 8: when a dowel or precision hole becomes another kind, every face of the hole goes back to the part's colour, not only the faces that carry an AWB fit colour.

## Scope

- `src/InventorAddin/Holes/HoleFitWriter.cs` (the colour removal); its callers if they pass a filter
- Spec 09 already records the answer

## Acceptance criteria

- [ ] Changing a dowel into a clearance or tapped hole sets `AppearanceSourceType = kPartAppearance` on every face of the hole, the bottom too, whatever appearance it had.
- [ ] Holes that never were dowels keep any colour the user set on their faces: the reset runs only when a dowel or precision hole stops being one.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Make a dowel, OK. Give one of its faces a colour by hand (Appearance on the face). Edit it (092) into a clearance hole, OK: every face is the part's colour, the hand-coloured one too. One Undo brings the dowel and both colours back.

## Follow-ups
