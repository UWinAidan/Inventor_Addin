# 106: Dowel seats sized in the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats", "Dowel and precision holes", question 9 and 14)
- **Status:** done
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

- [x] The starter workbook's Dowel tab has the four columns with values on every shown row; the reader reads them; every other entry of the package is unchanged (check entry by entry, as task 097 did); a workbook without the columns reads with no problem.
- [x] A dowel hole with Seat Counterbore shows Counterbore diameter and Counterbore depth boxes; with Countersink, Countersink diameter and Countersink angle; with None, neither. Fastener type is never shown for a dowel. Clearance and tapped holes behave as before.
- [x] The boxes start from the last values used with OK for that units and dowel size (Default, question 14), else the Dowel row's columns, else the Counterbore or Countersink row of the same size (today's rule), else empty with OK disabled and a message under the box.
- [x] The counterbore depth takes Inventor parameters and expressions as Depth does (task 100), and the hole stays parametric; the diameters and the angle take numbers in the window's units (degrees for the angle).
- [x] Checks as today's seat checks: the counterbore or countersink diameter wider than the hole, a depth above 0, an angle above 0 and below 180; a message under the field and OK disabled while one fails.
- [x] A changed value updates the preview like any other option; the seat is coloured by fit as before.
- [x] OK records the values for that units and dowel size in the add-in's settings, kept across restarts.
- [x] An edited dowel hole with a seat opens on its own seat dimensions (and its stored depth expression), not the defaults; a dowel made before this task (seat from the same-size row) opens on its dimensions as read from the hole.
- [x] Switching units, size, or away from AWB Dowel and back behaves predictably: a size change takes that size's start values; leaving Dowel drops the custom values.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds.

## Notes for the implementer

- Decisions: spec 09 "Dowel and precision holes" and `DECISIONS.md` rows dated 2026-10-23. Column names, the per-size memory, the fallback and which boxes take expressions are Defaults; keep them easy to change.
- Follow `HoleLastDepths` and `AddinSettings.LastBlindDepthMm` for the remembered values (a small keyed map per units is fine; keep the settings file readable and tolerant of a missing entry).
- Follow task 100's depth expressions for the counterbore depth (`HoleExpressions`, `HoleExpressionChecker`, the stored expression beside the value).
- Inventor API: use only members already used in `Holes/HoleFeatureWriter.cs` and `Holes/HoleFeatureReader.cs` for counterbore and countersink sizes; list any new member in Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 3446 passed, 0 failed (one run had an unrelated failure in `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`, a timing test; it passed on the two runs after). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: builds, 0 errors, no new warnings (the 8 CS8600 warnings are in files this task did not touch).
- **Workbook:** `libraries/AWB_Holes.xlsx` edited in its package XML with Python `zipfile` (script kept in the session scratchpad). Only `xl/worksheets/sheet7.xml` (Dowel) and `xl/sharedStrings.xml` changed; every other entry's content is identical to HEAD, checked entry by entry, in the same order. Dowel gets E:H `Cbore dia`, `Cbore depth`, `Csink dia`, `Csink angle` in the header style (s=15), values in the number style (s=16) on all 16 rows, column widths added and the dimension made A1:H20. Starter values (invented): Metric counterbore and countersink 1 mm wider than the dowel, counterbore half the dowel deep, 90 deg; Imperial 0.04 in wider, half deep, 90 deg. The existing `Cbore dia`, `Cbore depth`, `Csink dia` shared strings are reused; `Csink angle` is new (index 187); the sst counts are updated. The "How to edit" Dowel line now names the four columns. The autofilter (`A4:D20`) and the workbook's `_FilterDatabase` name were left as they were, so `xl/workbook.xml` is unchanged; the new columns have no filter buttons.
- **Not compiled (changed under `src/InventorAddin`):** none; the add-in was built on Windows. Not yet run inside Inventor.
- **Inventor API members not confirmed:** none new. `HoleFeature.CBoreDepth`, `Parameter.Expression`, `Parameter.set_Units`, `Parameter.DrivenBy` and `Parameter.Value` were already used in `Holes/HoleFeatureWriter.cs` and `Holes/HoleFeatureReader.cs` (spike 100). Not checked in Inventor: that writing an expression into `CBoreDepth` keeps the counterbore parametric the way it does for the hole depth (checklist step 4).
- **Choices made (all Defaults, easy to change):**
  - A dowel lists all three seats whatever its size, since its seat no longer needs a same-size row. Tapped holes keep task 099's filtering.
  - Start values are worked out value by value: the last value kept for the units and size, else the Dowel row's column, else the first shown same-size Counterbore or Countersink row (else the first). `HoleRules.DowelSeatStart` does the workbook part.
  - Remembered values are numbers only, as the blind depth is: a counterbore depth typed as `d0/8` is remembered as its value, since a parameter name belongs to one part. A counterbore kept with OK leaves the countersink values remembered for that size alone, and the other way round.
  - Settings file: `lastDowelSeatsMm` and `lastDowelSeatsIn`, objects keyed by the Dowel tab's Size (matched ignoring case), each `{ "cboreDiameter", "cboreDepth", "csinkDiameter", "csinkAngleDeg" }` with unknown values left out. A missing or null map, a null entry or a blank key is tolerated. Values a seat cannot take are dropped on read.
  - Typed seat values are kept while the units and size stay, also across a seat, fit or termination change; a units or size change takes the new size's start values; leaving AWB Dowel drops them (and any counterbore depth expression).
  - Stored data: a dowel stores `CboreDiameter`, `CboreDepth`, `CboreDepthExpression`, `CsinkDiameter`, `CsinkAngleDeg` and no fastener. The version stays 1: the task note said to bump it "as earlier tasks did", but no earlier task did (task 100 kept version 1 for optional values), and the new values are optional on read. A dowel stored by task 099 (fastener, no sizes) reads with its sizes null, and `HoleReadBack.WithDowelSeatFromHole` fills them, and the counterbore depth's expression, from the hole. A hole made by Inventor's command that reads as a seated dowel also opens on the hole's own seat.
  - Check messages for a dowel's seat go under its own boxes (not under Size). "Enter a counterbore diameter." and the like when a box is empty. The counterbore or countersink deeper than a blind hole is still shown under Depth, as for every seat.
  - The writer now writes the counterbore depth value even when it equals the old value if the parameter held an expression and the new depth is a plain number, so typing `3` over `d0/8` (= 3) makes it plain, as the depth box does. After review the parent limited this to dowels, so a clearance or tapped counterbore depth set as an expression in Inventor's own dialog is kept as before; rebuilt on Windows, 0 errors.
  - The OK path saves the blind depth and the dowel seat in one settings write (`HoleLastDowelSeats.RememberKept`).
