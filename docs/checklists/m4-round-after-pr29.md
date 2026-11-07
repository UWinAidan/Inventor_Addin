## Inventor checklist: the round since PR #29's first check

Everything below is built on Windows and passes Core tests; none of it has run in Inventor. Each step is labelled `task-step` (the full text, with context, is in `docs/tasks/NNN-*.md` under Verification). Send the log (`%APPDATA%\AwbAddin\awbaddin.log`) with any failure.

Before you start: point Settings at the starter workbook (`libraries/AWB_Holes.xlsx`), or copy its new tabs and rows into yours. Have a part with a sketch of hole centres and a length parameter `d0` (for example 24 mm), and a second parameter `d1`.

### How to run the self-test

A temporary self-test (task 139, branch `spike/139-selftest` only) runs every step marked `[AUTO]` or `[AUTO+LOOK]` on documents it makes itself. Open Inventor (no document needed). Optionally put `selftest.txt` in `%APPDATA%\AwbAddin` with `cases=116-1,136,122` (IDs or prefixes; default `all`) and `keep=yes` to leave the test documents open. Press **Export Libraries** on the developer panel. When it finishes, read `%APPDATA%\AwbAddin\selftest-results.md` (one row per ID: PASS, FAIL or SKIP, with what it measured); each line is also in `awbaddin.log`, prefixed `SELFTEST 139:`. `settings.json` is put back as it was. Then do the `[MANUAL]` steps, and glance at the `[AUTO+LOOK]` ones for what only a person can judge.

Labels: `[AUTO]` fully covered by the self-test; `[AUTO+LOOK]` values automated, a quick look still needed; `[MANUAL]` only Aidan can judge.

### 1. Starter workbook, in Excel (passed)

