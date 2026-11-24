# 235: Create Print opens an existing print without asking

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/02-part-creation-and-numbering.md`
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 193
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan decided: when a print already exists, Create Print just opens it, with no question, as Open Drawing does.

## Scope

- `CreatePrintCommand`: drop the "already exists, open it?" prompt; use `OpenPrint`. Specs 02 and 05 text.

## Acceptance criteria

- [x] Create Print on a part with a print opens it at once (manual check pending).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 13 warnings (none from this change). `dotnet test tests/InventorAddin.Core.Tests`: 5459 passed, 0 failed. Removed the unused `PrintPlan.AlreadyExistsQuestion` and its assertion.
- **Not compiled (changed under `src/InventorAddin`):** none; `Commands/CreatePrintCommand.cs` built on Windows (`OfferToOpen` removed, existing print goes straight to `OpenPrint`, logged).
- **Inventor API members not confirmed:** none new; `OpenPrint` is existing task 193 code.
- **Manual checklist for Inventor:**
  1. Open a saved part that already has a print in `Prints\`.
  2. Click Create Print.
  3. The print opens and becomes the active window at once, with no question and no Initial Views window.
  4. Close the print, rename or move it away, click Create Print again: the Initial Views window appears as before.
  5. With the print already open, click Create Print on the part: that print becomes active.

## Follow-ups

None. Spec 02 (Create Print) text updated; spec 05 had no text about the prompt.
