# 220: Hole tables stay on the sheet

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/04-hole-table-wizard.md`
- **Status:** blocked after two review rounds (2026-11-22). The work is on branch `wip/220-hole-tables-blocked`. Remaining must-fix: when clearance parts exist but do not fit, `HoleTableAlign.Fit` still gives the precision table the anchor, so Align (and Create of the precision table alone) can put it on top of a clearance table that stayed where it was. Fix: give the precision table no spot and warn, or fit it relative to the clearance table's position; add an Align test (an old, too-tall clearance table plus a precision table).
- **Depends on:** none
- **Needs:** windows build
- **Parallel-safe with:** any Core-only task

## Goal

On a small sheet (ANSI B) the stacked hole tables ran off the top of the sheet. Align and Create Tables must keep every table inside the border and above the title block (Aidan, 2026-11-22).

## Scope

- **Core** (the stacking and placement rule in `src/InventorAddin.Core/HoleTable/`). Given the border, the title block's box and each table's size:
  - Stack the tables as now while they fit.
  - When the stack does not fit, continue the clearance table in a second column to the left. Where a table may split into parts, use Inventor's own split, or two tables, whichever the current drawer supports; decide from the code and say which.
  - When it still does not fit, place nothing outside the border, and return a warning suggesting a bigger sheet.
- **Add-in:** Align and Create Tables use the rule, and show the warning in the existing report.
- Tests: the stack fits; a second column; too big (warning, nothing outside).

## Acceptance criteria

- [ ] On ANSI B, the tables that ran off the top now sit inside the border in two columns, or warn.
- [ ] No table is ever placed outside the border.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5269 passed, 0 failed. New Core tests cover: the stack fits; precision beside; a clearance table cut into parts (parts no taller than the limit, title and heading repeated, every row kept, whole groups kept, a row taller than a part); parts lined up to the left; parts past the left border (warn); a misfitting clearance table puts precision on the anchor; unknown width never gets a neighbour beside it; warning wording (not moved / nothing was changed); `KeepInside`; `BlockWidth`; precision beside the clearance table follows it when it widens; part names (`_Clearance_2`) and `PartOf`.
- **Not compiled (changed under `src/InventorAddin`):** none. `HoleTable/HoleTableDrawer.cs` and `HoleTable/HoleTableStateStore.cs` built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** none new (`Sheet.Border.RangeBox`, `SketchedSymbols`, `SketchedSymbolDefinitions`, `Definition.Delete`, `AttributeSets`, `SketchLine`, as used elsewhere in the drawer).
- **How it works:**
  - A clearance table taller than the space above the title block is cut between rows into parts (`HoleTableLayout.SplitRows`, `For(..., clearanceMaxHeightCm)`); each part repeats the title and heading. Part 1 keeps the definition name, later parts get `_2`, `_3` (`HoleTableNames.Definition(..., part)`, `PartOf`). Parts stand side by side to the left of the anchor, 0.5 cm apart.
  - Order: stack; precision beside the clearance table; then split the clearance table into columns; then warn "does not fit inside the border ... Use a bigger sheet".
  - Create Tables: if a drawn table has no spot, nothing is drawn, no tags, marks or state are written, and the transaction is aborted. Align moves what fits and warns "not moved" for the rest; Align cannot cut a drawn table, so it points to Create Tables.
  - Update: if the redrawn clearance table would stick out where it is, everything is placed again from the anchor (split if needed); the precision table is checked against its new size and moved to the fitted spot if it would stick out; a precision table beside the clearance table keeps beside it when that table widens. If nothing fits, Update is undone with the warning.
  - The state store finds every part of both tables.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a part with many clearance holes and some precision holes; make an ANSI B drawing with a view of it.
  2. Hole Table, Analyze, Create Tables. Every table sits inside the border and above the title block; the precision table is above the clearance table or beside it.
  3. Use a part with so many clearance holes that one column is too tall on ANSI B. The clearance table is continued in a second column to the left, each part with its title and heading, the report says it is continued, the hole letters and numbers run on in order, and every hole has one tag and no stray tags.
  4. Drag a table elsewhere, click Align. All tables return inside the border (parts side by side).
  5. Add holes to the part so the clearance table grows on a nearly full sheet, then Update. The table stays inside the border (it may become two columns); tags are right; nothing outside the border.
  6. With the precision table beside the clearance table, Update after adding clearance holes. The precision table moves left with the widened table and does not overlap it.
  7. Switch to a sheet too small for the tables (A4) and click Create Tables. A warning says the tables do not fit and to use a bigger sheet. One Undo afterwards changes nothing visible (no tables, tags or marks were left), and Edit Table shows no stored tables for the view.
  8. Same sheet, Align on tables placed earlier. The warning says they were not moved.
  9. On a large sheet where the stack fits, the old stack is unchanged.
  10. One Undo after Align, Create (split case) or Update reverts the whole command.

## Follow-ups

- Defaults taken (log in DECISIONS.md as Default): tables split only between rows of the clearance table (a group taller than a column is cut, both pieces keep the description and note); the precision table is never split; the column gap is 0.5 cm; Align cannot split a table already drawn (it warns and points to Create Tables); an Update that cannot fit is undone as a whole.
- When Create Tables draws only one kind and an already placed other kind cannot fit, the other kind is left where it is and not reported.
- Part counts are assumed to be numbered 1 to N without gaps; a hand-deleted middle part is not repaired until Create Tables or Update.
