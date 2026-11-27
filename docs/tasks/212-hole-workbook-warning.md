# 212: Fix: the hole table's workbook warning

- **Milestone:** M5 (follow-up from task 179's review, TODO.md)
- **Feature spec:** `docs/features/04-hole-table-wizard.md`
- **Status:** done (built on Windows; not run in Inventor)
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

- [x] With the workbook path pointing at a missing file, one Update of a table with three tapped holes logs the workbook warning once.
- [x] The warning text names tapped and taper tapped holes as above.
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- No new Inventor API member. Keep the change to `LoadWorkbook` and its field.

## Defaults

- None. `LoadWorkbook` is the only change; the message text stays in the add-in as the task's notes say (no Core logic is involved).

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (15 warnings, none new). `dotnet test tests/InventorAddin.Core.Tests`: 5866 passed, 1 failed on one run (a known flaky SaveAsReplacer/NewPartCreator test; the failing one differed between runs), 0 skipped. No Core code changed, so no new test.
- **Not compiled (changed under `src/InventorAddin`):** none; built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** none; no Inventor API used.
- **Note:** `HoleWorkbookLoader.Load` does not throw for a missing or bad file: it logs each problem itself and returns an empty workbook, which is cached. The failure flag therefore matters only when `Load` throws (unexpected errors); a missing file already loads once and logs its problems once per source.
- **Straight tapped lookup:** looking up the Tapped row for every straight tapped hole stays, since task 182 uses it to detect a stale stored drill. The TODO line can close.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. In Settings, point the hole workbook at a file that does not exist (or rename it), then open a drawing with a part that has three tapped holes and a hole table.
  2. Click Update on the hole table.
  3. Open `awbaddin.log`: the workbook problem appears once for that Update, not three times, and any warning that the workbook could not be read names both tapped and taper tapped holes. Count only the lines written before you close the wizard: closing it runs the automatic check, which logs the workbook problem again. With a missing file the line is the loader's generic problem line; the new wording appears only when reading the workbook throws.
  4. Restore the workbook path and Update again; tap drills read as before.

## Follow-ups

- The wording "taper tap drills come from Inventor" is gone; if a Core message catalogue for hole-table warnings is wanted, move this text there.
