# 04: Hole table wizard

For a drawing view, find every hole, sort the holes into precision and clearance groups, tag them on the view, and generate two hole tables with coordinates and descriptions.

Reference screenshots (local only): `hole wizard pop up.png`, `hole wizard popup after loading veiw data.png`, `hole table result.png`, `hole table drawing leaders.png`, `Screenshot 2026-09-04 082249.png`.

## What exists today

- `PartExtractor` reads hole features into `HoleData`: type, termination, diameter, depth, counterbore, countersink, spotface, thread data, and a centre point per instance in model space.
- `DrawingExtractor` reads sheets, views (name, scale, position, referenced file) and summarises existing hole tables.
- Nothing maps model holes into a view's coordinates, and nothing creates tables or tags.

## Target dialog: "Hole Table Wizard"

Seen in the reference:

- **Header:** active drawing name; a Sheet dropdown; a *Toggle View Names* button.
- **Two tabs:** *Create Tables* and *Edit Table*. Only *Create Tables* is shown in the screenshots.
- **Create Tables tab:**
  - A View dropdown with *Analyze View* and *Create Tables* buttons beside it.
  - Two lists, **Precision Holes** on the left and **Clearance Holes** on the right. Each row has a Description and a *Split* checkbox.
  - Between the lists: a swap button, which moves the selected row to the other list, and a delete button.
  - Under the lists: *Generate Precision Table* and *Generate Clearance Table*.
- **Bottom row:** *Align*, *Renumber*, *Update Reams*, *Cancel*, and a progress bar.

After *Analyze View* on a plate with thirteen holes, the lists held one row per hole instance: two toleranced holes on the precision side; six plain through holes of one diameter, one of another, and four tapped holes on the clearance side.

## Target output

Two tables per view, stacked on the sheet above the title block:

| Table title | Contents |
|---|---|
| `Precision Table: <view name>` | toleranced holes |
| `Tap and Clearance Table: <view name>` | plain and tapped holes |

Columns in both: **HOLE, XDIM, YDIM, DESCRIPTION, NOTES**.

Rules read off the reference result:

- **Tags** are a letter per distinct hole description and a number per instance: `A1`, `A2`, `B1` to `B6`, `C1`, `D1`, `D2`. Letters run on across both tables: precision groups take the first letters, clearance groups continue from there.
- **Order within a group** is by Y ascending, then X ascending.
- **Coordinates** are relative to an origin on the view, shown on the drawing with ordinate dimensions from `0.00`. The precision table shows two decimals, the clearance table one.
- **Description** is written once per group, in a cell merged across the group's rows.
  - Toleranced hole: diameter, fit class, the limit deviations stacked, and the termination, for example a 6.00 H7 hole shown with `+0.012 / -0.000` and `THRU`.
  - Plain hole: diameter and termination.
  - Tapped hole: two lines, the drill termination, then thread designation and thread depth.
- **NOTES** defaults to `-`.
- **On the view**, each hole gets its tag beside it. Precision holes appear to be drawn with a distinct centre marker (filled quadrants) so they stand out from clearance holes.

## Core logic (testable without Inventor)

This feature has the most logic that can be built and tested in the cloud.

- Classification: precision or clearance, from the hole's tolerance and thread data.
- Grouping holes with identical descriptions, ordering groups, ordering instances, assigning tags.
- Renumbering after rows are moved, deleted or split.
- Coordinate handling: origin offset, rounding, decimals per table.
- Description formatting for every hole type: drilled, counterbore, countersink, spotface, tapped, through or blind.
- ISO 286 limit deviations for a diameter and fit class.
- A table model (title, columns, rows, merged ranges) that the add-in renders.
- The dialog's view-model: lists, selection, swap, delete, which buttons are enabled.

## Add-in side

- Read the holes visible in a drawing view and their positions on the sheet.
- Read hole tolerances from the model.
- Create the two tables on the sheet and position them.
- Place tags on the view, and the origin indicator.
- The WPF window, bound to the Core view-model.

## Design decision to settle with a spike on Windows

Inventor's own hole table can be created from a view and stays associative, but it may not support two tables with one shared tag sequence, merged description cells and custom titles. The alternative is a custom table plus our own tags, which gives full control and loses associativity. This needs a short experiment in Inventor before the add-in side is planned.

## Open questions

1. **What makes a hole "precision"?** Any hole with a fit tolerance, or a specific list of fit classes, or a tolerance band below some limit.
2. **What does the *Split* checkbox do?** One guess: it gives that instance its own group and tag.
3. **What does *Update Reams* do?** One guess: it refreshes precision hole descriptions after the model changes.
4. **What does *Align* do?** One guess: it stacks the tables and snaps them to the corner above the title block.
5. **What does the *Edit Table* tab contain?**
6. **How are groups ordered** within the clearance table: by diameter, by type with plain before tapped, or in feature order?
7. **Where is the origin?** Picked by the user, taken from an existing origin indicator, or a fixed corner of the view.
8. Are counterbored and countersunk holes in scope for the first version?
