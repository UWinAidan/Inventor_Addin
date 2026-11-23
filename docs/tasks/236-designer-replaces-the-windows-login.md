# 236: Create Print replaces a Windows login Designer

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/02-part-creation-and-numbering.md`
- **Status:** todo
- **Depends on:** 188
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Old files show the Windows login (for example a short user name) as DESIGNER, because Inventor set Author before the add-in existed. Create Print replaces Designer with the Settings name only when it equals the Windows login; any other name is left alone.

## Scope

- Core: the rule (case-insensitive equality with the login), tests.
- Create Print, with the other backfills, in its transaction. Default: Mass Update does the same.

## Acceptance criteria

- [ ] A part whose Designer is the login prints the Settings name; another name is kept.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
