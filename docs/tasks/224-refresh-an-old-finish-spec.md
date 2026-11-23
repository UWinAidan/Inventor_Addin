# 224: Refresh an old Finish Spec

- **Milestone:** M5 (from Aidan's check of the task 216 rebuild, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md` (question 23); `docs/features/03-material-and-finish.md`
- **Status:** done (not run in Inventor)
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

- [x] A part whose Finish Spec reads "(long name), 13-25 µm" for a finish whose print name is shorter gets "(print name), 13-25 µm" when Material and Finish opens, in Create Print and in Mass Update.
- [x] A hand-typed Finish Spec is never changed.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5325 passed, 0 failed. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (10 existing warnings).
- **Not compiled (changed under `src/InventorAddin`):** none; built on Windows. Changed: `Commands/MaterialFinishCommand.cs`, `Commands/CreatePrintCommand.cs`, `Commands/InventorNewDocument.cs` (log wording only, now `PrintPropertyBackfill.Describe`). Not run in Inventor.
- **Inventor API members not confirmed:** none; no new Inventor calls.
- **Manual checklist for Inventor:**
  1. Open a part whose Finish is a list finish with a print name, and whose Finish Spec is the old long-name form (for example "(long name), 13-25 µm"; edit it in iProperties > Custom if needed).
  2. Click Material and Finish. Open Part Properties or iProperties: Finish Spec now reads "(print name), 13-25 µm". `awbaddin.log` says "Finish Spec refreshed on <part>" (not "Filled in").
  3. Ctrl+Z once: the spec returns to the old text (one transaction with the other backfills).
  4. Repeat with Create Print on such a part: the FINISH line in the title block is the short name and fits.
  5. Repeat through Mass Update (Save As Copy / rename): log line "Mass Update: Finish Spec refreshed on ...".
  6. Type your own Finish Spec (for example "ZINC PLATE PER SPEC") and repeat the three commands: it is not changed.

## Follow-ups

- Defaults chosen: only a finish whose print name differs from its name is refreshed (so a units mismatch alone never rewrites a spec); matching is exact, case and spacing included; a stored spec that equals today's is left. Mass Update has no plan line for backfills (they are applied and logged at write time), so "plan shows it" is met by the log only.
