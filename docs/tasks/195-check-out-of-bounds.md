# 195: Check Out of Bounds command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Commands: Check Out of Bounds")
- **Status:** todo
- **Depends on:** 184, 191, 194
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that checks every sheet and lists what falls outside, selecting each item when picked from the list.

## Scope

- `src/InventorAddin/Drawing/SheetExtentsReader.cs` (spike 184, question 1).
- `src/InventorAddin/Commands/CheckOutOfBoundsCommand.cs` + ribbon button; the result in a message (none) or a small list window (some).

## Acceptance criteria

- [ ] A clean drawing: "Everything is inside the border."
- [ ] A view dragged half off the sheet and a note over the title block are both listed with their sheet; picking one selects it.
- [ ] Query-only command type (it changes nothing).
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
