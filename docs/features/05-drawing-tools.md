# 05: Drawing tools

Smaller commands that speed up making and checking a drawing. The hole table wizard has its own spec (04).

Reference screenshots (local only): `print tab and features.png`, `Screenshot 2026-09-04 082249.png`, `addin tab.png`.

## What exists today

`DrawingExtractor` reads sheets (size, orientation, border), title block fields, views, hole tables, parts lists, revision tables and sketched symbol names. Nothing edits a drawing yet.

## Commands

The reference tool's drawing ribbon has the buttons below. Only the button names are known; the behaviour column marks what is a guess.

| Command | Where | Behaviour | Confidence |
|---|---|---|---|
| Create Drawing | part or assembly | Moved to spec 02 as **Create Print** (Aidan, 2026-10-10): a drawing from the default template with one base view, named and numbered like the model. Placing a full set of initial views stays here | decided |
| Open Drawing | part or assembly | Find and open the drawing that belongs to the active model | likely |
| Place Initial Views | drawing | Place a standard set of views of the referenced model at a sensible scale | likely |
| Change Title Block | drawing | Swap the sheet's title block for another from the template's drawing resources | likely |
| Title block fill | drawing | Title block fields filled from iProperties: part number, description, material, finish, designer, status | seen on the sheet |
| Check Out of Bounds | drawing | Report views or annotations that fall outside the sheet border | guess |
| Check Balloon Usage | drawing | Report parts list items that have no balloon, and balloons used more than once | guess |
| Reference Axis Symbol | drawing | Place a standard symbol | unknown |
| Reference Part Note | drawing | Place a standard note marking a part as reference | guess |
| Place Mirror Note | drawing | Place a standard note for a part made as a mirror image of another | guess |
| Add Parts List Column | drawing | Add a column to the parts list for a custom property | likely |

Other details seen on the reference sheets, as hints for templates and defaults:

- An assembly sheet with an isometric view, a section view, a front view, balloons, and a parts list in the top right corner. Parts list columns: item, quantity, part number, two short flag columns, description, manufacturer name, manufacturer part number.
- A part sheet with a dimensioned front view, a section, two isometric views, and the hole tables stacked above the title block.

## Custom drawing template

Decided by Aidan on 2026-10-10: a custom drawing template is a separate item for this milestone (M5), not for numbering (M2). Its title block must read every property from the model (part number, description, designer and the rest), not copy them into the drawing, so a change in the model shows on the print without any update step.

## Decided for M5 (Aidan, 2026-11-20)

- **Order:** the print template first. Then Open Drawing, Place Initial Views and Check Out of Bounds. The notes and symbols commands come later.
- **Template:** ISO A4 to A0 and ANSI A to E, metric and imperial; white sheets, not Inventor's cream; third angle projection. Revision table and parts list styles are in the template, but nothing places them automatically. A starter template ships in the repo, with a Settings path to Aidan's own copy. Create Print uses it.
- **Title block:** part number, description, material, finish, designer, detailer, status, weight, revision, date, scale, sheet x of y, units, general tolerances, the projection symbol, Aidan's logo (the files in `libraries/Branding/`, not a drawn logo) and aidanwbrown.com. Revision is Inventor's standard Revision Number and Status Inventor's built-in Design Status, both read from the model and set by hand in Part Properties for now. The Detailer is the drawing's Author and the Date the drawing's date. The black mark is the logo. General tolerances list the rules: metric "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN MM. TOLERANCES: X ±0.5, X.X ±0.2, X.XX ±0.05, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES."; imperial "DIMENSIONS IN INCHES. TOLERANCES: .X ±.03, .XX ±.01, .XXX ±.005, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.", laid out as a heading, the linear tolerances stacked in a column with angles beside them, and the edge note underneath. Designer and Detailer show the property values exactly as stored, with no reformatting. A Size field shows the sheet size. Layout: `docs/ui-mockups/title-block.png` (180 by 50 mm on every sheet size; logo panel 20 mm, tolerance and projection column 45 mm (tolerance box 45 by 22 mm); part number and revision 7 mm).
- **Finish notes:** a notes block on the sheet reads the finish note from the model and updates with it. From 2026-11-21 the FINISH line above the title block does this job (see below; Default, question 12).
- **Open Drawing:** opens the model's print from `Prints\`; when there is none, it offers Create Print.
- **Place Initial Views:** part: front, top, right and iso. Sheet metal: folded front and the flat pattern. Assembly: front and iso. Scale by Create Print's rule. A window shown before the print is created lets the user confirm or change the views.
- **Not this milestone:** release logic for revision and status. Aidan's long-term idea, a lightweight PDM on his home server, is in `TODO.md`.

