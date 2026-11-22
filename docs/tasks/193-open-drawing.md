# 193: Open Drawing

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Open Drawing")
- **Status:** todo
- **Depends on:** 188, 191
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Open Drawing opens the active model's print from `Prints\`; when there is none it offers Create Print.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs`: the question text when no print exists (reuse `FindExistingPrint`).
- `src/InventorAddin/Commands/OpenDrawingCommand.cs` + ribbon button next to Create Print (model environments).
- Tests for the message.

## Acceptance criteria

- [ ] A model with a print: the print opens (or is activated when already open).
- [ ] A model with none: "This part has no print. Create one now?"; Yes runs Create Print, No does nothing.
- [ ] An unsaved model says so, as Create Print does.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Share Create Print's checks (wrong document, unsaved) instead of copying them.
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
