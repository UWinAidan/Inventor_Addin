# 04: Hole table wizard

For a drawing view, find every hole, sort the holes into precision and clearance groups, tag them on the view, and generate two hole tables with coordinates and descriptions.

Reference screenshots (local only): `hole wizard pop up.png`, `hole wizard popup after loading veiw data.png`, `hole table result.png`, `hole table drawing leaders.png`, `Screenshot 2026-09-04 082249.png`.

## First: AWB Hole, the hole generator for part files

M4 part 1 is **AWB Hole**, our own hole command for part files, specified in its own spec: [`09-awb-hole.md`](09-awb-hole.md) (Aidan, 2026-10-17). It colours dowel and precision holes by fit (press red, normal blue, slip green, from `libraries/AWB_Holes.xlsx`). The ISO 286 limit logic is shared with this wizard. The wizard is M4 part 2, planned after AWB Hole.

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
- **Bottom row:** *Align*, *Renumber*, *Update* (the reference calls it *Update Reams*; Aidan, 2026-11-12), *Cancel*, and a progress bar.

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

## Decided behaviour (Aidan, 2026-11-12)

- **Holes in scope:** every AWB Hole type and seat in the first version: Simple, Clearance, dowel, counterbored, countersunk, spotfaced, Tapped and Taper Tapped (question 8).
- **Origin:** the wizard uses the view's existing ordinate-dimension origin. Aidan almost always dimensions with ordinates. If the view has no origin, the wizard stops and says to add one; it never invents one (question 7).
- **Group order in the precision table:** by diameter, ascending.
- **Group order in the clearance table:** by type first, then by diameter ascending within a type (question 6). The type order:
  1. plain: Simple and Clearance
  2. seated: counterbore, countersink, spotface
  3. tapped
  4. taper tapped
- **Order within a group:** Y ascending, then X ascending, as the reference shows.
- **Never by quantity.** Letters must stay stable when instances are added or removed, so a group's place never depends on how many holes it has.
- **Split:** ticking *Split* on a row gives that hole instance its own group and letter (question 2).
- **Update:** refreshes descriptions and positions in both tables after the model changes (question 3).
- **Align:** stacks the two tables and snaps them to the corner above the title block (question 4).
- **Edit Table tab:** edit the NOTES column, and re-open an existing table to swap, delete, split and renumber its rows (question 5).

Defaults chosen while planning (2026-11-12) are in "Open questions" 9 to 17. Each is built as stated until Aidan says otherwise.

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

Inventor's own hole table can be created from a view and stays associative, but it may not support two tables with one shared tag sequence, merged description cells and custom titles. The alternative is a custom table plus our own tags, which gives full control and loses associativity. This needs a short experiment in Inventor before the add-in side is planned. It is spike 160 (`docs/tasks/160-spike-hole-table-in-inventor.md`), which also settles reading a view's holes and its ordinate origin.

## Open questions

1. ~~**What makes a hole "precision"?**~~ Answered 2026-10-24 (Aidan, spec 09): a toleranced hole is precision: an AWB dowel, and an AWB Clearance or Simple hole with Toleranced ticked. Default: a hole with an Inventor tolerance on its diameter and no AWB data is precision too.
2. ~~**What does the *Split* checkbox do?**~~ Answered 2026-11-12 (Aidan): it gives that hole instance its own group and letter.
3. ~~**What does *Update Reams* do?**~~ Answered 2026-11-12 (Aidan): it is renamed *Update*, and refreshes descriptions and positions in both tables after the model changes.
4. ~~**What does *Align* do?**~~ Answered 2026-11-12 (Aidan): it stacks the tables and snaps them to the corner above the title block.
5. ~~**What does the *Edit Table* tab contain?**~~ Answered 2026-11-12 (Aidan): editing the NOTES column, and re-opening an existing table to swap, delete, split and renumber rows.
6. ~~**How are groups ordered?**~~ Answered 2026-11-12 (Aidan): see "Decided behaviour".
7. ~~**Where is the origin?**~~ Answered 2026-11-12 (Aidan): the view's existing ordinate-dimension origin; with none, the wizard stops and says to add one.
8. ~~Are counterbored and countersunk holes in scope for the first version?~~ Answered 2026-11-12 (Aidan): yes, with spotfaced, Simple and Taper Tapped holes.

Defaults chosen while planning M4 part 2 (2026-11-12). Each is built as written until Aidan says otherwise.

9. **Seated sub-order.** Within the seated type: counterbore, then countersink, then spotface, in the order Aidan listed them; diameter ascending inside each. (Or one seated type ordered by diameter alone?)
10. **A threaded hole with a seat** sorts as tapped (or taper tapped), not seated. The thread decides the type.
11. **The diameter a group sorts by** is the drilled diameter: the through hole for plain and seated holes, the tap drill for tapped and taper tapped holes. Groups of one type and diameter that differ (a depth, a seat size) sort by their description text.
12. **Where a split instance goes:** its new group sits straight after the group it came from, and takes the next letter.
13. **Letters:** A to Z, then AA, AB and so on. No letters are skipped (some shops skip I, O and Q).
14. **Decimals in inch drawings:** precision table 3, clearance table 2. Millimetre drawings use the reference's 2 and 1. Descriptions use their table's decimals.
15. **Description wording:** plain words rather than drawing symbols: `Ø6.6 THRU`, `Ø6.6 x 12.0 DP`, `C'BORE Ø11.0 x 6.4 DP`, `CSK Ø12.4 x 90°`, `SPOTFACE Ø14.0 x 1.0 DP`; a tapped hole's second line is the thread and its depth (`M6x1 x 10.0 DP`, or `M6x1 THRU`). The seat line comes after the hole line. Spike 160 checks whether Inventor's own symbols (counterbore, countersink, depth) can go in a table cell; if they can, Aidan may prefer them.
16. **Holes the table leaves out:** a hole whose axis is not along the view direction (seen from the side) is not listed. The wizard says how many were left out.
17. **More than one ordinate origin on a view:** the wizard stops and says the view has several origins, rather than picking one.
