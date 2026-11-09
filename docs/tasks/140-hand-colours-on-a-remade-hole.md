# 140: Hand colours on a remade hole: the feature's colour kept, new faces take the bore's

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 122
- **Needs:** windows build
- **Parallel-safe with:** 141, 142

## Goal

Two self-test failures from Aidan's check of the round (2026-11-07), both in task 122's colour carry:

- **122-6.** A colour set on the whole hole through Feature Properties is lost when the hole is remade (for example as tapped). Afterwards the new feature reports `kBodyAppearance` (Default). The feature's colour must be kept.
- **122-8.** When a remade hole gains new faces, for example a counterbore or spotface added, those faces must take the bore's hand colour (Aidan, 2026-11-07, `DECISIONS.md`). Faces that existed keep their own colours. Today the new seat faces come out Default.

## Scope

- `src/InventorAddin.Core/Holes/HoleFaceColours.cs` and its tests: `Carry` gives a new face, whose role and centre had no old face, the colour of the bore at the same centre, if that bore had a hand colour. The all-over rule and the per-role carry are unchanged otherwise.
- `src/InventorAddin/Holes/HoleFitWriter.cs` (`ReadHandColours`, `ApplyHandColours`): the feature's appearance is kept whatever source Inventor reports for a colour set by hand on the feature. Today only `kFeatureAppearance` is read. Find out which source a Feature Properties colour gives (likely `kOverrideAppearance`) and log it at Info on the read. Body, part and material sources are still not hand colours.
- `docs/features/09-awb-hole.md`: one line on new faces taking the bore's colour.

Out of scope:

- Task 096's dowel reset.
- Remembering colours between steps while the window is open (task 122 follow-up).

## Acceptance criteria

- [ ] Core tests: a new seat face with no old face of its role takes the bore's hand colour; with no bore colour it gets none; an existing seat face with its own colour keeps it.
- [ ] A Feature Properties colour on the hole survives a remade hole; the log names the source Inventor reported.
- [ ] `dotnet build InventorAddin.slnx` on Windows, 0 errors; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

Read task 122's "How it works" and Defaults first. The local spike branch `spike/139-selftest` (never merged) has `src/InventorAddin/SelfTest/CasesColour.cs`. It sets the feature's colour through the API (`HoleFeature.Appearance`) and shows how cases 122-6 and 122-8 measured. Read it for reference only; copy nothing from it.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Colour a clearance hole through Feature Properties, edit it to Tapped, OK: the hole keeps the colour, and the log names the source read.
  2. A clearance hole with no seat, its bore coloured by hand: edit, add a counterbore, OK. The counterbore wall and floor take the bore's colour.
  3. A counterbored hole with its wall red and bore blue: change it to a spotface. The wall stays red and the bore blue.
  4. One Undo brings each back as it was.

## Follow-ups
