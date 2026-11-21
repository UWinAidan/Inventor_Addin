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
- **Finish notes:** a notes block on the sheet reads the finish note from the model and updates with it.
- **Open Drawing:** opens the model's print from `Prints\`; when there is none, it offers Create Print.
- **Place Initial Views:** part: front, top, right and iso. Sheet metal: folded front and the flat pattern. Assembly: front and iso. Scale by Create Print's rule. A window shown before the print is created lets the user confirm or change the views.
- **Not this milestone:** release logic for revision and status. Aidan's long-term idea, a lightweight PDM on his home server, is in `TODO.md`.

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
