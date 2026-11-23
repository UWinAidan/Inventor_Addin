# 224: Refresh an old Finish Spec

- **Milestone:** M5 (from Aidan's check of the task 216 rebuild, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md` (question 23); `docs/features/03-material-and-finish.md`
- **Status:** todo
- **Depends on:** 217
- **Needs:** Core, plus the existing backfill call sites (no new Inventor calls)
- **Parallel-safe with:** 223

## Goal

The FINISH line still runs past the right edge of the title block, showing the finish's long name. The part's Finish Spec was written before the finish list had a print name (task 217), and the backfill only fills a blank Finish Spec, so it is never rewritten. The backfill must also refresh a Finish Spec the add-in wrote earlier, so the short print name reaches the print.

## Scope

- `src/InventorAddin.Core/Materials/FinishSpecBackfill.cs`: when the part's Finish is in the finish list and its stored Finish Spec is what the add-in would have built from the finish's long `Name` (either units, with or without the thickness), and that differs from today's spec (built from `SpecName`), write today's spec. Default: a Finish Spec that does not match the add-in's own form is someone's own text and is left alone.
- `PrintPropertyBackfill` and the three callers pick this up unchanged (Material and Finish on opening, Create Print, Mass Update). Mass Update's plan shows the refresh like its other backfill lines; the log says "Finish Spec refreshed".
- Tests: an old long-name spec is refreshed (metric and imperial, with and without thickness); a current spec is left; a hand-written spec is left; a finish not in the list is left.

## Acceptance criteria

- [ ] A part whose Finish Spec reads "(long name), 13-25 µm" for a finish whose print name is shorter gets "(print name), 13-25 µm" when Material and Finish opens, in Create Print and in Mass Update.
- [ ] A hand-typed Finish Spec is never changed.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
