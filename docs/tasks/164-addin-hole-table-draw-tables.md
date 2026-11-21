# 164: Add-in: draw the hole tables as sketched symbols, and Align

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", "Design decision, settled by spike 160", question 4)
- **Status:** done
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

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: succeeded, no new warnings (the 8 existing CS8600 warnings are in other files). `dotnet test tests/InventorAddin.Core.Tests`: 4805 passed, 0 failed (9 new tests in `HoleTableNamesTests`; rebuilt and rerun after the review fixes). One full run showed 2 timing-flaky Numbering failures (`SaveAsReplacerTests`, `NewPartCreatorTests`); they passed alone and on the rerun. Nothing was run in Inventor.
- **Compiled, not run in Inventor:** `src/InventorAddin/HoleTable/HoleTableDrawer.cs` (new) and `HoleTableSource.cs` (GenerateTable, CreateTables, AlignTables). They build on Windows but have never run.
- **Inventor API members not confirmed at runtime:**
  - All signatures were checked against `Autodesk.Inventor.Interop.dll` by reflection and the spike script. Not seen working from C#: `SketchedSymbolDefinition.Edit(out DrawingSketch)` (the spike used late binding), `HoleTables.Add(view, point)` with the two optional arguments left out, `SketchedSymbols.Add(def, point, 0, 1)` with the prompt strings left out, `HoleTableRows[1]`, and `SketchedSymbolDefinition.AttributeSets` (writing a double attribute on a definition).
  - `SketchedSymbolDefinition.Sketch.SketchLines` (the fallback height read when the attribute is missing) is unconfirmed outside edit mode.
  - Inventor refusing characters in a definition name: unknown which; `HoleTableNames.Clean` keeps only letters, digits, `_` and `-`.
  - `StylesManager.HoleTableStyles` exposes the three text styles and line weights but not the row height or the layer, so the temporary hole table is still used (as in the spike).
- **Manual checklist for Inventor** (a drawing of a part with an ordinate origin on the view, holes of both kinds, a title block; `-p:DeployToInventor=false` was used, so deploy first with Inventor closed):
  1. Open the Hole Table wizard, pick VIEW1, Create Tables: two symbols appear stacked on the title block's top-right corner (clearance on the corner, precision above), looking like spike round 6: grid, title, header, DESCRIPTION and NOTES merged per group, AIGDT symbols, stacks, two-line descriptions.
  2. Click the symbol of one table and drag it: all lines and text move together. Check no stray text box or line is left behind on the sheet, and the temporary hole table and measuring notes are gone (the browser shows no Hole Table).
  3. Press Undo once: both tables and their definitions' effect disappear in one step (check the browser's Sketched Symbols; the definitions may remain, report it).
  4. Generate Precision Table alone, then Generate Clearance Table: a lone table sits on the corner; the second goes where Align would put it. Generate again after changing a note: the table is redrawn in place and keeps its position.
  5. Drag both tables away, press Align: they return to the stack.
  6. Open a toleranced group with a seat or thread line (for example an H7 hole with a counterbore): one stack text plus one text per later line, centred in the DESCRIPTION cell. This has never been drawn in Inventor; look at the line spacing.
  7. Rename the view (for example to `Bob's view`) after Create Tables, then Create Tables again: the same two tables are redrawn in place (no second pair; the browser still lists one definition per table, named from the sheet and view keys), and the title shows the new name with its apostrophe.
  8. A sheet with no title block: tables go to the border's bottom-right corner. A sheet with neither: the wizard status says there is nothing to stack on and nothing is drawn.
     In that case, and when the view cannot be found, no Undo entry is left (the transaction is aborted, not ended empty).
  9. Force a failure if possible (for example a view with no holes seen end on cannot be analysed anyway): the status shows "Could not ..." and nothing stays on the sheet.

## Follow-ups

- Generate one table lays out with the whole plan's shared column widths, so a table generated after the other was drawn with older data can have different column widths from it. Create Tables or Update redraws both.
- Definition names now use the task 163 sheet and view keys (the readable names are kept in the definition's `AWB_HoleTableDrawing` attribute set for display), so a rename or punctuation cannot collide or orphan a table. A definition for a table that is no longer in the plan (all its holes deleted) is left on the sheet; Update (166) should delete it.
- The height attribute on the definition (`AWB_HoleTableDrawing`, `HeightCm`) is new; task 166 may fold it into its stored state.
