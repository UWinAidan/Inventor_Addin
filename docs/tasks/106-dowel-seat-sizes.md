# 106: Dowel seats sized in the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats", "Dowel and precision holes", question 9 and 14)
- **Status:** todo
- **Depends on:** 099, 100, 101
- **Needs:** windows build
- **Parallel-safe with:** none (run before 104, which lays these boxes out in the new window)

## Goal

Aidan, 2026-10-23 (spec 09 question 9): a dowel hole's counterbore or countersink follows no fastener standard, since it usually has a purpose of its own. For a dowel with a seat the window hides Fastener type and shows editable boxes for the seat, starting from new columns on the workbook's Dowel tab, then from the last values used.

## Scope

- `libraries/AWB_Holes.xlsx`: the Dowel tab gets four columns after its existing ones, headers `Cbore dia`, `Cbore depth`, `Csink dia`, `Csink angle`, in the tab's own header style, with starter values on every row in the row's units (invent sensible values: a counterbore a little wider than the dowel, a countersink angle of 90). Edit the package XML directly as task 097 did (unzip, change the Dowel sheet and shared strings, zip again), so every other cell, formula and cached value stays as it is. The "How to edit" tab's Dowel line mentions the new columns.
- Core: `HoleWorkbookRows.cs` / `HoleWorkbookReader.cs` (the four optional columns: a missing column is no problem, a blank cell is null, a non-number is a problem naming tab, row and header as today); `HoleSpec.cs`, `HoleRules.cs` (a dowel's seat comes from the spec's own seat values, not a Counterbore or Countersink row; Fastener type does not apply to dowels); `HoleStoredData.cs` if a seat expression or the seat values must be stored (bump the version as earlier tasks did); `AwbHoleViewModel.cs` (the seat boxes, their visibility, start values, checks, expression for the depth, and `= value` text is task 104's); a new remembered-values class like `HoleLastDepths` with its settings fields in `AddinSettings.cs` and their round-trip in the settings file.
- Add-in: `UI/AwbHoleWindow.xaml` (the boxes in today's layout, under Seat's fields; Fastener type hidden for dowels; task 104 then moves them), `Holes/HoleFeatureWriter.cs` and `Holes/HoleFeatureReader.cs` only as far as the seat values and the counterbore depth expression need, `Holes/HoleExpressionChecker.cs` reuse for the depth, the command's OK path to record the last values.
- `tests/InventorAddin.Core.Tests/`: tests for every Core change, including the starter workbook's new columns read with the reader.

Out of scope: the window redesign (104), seats on clearance and tapped holes (unchanged, still from their rows and Fastener type).

## Acceptance criteria

- [ ] The starter workbook's Dowel tab has the four columns with values on every shown row; the reader reads them; every other entry of the package is unchanged (check entry by entry, as task 097 did); a workbook without the columns reads with no problem.
- [ ] A dowel hole with Seat Counterbore shows Counterbore diameter and Counterbore depth boxes; with Countersink, Countersink diameter and Countersink angle; with None, neither. Fastener type is never shown for a dowel. Clearance and tapped holes behave as before.
- [ ] The boxes start from the last values used with OK for that units and dowel size (Default, question 14), else the Dowel row's columns, else the Counterbore or Countersink row of the same size (today's rule), else empty with OK disabled and a message under the box.
- [ ] The counterbore depth takes Inventor parameters and expressions as Depth does (task 100), and the hole stays parametric; the diameters and the angle take numbers in the window's units (degrees for the angle).
- [ ] Checks as today's seat checks: the counterbore or countersink diameter wider than the hole, a depth above 0, an angle above 0 and below 180; a message under the field and OK disabled while one fails.
- [ ] A changed value updates the preview like any other option; the seat is coloured by fit as before.
- [ ] OK records the values for that units and dowel size in the add-in's settings, kept across restarts.
- [ ] An edited dowel hole with a seat opens on its own seat dimensions (and its stored depth expression), not the defaults; a dowel made before this task (seat from the same-size row) opens on its dimensions as read from the hole.
- [ ] Switching units, size, or away from AWB Dowel and back behaves predictably: a size change takes that size's start values; leaving Dowel drops the custom values.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds.

## Notes for the implementer

- Decisions: spec 09 "Dowel and precision holes" and `DECISIONS.md` rows dated 2026-10-23. Column names, the per-size memory, the fallback and which boxes take expressions are Defaults; keep them easy to change.
- Follow `HoleLastDepths` and `AddinSettings.LastBlindDepthMm` for the remembered values (a small keyed map per units is fine; keep the settings file readable and tolerant of a missing entry).
- Follow task 100's depth expressions for the counterbore depth (`HoleExpressions`, `HoleExpressionChecker`, the stored expression beside the value).
- Inventor API: use only members already used in `Holes/HoleFeatureWriter.cs` and `Holes/HoleFeatureReader.cs` for counterbore and countersink sizes; list any new member in Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
