# 199: Part Marking window and marking on the drawing

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2)
- **Status:** todo
- **Depends on:** 184, 191, 198
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The Part Marking button opens the window; OK places a leader note to a picked edge on the iso view, or a dashed zone with its text, in the highlight colour.

## Scope

- `src/InventorAddin/UI/PartMarkingWindow.xaml(.cs)`.
- `src/InventorAddin/Drawing/PartMarkingWriter.cs` (spike 184, question 3) and `src/InventorAddin/Commands/PartMarkingCommand.cs` + ribbon button.

Out of scope: the engraved feature (200).

## Acceptance criteria

- [ ] Leader: after OK the user picks an edge; the note points to it, in the highlight colour.
- [ ] Zone: a dashed rectangle 40 by 15 mm centred on the picked view, with its text, in the highlight colour; the user can drag its sketch.
- [ ] One Undo removes the marking.
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
