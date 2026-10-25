# 122: A hand colour on a hole face survives the hole being remade

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes")
- **Status:** todo
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

- [ ] A clearance hole with one bore face coloured by hand, changed to tapped, keeps that colour on its bore; changed back, keeps it again.
- [ ] A hole coloured by hand all over keeps it on every face across a remake.
- [ ] Fit colours still follow task 096.
- [ ] Core tests for the face mapping; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Notes for the implementer

- Face roles: see how task 096 found every face of the hole, and `HoleGeometry` for the parts of each hole.
- Check every Inventor member against existing usage or the API reference.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Make a clearance hole. Colour its bore face by hand. Edit it, change Type to Tapped, OK: the bore keeps the colour.
  2. Edit again, back to Clearance: still coloured.
  3. Colour every face of a counterbored hole by hand, change it to tapped: every face keeps it.
  4. A dowel's fit colour still goes when it becomes a clearance hole.

## Follow-ups
