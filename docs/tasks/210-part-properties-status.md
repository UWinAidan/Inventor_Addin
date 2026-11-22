# 210: Part Properties: Status dropdown and Revise

- **Milestone:** M5
- **Feature spec:** `docs/features/07-part-properties.md` (question 7); `docs/features/05-drawing-tools.md` (title block Status)
- **Status:** todo
- **Depends on:** 219
- **Needs:** windows build
- **Parallel-safe with:** any Core-only task

## Goal

Part Properties shows the part's status as a dropdown (WORK IN PROGRESS, PENDING, RELEASED) and has a **Revise** button that steps the revision (A to B to C ...) (Aidan, 2026-11-22). Setting the status writes Design Status and Inventor's own Status field (User Status, task 219) together.

## Scope

- Core:
  - the snapshot and write plan carry Design Status and User Status through task 219's `PartStatus`;
  - a `RevisionStep.Next("A")` rule: A to B ... Z to AA. Skip I and O (Default; say so). A blank or unreadable revision becomes "A";
  - the view-model gains a Status dropdown and a Revise command (a pending change until Apply);
  - tests.
- Add-in: read Design Status and User Status in `Extraction/`; add the dropdown and the Revise button to `PartPropertiesWindow.xaml`; Apply writes everything in one transaction.
- On open, a User Status that does not match Design Status is a pre-filled pending change (the earlier Default, spec 07 question 7).

## Acceptance criteria

- [ ] Choosing RELEASED and Apply sets Design Status Released and Status "RELEASED"; one Undo reverts both.
- [ ] Revise on Rev "A" shows "B" as a pending change; Apply writes it.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
