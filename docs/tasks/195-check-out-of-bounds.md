# 195: Check Out of Bounds command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Commands: Check Out of Bounds")
- **Status:** done
- **Depends on:** 184, 191, 194
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that checks every sheet and lists what falls outside, selecting each item when picked from the list.

## Scope

- `src/InventorAddin/Drawing/SheetExtentsReader.cs` (spike 184, question 1).
- `src/InventorAddin/Commands/CheckOutOfBoundsCommand.cs` + ribbon button; the result in a message (none) or a small list window (some).

## Acceptance criteria

- [x] A clean drawing: "Everything is inside the border."
- [x] A view dragged half off the sheet and a note over the title block are both listed with their sheet; picking one selects it.
- [x] Query-only command type (it changes nothing).
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, rounds 1 and 2):
  - Inner rectangle: `Border.RangeBox`. Title block: `TitleBlock.RangeBox`.
  - Boxes from `RangeBox`: `GeneralNote`, `LeaderNote`, `PartsList`, `RevisionTable`, `HoleTable`, `CustomTable`.
  - Views have no `RangeBox`; use `Left`, `Top` (top edge), `Width` and `Height`.
  - Dimensions have no `RangeBox`; use `Text.RangeBox`.
  - Balloons: `Position` plus each `Leader.AllNodes` node's `Position`.
  - Sketched symbols: `Position` (the symbol's centre) only; `Definition.Sketch.RangeBox` is null.
  - A note past the border reports a range past it, so overlap is detectable.

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

Picked to keep work moving; the question is in spec 05's Open questions (number 37).

- A sheet with no border is checked against the sheet's own edge (`Sheet.Width` by `Sheet.Height`); no title block means no title block check.
- Sketched symbols are a point (their centre), so a symbol is reported only when its centre is outside or over the title block.
- The list is a modal dialog (the `WindowHost.ShowDialog` pattern). Picking a line activates that sheet and selects the item; the selection stays when the window closes.
- Everything on a sheet is checked: views, general and leader notes, dimensions (text box), balloons, sketched symbols, parts lists, revision tables, hole tables, custom tables. Items whose extent cannot be read are skipped, logged ("<name> was not checked: its extent could not be read.") and counted; "Everything is inside the border." then adds a line with that count.
- The button is large, on the drawing ribbon only, after Place Initial Views.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: Build succeeded, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5589 passed, 0 failed (added `OutOfBoundsReportTests`, including the not-checked sentence; ribbon and command-document tests extended for the new button; the icon count test went from 16 to 17). No Inventor probe was run: every member was checked against the interop assembly by reflection and by the compiler, and against spike 184.
- **Not compiled (changed under `src/InventorAddin`):** none; all of it compiled on Windows. It has not been run in Inventor: `Drawing/SheetExtentsReader.cs`, `Commands/CheckOutOfBoundsCommand.cs`, `UI/OutOfBoundsWindow.xaml(.cs)`, `UI/RibbonSetup.cs`.
- **Inventor API members not confirmed:** none by the compiler. Not exercised at run time: `Sheet.Border.RangeBox` and `Sheet.TitleBlock.RangeBox` when the sheet has none (read through `ComSafe`, so null), `Sheet.Width/Height` (fallback only), `SelectSet.Select` for a note, dimension, balloon and table, `Sheet.Activate()` while a modal window is open.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a drawing from the print template with a view or two inside the border. The Check Out of Bounds button is on the AWB drawing panel. Click it: "Everything is inside the border."
  2. Drag a view so about half of it is past the sheet edge, and move a general note onto the title block. Click the button: a list names both, each with its sheet ("Sheet:1: View VIEW1 goes past the border's right edge." and "... Note ... is over the title block.").
  3. Click each line: its sheet becomes active and the item is highlighted in the drawing. Close the list; the item stays selected.
  4. Add a second sheet with a note outside its border. The list shows it with that sheet's name, and picking it switches sheets.
  5. Press Ctrl+Z after checking: nothing to undo (the command changes nothing).
  6. Open a part: the button is not on its ribbon.
  7. Click the same line twice, with a click in the drawing between: the item is selected again each time. The list scrolls inside a fixed height and is readable in the dark theme.
  8. Try a balloon placed past the border, a hole table over the title block, and a dimension text outside the border: each is listed.

## Follow-ups

- The list is modal, so it can cover the item being selected; a modeless list (as AWB Hole's window) would let the user move the sheet while it is open. Decide after Aidan tries it.
- Views are measured by their `Left/Top/Width/Height` box, which can include space for view labels or be larger than the drawn lines; if that gives false reports, measure `DrawingCurves` instead.
- Leader notes are measured by their range box only; adding `LeaderNote.Leader.AllNodes` (a member exists in the interop but no spike confirmed it) would cover a leader running off the sheet.
- The list is re-pickable: a click selects on mouse up, so clicking the already-selected line selects its item again (untested in Inventor).
- Sketched symbols and balloons use points, so a large symbol that only partly crosses the border is missed (spike 184).