## Added for M5 (Aidan, 2026-11-21)

Aidan asked for these after looking at prints in general use. They are described here in our own words; nothing is copied from any other company's prints.

### Template additions

The title block mockup was approved on 2026-11-21 with these additions, drawn into `docs/ui-mockups/title-block.png` (blue numbers 18 to 25) for a last look before anything is built.

- **Zone grid** on the border: numbers across, letters down, with a centring arrow at the middle of each edge. Default: about 50 mm per zone (A4 portrait 4 by 6, A3 8 by 6, A2 12 by 8, A1 16 by 12, A0 24 by 16; ANSI A 4 by 2, B 4 by 4, C 8 by 4, D 8 by 4, E 16 by 8), numbers left to right and letters from the top on every size.
- **X/Y axis triad** inside the border in the bottom-left corner: X to the right, Y up.
- **FINISH line:** a full-width row directly above the title block for the long finish callout. It reads the model's Finish Note. The title block's Finish field stays the short name (Finish).
- **Small extra fields** in a row above the FINISH line: the model's file name (with its extension) and the top assembly or project (Default: Inventor's standard Project property, Design Tracking). The part number is repeated small (2.5 mm), rotated, in the right margin outside the border, level with the bottom of the border.
- **Material Size** field in the title block, between Material and Finish. It reads the custom Material Size property that the Stock Size command fills.
- The imperial tolerance box carries the "UNLESS OTHERWISE SPECIFIED:" heading too.
- With the two new rows the block is 180 by 62 mm; the approved 50 mm part is unchanged apart from the Material Size field.

### New tools (later M5 tasks, after the template)

