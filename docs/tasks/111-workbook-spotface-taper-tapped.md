# 111: Hole workbook: Spotface and Taper Tapped tabs

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The workbook", "Taper tapped holes", "Types and seats")
- **Status:** done
- **Depends on:** 106
- **Needs:** cloud or windows (Core and the starter workbook)
- **Parallel-safe with:** 110, 119

## Goal

The workbook reader knows two new tabs, Spotface and Taper Tapped (Aidan, 2026-10-24), and the starter workbook has them, so tasks 112 and 114 can offer spotfaces and taper tapped holes.

## Scope

- `src/InventorAddin.Core/Holes/HoleWorkbookReader.cs`, `HoleWorkbookRows.cs`, `HoleWorkbook.cs`, `HoleWorkbookMessages.cs`: the two tabs, laid out like the others (title row 1, note row 2, headers row 4).
  - **Spotface**: Units, Size, Spotface dia, Spotface depth, Show (Default, 2026-10-24).
  - **Taper Tapped**: Units, Size, Thread type, Tap drill, Default thread depth, Default drill depth, Show, like the Tapped tab (Default, 2026-10-24). The default depth columns may be formulas; a blank cell is worked out from the Settings tab's multipliers, as for Tapped.
- **Both tabs are optional**: a workbook without them (Aidan's own copy until he adds them) loads with no problem for a missing tab; Spotface rows are then none, and Taper Tapped has no sizes. A tab that is present with a missing header is reported as today.
- `libraries/AWB_Holes.xlsx` (the starter): add the two tabs with **invented** starter rows (`CLAUDE.md`: no copied data): a handful of NPT sizes as Imperial rows and BSPT sizes as Metric rows, and Spotface rows for the sizes the Counterbore tab has. Update the How to edit tab's text for them. Follow how tasks 097 and 106 edited the package (shared strings, the autofilter note in `TODO.md`).
- `tests/InventorAddin.Core.Tests/Holes/`: reader tests for both tabs, a workbook without them, bad values, `Show = N`, and the starter workbook loading with no problems.

Out of scope:

- Using the rows in the window (112, 114).

## Acceptance criteria

- [x] Both tabs are read into typed rows in the row's units, as `decimal`, like the Tapped and Counterbore rows.
- [x] A workbook without either tab loads with no problem reported for it.
- [ ] The starter workbook opens in Excel (not yet: manual checklist step 1) and loads in Core with no problems. (Loads in Core: tested. Opening in Excel: not done in this session; checklist step 1.)
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer. This task finished a stalled earlier attempt. Its uncommitted Core changes were checked against the brief and kept: the two row records, the reader's constants and headers, the optional-tab handling, `MissingOptionalTabs`, `HoleWorkbookMessages.MissingOptionalTab` and the two row readers. One change was made: a Spotface depth of 0 is now allowed (see Defaults).

- **Ran (Windows):** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 3903 passed, 0 failed.
- **Tests added.** In `HoleWorkbookReaderTests`:
  - A workbook without either tab loads with no problem and names both in `MissingOptionalTabs`. With one tab missing, only that one is named. `HoleWorkbook.Empty` has no rows for either tab.
  - Spotface rows are read in their units, `Show = N` is hidden, and a depth of 0 is allowed.
  - Taper Tapped uses cached formula values, works out blank depths from Settings with the tap drill as D, and keeps a typed drill depth.
  - Tab and header names match ignoring case, in any order, and extra columns are ignored.
  - Bad rows on both tabs are left out with one problem each: bad units, a blank size or thread type, non-numbers, 0 or negative values, Excel error cells, a bad Show.
  - A tab that is present with a missing header is reported, and is not named as missing.
  - The starter workbook has both tabs, in the spec's order and with the right headers, and loads with no problems. It has a Spotface row for every Counterbore size. Its Taper Tapped rows are BSPT Metric and NPT Imperial, and their cached depths equal what the reader works out. How to edit has a line for each tab.

  In `HoleWorkbookMessagesTests`: the missing-tab note.
- **Workbook:** `libraries/AWB_Holes.xlsx` was edited in its package XML with Python `zipfile` (script `add_tabs_111.py`, kept in the session scratchpad).
  - Changed entries: `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `[Content_Types].xml`, `xl/sharedStrings.xml` and `xl/worksheets/sheet1.xml` (How to edit). New entries: `xl/worksheets/sheet10.xml` (Spotface) and `sheet11.xml` (Taper Tapped). Every other entry is byte-identical to HEAD, in the same order. Every XML part parses.
  - Tab order follows spec 09: Spotface after Countersink, Taper Tapped after Tapped (sheetIds 10 and 11, rId13 and rId14). The `localSheetId`s of the `_FilterDatabase` names were renumbered for the new order. Each new tab has an autofilter over all its columns, with a matching `_FilterDatabase` name, so the narrow-filter problem in the `TODO.md` note about Dowel does not apply to these two.
  - Both tabs are laid out like the others: title in row 1 (style 7), note in row 2 (style 14), headers in row 4 (style 15), frozen below row 4, text in style 10, numbers in style 16, and Units and Show dropdowns on rows 5 to 304. Taper Tapped's depth columns are grey formula cells (style 17), like Tapped's: `ROUND(Dn*Settings!$B$5,3)` and `ROUND(En+Dn*Settings!$B$6,3)`, with cached values worked out at the starter multipliers.
  - **Starter rows.** Spotface values are invented round numbers. The Taper Tapped tap drills are rounded from common published tap drill sizes (public standard values, not employer data); the depths are formulas.
    - Spotface: one row per Counterbore size. Metric, M3 to M20: the diameter is about 2 mm over the counterbore (8, 10, 11, 13, 16, 20, 22, 28, 34), and the depth is 0.5 mm up to M6 and 1 mm from M8. Imperial, #4 to 1/2: the diameter is about 0.06 in over the counterbore, and the depth is 0.02 in up to #10 and 0.04 in from 1/4.
    - Taper Tapped, Metric: `BSP Taper Internal` Rc 1/8, 1/4, 3/8, 1/2 and 3/4, with tap drills of 8.4, 11.2, 14.8, 18.4 and 24 mm.
    - Taper Tapped, Imperial: `NPT` 1/16, 1/8, 1/4, 3/8, 1/2 and 3/4, with tap drills of 0.25, 0.34, 0.44, 0.58, 0.72 and 0.92 in.
  - There are 19 new shared strings (indices 188 to 206). `count` (675) and `uniqueCount` (207) were recounted from the sheets.
  - How to edit gets a "Spotface:" line after "Countersink:" and a "Taper Tapped:" line after "Tapped:". Both lines say the tab is optional. The rows below moved down by one or two, to 26 rows in all.
- **Not compiled (changed under `src/InventorAddin`):** none. This task changed nothing under `src/InventorAddin`, so the add-in was not rebuilt. Nothing there builds a `HoleWorkbook` positionally, and the new properties are `init` properties with defaults.
- **Inventor API members not confirmed:** none are used. Not confirmed: that Inventor's thread table names every starter size as written. Spike 108 confirmed only `NPT` with `1/4` and `BSP Taper Internal` with `Rc 1/4`. The other sizes (1/16, 1/8, 3/8, 1/2, 3/4, and Rc 1/8, 3/8, 1/2, 3/4) follow the same pattern; they get checked when task 114 makes holes from them.
- **Manual checklist:**
  1. Open `libraries/AWB_Holes.xlsx` in Excel. It opens with no repair prompt. The tabs read: How to edit, Settings, Clearance, Counterbore, Countersink, Spotface, Tapped, Taper Tapped, Dowel, Precision Tol, Drill Point.
  2. Spotface tab: the title is "Spotfaces", with a note under it. The headers are in row 4, with filter buttons over A:E, and the rows below row 4 are frozen. There are 17 rows (M3 to M20, #4 to 1/2). The Units and Show cells offer dropdowns.
  3. Taper Tapped tab: the headers are Units, Size, Thread type, Tap drill, Default thread depth, Default drill depth and Show, with filter buttons over A:G. The two depth columns are grey formulas. Change Settings > Thread depth (x D) from 1.5 to 2: the Taper Tapped thread depths recalculate (Rc 1/4 goes from 16.8 to 22.4). Undo the change and close without saving.
  4. How to edit: there is a Spotface line after Countersink and a Taper Tapped line after Tapped.
  5. Optional, in Inventor: open AWB Hole with the starter workbook. The window opens with no workbook problem; the window does not use the new tabs until tasks 112 and 114. Point Settings at an older copy without the two tabs: the window also opens with no workbook problem.

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **A blank Taper Tapped depth uses the tap drill as D**, since the tab has no Nominal column: thread depth = round(tap drill x Thread depth (x D), 3), and drill depth = round(thread depth + tap drill x Drill extra depth (x D), 3). The starter's formulas do the same.
- **A Spotface depth of 0 is allowed** in the workbook, as the window allows it (`DECISIONS.md`, 2026-10-28; spike 108). Spotface dia must be more than 0.
- **A missing optional tab is not a problem.** It is listed in `HoleWorkbook.MissingOptionalTabs` (Spotface first, then Taper Tapped). `HoleWorkbookMessages.MissingOptionalTab` gives the note for the window: "The hole workbook has no "Taper Tapped" tab, so it gives no Taper Tapped sizes. Copy the tab from the starter workbook into yours to add them." A tab that is present but has a missing header is a problem, as for any tab, and is not listed as missing.
- The Spotface tab is keyed by Size only (no Screw column). Its starter rows use the Counterbore tab's sizes.
- The starter Taper Tapped rows are Metric for BSPT (Rc sizes, tap drill in mm) and Imperial for NPT (tap drill in inches), as the brief says.

## Follow-ups

- Tasks 112 and 114 use the rows: `HoleWorkbook.Spotface` and `HoleWorkbook.TaperTapped`, plus `MissingOptionalTabs` with `HoleWorkbookMessages.MissingOptionalTab` for the note shown where Taper Tapped lists no sizes (spec 09, "The workbook").
- Task 114 should check each starter Taper Tapped size against Inventor's thread table (spike 108 proved only NPT 1/4 and Rc 1/4), and fix any starter row that Inventor names differently.
- Spotface rows are keyed by Size as the Clearance tab writes it (M6, 1/4). A tapped hole's size is its thread (M6, 1/4-20 UNC), so task 112 needs a rule for matching a tapped hole to a Spotface row, for example by the Tapped row's nominal or by the thread's leading size.
- Aidan's own copy of the workbook needs the two tabs copied in from the starter before it can offer spotface starting values and taper tapped sizes.
