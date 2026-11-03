# 132: Several tolerance levels per fit

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes")
- **Status:** done
- **Depends on:** 111, 113
- **Needs:** cloud or windows (Core and the starter workbook)
- **Parallel-safe with:** none (the starter workbook is shared with 111, 131)

## Goal

Aidan, 2026-10-25: more than one tolerance level per fit, for example light, medium and heavy press, close and loose slip, and a couple of normal levels. The starter Precision Tol tab gets such rows, metric and imperial, with clear names, and the Tolerance dropdown lists every row for the chosen fit.

## Scope

- `libraries/AWB_Holes.xlsx` (starter): Precision Tol rows, metric and imperial, invented by us from ISO 286 hole codes and explicit limits, with clear names (Default), for example:
  - Press: Light press (P7), Medium press, Heavy press
  - Slip: Close slip (G6), Loose slip (F7)
  - Normal: Normal (H7), Normal fine (H6)
  - one Default = Y row per fit and units.
  Pick codes that Core's ISO 286 table covers (task 083: F, G, H, JS, K, M, N, P at IT5 to IT11) and say in the How to edit tab what each level is for. Imperial rows as Limits rows in inches.
- Core: check the list already shows every row of the fit (spec 09, task 098) and that one Default per fit and units is preselected; if two rows of a fit say Default = Y, the first wins and a problem names the second (Default). Tests with the new starter workbook.
- The secondary text in the list is the row's name, so the names must read well there.

## Acceptance criteria

- [x] The starter workbook has at least two levels for each fit, metric and imperial, and loads with no problems.
- [x] The Tolerance dropdown lists every row for the chosen fit and units, with the tolerance at the size and the name.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

