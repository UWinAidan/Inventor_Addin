# 164: Add-in: draw the hole tables as sketched symbols, and Align

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", "Design decision, settled by spike 160", question 4)
- **Status:** todo
- **Depends on:** 161, 163
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 165

## Goal

The wizard's Generate Precision Table, Generate Clearance Table and Create Tables commands draw each table as one sketched symbol on the sheet (Aidan, option 1). The tables look exactly like spike 160's round 6 tables. Align stacks them on the title block corner.

## Scope

- **`src/InventorAddin/HoleTable/HoleTableDrawer.cs`.**
- **Styles and metrics.** Read these from the drawing's active hole table style (`StylesManager`). If the style doesn't expose them, make a temporary hole table on the view, read them, and delete it (spike rounds 3 to 6):
  - the data, header and title text styles;
  - the one-line row height;
  - the inside and outside line weights;
  - the table layer.
- **Text measure.** Implement task 161's measure: a fitted general note in the given style (`GeneralNotes.AddFitted`), read its `RangeBox` width, delete it. Cache by style and text. Every temporary object is deleted, even on failure.
- **Definitions.**
  - One `SketchedSymbolDefinition` per table, named `AWB_HoleTable_<sheet>_<view>_Precision` or `_Clearance`, with the names cleaned of characters Inventor refuses.
  - Draw inside `Edit(out DrawingSketch)` with `SketchLines.AddByTwoPoints` (setting `LineWeight`) and `TextBoxes.AddFitted`.
  - Each text box: centre it (`kAlignTextCenter` and `kAlignTextMiddle`), then shift its `Origin` by the difference between the asked centre and its `RangeBox` centre. A stacked text also sets `StackedTextPosition = kAlignTextMiddle`.
  - A new definition gets a sketch point at (0, 0) with `InsertionPoint = true`.
  - Redrawing deletes the definition's text boxes and lines and draws again. Then `ExitEdit(true)`.
- **Placing.** One `SketchedSymbols.Add(def, point, 0, 1)` per table, on the table layer. Keep the placed symbols found by their definition names (and the attribute set from task 166).
- **Align.** Task 161's positions from `Sheet.TitleBlock.RangeBox` (the max corner). If the sheet has no title block, the bottom-right of `Border.RangeBox`; with neither, stop with a message. Create Tables runs Align once.
- One transaction per command (`CLAUDE.md`), aborted on failure, so one Undo removes the tables.

Out of scope: tags and marks (task 165), stored state and Update (task 166).

## Acceptance criteria

- [ ] Builds on Windows.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Spike 160 "Recommendations" and round 6. The proved script is `spikes/160/spike160-round6b.ps1` on branch `spike/160-hole-table` (`EditSketch`, `Txt`, `Draw-Table`, `Build-Tables`).
- In C#, `SketchedSymbolDefinition.Edit` takes an `out DrawingSketch`. The sketch is not usable after `ExitEdit`.
- Sketched symbols have no `RangeBox`: use the heights from task 161.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Create Tables on VIEW1: two tables, stacked on the title block corner, looking like spike round 6 (symbols, stacks, merged DESCRIPTION and NOTES).
  2. Drag a table: everything moves with it.
  3. One Undo removes both.
  4. Generate one table alone.
  5. Align after dragging puts them back.

## Follow-ups

Things noticed but not done.
