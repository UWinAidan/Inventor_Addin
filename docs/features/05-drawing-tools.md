# 05: Drawing tools

Smaller commands that speed up making and checking a drawing. The hole table wizard has its own spec (04).

Reference screenshots (local only): `print tab and features.png`, `Screenshot 2026-09-04 082249.png`, `addin tab.png`.

## What exists today

`DrawingExtractor` reads sheets (size, orientation, border), title block fields, views, hole tables, parts lists, revision tables and sketched symbol names. Nothing edits a drawing yet.

## Commands

The reference tool's drawing ribbon has the buttons below. Only the button names are known; the behaviour column marks what is a guess.

| Command | Where | Behaviour | Confidence |
|---|---|---|---|
| Create Drawing | part or assembly | Create a drawing of the active model from the drawing template, save it beside the model, place initial views | likely |
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

## Core logic (testable without Inventor)

- Balloon check: given the parts list items and the balloons on a sheet, which items are missing and which are duplicated. `DrawingData` already carries the parts list rows.
- Out-of-bounds check: given the sheet size, border margins and the bounding boxes of views and annotations, which ones fall outside.
- Initial view layout: given the model's bounding box and the sheet's usable area, a scale and a position for each view.
- Finding a model's drawing: the path rule for where it should be.
- The text of the standard notes, held in settings so it can be changed without a rebuild.

## Add-in side

Each command is a thin read, call Core, apply. Creating drawings and placing views depend on templates (see spec 02, question 5).

## Open questions

1. For each row marked guess or unknown, what should the command actually do? Rows Aidan does not want can be dropped.
2. Which views count as "initial views" for a part, a sheet metal part and an assembly?
3. Which title blocks are there to switch between, and when is each used?
4. Which commands matter most? This list is long, and the order decides what the first drawing milestone contains.
