# 204: Overall REF Dimensions command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 4)
- **Status:** todo
- **Depends on:** 184, 191, 203
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that adds bold overall REF dimensions to the selected view (or the sheet's first base view).

## Scope

- `src/InventorAddin/Drawing/RefDimensionWriter.cs` (spike 184, question 5); `src/InventorAddin/Commands/RefDimensionsCommand.cs` + ribbon button.

## Acceptance criteria

- [ ] On a part's front view: a horizontal and a vertical dimension across the part, text "<value> REF" in bold; they update when the part changes size.
- [ ] One Undo removes both.
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
