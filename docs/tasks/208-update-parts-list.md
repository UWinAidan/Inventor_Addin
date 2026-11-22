# 208: Update Parts List command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 6)
- **Status:** todo
- **Depends on:** 184, 187, 191, 207
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that fills the Make/Buy cells of every parts list on the sheet; the manufacturer columns already read Vendor and Stock Number from the template's style.

## Scope

- `src/InventorAddin/Drawing/PartsListMakeBuyWriter.cs` (spike 184, question 7); `src/InventorAddin/Commands/UpdatePartsListCommand.cs` + ribbon button.

## Acceptance criteria

- [ ] An assembly print with an AWB part, a Content Center screw and a bought part from a library: Make, Buy, Buy.
- [ ] A parts list with no Make/Buy column gets one added.
- [ ] One Undo reverts.
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
