# 152: New faces take the bore's hand colour when a hole is changed in place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
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

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
