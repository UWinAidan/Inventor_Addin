# 217: Finish print name and Finish Spec backfill

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/03-material-and-finish.md`; `docs/features/05-drawing-tools.md` (question 23)
- **Status:** done
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

- [x] A finish with print name "ENP, MID PHOS" and 0.013 to 0.025 mm writes Finish Spec "ENP, MID PHOS, 13-25 µm" on a metric part; Finish stays the full name.
- [x] A part with Finish and no Finish Spec gets one on opening Material and Finish, on Create Print and in Mass Update; a part with both is left alone.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

Do not copy finish names from any company library; invent short names for the starter list. Read DECISIONS.md rows dated 2026-11-22.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: Build succeeded, 0 errors (Windows). `dotnet test tests/InventorAddin.Core.Tests`: 5182 passed, 0 failed (new tests in `FinishSpecBackfillTests`, `FinishListCsvTests`, `FinishPropertiesTests`; `CsvTableTests` column counts raised from 9 to 10 for the new starter-file column).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** none new. Only `Document.DisplayName`, `UnitsOfMeasure.LengthUnits` and the property reads, all already used in the add-in.
- **Defaults chosen:** the column header is `PrintName` (the file's other headers have no spaces); `Print name` with a space is read too. Backfill runs on part documents only (assemblies have no Finish). Mass Update backfills only files it opens to change (a file whose only update is Register is not opened). Create Print writes to the model in its own transaction and does not save it; if the model cannot be changed it logs and still makes the print. A failed backfill in Material and Finish is logged and does not stop the window.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a metric part that has the custom property Finish (say "Electroless nickel (mid phos)" with Finish Thickness "0.013-0.025 mm") but no Finish Spec (delete Finish Spec in iProperties first). Click Material and Finish. Open iProperties, Custom tab: Finish Spec is now set, for example "ENP, MID PHOS, 13-25 µm" for the starter list's Electroless nickel (mid phos) with the same thickness. Finish still reads the full name. Press Undo once: Finish Spec goes. The log has a "Finish Spec ... filled in" line.
  2. Repeat with a part whose Finish is not in finishes.csv and whose Finish Thickness is "0.005-0.018 mm": Finish Spec reads "<name>, 5-18 µm". On an inch part it reads ".0002-.0007 IN".
  3. A part that already has a Finish Spec is left alone (value unchanged, no log line).
  4. Delete Finish Spec again and run Create Print on the part: the print's title block Finish field shows the spec, and the model now has Finish Spec.
  5. Put a part with Finish and no Finish Spec in a folder, run Mass Update on it with a change ticked (for example replace names): after Apply, open the part: Finish Spec is set.
  6. Apply a finish in Material and Finish: Finish is the full name and Finish Spec uses the short PrintName from the list.

## Follow-ups

Things noticed but not done.

- Spec 03's list of the starter finishes' print names is only in `libraries/finishes.csv`; Aidan may want to edit the short names (invented by us).
- Mass Update does not open a file whose only change is Register, so such a file is not backfilled; say so if it should be.
