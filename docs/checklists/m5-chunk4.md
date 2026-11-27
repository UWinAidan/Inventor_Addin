# M5 chunk 4 check: fixes 242 to 245, then tasks 195 to 212

Built on Windows and reviewed, not yet run in Inventor. Each task file in `docs/tasks/` has the full steps; this is the condensed list. Use invented values only.

## Setup

1. Close Inventor, `git pull`, `dotnet build InventorAddin.slnx` (check it says deployed), start Inventor.
2. Use your own models only, as copies. Never open company files for this check.
3. **196, 205:** open `libraries/AWB_Notes.xlsx` and `libraries/AWB_Stock.xlsx` in Excel. Both open with no repair prompt. Notes has tabs How to edit, Material, Finish (headers in row 4). Stock has How to edit, Plate, Round, Settings, and reads clearly.
4. **Before 245:** the older task 231 build wrote "—" into test assemblies' Material, Material Size and Finish Spec. Clear those three iProperties by hand on every test assembly and save.
5. Keep `%APPDATA%\AwbAddin\awbaddin.log` open in an editor that reloads. Throughout, watch for a false "FINISH and WEIGHT could not all be pointed" warning on Create Print, and on opening or saving a print (most likely on an assembly print). Note which print it was.

## Sheet metal folded view (242)

6. Z-shaped sheet metal part, Create Print with Front view Auto. The folded view shows the Z profile, base flange horizontal, not upside down.
7. The flat pattern has no new problems; nothing else about it changed.
8. A flat plate (sheet metal, no bends) still shows face on.
9. Front view Top: the folded view follows that choice.
10. The log has no "bend" warnings.

## Iso follows the front view (243)

11. On an L bracket (clearly not symmetric), Create Print. Front view Front: the iso looks as before.
12. Front view Left: the iso shows the top-right corner of the Left view, upright, top visible. Repeat for Back, Top, Bottom and Right.
13. Auto on a part that picks a non-standard front: the iso matches that front.
14. Sheet metal on Auto: the standard iso. On Left: it follows Left.
15. After changing Front view, the placed iso fits the sheet at the size the preview showed.

## Off-units FINISH and WEIGHT (244)

16. Inch part, Create Print, choose Metric. FINISH is in µm, WEIGHT in kg. The log says "2 title block field(s) now read the drawing's Mass, Finish Spec."
17. Metric part on an Imperial print: lb and the inch spec.
18. Close the print. Change the part's finish and its geometry, save, open the print. FINISH and WEIGHT update (log: "Print units: on opening ...").
19. With the print open, change the part again, save the print: the fields update before the save.
20. A print in the part's own units, and an up-to-date off-units print: nothing logged, no `*` on the tab after opening.
21. A drawing that is not an AWB print: nothing changes.

## Assembly dashes on the print only (245)

22. Assembly with blank Material, Material Size and Finish Spec (cleared in step 4). Create Print. The print shows "—" in MATERIAL, MATERIAL SIZE and FINISH.
23. The assembly's iProperties: those three are still blank (no "—" on the Project or Physical tab). Only Revision Number and Status are filled in.
24. Mass Update on the assembly: the three stay blank.
25. Close the print. Set the assembly's Material by hand (an invented name), save, open the print: MATERIAL shows it.
26. Set Material Size (for example `1 x 2 x 3 mm`), save, open the print: MATERIAL SIZE shows it, FINISH still "—". Clear both, save, open: "—" is back.
27. A part's print: nothing changes; MATERIAL reads the part.
28. Open the print again with nothing changed: no `*` on the tab.

## Check Out of Bounds (195)

29. A drawing from the template with views inside the border. The button is on the AWB drawing panel. Click: "Everything is inside the border."
30. Drag a view half past the sheet edge, and move a note onto the title block. Click: the list names both, each with its sheet.
31. Click each line: its sheet becomes active and the item is highlighted. Close the list; it stays selected. Clicking the same line twice (with a click in the drawing between) selects it again.
32. Second sheet with a note outside its border: listed with that sheet's name; picking it switches sheets.
33. A balloon past the border, a hole table over the title block, a dimension text outside the border: each is listed.
34. Ctrl+Z after checking: nothing to undo. On a part the button is not on the ribbon. The list scrolls in a fixed height and reads in the dark theme.

