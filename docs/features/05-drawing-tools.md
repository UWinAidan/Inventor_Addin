# 05: Drawing tools

Smaller commands that speed up making and checking a drawing. The hole table wizard has its own spec (04).

Reference screenshots (local only): `print tab and features.png`, `Screenshot 2026-09-04 082249.png`, `addin tab.png`.

## What exists today

`DrawingExtractor` reads sheets (size, orientation, border), title block fields, views, hole tables, parts lists, revision tables and sketched symbol names. Nothing edits a drawing yet.

## Commands

The reference tool's drawing ribbon has the buttons below. Only the button names are known; the behaviour column marks what is a guess.

| Command | Where | Behaviour | Confidence |
|---|---|---|---|
| Create Drawing | part or assembly | Moved to spec 02 as **Create Print** (Aidan, 2026-10-10): a drawing from the AWB template with the initial views chosen in the Initial Views window (task 192), named and numbered like the model. Place Initial Views on an existing drawing is also here | decided |
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
- **Title block:** part number, description, material, finish, designer, detailer, status, weight, revision, date, scale, sheet x of y, units, general tolerances, the projection symbol, Aidan's logo (the files in `libraries/Branding/`, not a drawn logo) and aidanwbrown.com. Revision is Inventor's standard Revision Number, read from the model. Status (Aidan, 2026-11-22, revised the same day): the title block prints Inventor's own Status field (Design Tracking "User Status", the iProperties Status tab). The add-in writes it in words matching Design Status ("WORK IN PROGRESS", "PENDING", "RELEASED") whenever it sets the status and keeps Design Status in step (Design Status prints as "WorkInProgress", spike 183). Revision and Status fill in automatically (tasks 210, 219). The Detailer is the drawing's Author and the Date the drawing's date. The black mark is the logo. General tolerances list the rules: metric "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN MM. TOLERANCES: X ±0.5, X.X ±0.2, X.XX ±0.05, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES."; imperial "DIMENSIONS IN INCHES. TOLERANCES: .X ±.03, .XX ±.01, .XXX ±.005, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.", laid out as a heading, the linear tolerances stacked in a column with angles beside them, and the edge note underneath. Designer and Detailer show the property values exactly as stored, with no reformatting. A Size field shows the sheet size. Layout: `docs/ui-mockups/title-block.png` (180 by 50 mm on every sheet size; part number and revision 7 mm). Left section, 65 mm wide (Aidan, 2026-11-21, replacing the 20 mm logo panel): the logo in its own 22 mm square cell top-left with about 1.5 mm margin round the mark, the tolerance box beside it (43 by 22 mm), the projection symbol in a wide, short cell across the whole section under them (65 by 20 mm), and "aidanwbrown.com" in its own cell across the bottom (65 by 8 mm), level with the Scale/Sheet row. No vertical URL.
- **Finish notes:** a notes block on the sheet reads the finish note from the model and updates with it. From 2026-11-21 that block is the NOTES list: the Finish Note is a numbered note there, with the finish's process notes (see "Finish on the print" below).
- **Open Drawing:** opens the model's print from `Prints\`; when there is none, it offers Create Print.
- **Place Initial Views:** part: front, top, right and iso. Sheet metal: folded front (looking along the bend lines, so the bend profile shows; no bend notes), flat pattern and iso. Assembly: front and iso. Every view, iso included, is black and white (hidden lines removed, no shading; Aidan, 2026-11-23). Scale by Create Print's rule. A window shown before the print is created lets the user confirm or change the views. Before the drawing is made, Create Print replaces a Designer or Author that equals the Windows login with the Settings name; any other name is left alone (task 236). In Create Print the window also offers Metric | Imperial (default the part's units), which picks the AWB template, and a sheet size list (A4 to A0, ANSI A to E) that follows the views until the user picks one, offering the next size when a chosen scale does not fit (task 232). It also has a Front view list (Auto, Front, Back, Top, Bottom, Left, Right; task 233; the iso looks at the front-top-right corner of the chosen front, task 243) and a live preview of the sheet: the border, a plain title block band and a labelled box per planned view, outlined in the error colour when the views do not fit (task 234).
- **Not this milestone:** release logic for revision and status. Aidan's long-term idea, a lightweight PDM on his home server, is in `TODO.md`.

