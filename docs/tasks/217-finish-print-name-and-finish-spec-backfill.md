# 217: Finish print name and Finish Spec backfill

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/03-material-and-finish.md`; `docs/features/05-drawing-tools.md` (question 23)
- **Status:** todo
- **Depends on:** 213
- **Needs:** windows build
- **Parallel-safe with:** 215

## Goal

Long finish names overflow the title block. finishes.csv gets a short **print name** column (for example "ENP, MID PHOS"), and Finish Spec uses it; the full wording stays in the finish note (Aidan, 2026-11-22). Parts finished before task 211 have Finish but no Finish Spec, so it is filled in automatically.

## Scope

- **Print name in Core:**
  - The finish list reader (`FinishListCsv`) reads an optional "Print name" column, and `Finish` carries it. A blank or missing column means the finish's name is used.
  - `FinishProperties.SpecText` uses the print name. The Finish property itself stays the full name.
  - `libraries/finishes.csv` (our starter list) gains the column with short print names, invented by us in our own words.
- **Backfill in Core:** a rule such as `FinishSpecBackfill.For(storedFinish, storedFinishThickness, storedFinishSpec, finishList, units)`. When Finish is set and Finish Spec is missing, it returns the Finish Spec write.
  - Find the finish by name in the list, for its print name and thickness.
  - If the finish is not in the list, build the spec from the stored Finish and Finish Thickness (mm text) converted as in task 213.
  - Otherwise it returns nothing.
- **Where the add-in runs it**, each part's own units, one transaction per command:
  - on opening Material and Finish, for the parts it opens on (written at once, logged);
  - in Create Print, for the model before the drawing is made;
  - in Mass Update, for each part it touches.
- Tests: the reader with and without the column, the spec using the print name, and each backfill case.

## Acceptance criteria

- [ ] A finish with print name "ENP, MID PHOS" and 0.013 to 0.025 mm writes Finish Spec "ENP, MID PHOS, 13-25 µm" on a metric part; Finish stays the full name.
- [ ] A part with Finish and no Finish Spec gets one on opening Material and Finish, on Create Print and in Mass Update; a part with both is left alone.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

Do not copy finish names from any company library; invent short names for the starter list. Read DECISIONS.md rows dated 2026-11-22.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
