# 212: Fix: the hole table's workbook warning

- **Milestone:** M5 (follow-up from task 179's review, TODO.md)
- **Feature spec:** `docs/features/04-hole-table-wizard.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build
- **Parallel-safe with:** any Core-only task

## Goal

When the hole workbook cannot be loaded, the hole table logs one accurate warning instead of one per tapped hole that misleadingly says only taper tap drills are affected.

## Scope

- `src/InventorAddin/HoleTable/HoleTableSource.cs`, `LoadWorkbook`:
  - Remember a failed load for the life of the source (a flag beside `_workbook`), so the load is tried and warned about once per source, not once per hole.
  - Reword the warning to cover both cases: "the hole workbook could not be read; tapped holes use Inventor's or AWB's stored drill, taper tapped holes Inventor's: <message>".

Out of scope: looking up the Tapped row for every straight tapped hole. Since task 182 that lookup is needed: it is how a stale stored drill is detected. Say so under Verification so the TODO line can close.

## Acceptance criteria

- [ ] With the workbook path pointing at a missing file, one Update of a table with three tapped holes logs the workbook warning once.
- [ ] The warning text names tapped and taper tapped holes as above.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- No new Inventor API member. Keep the change to `LoadWorkbook` and its field.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
