# M5 chunk 3 check: tasks 227 to 240

Built on Windows and reviewed on 2026-11-23, not yet run in Inventor. Each task file in `docs/tasks/` has the full steps; this is the condensed list. Use invented values only.

## Setup

1. Close Inventor, `git pull`, `dotnet build InventorAddin.slnx` (check it says deployed), start Inventor.
2. **239:** Build Print Template into the repo's `libraries\Templates`. The message names that folder and the deployed one. Without a redeploy, Create Print uses the new template. If a Settings template path is set, the message says Create Print uses it.

## Initial Views window (Create Print on a part with no print)

3. **Layout:** the window shows a sheet preview, Units (the part's units), a sheet size and Front view: Auto. It opens a second or two later than before, because it measures the views first. No scratch drawing is left open.
4. **227 fit:** on your test part, the suggested scale is 1:2 on A3. The placed views fit. On the test assembly, the scale is much larger than 1:20 and the views fill the sheet. In the log, "measured at scale 1" sizes are close to "placed" sizes, and there is no "placed at scale" warning (iso at the sheet scale).
5. **232 units and sheet:**
   - Switching to Imperial changes the list to ANSI A to E and shows a short wait once.
   - Pick A3 yourself, then 1:1 on a big part: a warning and "Use A2" appear, and the button fixes it.
   - Left automatic, 1:1 moves the sheet to the smallest that fits.
6. **233 front view:**
   - Auto on the test assembly gives a full front view (no edge-on view), long side horizontal.
   - Try Front, Back, Top, Bottom, Left and Right: each re-measures, and the views look from that side and are not upside down.
   - Sheet metal on Auto keeps the base face rule.
7. **234 preview:**
   - Unticking Iso removes its box; 1:1 on a big part turns the boxes red.
   - Front view Right relabels the box "Front: Right", and Imperial changes the sheet.
   - Check light and dark themes, and that no border is cut off at 150%.

## The print

8. **228:** every view, the iso included, is black and white. A sheet metal print has folded front, flat pattern and iso (iso above the folded front, not clipped). An assembly gets front and iso.
9. **229 sheet metal:**
   - The folded front looks straight at the base face, longest edge horizontal, not upside down.
   - The flat pattern has bend notes ("UP 90.00 deg R…") on every bend.
   - On an imperial print, the bend radius is in inches.
   - One Ctrl+Z reverts the placement.
10. **230:**
    - A metric sheet metal part (1.5 thick, flat 60 x 120) gets Material Size "1.5 x 60 x 120 mm" and the print shows it. An inch part shows "0.063 x … in".
    - Editing the thickness and making the print again refreshes it.
    - A part with no flat pattern is left alone.
11. **231 assembly with blank fields:**
    - The print shows REV A, STATUS WORK IN PROGRESS, and "—" in MATERIAL, MATERIAL SIZE and FINISH.
    - The assembly's iProperties hold the same values.
    - If the log says "Could not set the property 'Material'", report it: everything else should still be written.
    - Mass Update on one assembly still applies its renames.
12. **232 off-units:**
    - Imperial print of a metric part: WEIGHT in lb, FINISH as the inch spec. The drawing's Custom tab holds Finish Spec and Mass, and the part is unchanged.
    - Metric print of an inch part: kg and the µm spec.
    - The log says "title block field(s) now read the drawing's" (2 fields), with no Problems.
13. **Recheck from 226:** an imperial print prints a period on its dimensions.
14. **236 Designer:**
    - With Settings Default designer set to your name, a part whose Designer is the Windows login prints your name, and its Designer and Author change. One Undo reverts them.
    - Another name is left alone.
    - Check DETAILER does not show the login.
15. **235:** Create Print on a part that has a print opens it at once, with no question and no window. If the print is already open, it becomes active.

## Long title block values (238)

16. On a metric part, set a Description of about 115 characters and long Finish Spec, Material and Material Size values (invented). Create Print:
    - Description prints on two lines inside its cell, below its label.
    - The others print narrower and smaller but readable, inside their cells and on their row's baseline.
    - There is no warning.
17. A Finish Spec of 120 characters gives a warning naming FINISH and how much too long it is. It stays on one line at 0.6 width and 1.8 mm.
18. Save and reopen: the sizes hold. Repeat on an inch part with AWB Imperial.
19. Place Initial Views then one Undo reverts the views and the fit together.

## Hole tag leaders (240)

20. Make hole tables, drag a tag about 20 mm away (try one up-right and one down-left), and click Update:
    - A leader runs from the tag to its hole centre, with a sensible arrow, and the status line says so.
    - Drag it back beside the hole and Update: the leader goes.
    - Dragging alone (then switching documents) does not report the tables out of date.
    - One Ctrl+Z reverts an Update.