## Added for M5 (Aidan, 2026-11-21)

Aidan asked for these after looking at prints in general use. They are described here in our own words; nothing is copied from any other company's prints.

### Template additions

The title block mockup was approved on 2026-11-21 with these additions, drawn into `docs/ui-mockups/title-block.png` (blue numbers 18 to 25) for a last look before anything is built.

- **Zone grid** on the border: numbers across, letters down, with a centring arrow at the middle of each edge. Default: about 50 mm per zone (A4 portrait 4 by 6, A3 8 by 6, A2 12 by 8, A1 16 by 12, A0 24 by 16; ANSI A 4 by 2, B 4 by 4, C 8 by 4, D 8 by 4, E 16 by 8), numbers left to right and letters from the top on every size.
- **X/Y axis triad** inside the border in the bottom-left corner: X to the right, Y up.
- **Finish on the print** (Aidan, 2026-11-21, replacing the full-width FINISH line he asked for earlier the same day): no FINISH line. The title block's Finish field shows the finish spec, for example "ANODISE, CLEAR, 10-15 µm", and is widened to 45 mm (Material narrows to 40 mm). It reads the custom **Finish Spec** property, which applying a finish writes as the finish name, then ", " and the thickness in µm (metric parts) or inches (imperial parts) when there is one (Aidan, 2026-11-22, tasks 211 and 213). Finish process instructions (masking, handling, surface requirements) are numbered notes in the NOTES list above the title block, together with the model's Finish Note (stored by M3), not in the title block. Update Notes writes them (New tools, item 1).
- **Small extra fields** in a row on top of the title block: the model's file name (with its extension) and the top assembly or project (Default: Inventor's standard Project property, Design Tracking). The part number is repeated small (2.5 mm), rotated, in the right margin outside the border, level with the bottom of the border.
- **Material Size** field in the title block, between Material and Finish. It reads the custom Material Size property that the Stock Size command fills. The property name is "Material Size" with a capital S, like Finish Thickness (Aidan, 2026-11-22).
- The imperial tolerance box carries the "UNLESS OTHERWISE SPECIFIED:" heading too.
- With the new row the block is 180 by 55 mm; the approved 50 mm part is unchanged apart from the Material Size field and the wider Finish field.

### New tools (later M5 tasks, after the template)

