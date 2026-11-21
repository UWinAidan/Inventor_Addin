# 166: Add-in: store the wizard's state, Update, and re-open a table on the Edit Table tab

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update, Edit Table tab)
- **Status:** done
- **Depends on:** 162, 164, 165
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** none

## Goal

After the model changes, Update redraws both tables with fresh XDIM, YDIM and descriptions, keeps every tag, and tags any new hole. The Edit Table tab re-opens a view's tables with their edits and notes. Everything is stored in the drawing, so it survives save and reopen.

## Scope

- **Storing.** Store task 162's `HoleTableState` JSON in an attribute set `AWB_HoleTable` (attribute `State`, a string) on the placed table symbols. Store the view link on the sheet (spike 160, C10). Write it on Create, Update and Renumber.
  - On the document, use `AttributeSets.Add(name, false)`; `true` fails.
  - Find stored views by scanning the sheet's sketched symbols for the set.
- **`ReadState` and `UpdateTables` (task 162's source methods):**
  1. read the view again (task 163);
  2. refresh the plan (task 162);
  3. redraw both definitions in place (task 164);
  4. add tags, precision marks and centre marks for new holes;
  5. delete the ones whose hole is gone (found by task 165's mark attribute set, `HoleTableMarks.AttributeSetName`, with `HoleId`, `ViewKey` and `Role`; it is a different set from the table state's, so tags and marks are never mistaken for tables);
  6. update the tag text of any hole whose tag changed (Renumber).

  A table that no longer has any group is deleted along with its definition.
- One transaction per Update, aborted on failure.
- The status line and the log name what changed (task 162's counts).

## Acceptance criteria

- [ ] Builds on Windows.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Spike 160 round 6: the update test in `spikes/160/spike160-round6b.ps1`. Round 5: C10, attribute sets survive `SaveAs` and reopen. An `AttributeSet` is enumerable, so do not pass one through LINQ that might flatten it.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors (warnings are the existing CS8600 ones in other commands). `dotnet test tests/InventorAddin.Core.Tests`: 4831 passed, 0 failed after the review fixes (4815 before the task). Not run in Inventor.
- **Changed:** Core: `HoleTableMarkSync.cs` (new: which holes to mark again or unmark, and the status text), `HoleTableNames.cs` (`StateAttributeSetName` = `AWB_HoleTable` from Branding, `StateAttributeName`, `UpdateTransactionName`), `HoleTableState.cs` (`HoleTableStateJson.WarnLength`). Add-in: `HoleTableStateStore.cs` (new), `HoleTableSource.cs` (`ReadState`, `UpdateTables`, state written on Create and Generate), `HoleTableDrawer.cs` (`DeleteTable`), `HoleTableMarker.cs` (`ReadMarks`, `Remove`). Review round 1 added: `HoleTableNames.SheetRecordAttributeSetName` and `SheetRecordAttributeName`, `HoleTableMessages.StoredStateUnreadable`, `HoleTableUnchangedException` (in `HoleTableDrawer.cs`), `HoleTableMarker.StampOrDelete`.
- **How it works:**
  - The state JSON is written to each placed table symbol of the view (set `AWB_HoleTable`, attribute `State`, `AttributeSets.Add(name, false)`), so it survives one table being deleted. `ReadState` finds the sheet by key and reads the symbols whose definition name is the view's two table names (the keys are in the name); the view itself need not exist.
  - Update (one transaction `AWB update hole tables`, aborted on failure or when it changed nothing): reads the previous state; deletes a table with no group (symbols, then the definition); redraws the others in place without moving them; compares the plan's tags with the previous stored tags and the marks on the view (`HoleTableMarkSync.Decide`); removes marks of holes no longer in the plan; places tag, precision mark and centre mark again only for new holes, holes whose tag changed (Renumber, swap, split), holes with a missing mark, and holes whose precision mark changed; writes the state. The status line is the drawer's message plus the marks line (counts of holes marked and removed); the view-model adds task 162's counts (added, gone, description changed).
  - The viewer's `Refresh` runs in the view-model, so the source receives the refreshed plan and does not refresh again. Update on a view with no stored state does nothing and says so.
  - Review round 1 (Aidan's decision, DECISIONS: shared state on the sheet): the state is also written to a record on the sheet (set `AWB_HoleTableViews`, one attribute `View_<viewKey>` per view), on Create, Generate and Update, also when an Update leaves both tables empty. `ReadState` reads the table symbols first, then the sheet's record, so Edit Table still lists a view whose tables were all deleted. A stored text that cannot be read throws with the reason, so the window says "Could not read the stored tables: ..." instead of "no stored tables". A table action that changed nothing aborts its transaction and throws (`HoleTableUnchangedException`), so the window shows "Could not ..." and does not take it for success (no base tags set). A tag, precision mark or centre mark whose `AWB_HoleMark` stamp fails is deleted and the hole counts as could-not-be-marked, so Update never piles up unstamped duplicates.
  - The `AWB_HoleTableDrawing` set (HeightCm, SheetName, ViewName) on the definitions is left as it is; the state keeps its own sheet and view names. Align still reads the height from it.
  - 32 KB: the state's length is logged on every write (`Hole table: stored state is N characters`), as a warning above 28000. Check one real id in the log.
- **Not compiled (changed under `src/InventorAddin`):** nothing; all of it built on Windows. It has not been run in Inventor.
- **Inventor API members not confirmed:** none new beyond existing use. `SketchedSymbolDefinition.Delete()` and `SketchedSymbol.Delete()` are used in spike 160 round 6b/7 (cleanup). `AttributeSets.Add(name, false)` on a `SketchedSymbol`, `Centermark.AttributeSets` and `Sheet.Centermarks` iteration are already used by `HoleTableMarker`. Untested: deleting a definition straight after deleting its last symbol in the same transaction (spike did it in one cleanup pass).
- **Manual checklist for Inventor:** (use a drawing of a personal part with a precision and a clearance hole, one ordinate origin on the view)
  1. Hole Table, Create Tables. Check `%APPDATA%\AwbAddin\awbaddin.log` for `stored state is N characters` with a realistic hole count; N should be well under 28000 (about 75 characters per hole id).
  2. Change a parameter that moves holes, then Hole Table, Edit Table tab, open the view (its tags and notes show), Update. XDIM and YDIM change, every tag stays, and the tables stay where they were. Status shows the counts.
  3. Add a centre to a hole feature, Update: the hole gets the next number in its group with a tag, centre mark (and precision mark if in Precision). Other holes' marks do not move or duplicate.
  4. Remove a centre, Update: its row, tag and marks go; the other tags are unchanged (a gap is left).
  5. Renumber, then Update (Default, spec 04 question 20): tags change on the tables and on the view, with no duplicate tags left behind.
  6. Swap a hole between tables, Update: its tag and precision mark follow it.
  7. Remove every hole of one table (for example all clearance holes), Update: that table and its symbol are gone, the other stays. Add one back, Update: the table returns at the stack position.
  8. Save, close, reopen. Open the wizard: Edit Table lists the view; opening it shows the same tags, swaps, deletes, splits and notes.
  9. Press Undo once after an Update: the whole Update reverts (tables, tags, marks, stored state). Undo after Create reverts the state too.
  10. A second view or sheet with its own tables does not list under the first view, and Update on one leaves the other's marks alone.
  11. Remove every hole from both tables (or suppress the features), Update: both tables and all tags and marks go, the status says so. Edit Table still lists the view; opening it keeps the notes, swaps and splits. Add the holes back and Update: the tables return with the old tags.
  12. Delete both table symbols by hand, then Edit Table, open the view: it opens from the sheet's record with its notes and edits; Create Tables draws them again.
  13. Create Tables on a sheet with no title block or border: the status reads "Could not create the tables: ...", nothing is left in the Undo list, and a later edit in the window still renumbers (no stored tags were taken).

## Follow-ups

- Records for views that were later deleted stay in the sheet's `AWB_HoleTableViews` set (harmless; the list only checks views that exist); prune some day.
- `HoleTableStateStore.Read` logs a parse reason only when nothing could be read; a corrupt copy on a symbol with a good sheet record behind it goes unlogged.
- A view whose stored state cannot be read is left out of Edit Table, so Analyze treats it as fresh and Create could overwrite it (acceptable while only state version 1 exists).
- Align on a view with no tables now reads "Could not align the tables: ..." (an unchanged action throws after its abort).
- Update redraws a table in place but does not move it. If a table grows (more rows), a table stacked above it may overlap until Align is pressed. Update could Align when a height changes.
- A hole's tag, precision mark and centre mark are replaced as three separate objects; a mark Aidan moved by hand is replaced (and put back to the default place) when that hole's tag changes.
- `HoleTableSource.MarkHoles` analyses the view again on every Update that has holes to mark; fine for now.
- Update decides from the stored tags, not from the text on the view's tags, so a tag edited by hand on the sheet is not noticed.
