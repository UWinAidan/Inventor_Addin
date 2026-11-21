# M4 part 2 add-in check: hole table wizard (tasks 163 to 167)

Built on Windows on 2026-11-17 on branch `claude/m4-hole-table`; nothing has been run in Inventor yet. Close Inventor, build with `dotnet build InventorAddin.slnx` (check it says the add-in was deployed), start Inventor, and check the Built time in About. Then work through the tasks in this order: the window first, since the other checks use it. Each section is copied from its task file's Verification; the task file also lists the Inventor API members not yet confirmed at runtime.

Use a drawing of one of your own parts with precision and clearance holes and an ordinate origin on the view. Log: `%APPDATA%\AwbAddin\awbaddin.log`.

## 167: Add-in: the Hole Table Wizard window and its ribbon button

From `docs/tasks/167-addin-hole-table-window-and-button.md`.

(close and reopen Inventor after the deploy build; the add-in loads at start):
  1. Open a part, then an assembly: the AWB Addin tab has no Hole Table button. Open a drawing (`.idw`) with a part view that has holes: the AWB Addin tab has a large **Hole Table** button on a Drawing panel, with the AWB Hole icon (a placeholder). With no document open, there is no button.
  2. Click it with the drawing active: the window "Hole Table" opens, the header says the drawing's name. Sheet shows the active sheet, View shows its first view. Click it on a drawing with no views: the lists are empty and the status says to pick a view.
  3. Pick another sheet: the View list changes. Pick another view: the lists clear and the status says to click Analyze View.
  4. Click **Analyze View**: the Precision and Clearance lists fill, each row showing a tag (such as A1), a description and a Split checkbox. The status says how many holes were found.
  5. Click a row, then **Swap**: the hole moves to the other list and its tag changes. Click a row, then **Delete**: the row goes. **Renumber**: tags restart from A1 with no gaps. Tick **Split** on a row: that hole gets its own group and letter.
  6. Click **Create Tables**, **Generate Precision Table**, **Generate Clearance Table**, **Update** and **Align**: each one runs (the sections for 164 to 166 below check what they draw); here, check only that the thin busy bar shows under the body while each runs (it shows without moving; known stopgap).
  7. Open the **Edit Table** tab: Stored tables lists the views that have tables (empty on a fresh drawing). Notes lists one entry per group after an Analyze View. Type in a Notes box and click elsewhere: the box keeps its text and the list is rebuilt once, not on every key.
  8. Tab keys: Alt+C and Alt+E switch tabs; Alt+A analyzes; Esc and **Cancel** close the window with no change to the drawing and no Undo entry.
  9. Look at the window at 100% and at 150% display scale, in the light and dark Inventor themes: the tab strip underline, the two lists, the buttons between them and every text box and dropdown show their full borders. Resize the window by its edges: the lists widen. Close and reopen: the size is remembered.
  10. The log file `awbaddin.log` in the data folder (`%APPDATA%/AwbAddin`) shows "Command Awb_HoleTable (Hole Table) started." and no errors.

## 163: Add-in: read a drawing view's holes and origin into Core (`IHoleTableSource`)

From `docs/tasks/163-addin-hole-table-read-view.md`.

(task 167's window is needed to call the source; until then, read the log `awbaddin.log`; look for `Hole table:` lines.) Open the test drawing with the part views. (1) Open the wizard: the sheets and views list and the active sheet is preselected. The drawing shows as modified only after the first listing; open the wizard a second time and no new undo entry appears. After the first listing, one Undo removes all the keys. (2) Rename a view and reorder the sheets, reopen: the same view and sheet are still found by their keys. (3) Copy a view and copy a sheet, reopen: both copies are listed (with their own keys) and can be analysed. (4) Analyse VIEW1 (end on): all 18 holes listed, tags in the expected groups, origin from the ordinate origin, scale and units matching the drawing; reference keys are non-empty and the same across two analyses. (5) Analyse VIEW2 (side view): no holes and 18 left out. (6) A view with no ordinate origin: the no-origin message. An assembly view: the message that only single part views work for now. (7) A tapped hole shows its class (`- 6H`) and a taper tapped hole its depth.

## 164: Add-in: draw the hole tables as sketched symbols, and Align

From `docs/tasks/164-addin-hole-table-draw-tables.md`.

(a drawing of a part with an ordinate origin on the view, holes of both kinds, a title block; `-p:DeployToInventor=false` was used, so deploy first with Inventor closed):
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

## 165: Add-in: hole tags, precision marks and the AWB Centre Mark on the view

From `docs/tasks/165-addin-hole-table-tags-and-marks.md`.

(open the drawing with a part view that has an ordinate origin and a mix of holes: a precision hole, a tapped hole, a counterbore, a countersink, a plain hole; Inventor must have this add-in loaded from a Windows build with deploy):
  1. Open the Hole Table window (Hole Table button), pick the sheet and VIEW1, click Create Tables. The status line says the tables were drawn and "Marked N holes on the view."
  2. Every hole on VIEW1 has its tag (A1, B1, ...) up and right of it at 45 degrees, clear of its circles, with no leader line showing.
  3. Every hole, precision ones included, has a centre mark: lines run continuously through the hole and end 2 mm past the outermost visible circle (the seat of a counterbore, spotface or countersink; the thread circle of a tapped hole). Check one of each type, and compare with spike 160 round 7's pictures.
  4. Precision holes show a circle with the top right quadrant filled solid, the same size as the hole, and the tag stands clear of it. Clearance holes have no filled quadrant.
  5. The drawing's Styles list has one `AWB Centre Mark` centre mark style (gap 0.0001 cm, size 0.01, extension and overshoot 0.2 cm) in the drawing, and the sketched symbol list has `AWB_HoleTag` and `AWB_PrecisionMark` once each.
  6. Click Create Tables again: the tags, precision marks and centre marks are replaced, centre marks not doubled. Check awbaddin.log for "could not be marked".
  7. Press Undo once: the tables, tags, precision marks and centre marks all go.
  8. Move VIEW1 by dragging it: the tags, precision marks and centre marks follow.
  9. Change a hole position in the part, return to the drawing and update: everything follows the hole.
  10. Generate one table only (for example Precision): only that table's holes are tagged and marked.
  11. A tapped or counterbored hole on a precision table: the tag stands clear of the larger of the seat and the mark.

## 166: Add-in: store the wizard's state, Update, and re-open a table on the Edit Table tab

From `docs/tasks/166-addin-hole-table-update-and-edit-table.md`.

(use a drawing of a personal part with a precision and a clearance hole, one ordinate origin on the view)
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

## Report back

For each step: pass, or what happened (a screenshot of the drawing and the log lines help). Failures become fix tasks.
