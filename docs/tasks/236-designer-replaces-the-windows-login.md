# 236: Create Print replaces a Windows login Designer

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`; `docs/features/02-part-creation-and-numbering.md`
- **Status:** done (built on Windows; not run in Inventor)
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

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (13 existing warnings). `dotnet test tests/InventorAddin.Core.Tests`: 5470 passed, 0 failed (8 new in `DesignerBackfillTests`).
- **Not compiled (changed under `src/InventorAddin`):** none; `Commands/CreatePrintCommand.cs` built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** none new; reuses `PropertyReader.GetString`, `PropertyWriter.Apply` and the existing transaction.
- **Manual checklist for Inventor:**
  1. Open a saved part whose Designer shows the Windows login (an old file); Settings, Default designer set to a different name.
  2. Click Create Print, confirm Initial Views. Check the new print's DESIGNER shows the Settings name; in the part, Designer (and Author) now show it. One Undo on the part reverts.
  3. Repeat on a part whose Designer is another name: it is unchanged.
  4. Clear Default designer in Settings and repeat with a login part: Designer is unchanged.

## Follow-ups

- Mass Update (the task's Default "does the same") is not done: its writes come from the shared `FinishSpecBackfiller.WritesFor`, which Material and Finish also uses, and Mass Update already has Replace names. Aidan to say whether it should auto-replace there too.
- Author is replaced under the same rule (equals the login), to keep it equal to Designer (spec 02); the decision row names Designer only.
- Only the Windows login counts; "Other names that mean me" are left alone, as the decision says.
