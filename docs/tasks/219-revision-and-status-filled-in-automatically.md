# 219: Revision and Status filled in automatically

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/07-part-properties.md`; `docs/features/02-part-creation-and-numbering.md`; `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 217
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Revision and Status fill themselves in, so Aidan never types them per drawing (Aidan, 2026-11-22). Status is Inventor's own Status field (Design Tracking "User Status", id 17), written in words that match Design Status (Design State) and kept in step with it.

## Scope

- **Core.** A `PartStatus` type (Work In Progress = Design Status 1, Pending = 2, Released = 3, per the Inventor API reference) with its User Status text: "WORK IN PROGRESS", "PENDING", "RELEASED". Then write plans:
  - New part / copy: Revision Number "A", Design Status 1, User Status "WORK IN PROGRESS".
  - Backfill for older parts:
    - a missing Revision Number becomes "A";
    - a missing User Status becomes the words for the current Design Status;
    - a Design Status of 0 (not set) becomes Work In Progress for both.
    - Extend task 217's backfill so one call returns every missing print property.
- **Add-in:**
  - New Part and Save As Copy write the new-part plan.
  - Create Print and Mass Update run the backfill. In Create Print, Finish Spec, Revision and Status are filled in one transaction.
- **Replace the custom Status property.** It came from Aidan's earlier decision, now replaced, so nothing writes it any more. Leave existing custom Status properties alone; say so under Verification.
- Tests for every plan.

## Acceptance criteria

- [ ] A new part and a Save As Copy have Rev "A", Design Status Work In Progress and Status "WORK IN PROGRESS".
- [ ] Create Print and Mass Update fill a missing Revision or Status and leave set ones alone.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

Read DECISIONS.md rows dated 2026-11-22 (Status and Revision). Task 210 (Part Properties dropdown and Revise) builds on this.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