- [x] **111-2** Spotface tab: title "Spotfaces" with a note; headers in row 4 with filters over A:E, frozen below row 4; 17 rows (M3 to M20, #4 to 1/2); Units and Show offer dropdowns.
- [x] **111-3** Taper Tapped tab: headers Units, Size, Thread type, Tap drill, Default thread depth, Default drill depth, Show, filters over A:G; the depth columns are grey formulas. Settings > Thread depth (x D) 1.5 → 2: Rc 1/4 goes 16.8 → 22.4. Undo, close without saving.
- [x] **111-4** How to edit: a Spotface line after Countersink, a Taper Tapped line after Tapped.
- [x] **131-1** No repair prompt; Drill Point reads 118, 90, 60 deg, Flat, with filters over all five columns.
- [x] **132-1** No repair prompt; Precision Tol has 16 rows with filters A to I down to row 20; How to edit has the Normal, Slip and Press levels lines.
- [x] **111-5** (optional) AWB Hole opens with no workbook problem on the starter workbook, and on an older copy without the two new tabs.

### 2. Ribbon, Hole takeover and reload

- [ ] **121-1** [MANUAL] Add-In Manager, unload: the AWB Addin tab is gone from every ribbon, Inventor's Hole is back, H opens it. Log: "Hole takeover (add-in unloading) ...", "Ribbon (add-in unloading): removed 4 tabs ...", no "could not remove".
- [ ] **121-2** [MANUAL] Load again: the tab is back with icons, AWB Hole is in Hole's place, H opens AWB Hole. Log: "firstTime = False", "nothing was left from an earlier load", "13 commands registered" with no "Already there and reused".
- [ ] **121-3** [MANUAL] Unload and load once more: no duplicate buttons or panels.
- [ ] **121-4** [MANUAL] Restart Inventor: ribbon as before; the first load's log line says "nothing was left".
- [ ] **121-5** [MANUAL] Give About a keyboard shortcut and put it on the Quick Access Toolbar. (a) restart Inventor, (b) unload and reload: both still there and working.
- [ ] **121-6** [MANUAL] If step 2 shows "removed what an earlier load left", "Already there and reused" or "could not remove", send the log.
- [ ] **128-1** [AUTO] Log after start: "Hole takeover: Hole commands on the Part ribbon: ..." lists the 3D Model, Sheet Metal and Flat Pattern panels with `PartHoleCmd`, then "On the Sheet Metal tab, AWB Hole goes before PartHoleCmd ..." and "AWB Hole has the Hole button on the 3D Model and Sheet Metal tabs and the H shortcut". A warning instead: send it.
- [ ] **128-2** [MANUAL] Sheet metal part, Sheet Metal tab: AWB Hole (large) in Hole's place. Make a hole on a flange: right, OK keeps it, one Undo removes it.
- [ ] **128-3** [MANUAL] H on the sheet metal part opens AWB Hole; log "Command Awb_AwbHole (AWB Hole) started.".
- [ ] **128-4** [MANUAL] 3D Model tab of the same part: AWB Hole in Hole's place.
- [ ] **128-5** [MANUAL] Flat Pattern tab: Inventor's Hole, no AWB Hole.
- [ ] **128-6** [MANUAL] Settings, tick "Use Inventor's Hole command" (the note names both tabs), Save: Inventor's Hole back on both tabs, H opens it. Untick, Save: AWB Hole back on both.
- [ ] **128-7** [MANUAL] Unload: the log counts 2 buttons removed on Inventor's panels; Inventor's Hole shows on both tabs. Load: AWB Hole back on both. (If the log says "tab id_TabSheetMetal was not found", the count is 1: see the task file.)
- [ ] **128-8** [MANUAL] Customize > Keyboard > Reset All, close: H still opens AWB Hole on a sheet metal part.
- [ ] **120-1** [MANUAL] "Use Inventor's Hole" off. Customize > Keyboard > Reset All, close the dialog. H in a part opens AWB Hole; Inventor's Hole button still hidden.
- [ ] **120-2** [MANUAL] Setting on, Save, Reset All again: H opens Inventor's Hole.
- [ ] **120-3** [MANUAL] Log lines in order: "Inventor reported a shortcut reset; Inventor's window is disabled ...", "the dialog has closed (N s after the reset) ... Re-applying.", give-back and take-over "(shortcuts reset)", then "done; shortcuts 5 s after re-applying: AWB Hole 'H', Inventor's Hole none". If the first line says **enabled**, send the log.
- [ ] **129-1** [AUTO+LOOK] Modify panel: AWB Hole is a large split button. Dropdown: AWB Hole, Inventor Hole (Inventor's icon), Use Inventor's Hole by default (Settings icon, not pressed). Log "split button added on panel id_PanelP_ModelModify" with no "cannot be found again".
- [ ] **129-2** [MANUAL] Pick Inventor Hole: Inventor's Hole opens; make a hole, OK. H opens AWB Hole; the top half of the split button opens AWB Hole.
- [ ] **129-3** [MANUAL] With AWB Hole open, pick Inventor Hole: AWB Hole closes as Cancel, Inventor's Hole opens.
- [ ] **129-4** [MANUAL] Pick Use Inventor's Hole by default: the split button goes, Inventor's Hole is back, H opens it; Settings shows the box ticked. Untick, Save: split button back, not pressed; H opens AWB Hole.
- [ ] **129-5** [MANUAL] Sheet Metal tab: the same split button; repeat 129-2 and 129-4 there. Flat Pattern keeps Inventor's Hole.
- [ ] **129-6** [MANUAL] Window closed: mini toolbar Hole on a sketch point opens AWB Hole. Then Inventor Hole from the split button, Cancel it, mini toolbar Hole again: AWB Hole opens.
- [ ] **129-7** [MANUAL] Unload and load: one split button per tab; the unload line counts them.
- [ ] **129-8** [MANUAL] Make `settings.json` read-only, pick Use Inventor's Hole by default: a message says nothing changed. Make it writable again.
- [ ] **133-1** [MANUAL] Left-click a sketch point: the mini toolbar shows AWB Hole's icon; it opens AWB Hole with that point picked.
- [ ] **133-2** [MANUAL] The same with the part edited in place in an assembly.
- [ ] **133-3** [MANUAL] "Use Inventor's Hole command" on: the mini toolbar has Inventor's Hole and opens it. Turn it off.
- [ ] **133-4** [MANUAL] Inventor Hole from the split button opens Inventor's Hole and is not stopped.
- [ ] **133-5** [MANUAL] Sheet metal part, left-click a sketch point: report what the mini toolbar shows.
- [ ] **133-5a** [MANUAL] Flat pattern, left-click a sketch point: it should keep Inventor's Hole; report if AWB Hole shows.
- [ ] **133-6** [AUTO] Log: no "Hole takeover: replacing Hole on the mini toolbar failed". If 133-1 logged "Inventor's Hole from the mini toolbar started", report it.

### 3. Editing a part in place in an assembly

- [ ] **105-1** [MANUAL] Double-click a part to edit it in place, press H: AWB Hole opens on the part, default centres picked and blue.
- [ ] **105-2** [AUTO+LOOK] Unpick and pick centres: highlight and preview follow. OK: the hole is in the part. One Undo in the assembly: gone.
- [ ] **105-3** [MANUAL] AWB Hole from the AWB Addin tab, Cancel: nothing changed.
- [ ] **105-4** [MANUAL] Double-click an AWB hole's face in place: the window opens on it; change the size, OK, Undo once: reverts. Browser right-click > Edit Feature: our window.
- [ ] **105-4a** [MANUAL] In place, the AWB Hole button on the 3D Model tab's Modify panel opens the window.
- [ ] **105-4b** [MANUAL] In place, left-click an AWB hole's face, Edit Feature on the mini toolbar: our window.
- [ ] **105-4c** [MANUAL] In place, double-click an extrusion's face: Inventor's own edit opens.
- [ ] **105-5** [MANUAL] Window open in place: Return closes it with nothing changed. Again, close the assembly: it closes. Again, switch documents: it closes, nothing changed.
- [ ] **105-6** [MANUAL] In a part's own window, open AWB Hole and switch documents: it closes.
- [ ] **109-1** [MANUAL] Create Print: the part's print, in the part's `Prints` folder.
- [ ] **109-2** [MANUAL] Save As Copy: shows and copies the part; the assembly is unchanged.
- [ ] **109-3** [MANUAL] New Part: opens on its own in the assembly's folder; the in-place edit is not disturbed.
- [ ] **109-4** [MANUAL] Save As and Replace with a component selected: replaces that occurrence.
- [ ] **109-5** [MANUAL] Part Properties: change Designer, apply; back in the assembly, one Undo reverts it (open since task 006).
- [ ] **109-6** [MANUAL] Material and Finish: apply a finish, one Undo in the assembly reverts it.
- [ ] **109-7** [MANUAL] Export Model Data exports the part.
- [ ] **109-8** [MANUAL] Mass Update, Settings, About open and work.
- [ ] **109-9** [MANUAL] Repeat 109-1, 2, 5 and 7 with a sub-assembly edited in place: each acts on the sub-assembly.
- [ ] **110-8** [MANUAL] In place, AWB Hole: the red preview lands on the part, not offset. Switch back to the assembly with the window open: it closes and no red is left.
- [ ] **117-6** [MANUAL] In place, repeat 117-2: the same two points are picked.

### 4. Rename and Save As and Replace

- [ ] **134-1** [AUTO] Select an occurrence named after its file, Save As and Replace with a new name: the browser shows the new file's name with `:1`.
- [ ] **134-2** [AUTO] One Undo: the original component and name are back.
- [ ] **134-3** [AUTO] On an occurrence renamed by hand: it keeps your name.
- [ ] **134-4** [MANUAL] On a component inside a sub-assembly: the sub-assembly's occurrence takes the new name (open the sub-assembly to check).
- [ ] **134-5** [AUTO] After 134-1, Rename the copy in Part Properties: the occurrence follows.
- [ ] **135-1/2** [AUTO] In an assembly with a stale AWB-format occurrence name for another file (for example `BRACKET_V4,AWB-10017:1` on `BRACKET_V5,AWB-10022.ipt`), select the part, AWB Rename to `BRACKET_V6,AWB-10022.ipt`: the occurrence becomes `BRACKET_V6,AWB-10022:1`.
- [ ] **135-3** [AUTO] The same with only the part open; reopen the assembly: same result.
- [ ] **135-4** [AUTO] Another occurrence named `MY_NAME:2`, rename the part again: `MY_NAME:2` is kept.
- [ ] **135-5** [AUTO] Save As and Replace on an occurrence with a stale AWB name (`OTHER,AWB-10017:1`): replaced, and the name is kept. An occurrence named after the original still takes the copy's name.

### 5. The AWB Hole window: layout, keys, icons

- [ ] **118-1** [MANUAL] Header: "AWB Hole", the part's name, and the Metric | Imperial toggle on the same line at the right. No Units row and no Centres section in the body.
- [ ] **118-2** [MANUAL] Diagram open, Termination Distance: the whole window, OK and Cancel included, is on screen with no scroll bar. Tapped, Distance: still none.
- [ ] **118-3** [MANUAL] Tapped M6 with Seat Counterbore: the body scrolls with the thin bar; header, footer line, OK and Cancel stay in view. Collapse the diagram: the bar goes.
- [ ] **118-4** [MANUAL] Collapse the diagram, Cancel, reopen: still collapsed. Open it again.
- [ ] **118-5** [AUTO+LOOK] The last row of Depth and direction reads "Centres  3 picked"; its tooltip says how to pick. Click points: the count follows. Unpick all: "None picked", the problem in red under it, OK disabled.
- [ ] **118-6** [AUTO+LOOK] Edit a hole whose centres are not sketch points: "N picked  cannot be changed", tooltip explains. Edit any hole: "Edit AWB Hole" in full, toggle after it (also with a long part name).
- [ ] **118-7** [MANUAL] Focus opens on the chosen Units part. Tab: Type, Seat, the Diagram header, the boxes in order, Flip, OK, Cancel. Alt+U from anywhere goes back to Units. Spot-check Alt+Y, Alt+E, Alt+Z, Alt+P, Alt+L.
- [ ] **118-8** [AUTO+LOOK] Clearance, Counterbore, Size M3 ↔ M2 (no counterbore row): Fastener type and Fit swap; the window grows only by the two problem lines and stays on screen.
- [ ] **118-9** [AUTO+LOOK] Tick Toleranced on a clearance hole: the checkbox sits in the value column under Fit, Tolerance appears under it.
- [ ] **118-10** [MANUAL] Other theme: the header toggle, tight rows and diagram read well. Settings and Part Properties keep their roomier spacing.
- [ ] **136-12** [MANUAL] Alt with each underlined letter reaches its field. Full depth has none.
- [ ] **119-1** [MANUAL] Type row: four buttons. Hover: Clearance (plain narrow), Simple (wider, double arrow across the top), Tapped (straight saw-tooth), Taper Tapped (saw-tooth closing in).
- [ ] **119-2** [MANUAL] Seat row: None, Counterbore (deep step), Countersink (cone), Spotface (shallow wide step).
- [ ] **119-3** [MANUAL] The chosen button has the tile fill and lighter border; icons read at 100 and 150 percent.
- [ ] **119-4** [MANUAL] Other theme: the three new icons read in both.
- [ ] **114-10** [AUTO+LOOK] On a seated clearance hole, switch Size between a size with seat rows and one without: Fit appears and goes, and the window does not jump under the mouse.

### 6. Default centres

- [ ] **117-1** [AUTO] New part: sketch A with one hole-centre point; sketch B (newer) with four. Make a hole on two of B's four (deselect two in the Hole command).
- [ ] **117-2** [AUTO] Show sketch B, clear the selection, AWB Hole: "2 picked", the other two blue. Log: `newest visible with free centres, 2 of 4 hole centres`. Cancel.
- [ ] **117-3** [AUTO] Hole on B's remaining two. Show B and A, clear selection, AWB Hole: B is skipped, A's point is picked.
- [ ] **117-4** [AUTO] Select sketch B in the browser, AWB Hole: all four picked; log `selected, all 4 hole centres`.
- [ ] **117-5** [AUTO] Select one point of B, AWB Hole: all four; log `of the selected points, all 4 hole centres`.

### 7. Preview and live typing

- [ ] **110-1** [MANUAL] Through clearance hole: the hole volume is faint red over the model; no face turns red. Looking down the bore: red end caps.
- [ ] **110-2** [MANUAL] Blind with a drill point: cylinder and cone red through the part's faces.
- [ ] **110-3** [MANUAL] Counterbore, then countersink: each seat drawn. A centre at the block's edge: the seat drawn whole, outside the part too.
- [ ] **110-4** [MANUAL] Type in Diameter: the red follows about 300 ms after you stop. A value Inventor refuses: the red stays on the last good hole.
- [ ] **110-5** [MANUAL] Pick and unpick centres: the red follows; none picked, no red.
- [ ] **110-6** [AUTO+LOOK] OK: red goes, part's colour (fit colour for a dowel). One Ctrl+Z removes the hole, no red. Cancel: no red, no hole. Close the part with the window open, reopen: no red.
- [ ] **110-7** [MANUAL] Edit an AWB hole: red at once; OK and Cancel leave none; a hand colour is kept.
- [ ] **110-9** [MANUAL] Log: no "preview solid could not be made", "could not be joined" or "could not be drawn"; note any "drawing the preview took N ms".
- [ ] **130-1** [AUTO+LOOK] Blind clearance, type `12` in Depth: about a third of a second after the last key, the preview and diagram show 12.
- [ ] **130-2** [AUTO] Type `d0+`: message after the pause, OK greyed, preview at the last good depth. Add `2`: message goes, `= ... mm` shows, preview updates.
- [ ] **130-3** [AUTO] Type `1.` and pause: the box keeps `1.`, preview at 1. Finish `1.5`, Tab: shows `1.5`.
- [ ] **130-4** [AUTO] Clear Depth and pause: no move, no message. Tab: the blank is applied as before.
- [ ] **130-5** [AUTO] The same in a seat box (Cbore depth `d0+` then `d0+2`) and in Thread and Drill depth on a blind tapped hole.
- [ ] **130-6** [AUTO] Countersink: type `82` in Csink angle and wait: the preview follows. Pick 60 from the list: applies at once.
- [ ] **130-7** [AUTO] Type `14` and press Enter at once: made at 14. Again, clicking OK straight after typing: the typed value.
- [ ] **130-8** [AUTO+LOOK] Type and press Esc within a third of a second: closes, part as it was, no flash. Clicking Cancel: part as it was (a flash before closing is expected).
- [ ] **130-9** [MANUAL] Type, then click a sketch point before the pause ends: picked, and the depth still applies.
- [ ] **130-10** [MANUAL] OK with a blank box waiting and no centres: note whether a clickable OK that does nothing is confusing.

### 8. Seats and typed values

- [ ] **123-1** [AUTO] Tapped M6, Counterbore → Clearance: still Counterbore with its preview → Tapped: still Counterbore. OK, Edit: opens on the counterbore.
- [ ] **123-2** [AUTO] AWB Dowel 6 mm, Counterbore → AWB Clearance → AWB Dowel: still Counterbore, with seat boxes.
- [ ] **123-3** [AUTO+LOOK] Tapped with Countersink, change to a size with no countersink row: seat None, no message, no countersink in the preview. Back: Countersink returns.
- [ ] **123-4** [AUTO+LOOK] As 123-3 but set None by hand at the size with no row, then back: stays None.
- [ ] **123-5** [AUTO] Clearance with countersink → Tapped → Clearance: the countersink is back.
- [ ] **124-1** [AUTO] Tapped M6, Distance, Thread depth `d0`, Drill depth `d0+5 mm`, OK. Edit, change to M8: both boxes keep `d0` and `d0+5 mm`. OK, change `d0` in Parameters: the depths follow.
- [ ] **124-2** [AUTO] New blind tapped M6, depths left alone, change to M8: the depths take M8's values.
- [ ] **124-3** [AUTO] Type drill depth `d0+5 mm`, then clear it: the table value; change size: it follows.
- [ ] **124-4** [AUTO] Blind clearance, Depth `12`, switch to Imperial: `12 mm` with `= 0.4724 in`, preview unchanged. Back to Metric: `12`. Imperial again, OK: Inventor's Edit Hole reads 12 mm.
- [ ] **124-5** [AUTO] Thread depth typed, Tapped → Clearance → Tapped: the typed value comes back.
- [ ] **124-6** [AUTO] AWB Dowel counterbore, type a diameter, change size: the diameter stays, the depth takes the new size's value.
- [ ] **125-1** [AUTO] AWB Dowel 6 mm, Counterbore, diameter 9, depth `d0/8`, OK. Log after "made HoleN": `remembered the seat for the next 6 mm dowel (Metric): counterbore 9 mm x d0/8 (= 3 mm).`; `settings.json` `lastDowelSeatsMm` has the `6 mm` entry.
- [ ] **125-2** [AUTO] New AWB Dowel 6 mm Counterbore: boxes start at 9 and `d0/8` (`= 3 mm`). OK: Parameters shows the depth as `d0/8`.
- [ ] **125-3** [AUTO] Change to 8 mm: 9 and 4 from the workbook. Back to 6 mm: 9 and `d0/8`.
- [ ] **125-4** [MANUAL] Restart Inventor, repeat 125-2: still 9 / `d0/8`.
- [ ] **125-5** [AUTO] A part without `d0`: a 6 mm dowel counterbore starts at 9 and 3, no message, OK enabled.
- [ ] **125-6** [AUTO] Depth typed `2`, OK: the next 6 mm starts at 9 and 2.
- [ ] **125-7** [AUTO] If 125-2 shows 7 / 3, send the 125-1 log lines and `lastDowelSeatsMm`.
- [ ] **106-6** [AUTO] Size 6 mm → 8 mm → back: each size shows its own start values. Imperial: 1/4" row's values (0.29, 0.125).
- [ ] **106-7** [AUTO] Edit the hole from 125-1: it opens on its own diameter and `d0/8`, not the remembered values. Change only the fit, OK: the seat is unchanged.
- [ ] **106-10** [AUTO] Ctrl+Z after OK removes the hole in one step.
- [ ] **126-1** [AUTO] Clearance M6, Counterbore, SHCS: Fastener type above Cbore dia and depth, showing row values. Diameter 12, OK: the model has 12. Edit: shows 12, depth still the row's.
- [ ] **126-2** [AUTO] Tapped M6, Countersink with no M6 row: Fastener type hidden, empty boxes with "Enter a countersink diameter.", OK disabled. Type 7 and 90, OK. A new Tapped M6 Countersink starts at 7 and 90; log "remembered the seat for the next M6 tapped hole (Metric): countersink 7 mm at 90 deg".
- [ ] **126-3** [AUTO] Clearance M6 Counterbore SHCS: type a diameter, change Fastener type: only the depth changes. Empty the diameter, change again: it follows the row.
- [ ] **126-4** [AUTO] Clearance M6 counterbored from its row, OK, Edit, Size M8: both boxes follow the M8 row. Cancel.
- [ ] **126-5** [AUTO] Countersink angle 0.5 or 180: "between 1 and 179 degrees", OK disabled.
- [ ] **126-6** [AUTO] Counterbore depth `d0/2`: "= ... mm". OK: the parameter holds the expression. Edit, plain number, OK: plain value again.
- [ ] **126-7** [MANUAL] A counterbored clearance hole from an earlier build: Edit opens on row values, no re-apply offer; OK stores the new data.
- [ ] **126-8** [AUTO] With Inventor's Hole, change a clearance counterbore's diameter, then Edit in AWB Hole: the re-apply offer lists the counterbore diameter.
- [ ] **137-1** [AUTO] Counterbore: Size lists every Clearance size.
- [ ] **137-2** [AUTO] A size with no Counterbore row: Fastener type hidden, Fit (Normal) shows, empty boxes with "Enter a counterbore diameter." and "Enter a counterbore depth.", OK disabled.
- [ ] **137-3** [AUTO] Type diameter and depth: OK enables, preview at the Normal clearance with that counterbore. Fit Loose: the hole grows.
- [ ] **137-4** [AUTO] A size with a row: Fastener type shows, Fit hides, untyped boxes take the row; typed boxes keep your values.
- [ ] **137-5** [AUTO] Back to the size with no row: untyped boxes empty again.
- [ ] **137-6** [AUTO] Repeat 137-1 to 3 with Countersink (diameter and angle).
- [ ] **137-7** [AUTO] OK, then double-click to edit: same size, Fit and seat values, Fastener type hidden, no re-apply offer.
- [ ] **137-8** [AUTO] The same counterbored hole made with Inventor's Hole (clearance diameter, a counterbore no row has), edited in AWB Hole: that size, its own counterbore, the matching fit, a note.
- [ ] **127-1** [AUTO+LOOK] Tapped M6, Countersink: the Csink angle box has a dropdown arrow; pick 60: box, diagram and preview follow at once. Csink dia 7, OK. Edit reads 7 at 60.
- [ ] **127-2** [AUTO] Dowel 6 mm, Countersink at 90, OK: in the model.
- [ ] **127-3** [AUTO] Simple 4 mm and a clearance hole, Countersink at 60: likewise.
- [ ] **127-4** [AUTO] Type 82, Tab: kept. Pick 90: back to 90. (Typing 6 may autocomplete to 60.)
- [ ] **127-5** [MANUAL] Angle box in both themes: border, fill, hover, focus outline and list match the other dropdowns; text lines up. Shift+Tab goes to the previous field.
- [ ] **127-6** [AUTO] After picking 60 or 90, the preview updates with no other click.
- [ ] **127-7** [AUTO+LOOK] Counterbore depth 0 on a clearance hole and spotface depth 0 on a tapped hole: no message, no seat wall in the diagram. OK makes it; Edit reads depth 0, no re-apply offer.
- [ ] **127-8** [AUTO] Counterbore depth -1: "The counterbore depth is less than zero.", OK disabled.
- [ ] **127-9** [AUTO] Dowel with counterbore depth 0, OK. A new one of that size starts at 0.

### 9. Hole types: Simple, Taper Tapped, spotface, 60 deg point

- [ ] **114-1** [AUTO+LOOK] Clearance, Seat Spotface: Spotface dia and depth boxes show. 11 and 0.5, blind Depth 12, OK. Inventor's Hole dialog reads spotface 11 x 0.5; the drill bottom (not the point) is 12 below the top. One Undo removes it.
- [ ] **114-2** [AUTO] Edit: opens Spotface 11 x 0.5, Depth 12. Seat Counterbore, Countersink, None, Spotface, OK between some: browser name and position stay.
- [ ] **114-3** [AUTO] Counterbored (2 deep) and spotfaced (2 deep) holes with Depth 12: both drills end 12 below the top.
- [ ] **114-4** [AUTO] Simple: Standard and Size go, a Diameter box shows. Type `d0*2`: value beside it. OK, change `d0`: the hole follows. Edit: your expression without ` ul`.
- [ ] **114-5** [AUTO+LOOK] Taper Tapped, Imperial, NPT 1/4: Counterbore greyed with a tooltip. Blind with no seat, countersink, spotface, then Through All: each threaded. Blind shows Drill depth and no Thread depth.
- [ ] **114-6** [AUTO] Clearance with counterbore → Taper Tapped: Seat None with a note. Back to Clearance: counterbore returns, note goes.
- [ ] **114-7** [AUTO] An existing taper tapped hole, seat None → Spotface, OK: same name and place (remade in place). Undo once.
- [ ] **114-8** [AUTO] With Inventor's Hole make a spotface hole and an NPT 1/4 taper tapped hole; Edit each in AWB Hole: Spotface with the right sizes (Depth plus spotface depth) and Taper Tapped NPT 1/4. If "not in the Taper Tapped tab", note the designation in the message.
- [ ] **114-9** [AUTO] Edit an Inventor hole of 6.25 mm: opens as Simple, Diameter 6.25, with a note.
- [ ] **114-11** [MANUAL] Blind tapped M-size, 1 mm spotface, thread 7.5, drill 10: where the thread texture starts and ends.
- [ ] **131-2** [AUTO] Your own workbook: add `60 deg | 60 | N | Y | ...` to Drill Point (or use the starter).
- [ ] **131-3** [AUTO+LOOK] Termination Distance: Drill point shows 118, 90, 60, Flat, each its own icon; 60 is the tallest, narrowest.
- [ ] **131-4** [AUTO+LOOK] 60 deg: the diagram shows a deeper point labelled 60 deg, the preview a 60 deg point. OK, Edit: still 60.

### 10. Tolerances and fit colours

- [ ] **132-2** [AUTO] AWB Dowel, Metric, 6 mm, Press: `M7 0 / -0.012` Light press, `N7 -0.004 / -0.016` Medium press (preselected), `P7 -0.008 / -0.020` Heavy press, Custom. Each in turn: Inventor's hole dialog shows M7, N7, P7.
- [ ] **132-3** [AUTO] Slip: G6, G7 (preselected), F7. Normal: H7 (preselected), H6. Inventor takes G6 and H6.
- [ ] **132-4** [AUTO] Imperial Press, Slip, Normal: levels as inch limits (Slip `+0.0007 / +0.0003`, `+0.0010 / +0.0005`, `+0.0015 / +0.0008`).
- [ ] **132-5** [AUTO] Clearance, Toleranced: preselects Normal (H7), all eight levels, then Custom; normal blue.
- [ ] **132-6** [MANUAL] In a workbook copy, Default = Y on two Press rows, or two rows with one name: the window names the row.
- [ ] **115-1** [AUTO] Clearance M6, no seat: Toleranced on its own row, unticked, no Tolerance row. Tick: every Precision Tol row, Default preselected, Custom last. OK: Inventor's Edit Hole and the diameter parameter show the tolerance (H7). Every face, bottom included, in the row's fit colour.
- [ ] **115-2** [AUTO] With Counterbore: the tolerance on the hole diameter only; every face coloured.
- [ ] **115-3** [AUTO+LOOK] Simple 5, Toleranced, Custom: Upper and Lower with messages, OK disabled. Upper `0.02`, Lower `0`: the preview follows. Upper `-1`: Upper-above-Lower message, OK waits. Back to `0.02`, OK: +0.02 / 0, normal fit colour.
- [ ] **115-4** [AUTO] Edit the 115-1 hole: opens toleranced on the same row. OK unchanged: tolerance and colour stay.
- [ ] **115-5** [AUTO] Untick Toleranced, OK: part's colour, no tolerance. One Undo brings both back.
- [ ] **115-6** [AUTO] Edit the Custom Simple hole: Custom, +0.02 and 0.
- [ ] **115-7** [AUTO] AWB Dowel with Custom: Upper and Lower show; with limits, OK enables.
- [ ] **115-8** [AUTO] A Clearance hole made in Inventor's Hole with a diameter tolerance, edited in AWB Hole: opens toleranced; OK keeps it and colours every face.
- [ ] **115-9** [AUTO] Switch Units with Custom limits typed: converted (4 decimals mm, 5 inch).
- [ ] **138-1/2** [AUTO+LOOK] Clearance M6, no seat: no swatch. Tick Toleranced: a small coloured square at the right end of the Tolerance line; its tooltip names the colour ("Normal fit"); the entry is not cut off.
- [ ] **138-3/4** [AUTO+LOOK] Choose a press, a normal and a slip row: the square follows (red, blue, green in the starter workbook). The longest entry (an N7 row) shows in full beside it.
- [ ] **138-5/6** [AUTO] Custom: the square turns normal at once. Type the limits, OK: every face takes the square's colour; Edit shows the same square.
- [ ] **138-7** [AUTO] A toleranced clearance hole on a press row: faces in the press colour, matching the square.
- [ ] **138-8** [AUTO] Simple with a diameter, Toleranced: the square stays. Tapped: Toleranced and the square go. Back to Clearance: unticked, no square.
- [ ] **138-9** [AUTO] AWB Dowel: Fit's square and name as before; no square on Tolerance.
- [ ] **138-10** [AUTO] Switch Metric | Imperial with Toleranced: the square follows the new units' row.
- [ ] **138-11** [MANUAL] With the diagram open the window is no taller than before (a through all toleranced clearance hole was 647 at 150 percent); the square reads in both themes.

### 11. Depths below the seat, and Full depth

- [ ] **116-1** [AUTO+LOOK] Tapped M6, Distance, Counterbore depth 6, Thread 9, Drill 12: the diagram dimensions 9 and 12 from the counterbore floor. OK. Inventor: drill 18, thread 15; the texture runs 9 below the floor.
- [ ] **116-2** [AUTO] Edit: Thread 9, Drill 12, no re-apply offer. Counterbore depth 3, OK: Inventor drill 15, thread 12. One Undo: 18 and 15.
- [ ] **116-3** [AUTO] Thread `d0`, counterbore depth `d1`: Inventor's thread reads `(d0) + (d1)`; change `d1`: the thread follows. Edit: shows `d0` (note anything longer).
- [ ] **116-4** [AUTO+LOOK] Countersink 12 at 90 on M6 blind, thread 9, drill 12: dimensions from where the cone meets the hole. Inventor's depths are 12 and 9 plus the cone depth. Where does the texture start?
- [ ] **116-5** [AUTO+LOOK] Spotface 11 x 1 on M6 blind, thread 9, drill 12: Inventor reads 12 and 9; the thread starts at the spotface bottom.
- [ ] **116-6** [AUTO] Taper Tapped NPT 1/4 blind with a countersink: Drill depth below the cone; Inventor's depth is drill plus cone.
- [ ] **116-7** [AUTO] Clearance blind Depth 12 with a 2 mm counterbore, and with a 2 mm spotface: both drills end 12 below the top face.
- [ ] **116-8** [MANUAL] A seated blind tapped hole made before this build (for example from 114): Edit opens with depths less the seat depth, no re-apply offer; OK unchanged leaves Inventor's depths alone.
- [ ] **116-9** [AUTO] Change a counterbored tapped hole's counterbore depth in Inventor's Hole, Edit in AWB Hole: the offer lists the counterbore, drill and thread depths; OK restores the thread's length below the seat.
- [ ] **116-10** [AUTO] Counterbore 14, drill 12: no problem shown, Inventor makes it 26 deep.
- [ ] **116-11** [AUTO] Inventor-made M6 blind tapped, 6 mm counterbore, drill 18, thread 15: AWB Hole opens 12 and 9. With a countersink: less the cone at the workbook's tap drill. With a spotface: as Inventor shows.
- [ ] **136-1** [AUTO+LOOK] Tapped M6 Through All: Full depth shows ticked, no Thread depth box, the diagram threads the whole hole. OK: threaded through.
- [ ] **136-2** [AUTO] Untick: Thread depth 9 (1.5 x D); the diagram stops the thread at 9. Type 12, wait: preview follows. OK: texture 12; Inventor shows Full Depth off, 12.
- [ ] **136-3** [AUTO] Tick and untick with the window open: the preview follows. Cancel: part as it was.
- [ ] **136-4** [AUTO+LOOK] With a 6 mm counterbore and Thread 9: Inventor reads 15; the texture runs 9 below the floor; the diagram dimensions 9 from it.
- [ ] **136-5** [AUTO] Thread `d0` with the 6 mm counterbore: Inventor reads `(d0) + (6 mm)`; change `d0`: follows.
- [ ] **136-6** [AUTO] Edit the holes from 136-2, 4 and 5: unticked, 12, 9 and `d0`, no offer. Size M8: the typed depth stays.
- [ ] **136-7** [AUTO] A full-depth AWB hole: opens ticked. Untick Full Depth in Inventor's Hole with a depth, Edit: the offer says "The thread stops at ...; AWB Hole threaded the full depth."
- [ ] **136-8** [AUTO] Inventor-made M6 Through All, Full Depth off, thread 10: opens unticked with 10. With a 6 mm counterbore and thread 16: opens with 10.
- [ ] **136-9** [MANUAL] A Through All tapped AWB hole from before this build: opens ticked, no offer.
- [ ] **136-10** [AUTO] Blind tapped and Taper Tapped Through All: no Full depth checkbox; blind keeps Thread and Drill depth.
- [ ] **136-11** [AUTO] Unticked, Thread depth 0: "greater than zero", OK waits.

### 12. Hand colours kept when a hole is remade

- [ ] **122-1** [AUTO] Blind clearance, colour its bore by hand. Edit to Tapped, OK: the bore keeps the colour, the drill point does not. Log "made again" and "hand colours put back on 1 of N face(s)".
- [ ] **122-2** [AUTO] Back to Clearance: still coloured.
- [ ] **122-3** [AUTO] Counterbored clearance, every face one colour by hand → tapped: every face keeps it.
- [ ] **122-4** [AUTO] Counterbore wall red, bore blue → tapped: red and blue kept. → tapped with no seat: bore blue, red gone.
- [ ] **122-5** [AUTO] Two centres, only the second's bore coloured → tapped: only the second's.
- [ ] **122-6** [AUTO] A colour on the whole hole through Feature Properties → tapped: kept.
- [ ] **122-7** [AUTO] A dowel (fit colour) edited into clearance or tapped: every face back to the part's colour, no "hand colours put back" line.
- [ ] **122-8** [AUTO] Through clearance with its one face coloured → counterbored tapped: seat and bore all take the colour (Default; say if not wanted). Also try a spotface.
- [ ] **122-9** [AUTO] One Undo after any of these brings the hole back, colours included.

### Optional

- The older Part Properties checks in `TODO.md` ("Waiting on Aidan: checks in Inventor") are still open.
