# 235: Create Print opens an existing print without asking

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/02-part-creation-and-numbering.md`
- **Status:** todo
- **Depends on:** 193
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan decided: when a print already exists, Create Print just opens it, with no question, as Open Drawing does.

## Scope

- `CreatePrintCommand`: drop the "already exists, open it?" prompt; use `OpenPrint`. Specs 02 and 05 text.

## Acceptance criteria

- [ ] Create Print on a part with a print opens it at once.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