1. **Automatic notes from material and finish.** Rules in a workbook (`libraries/AWB_Notes.xlsx`, like the hole workbook, with a Settings path to Aidan's own copy) map materials and finishes to notes. Examples: a 3D-print material adds a bold "NOTE: 3D PRINTED PART" at the bottom of the sheet; a hardened material adds its heat-treat note; a finish adds its finish note. Defaults: a rule matches the material or finish name with `*` and `?` wildcards, ignoring case; every matching row adds its note, in workbook order, each note once; a row's placement is either the numbered NOTES list above the title block or a bold line at the bottom of the sheet. An **Update Notes** command on a drawing writes or refreshes AWB's notes on the first sheet, and Create Print runs it once. AWB's notes are found again by an attribute, so notes typed by hand are never touched.
2. **Part marking wizard.** Choose the method (laser engrave, stamp, etch, ink, none), extra lines (for example "DO NOT STAMP"), and the target: a leader to a face on the iso view, or a dashed marking zone with "MARKING MUST BE PLACED INSIDE THIS ZONE", drawn in a highlight colour. Optionally it adds an engraved part number feature in the model whose text is linked to the Part Number property. Defaults: the marked text is the part number; character height 3 mm (0.12 in); engraving depth 0.2 mm (0.008 in); the highlight colour is a setting (blue, #0070C0); the zone is a 40 by 15 mm rectangle in a sketch on the chosen view, centred on it, for the user to move and resize.
3. **Batch Print:** one PDF of an assembly and every drawing under it, the assembly's own drawing first, saved next to the assembly. Defaults: drawings are found by the same rule as Open Drawing (`Prints\` first); components are walked depth first in browser order and each file is printed once; Content Center and library parts are skipped; a model with no drawing is listed in the report at the end, not an error; the PDF is `<assembly name>.pdf`, and an existing one is replaced only after asking; every sheet of each drawing is included, in colour as drawn.
4. **Overall REF dimensions:** bold overall size dimensions marked REF, from the part's bounding box, on the chosen view. Defaults: one horizontal and one vertical dimension across the view's extremes, the text "<value> REF" in bold, placed 10 mm outside the view; with no view selected, the sheet's first base view.
5. **Stock size:** the raw stock size from the bounding box plus a machining allowance, rounded up to standard stock sizes from a workbook (`libraries/AWB_Stock.xlsx`, Settings path as for the others), written to a custom **Material Size** property that the title block shows. Defaults: plate by default, round bar on request in the command's window; the plate thickness rounds up to the next listed thickness and the width and length to the next listed step; the allowance per side is in the workbook's Settings tab; the text reads like "PL 12 x 65 x 130" or "Ø30 x 85" (imperial "PL .500 x 2.50 x 5.00"); the window shows the result, which can be edited before it is written.
6. **Parts list Make/Buy column** (answers M5 planning question 18): AWB-numbered parts are Make, library and Content Center parts are Buy, plus manufacturer name and manufacturer part number columns read from properties. Defaults: a part that is neither is left blank; the manufacturer columns read Inventor's standard Vendor and Stock Number properties (Design Tracking); an **Update Parts List** command fills the Make/Buy cells of the parts list on the sheet, and the template's parts list style has all three columns.

The Inventor checks for every M5 task are collected into one checklist (`docs/checklists/m5.md`) for one combined check at the end of the milestone.

## Core logic (testable without Inventor)

- Balloon check: given the parts list items and the balloons on a sheet, which items are missing and which are duplicated. `DrawingData` already carries the parts list rows.
- Out-of-bounds check: given the sheet size, border margins and the bounding boxes of views and annotations, which ones fall outside.
- Initial view layout: given the model's bounding box and the sheet's usable area, a scale and a position for each view.
- Finding a model's drawing: the path rule for where it should be.
- The text of the standard notes, held in settings so it can be changed without a rebuild.

## Add-in side

Each command is a thin read, call Core, apply. Creating drawings and placing views depend on templates (see spec 02, question 5).

## Open questions

1. For each row marked guess or unknown, what should the command actually do? Rows Aidan does not want can be dropped. Deferred until those commands come up (Aidan, 2026-11-20).
2. ~~Which views count as initial views?~~ Answered 2026-11-20, see above.
3. Which title blocks are there to switch between, and when is each used? Deferred with Change Title Block.
4. ~~Which commands matter most?~~ Answered 2026-11-20, see above.
5. to 11. Title block questions A to G: answered by Aidan on 2026-11-20, see Decided above.
12. Default: the FINISH line above the title block (the Finish Note) replaces the separate finish notes block decided on 2026-11-20, and automatic notes do not repeat the Finish Note; a finish rule adds only extra notes (masking, for example). Keep both?
13. Default: zone counts and labelling as listed under "Template additions" (about 50 mm per zone, numbers left to right, letters from the top on every size, ANSI included). Change any?
14. Default: the Project field reads Inventor's standard Project property. Or a custom property, or the top assembly's part number?
15. Default: automatic notes match names by wildcard, all matching rows apply in workbook order, and placement is the NOTES list or a bold bottom line. Other placements or rule columns wanted?
16. Default: part marking marks the part number, 3 mm characters, engraved 0.2 mm deep, zone 40 by 15 mm, highlight blue #0070C0. Change any? Should the revision be marked too?
17. Default: Batch Print skips Content Center and library parts, lists models with no drawing in a report, names the PDF `<assembly name>.pdf`, and prints in colour as drawn. Black and white instead?
18. Default: overall REF dimensions go on the chosen view (or the sheet's first base view), 10 mm outside it.
19. Default: stock is plate unless round bar is chosen; thickness rounds to the workbook's list, width and length to its step, allowance per side from the workbook. Other stock shapes (flat bar, square bar, tube)?
20. Default: Make/Buy leaves a part that is neither AWB-numbered nor a library or Content Center part blank; manufacturer columns read Vendor and Stock Number. Custom properties instead?
21. Default: Create Print uses an A3 landscape sheet (metric) or ANSI B landscape (imperial) from the AWB template; the user changes the size with Inventor's Edit Sheet. When the AWB template file is missing, Create Print says where it looked and stops (no fallback to Inventor's template).
22. Default: the starter templates are built by a developer command (Build Print Template) from the layout in Core, so the layout lives in code and can be rebuilt; Aidan's own copy is whatever he saves from one and edits by hand.