## Notes data in Settings (196)

35. Settings, Material and finish: a "Notes data" row with Browse (.xlsx). The summary names the file. A wrong extension or missing file is refused on Save, naming "Notes data". Blank uses the starter.

## Update Notes (197)

36. Add a Material row to your notes workbook (for example Pattern `Steel*`, Note `HEAT TREAT AS SPECIFIED.`). Create Print on a steel part: a bold "NOTES:" heading and "1. HEAT TREAT AS SPECIFIED." just above the title block, aligned with its left edge.
37. Change the material so the rule does not match, press Update Notes: the note goes (a message says no notes apply). Change it back: it returns.
38. Apply a finish with a Finish Note, Update Notes: it is the next number after the material notes, then any Finish rule notes. Edit the Finish Note in Part Properties, Update Notes: the text changes.
39. A rule with Placement Bottom (for example `*PLA*`, `NOTE: 3D PRINTED PART`): a bold line at the bottom of the border, clear of the X/Y triad, left of the title block. On A4 portrait it sits above the NOTES list instead. Change material: it goes.
40. A hand-typed note stays untouched, not renumbered or moved.
41. Update Notes then one Ctrl+Z: reverted in one step. Twice in a row: not doubled. Save, close, reopen, Update Notes: replaced, not added to.
42. Notes path in Settings pointing to a missing file: Update Notes names the path, says the notes were left as they were, nothing changes. Create Print still makes the print, with a warning.
43. Check Out of Bounds on a print with a long NOTES list: inside the border.

## Part Marking (199)

44. A drawing with an iso view, Part Marking on the drawing ribbon. The window shows Laser engrave, 3 mm, Extra lines, Leader to an edge, and the note "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS".
45. Method None and no extra lines: OK disabled, footer explains. Type "DO NOT STAMP": OK enabled.
46. Laser engrave, "DO NOT STAMP", Leader, OK, click an edge on the iso. A note above the view's top-right with a leader to the edge: method line, part number, "DO NOT STAMP", in blue (#0070C0). **Check the leader line itself is blue**, not only the text.
47. Ctrl+Z once removes it; Ctrl+Y brings it back.
48. Marking zone, click an edge in the front view. A dashed blue 40 x 15 mm rectangle (on the part) centred on the view, "MARKING MUST BE PLACED INSIDE THIS ZONE" inside, method and extra lines under it. The sketch can be edited.
49. **Zone text size:** repeat on a view at a scale other than 1:1 (for example 1:2 and 2:1). The zone text stays a readable, sensible size.
50. Ctrl+Z once removes the whole zone. Esc at the edge prompt adds nothing, no error.
51. With Inventor closed, set PartMarkingColour in `%APPDATA%\AwbAddin\settings.json` to #FF0000: the marking is red.
52. Imperial part: 0.12 in, zone 1.5 x 0.6 in on the part. On a part or assembly window: "open a drawing".

## Engraved part number (200)

53. Part Marking on a plain part's drawing: a tick box "Engrave part number in the part", off. It hides for Method None and for an assembly drawing.
54. Tick it, Leader, OK, pick an edge. Open the part: a feature "AWB Part Number" with its sketch, text 3 mm high on the largest flat face, cut 0.2 mm into the material (Mass Properties volume drops).
55. Change Part Number in iProperties, Update: the engraving follows, no warning icon.
56. **Undo:** one "Part Marking" entry in the drawing's Undo list and exactly one in the part's. Ctrl+Z in the part removes engraving and sketch together.
57. A part whose largest face points the other way: still one sketch plus one feature, and the volume drops.
58. Imperial part: 0.12 in text, 0.008 in deep. Unticked: the part is not changed.
59. A part with no planar face: the drawing marking is placed, a warning says why, no new sketch or feature in the part.

## Batch Print (202)

