# 220: Hole tables stay on the sheet

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/04-hole-table-wizard.md`
- **Status:** todo
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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