1. **Automatic notes from material and finish.** Rules in a workbook (`libraries/AWB_Notes.xlsx`, like the hole workbook, with a Settings path to Aidan's own copy) map materials and finishes to notes. Examples: a 3D-print material adds a bold "NOTE: 3D PRINTED PART" at the bottom of the sheet; a hardened material adds its heat-treat note; a finish adds its process notes (masking, for example). The model's Finish Note is always a numbered note in the list when it is not blank (Aidan, 2026-11-21). Defaults: the list is material notes first, then the Finish Note, then the finish's notes (question 23); a rule matches the material or finish name with `*` and `?` wildcards, ignoring case; every matching row adds its note, in workbook order, each note once; a row's placement is either the numbered NOTES list above the title block or a bold line at the bottom of the sheet. An **Update Notes** command on a drawing writes or refreshes AWB's notes on the first sheet, and Create Print runs it once. AWB's notes are found again by an attribute, so notes typed by hand are never touched.
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
12. ~~FINISH line or a finish notes block?~~ Answered by Aidan 2026-11-21: no FINISH line; the Finish field shows the finish spec, and the Finish Note and the finish's process notes are numbered notes in the NOTES list.
13. Default: zone counts and labelling as listed under "Template additions" (about 50 mm per zone, numbers left to right, letters from the top on every size, ANSI included). Change any?
14. Default: the Project field reads Inventor's standard Project property. Or a custom property, or the top assembly's part number?
15. Default: automatic notes match names by wildcard, all matching rows apply in workbook order, and placement is the NOTES list or a bold bottom line. Other placements or rule columns wanted? Also (task 196): a blank Placement means List, a blank material or finish name matches no rule, and a note repeated by two rows is placed once.
16. Default: part marking marks the part number, 3 mm characters, engraved 0.2 mm deep, zone 40 by 15 mm measured on the part, so it scales with the view (spike 184), highlight blue #0070C0. Change any? Should the revision be marked too? Also (task 198): the note reads "STAMP / ETCH / INK MARK PART NUMBER, ..." for the other methods, and the engraved feature is offered for any method except None (not for assemblies). Engrave only for laser engrave and etch? Also (task 199): the zone asks the user to click an edge in the view to mark (not the view itself); in zone mode the method and extra lines are a second text under the zone, and the leader note carries the part number as a field line under the method line. Change any? Also (task 200): the engraving goes on the part's largest planar face, at a point on it, not on a face the user picks, and is a separate Undo step in the part. Pick the face instead?
17. Default: Batch Print skips Content Center and library parts, lists models with no drawing in a report, names the PDF `<assembly name>.pdf`, and prints in colour as drawn. The drawings' PDFs are merged with PDFsharp (MIT), since Inventor cannot append (spike 184). Black and white instead?
18. Default: overall REF dimensions go on the chosen view (or the sheet's first base view), 10 mm outside it.
19. Default: stock is plate unless round bar is chosen; thickness rounds to the workbook's list, width and length to its step, allowance per side from the workbook. Other stock shapes (flat bar, square bar, tube)?
20. Default: Make/Buy leaves a part that is neither AWB-numbered nor a library or Content Center part blank; manufacturer columns read Vendor and Stock Number. Custom properties instead?
21. Default: Create Print uses an A3 landscape sheet (metric) or ANSI B landscape (imperial) from the AWB template; the user changes the size with Inventor's Edit Sheet. When the AWB template file is missing, Create Print says where it looked and stops (no fallback to Inventor's template).
22. Default: the starter templates are built by a developer command (Build Print Template) from the layout in Core, so the layout lives in code and can be rebuilt; Aidan's own copy is whatever he saves from one and edits by hand.
23. Answered by Aidan 2026-11-22: a ready-made **Finish Spec** property, written with the finish (tasks 211, 213). It is the finish name, then ", " and the thickness in the part's units: µm on metric parts ("ANODISE, CLEAR, 10-15 µm"), inches on imperial parts (".0004-.0006 IN"). There is no trailing comma when there is no thickness. Finish Thickness stays stored in mm. Default too: in the NOTES list the Finish Note comes after the material notes and before the finish's own notes.
24. Answered by Aidan 2026-11-22: on A4 and ANSI A portrait, the X/Y triad sits just above the title block at its left end (task 214).
25. Answered by Aidan 2026-11-22, then revised the same day: the border is centred, with equal margins all round (10 mm ISO, 12.7 mm ANSI; task 215).
26. Answered by Aidan 2026-11-22: ANSI sheets keep the listed (ASME) zone counts; ISO sheets use about 50 mm per zone (the listed ISO counts already do).
27. Answered by Aidan 2026-11-22: keep it. Unticking Front unticks Top and Right, and Iso stays.
28. Mass in lb on the imperial template (task 187 check, 2026-11-22): the WEIGHT field (Inventor's `<PhysicalProperty>` mass) printed "4.33 kg" on the imperial template, although the build set the drawing's mass unit to lb first. Spike 183 also found that unit attributes on the field are dropped, and that changing the drawing's or the part's mass unit later does not change it. Options:
    (a) an imperial part's own mass unit decides: the add-in sets the part's mass unit to lb when it makes or converts an imperial part (New Part, Mass Update), then checks whether a fresh drawing prints lb (a short spike round);
    (b) the add-in writes a custom text property, for example "Mass", with the mass formatted in the part's units (kg or lb, 2 decimals) on Create Print and Mass Update, and the template prints that instead of Inventor's live mass. It is always readable, but it goes stale when the part changes until the next Create Print or Mass Update;
    (c) the imperial template prints kg.
    Which?
    Answered by Aidan 2026-11-22: try (a) first in a short spike round; if a fresh drawing still prints kg, use (b), with the Mass property refreshed whenever the part is saved.
    Spike 183 round 4 (2026-11-22): (a) works, so (a) is built (task 222).
29. The lb mass prints as "9.55 lbmass" (Inventor's own unit text, spike 183 round 4). Is that acceptable, or should the imperial print show "LB"? Getting "LB" needs another way, for example a Mass text property as in question 28 (b) for imperial parts only. Default until answered: "lbmass" stays.
    Answered by Aidan 2026-11-23: "lbmass" is not acceptable. First try making the mass field print "lb" (spike 225); if Inventor cannot, use option (b): a "Mass" text property ("9.55 lb" / "4.33 kg", 2 decimals) refreshed automatically when the part is saved, printed by the template (task 222).
30. Long title block values (spike 218): a value longer than its cell is narrowed with Inventor's text width scale on each drawing (task 238). What is the narrowest you accept? Default: 0.6 of normal width.
    Answered by Aidan 2026-11-23: 0.6 of normal width is the narrowest.
31. Below that floor: only warn, or also wrap Description onto two lines? Default: warn only.
    Answered by Aidan 2026-11-23: warn only. The warning names the field and how much too long the value is.
    Overturned by Aidan 2026-11-23, after his check: every title block value (not only the file name and Description) first narrows to 0.6 width; if still too long it also gets smaller, down to a readable floor (question 32); Description may wrap onto two lines in its cell; single-line fields never wrap, they shrink to the floor and only then warn (naming the field and how much too long the value is). Spike 241 finds a way to wrap that works, or shows why it cannot (task 238).
32. Answered by Aidan 2026-11-26: 1.8 mm (task 238).
33. Answered by Aidan 2026-11-26: FINISH and WEIGHT follow the print's units, as drawing properties the fields read, refreshed whenever the drawing is opened or saved as well as by Create Print (task 244).
34. Answered by Aidan 2026-11-26: a hand-typed sheet metal Material Size is replaced by the computed one, and Material Size stays in the part's units on an off-units print.
35. Answered by Aidan 2026-11-26: nothing is written into the assembly; the print shows "—" for a blank Material, Material Size or Finish and the model stays untouched (task 245).
36. Answered by Aidan 2026-11-26: the first sheet whose scale is at least 1:2, else the largest.
37. Check Out of Bounds (task 195): defaults picked. A sheet with no border is checked against the sheet edge; sketched symbols count as a point (their centre); the list is a modal window that selects the picked item on its sheet; items whose extent cannot be read are skipped and logged. Is that right, or should the list be modeless, or views be measured by their drawn lines? Default until answered: as built.
38. Update Notes (task 197): defaults picked. The list is headed "NOTES:" in bold with plain "1. " numbering, left-aligned with the title block, as wide as it, 2 mm above it, growing upward; the bottom line is bold, starts about 30 mm in from the border's left edge (clear of the X/Y triad), is at most 150 mm wide, sits at the bottom of the border and goes above the list when the title block leaves it no room (A4 and ANSI A portrait); both use the drawing's default note style; the material is the part's active material name (an assembly has none, so only finish rules apply); the command shows what it wrote after each run. Is that right, or should the list use a narrower width, a different heading, or the command stay silent on success? Default until answered: as built.
39. Overall REF dimensions (task 203): how much larger than normal dimension text should the REF text be? Default until answered: 1.25 times the normal dimension text height.
40. Overall REF dimensions (task 204): Inventor measures a linear dimension to an arc or circle from its centre, so a view whose leftmost, rightmost, lowest or highest geometry is an arc (a slot end, a round bar) cannot get an associative overall dimension. Default until answered: that dimension is not placed and the command says so. Should it instead dimension centre to centre and add the radii by hand, or draw a helper sketch line between the extremes and dimension that?
41. Stock size text (task 205): the spec example for imperial is "PL .500 x 2.50 x 5.00", but Aidan asked that the numbers use the same format as the sheet metal Material Size text (task 230), so imperial reads "PL 0.5 x 2.5 x 5" (3 decimals at most, trailing zeros dropped, leading zero kept), and metric "PL 12 x 65 x 130". Default until answered: no unit suffix on a stock text (the sheet metal text ends in " mm" or " in"), so an imperial stock text carries no unit word. Also: width and length share one step per units, a round bar's length also gets the allowance and the step, its diameter is the larger of the two smaller box sides plus the allowance, and a part too big for the list gets no text (the window says so and the user types one). Add the unit suffix, or keep the fixed-decimal imperial form (".500 x 2.50 x 5.00") for stock text?
42. Stock Size on sheet metal (task 206): the command declines a sheet metal part, because its Material Size is the flat pattern's (task 230) and would overwrite a "PL ..." text. Default until answered: it shows a message and writes nothing, also when the part has no flat pattern yet. Should it instead write the stock size for a sheet metal part that has no flat pattern, or for any sheet metal part?