- **Manual checklist for Inventor:**
  1. Open a part with a sketch of hole centres. AWB Hole, Type Clearance, Standard AWB Dowel, size 6 mm, Seat Counterbore. Fastener type is not shown; Counterbore diameter shows 7 and Counterbore depth 3 (the starter Dowel tab). The preview has a 7 x 3 counterbore, in red.
  2. Seat Countersink: Countersink diameter 7, Countersink angle 90 deg. Seat None: neither box. Switch to AWB Clearance with a counterbore: Fastener type is back and no seat boxes.
  3. Back on AWB Dowel 6 mm counterbore: type diameter 9, depth 2. The preview follows each change. Type diameter 5: the message under the box says the hole is not smaller than its counterbore, OK disabled. Type depth 0: message, OK disabled. On a countersink type angle 180: message, OK disabled. Type `abc` in the diameter box: "Enter a number."
  4. In a part with a parameter `d0` (say 24 mm), a 6 mm dowel counterbore with diameter 9 and depth `d0/8`: the counterbore is 3 deep. OK. Manage > Parameters: the counterbore depth reads `d0/8`; change `d0` to 40: the counterbore follows. The seat faces take the fit colour.
  5. Make a new 6 mm dowel counterbore: it starts at the last values kept (9 and 3). An 8 mm dowel starts at the Dowel tab's 9 and 4. Close and reopen Inventor: the 6 mm starts at 9 and 3 still; `%APPDATA%\AwbAddin\settings.json` has `lastDowelSeatsMm` with a `6 mm` entry.
  6. Change the size from 6 mm to 8 mm and back: each size shows its own start values. Units Imperial: the 1/4" row's start values (0.29, 0.125).
  7. Edit the hole from step 4 (Edit Feature or double-click): it opens on its own diameter and `d0/8`, not the remembered values. Change the fit only and OK: the seat is unchanged.
  8. A dowel with a counterbore made before this task (task 099 build): Edit opens on its counterbore as the hole has it, with no Fastener type; OK keeps it.
  9. Point Settings at a hole workbook without the four Dowel columns: the window opens with no workbook problem, and a 6 mm counterbore starts from the M6 Counterbore row. A size with no Counterbore row of its size: the boxes start empty, messages under them, OK disabled until both are typed.
  10. Ctrl+Z after OK: the hole goes in one step.

## Follow-ups

- Task 104 lays out the four seat boxes in the redesigned window and shows `= value` beside a counterbore depth typed as an expression.
- Task 092's stored-versus-applied comparison does not look at a dowel's seat sizes: a seat changed in Inventor's own Hole dialog is not offered for re-applying, and Edit opens on the stored sizes.
- The Dowel tab's autofilter still covers A:D only; widening it means changing `xl/workbook.xml`'s `_FilterDatabase` name too.
- `STATUS.md` was already modified in the working tree when this task ran, not by this task.