60. Assembly with three parts that have prints in `Prints\` and one without, saved. Batch Print (assembly ribbon): a status bar count, then "Printed 4 drawings" naming the part with no drawing. `<assembly name>.pdf` sits beside the assembly: the assembly's sheets first, then the parts', every sheet, in colour.
61. **The merge works inside Inventor:** the log has no FileLoadException or other error mentioning Microsoft.Extensions or PdfSharp.
62. Run again: asks to replace. No leaves the file; Yes replaces it. With the PDF open in a viewer: the "close it in your PDF viewer" message, old PDF untouched.
63. Afterwards no new drawings are open; none changed or saved (check modified times). A print open before the run stays open and unchanged.
64. Add a Content Center part and a sub-assembly: "Skipped 1 library part"; the sub-assembly's drawing comes before its children's.
65. Break one print (rename it): "Printed N of M", naming it with the reason. **No hidden drawing is left** in the Documents list after this run.
66. On a part, a drawing, an unsaved assembly and a library assembly: each gets a plain message.

## Overall REF dimensions (204)

67. Front view of a plain rectangular part, nothing selected, Overall REF Dimensions. A dimension below and one to the right, each "<value> REF", bold, a bit larger than other dimensions, no warning.
68. One Undo removes both. Change the part's size and update: they follow.
69. Select a side view and click: dimensions go on that view.
70. **Slot part (arc-ended):** the straight-ended dimension is placed and a warning names the other. No wrong-valued or stray dimension is left on the sheet.
71. On a part or assembly window: "Open a drawing first".

## Stock data in Settings (205)

72. Settings, Material and finish: a "Stock data" row with Browse under "Notes data". A `.csv` path is refused. Clear it and save.

## Stock Size (206)

73. A block part (for example 61 x 123 x 10.2 mm, metric). Stock Size sits under Material Finish on the Part ribbon. The window shows Plate and a text such as "PL 16 x 65 x 125", box focused, "Now" blank.
74. OK: Material Size holds the text. One Undo removes it ("Stock Size" in the list).
75. Round bar: the text becomes "Ø.. x ..". Type "PL 20 x 70 x 130", OK: held exactly. Run again and OK unchanged: no new Undo step.
76. A bar modelled off-axis: the true bar size, not the axis-aligned box.
77. A part bigger than the workbook's largest thickness: blank text, red message, OK disabled until typed.
78. Sheet metal (with and without a flat pattern): a message, nothing written. Drawing or assembly: no button.
79. Imperial part: for example "PL 0.5 x 2.5 x 5". Create Print on the block: MATERIAL SIZE shows the text.
80. Stock data pointing to a missing file: the window still opens, blank, with the problem in muted text.

## Update Parts List (208)

81. Assembly drawing with an AWB-numbered part, a Content Center screw and a project library part, parts list from the AWB template. Update Parts List: Make 1, Buy 2; cells MAKE, BUY, BUY.
82. Save, close, reopen: same. One Undo reverts all three cells.
83. A parts list style with no Make/Buy column: a MAKE/BUY column appears at the end, filled; one Undo removes column and text.
84. A typed value on a part that is neither AWB nor library stays.
85. **Clearing MAKE:** change the MAKE part's Part Number to a non-AWB value, run again: the cell is blank and the message says one cell was cleared.
86. No parts list: "place a parts list first", nothing changes.

## Hole workbook warning (212)

87. Point the hole workbook at a missing file. A drawing of a part with three tapped holes and a hole table: Update.
88. The log shows the workbook problem once for that Update, not three times. Count only lines written before you close the wizard: closing it runs the automatic check, which logs the problem again. With a missing file the line is the loader's generic problem line; the new "could not be read" wording (naming tapped and taper tapped holes) appears only when reading the workbook throws.
89. Restore the path, Update: tap drills read as before.

## Open questions waiting on you

Spec 05 (`docs/features/05-drawing-tools.md`), Open questions. Each has a default that holds until you answer:

- **37** Check Out of Bounds: modal list, symbols as a point, no border means sheet edge. Modeless instead, or measure views by drawn lines?
- **38** Update Notes: heading, numbering, width and position of the list and bottom line, message after each run. Keep, or change?
- **39** REF text size: 1.25 times normal dimension text. Right size?
- **40** REF on an arc end: not placed, with a warning. Or centre to centre plus radii, or a helper sketch line?
- **41** Stock text: no unit suffix, imperial as "PL 0.5 x 2.5 x 5". Add a suffix, or use ".500 x 2.50 x 5.00"?
- **42** Stock Size on sheet metal: declined. Allow it when there is no flat pattern, or always?
- **16 (extensions)** Part marking: mark the revision too? Engrave only for laser engrave and etch? Click an edge for the zone, and the zone and leader text layout from task 199? Engrave on the largest flat face, or let you pick the face?