- **Ran (Windows, Aidan's PC):**
  - `dotnet test tests/InventorAddin.Core.Tests`: 4131 passed, 0 failed, 0 skipped. (The first run had 1 failure, `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`, the known timeout-based flaky test in TODO.md; it passed on the re-run, with every other test.)
  - `dotnet build InventorAddin.slnx` (Inventor was not running, so the deploy step ran): 0 errors, 8 warnings, all existing CS8600 warnings in files this task did not touch.
- **Workbook:** `libraries/AWB_Holes.xlsx` edited in its package XML with Python `zipfile` (script `levels_132.py` in the session scratchpad). Only `xl/sharedStrings.xml`, `xl/worksheets/sheet8.xml` (Precision Tol), `xl/worksheets/sheet1.xml` (How to edit) and `xl/workbook.xml` changed; every other entry is byte-identical to HEAD, in the same order, with the same compression. The Precision Tol tab now has 16 rows (A5:I20), in the order Normal, Slip, Press for each units:

  | Name | Fit | Metric (ISO) | Imperial (Limits, in) | Default |
  |---|---|---|---|---|
  | Normal | Normal | H7 | +0.0005 / 0 | Y |
  | Normal fine | Normal | H6 | +0.0003 / 0 | N |
  | Close slip | Slip | G6 | +0.0007 / +0.0003 | N |
  | Standard slip | Slip | G7 | +0.0010 / +0.0005 | Y |
  | Loose slip | Slip | F7 | +0.0015 / +0.0008 | N |
  | Light press | Press | M7 | 0 / -0.0003 | N |
  | Medium press | Press | N7 | 0 / -0.0005 | Y |
  | Heavy press | Press | P7 | -0.0003 / -0.0008 | N |

  The defaults keep the codes and limits that were the defaults before (H7, G7, N7; imperial +0.0005/0, +0.001/+0.0005, 0/-0.0005). Old names "Dowel pin, standard press", "Location fit" and "Slip fit, removable" are gone (renamed Medium press, Normal, Standard slip). The dimension, the autofilter and the `_FilterDatabase` name cover A4:I20; the four data-validation lists already run to row 304. The How to edit tab's Precision Tol line now says names are used once per units and one row per fit and units has Default = Y, and three new lines after it (Normal levels, Slip levels, Press levels) say what each level is for; the blank line and STARTER VALUES moved to rows 28 and 29. Four shared strings used only by the old rows were rewritten in place and 11 added (220 unique); the sst count is recounted from the sheets (733). The cell comment on Precision Tol C6 is unchanged (it is an example, not tied to the row).
- **What a toleranced Clearance or Simple hole preselects:** the first Default = Y row of the units (task 113) is now **Normal** (H7 metric, +0.0005 / 0 imperial), because Normal rows come first. Before this task it was the metric press row (N7). Tested.
- **Core:** the list already showed every row of the fit (task 098) and preselected the first Default = Y; confirmed with the starter workbook in new view-model tests. `HoleWorkbookReader` now checks the Precision Tol rows against each other (`CheckPrecisionTolerances`):
  - a second Default = Y for one fit and units: the first wins, the later row is kept but not as the default, and a problem names it: `Precision Tol tab, row 7: Default is Y on row 6 too, for Metric Press fit; row 6 is the default.`
  - a name used twice in one units (ignoring case and spaces, whatever the fit): the later row is left out with a problem: `Precision Tol tab, row 8: Name / purpose "Test snug" is already used by row 5 for Metric holes; row left out.` This settles the task 113 reviewer's follow-up (two rows of one name in different fits could not both be reached, since the window lists and stores a tolerance by name). The same name in Metric and Imperial is fine.
- **Tests added:** `HoleWorkbookReaderTests` (duplicate name, two defaults, the starter's levels and defaults per units, every metric level has ISO 286 limits at every metric dowel size, the How to edit level lines); new `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleStarterTolerancesTests.cs` (a 6 mm metric dowel lists each fit's levels with the tolerance and the name, e.g. `M7  0 / -0.012`, `N7  -0.004 / -0.016`, `P7  -0.008 / -0.020`; imperial lists Limits in inches; each press level builds with its ISO code; a toleranced clearance hole starts on Normal and lists all eight levels).
- **Stored hole data:** unchanged (version 4).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed.
- **Inventor API members not confirmed:** none (no Inventor API code changed). Not yet seen in Inventor: that its hole fit tolerance accepts **M7, H6 and G6** as it does N7, P7, H7, G7 and F7.
- **Manual checklist for Inventor:**
  1. Open `libraries/AWB_Holes.xlsx` in Excel: no repair prompt. The Precision Tol tab reads the 16 rows above, the filter buttons cover A to I down to row 20, and the How to edit tab has the Normal, Slip and Press levels lines under Precision Tol. Point Settings at the starter workbook (or copy the rows into your own).
  2. Open a part, AWB Hole, Standard AWB Dowel, Metric, size 6 mm, Fit Press: the Tolerance list shows `M7  0 / -0.012` Light press, `N7  -0.004 / -0.016` Medium press (preselected), `P7  -0.008 / -0.020` Heavy press, then Custom. Pick each and OK (or watch the preview): Inventor's hole dialog shows the diameter tolerance as M7, N7 and P7 in turn.
  3. Fit Slip: Close slip G6, Standard slip G7 (preselected), Loose slip F7. Fit Normal: Normal H7 (preselected), Normal fine H6. Pick G6 and H6 and check Inventor takes them.
  4. Imperial, Press, Slip, Normal: each lists its levels as limits in inches, for example Slip `+0.0007 / +0.0003`, `+0.0010 / +0.0005`, `+0.0015 / +0.0008`.
  5. A Clearance hole, tick Toleranced: the list preselects Normal (H7) and lists all eight levels, then Custom; the hole is coloured normal blue.
  6. In a copy of the workbook, set Default = Y on two Press rows, or give two rows one name: the window reports the problem naming the row.

## Follow-ups

- Spec 09 "Dowel and precision holes" could say the two workbook rules this task added: a Precision Tol name is used once per units (a repeat is left out with a problem), and with two Default = Y rows for a fit and units the first wins and the second is reported (Default).
- A hole made with the old starter rows ("Dowel pin, standard press", "Location fit", "Slip fit, removable") and edited against the new starter will not find its row by name. Only holes built while Settings pointed at the starter are affected; Aidan's own workbook keeps its names.
- A Precision Tol row named "Custom" is still silently left out of the list (`ToleranceOptionsFor`); the reader could report it as it now reports repeated names.
