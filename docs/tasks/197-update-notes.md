# 197: Update Notes command; Create Print runs it

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 1)
- **Status:** todo
- **Depends on:** 184, 188, 191, 196
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button writes or refreshes AWB's notes on the first sheet from the model's material, finish and Finish Note; Create Print runs it once.

## Scope

- `src/InventorAddin/Drawing/NotesWriter.cs`: the NOTES list above the title block and the bold bottom line, marked with an attribute (spike 184, question 2); old AWB notes replaced, hand-typed notes untouched.
- `src/InventorAddin/Commands/UpdateNotesCommand.cs` + ribbon button; Create Print calls the writer after placing views.

## Acceptance criteria

- [ ] Steel part with a heat-treat rule: one numbered note; changing the material and pressing Update Notes changes it.
- [ ] A part with a finish: its Finish Note is a numbered note, then the finish's process notes; editing the Finish Note and pressing Update Notes changes the note.
- [ ] A 3D-print material: the bold bottom line appears; changing to aluminium removes it.
- [ ] A note typed by hand stays. One Undo reverts an update.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

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
