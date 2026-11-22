# 196: Automatic notes: workbook and rules (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 1; open questions 12, 15 and 23)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 194

## Goal

Core reads the notes workbook and gives, for a material, a finish and the model's Finish Note, the notes to place and where.

## Scope

- `src/InventorAddin.Core/Drawing/NotesWorkbook*.cs`: tabs How to edit, Material (Pattern, Note, Placement), Finish (Pattern, Note, Placement); reader on `XlsxPackage` with messages as the hole workbook has.
- `src/InventorAddin.Core/Drawing/NotesRules.cs`: wildcard match ignoring case, every matching row in order, each note once; placement List or Bottom.
- `libraries/AWB_Notes.xlsx`: starter with **invented** rows (a 3D print material, a hardened steel, an anodise finish); `LibraryPaths.NotesWorkbook` and a Settings row as for the hole workbook.
- Tests.

Out of scope: Inventor (197).

## Acceptance criteria

- [ ] A 3D-print material gives a Bottom note "NOTE: 3D PRINTED PART"; a hardened material its heat-treat note in the list; a finish rule its extra note.
- [ ] The Finish Note is a numbered List note when it is not blank: after the material notes, before the finish rows' notes (Aidan 2026-11-21; order is Default, question 23). Blank adds nothing; a rule note with the same text is not repeated.
- [ ] Starter finish rows are process instructions (masking, handling, surface requirements), invented.
- [ ] Bad rows (blank pattern, unknown placement) give a message naming tab and row; the rest still load.
- [ ] The workbook opens in Excel with no repair prompt (Aidan checks).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how tasks 097, 106 and 111 wrote the hole workbook package (Python `zipfile`, script kept in the scratchpad). No material names copied from any library (`CLAUDE.md`).
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
