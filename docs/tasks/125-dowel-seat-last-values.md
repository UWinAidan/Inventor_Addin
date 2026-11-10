# 125: A dowel seat starts at the last values used for its size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"; question 14)
- **Status:** done
- **Depends on:** 106
- **Needs:** cloud or windows (Core first, then a look at the add-in's save path)
- **Parallel-safe with:** 122, 123

## Goal

PR #29 check 106-5 failed (Aidan, 2026-10-25): after OK on a 6 mm dowel with a counterbore of 9 x `d0/8` (= 3), a new 6 mm dowel counterbore started at 7 / 3 (the workbook values), not 9 / 3. An 8 mm dowel did start at its last values (9 / 4). The last values per units and dowel size (Aidan, question 14) must win over the workbook columns.

## Scope

- `src/InventorAddin.Core/Holes/HoleLastDowelSeats.cs`, `ViewModels/AwbHoleViewModel.cs`, and where the add-in records the last values on OK (`AwbHoleCommand` or the window code). Find which step loses the 6 mm values: not recorded (for example because the depth was an expression, or the key for the size differs between record and lookup, such as `6` and `6.0`), recorded but not saved to settings, or looked up after the workbook column has already filled the boxes.
- Fix and add Core tests that reproduce it: a counterbore depth given as an expression, record, then a new window for the same size starts at the recorded values; the expression is kept as the depth (task 100), the diameter as a number.

## Acceptance criteria

- [x] The 106-5 case starts at 9 / `d0/8` for the next 6 mm dowel.
- [x] The workbook columns are used only when there are no last values for that units and size.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows if the add-in changed.

## Findings

- **Which step lost the 6 mm values.** Two things, only one of which could be reproduced:
  - **The expression was dropped by design.** Task 106 stored only the number of a counterbore depth typed as `d0/8` (its Default: "remembered values are numbers only"). So even when the record worked, the next seat could only start at the number, not at `d0/8`. Fixed here.
  - **The diameter (9 shown as 7) could not be reproduced in Core.** A Core test of the whole path (type 9 or 11 and `d0/8`, OK, `RememberKept`, `SettingsStore.Save`, `Load`, `HoleLastDowelSeats.From`, a new window on the same size) starts at the remembered diameter. It does so whether Standard, Seat or Size is chosen first. The record is not skipped when the depth is an expression. The size key is the workbook's Size text (`6 mm`) on both record and lookup. The lookup runs before the workbook columns, value by value. The add-in's OK path (`HoleSession.Ok` to `RememberDepth(build.Resolved)` to `SettingsStore.Save` and `AddinServices.UpdateSettings`) and the command (`HoleLastDowelSeats.From(AddinServices.Settings)` when each window opens) match the tests. No other settings write (Settings window, hole toggle, diagram state, preferred materials) starts from a stale copy.
  - **What the field evidence shows.** The "8 mm started at its last values (9 / 4)" in the report is the starter workbook's own 8 mm row (`Cbore dia` 9, `Cbore depth` 4), so it does not show that any last value was used. Aidan's `%APPDATA%\AwbAddin\settings.json` today has only a `2 mm` entry (3 x 1, also the workbook's values), and the log has no settings save error. So the 6 mm seat from the 2026-10-24 check is not in the file now: it was never written, or a later write replaced the file. The log does not say which. To make the next check conclusive, OK now logs the seat it remembered (see the checklist).

## Defaults

- A counterbore depth typed as an expression is remembered as that expression with the value it came to (`"cboreDepthExpression": "d0/8"` beside `"cboreDepth": 3` in `lastDowelSeatsMm`/`lastDowelSeatsIn`; left out for a number). This replaces task 106's Default that remembered values are numbers only, as this task's brief asks. The parent should log it in `DECISIONS.md` and may want to add a line to spec 09 "Dowel and precision holes".
- When the next window starts on a remembered expression, it reads the expression again in the open part, as Edit does: the box shows `d0/8` at the value it comes to in this part now. If the part cannot read it (no `d0`), or there is no expression checker, the box starts at the remembered number as a plain number, with no message.
- A start value from the remembered seat is not "typed" (task 124). It follows a size or units change like any start value, and the expression is checked only when the start value is actually used.
- A number kept with OK over a remembered expression replaces it, even when the value is the same, so the next seat starts at the number.
- OK logs the seat it remembered, for example `AWB Hole: remembered the seat for the next 6 mm dowel (Metric): counterbore 9 mm x d0/8 (= 3 mm).` Nothing is logged when the values were already the ones kept.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 3827 passed, 0 failed (11 new tests; one existing test changed because it asserted the old numbers-only rule). `dotnet build InventorAddin.slnx -tl:off` on Windows with Inventor closed: build succeeded, 0 errors, 8 warnings (the existing CS8600 warnings in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand`; none in files this task touched). The build deployed the add-in to Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none. `src/InventorAddin/Holes/HoleSession.cs` (one log line after the settings save) was built on Windows. Not run inside Inventor yet.
- **Inventor API members not confirmed:** none. No Inventor API is used that was not already there.
- **Manual checklist for Inventor:**
  1. Open a part with a sketch of hole centres and a model parameter `d0` (for example 24 mm, so `d0/8` = 3). AWB Hole, Standard AWB Dowel, size 6 mm, Seat Counterbore. Type diameter 9 and depth `d0/8`, then OK. In `%APPDATA%\AwbAddin\awbaddin.log`, the line after "made HoleN" reads `remembered the seat for the next 6 mm dowel (Metric): counterbore 9 mm x d0/8 (= 3 mm).` `settings.json` has `lastDowelSeatsMm` with a `6 mm` entry holding `cboreDiameter` 9, `cboreDepth` 3 and `cboreDepthExpression` `d0/8`.
  2. New AWB Hole, AWB Dowel, 6 mm, Counterbore: the boxes start at 9 and `d0/8`, with `= 3 mm` beside the depth. OK: Manage > Parameters shows the new counterbore depth as `d0/8`.
  3. In the same window, change the size to 8 mm: 9 and 4 (the workbook, since no 8 mm seat has been kept). Back to 6 mm: 9 and `d0/8` again.
  4. Restart Inventor and repeat step 2: still 9 / `d0/8`.
  5. Open a part without a `d0` parameter. A 6 mm dowel counterbore starts at 9 and 3 as plain numbers, with no message under the depth box, and OK is enabled.
  6. Make a 6 mm dowel counterbore with depth typed as `2` and OK. The next 6 mm starts at 9 and 2, with no expression.
  7. If step 2 still shows 7 / 3, send the log lines from step 1 and the `lastDowelSeatsMm` part of `settings.json`. The remembered line shows whether the record was made, and the file shows whether it was saved.
  8. 106 checks 6, 7 and 10, not tested yet, at the same time.

## Follow-ups

- The blind depth (`HoleLastDepths`, task 097) is still remembered as a number only. Should a blind depth typed as an expression be remembered as one too, for consistency with this task? A question for Aidan, not done here.
- Spec 09 "Dowel and precision holes" does not say whether a remembered counterbore depth keeps its expression. The parent may add the Default above to the spec and `DECISIONS.md`.
