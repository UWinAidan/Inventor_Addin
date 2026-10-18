# 09: AWB Hole (hole generator for part files)

M4 part 1. Our own hole command for part files. It creates real Inventor hole features with options taken from a workbook Aidan edits, applies fit tolerances to dowel and precision holes, and colours those holes by fit. The hole table wizard for drawings (spec 04) is M4 part 2, planned later.

Decided by Aidan on 2026-10-17 unless marked Default. How each part is done in Inventor was settled by spike 081 (`docs/tasks/081-spike-awb-hole.md`, Results and Recommendations), walked through with Aidan.

## The command

- **AWB Hole** creates real Inventor hole features (`HoleFeature`), editable afterwards like any hole. The command works in part files.
- **It works in a part file and in a part edited in place inside an assembly** (Aidan, 2026-10-18; spike 103, task 105).
- **It replaces Inventor's Hole** on the 3D Model tab of the part environment: the AWB Hole button sits where Inventor's Hole button is (large, before `PartHoleCmd` in the Modify panel), Inventor's Hole button is hidden, and the **H** shortcut opens AWB Hole. The takeover survives a restart and is re-applied if the user resets shortcuts. The Sheet Metal and Flat Pattern tabs keep Inventor's Hole (Default).
- **A Settings switch, "Use Inventor's Hole command", brings Inventor's Hole back**: its button and its H shortcut. **H always opens whichever Hole command is showing** (Aidan, spike round 2). The switch takes effect when Settings is saved (Default; the spike showed the change works while Inventor runs). AWB Hole also has a button on the AWB Addin tab, so it stays available either way (Default).
- **Editing opens AWB Hole.** Inventor's own **Edit Feature** on a hole, from the browser's right-click menu and from the viewport's left-click mini toolbar, and **double-click** on a hole, open the AWB Hole window for that hole; there is no separate menu item (Aidan). Inventor's Edit Feature entry is replaced by ours with Inventor's icon. If Inventor's own hole edit starts by some other route, the add-in stops it and opens AWB Hole. A hole made by Inventor's command opens too and becomes an AWB hole on OK (Aidan, question 5).
- **The AWB settings are stored on the hole feature** (an `AWB_Hole` attribute set with a version number).
- **A hole changed while Inventor's Hole was restored** (switch on): when its stored AWB data no longer matches the hole, the add-in offers to re-apply the AWB fit, tolerance and colour, when the hole is next opened in AWB Hole (Default). Inventor's own edit keeps the colour.

## The window

It follows Inventor's Hole panel, with a **Metric or Imperial** selector at the top. Fields, in order:

| Field | Contents |
|---|---|
| Units | Metric or Imperial. Starts on the workbook's Settings "Default units" |
| Type | Clearance or Tapped (Default: Inventor's Simple is not listed, since every size comes from the workbook; see "Types and seats") |
| Seat | As Inventor's: None, Counterbore, Countersink, for every type and standard (Aidan, 2026-10-18; see "Types and seats") |
| Standard | AWB Clearance, AWB Tapped, AWB Dowel, for the units chosen at the top (Aidan, question 2). Clearance lists AWB Clearance and AWB Dowel; Tapped lists AWB Tapped |
| Fastener type | The Screw values of the seat's tab (Counterbore or Countersink), for example SHCS, FHCS, in workbook order (Aidan, question 2); shown only for a seated hole |
| Size | The rows of the matching tab for the units, in workbook order, `Show = N` rows left out |
| Fit | Clearance: Close, Normal, Loose. Dowel and precision: Slip, Normal, Press |
| Tolerance | Dowel and precision only: the Precision Tol rows for the units and fit; the row with Default = Y is preselected. Each entry shows the tolerance at the chosen size, for example `H7  +0.012 / 0`, with the row's name as secondary text (Aidan, 2026-10-18) |
| Thread depth, drill depth | Blind tapped holes only: defaults from the workbook, editable per hole. Like Depth, they take Inventor parameters and expressions with units |
| Termination | Distance, Through All (Default). To is left out until the end face can be picked; an edited hole that already ends at a face shows To with a note (task 090) |
| Depth | Blind clearance and dowel holes, in the window's units. Starts at the last depth used, or the Settings tab's multiplier x D (starter 2) the first time (Aidan, question 7). Takes Inventor parameters and expressions with units (`d0`, `1.5*d0+1`, `12 mm`), and the hole stays parametric (Aidan, 2026-10-18) |
| Drill point | Blind holes only: the Drill Point rows (118 deg, 90 deg, Flat); Default = Y preselected |
| Direction | Flip, as Inventor's |
| Centres | How many centres are picked; for an edited hole whose centres are not sketch points, says they cannot be picked (task 090) |

- **The window is modeless** and uses the add-in's shared light and dark look (Aidan). Aidan wants it closer to Inventor's Hole panel (2026-10-18): compact, icon buttons for Type, Seat, Termination and Drill point, and a live diagram of the hole with its dimensions. A mockup is approved by Aidan before it is built (tasks 102 and 104). It stays open while the user picks hole centres in the model.
- **Hole centres, as Inventor's Hole picks them** (Aidan, spike round 2): when the window opens, the sketch the user selected (or the sketch of the selected points), else the newest visible unconsumed sketch with hole-centre points (one no feature uses yet; Default, task 094), gives the centres, all of them picked. Clicking a point adds or removes it, without Ctrl. Picked centres are highlighted blue. The preview follows. There is no "use selected points" button. When editing, the hole's own centres are picked and points of its sketch can be added or removed. The sketch is shown while the window is open and put back afterwards. OK needs at least one centre. One feature holds all the centres (question 4).
- **Live 3D preview** while options change, the preview holes in translucent red, as Inventor's Hole shows them; on OK they take the part's colour, or the fit colour for a dowel (Aidan, 2026-10-18): the real hole feature is created and changed as options change (about 30 to 60 ms a change). Each change is applied on its own; if Inventor refuses one, the hole stays at its last good state and the window says why (Aidan, spike round 6). Core checks values before they reach Inventor so refusals are rare.
- **OK** keeps the hole as one Undo step; **Cancel** leaves the part as it was, for a new hole and for an edited one.
- **Editing reads the hole first**: type, seat, size, fit, tolerance, depths, termination, drill point and the dowel setting are loaded from the hole and its stored AWB data before the first preview, and a seat is kept unless the user changes the type (Aidan, spike round 5).

## Types and seats

Hole types and seats stay as Inventor's. A dowel or precision hole is the Clearance type with Standard AWB Dowel; there is no separate Type entry (Aidan, question 3). How the workbook's tabs map onto them:

| Type, Standard | Seat | Sizes from | Fit | Coloured |
|---|---|---|---|---|
| Clearance, AWB Clearance | None | Clearance | Close, Normal, Loose (the column used) | no |
| Clearance, AWB Clearance | Counterbore | Counterbore, through hole from Clearance at its "Hole fit" | from the row | no |
| Clearance, AWB Clearance | Countersink | Countersink, through hole from Clearance at its "Hole fit" | from the row | no |
| Tapped, AWB Tapped | None, Counterbore, Countersink | Tapped; a seat from the Counterbore or Countersink row of the same size (Aidan, question 6) | none | no |
| Clearance, AWB Dowel (dowel and precision) | None, Counterbore, Countersink | Dowel; a seat from the Counterbore or Countersink row of the same size (Default, question 9) | Slip, Normal, Press, with a Tolerance | **yes**, the seat too |

"The same size" (Default): the seat rows, in the same units, whose Size has a Clearance row with the same nominal diameter as the thread or dowel. `M6` and a 6 mm dowel use the `M6` rows, `#10-32 UNF` uses `#10`, `1/4-20 UNC` and a 1/4 in dowel use `1/4`.

## The workbook: `libraries/AWB_Holes.xlsx`

- **Every option in the window comes from it.** A starter copy is committed in `libraries/` and shipped beside the DLL, like the finish list. A Settings path points at Aidan's own copy in `B:\aidan\AWB_Design_Data`; blank uses the starter (Default, as for the material library and finish list).
- **Read it as it is.** Do not rename tabs or headers; the add-in finds data by tab name and header text. The values are starter values Aidan will edit.
- Tabs: How to edit (text only, not read), **Settings**, **Clearance** (Close, Normal, Loose), **Counterbore**, **Countersink**, **Tapped**, **Dowel**, **Precision Tol**, **Drill Point**. Each data tab has a title in row 1, a note in row 2, headers in row 4 and one row per entry below; the Settings tab has no note row, so its headers are in row 3. Metric rows are in mm, Imperial rows in inches. `Show = N` hides a row.
- Read without Excel (Default): Core reads the `.xlsx` package directly, using the value each cell last showed in Excel, so formulas need no recalculation.
- A missing tab, a missing header or a value that cannot be read is reported the way the finish list reports its problems, naming the tab, row and header. Rows with problems are left out; the rest are used.

## Tapped holes

- The standard tap drill from the Tapped tab, modelled at that diameter (spike 081: Inventor otherwise models the thread's minor diameter). The thread is named as Inventor's thread table names it: the pitch is added to a metric size (`M6` with pitch 1 is `M6x1`), and number sizes drop the `#` (`#10-32 UNF` is `10-32 UNF`).
- **Default thread depth 1.5 x D and drill depth thread depth + 0.5 x D**, the multipliers from the Settings tab. Both are editable per hole in the window.
- The Tapped tab's "Default thread depth" and "Default drill depth" columns are formulas over the same Settings values. Default: the add-in uses the value in the cell, so a depth Aidan types over a formula is honoured; it works the depth out from Settings when the cell is blank.

## Blind holes

The bottom comes from the Drill Point tab: 118 deg (default), 90 deg, or Flat. A new blind clearance or dowel hole starts at the last depth used with OK, for each units, kept across restarts (Default), or at the Settings tab's "Blind depth multiplier" x the nominal diameter (starter 2) the first time (Aidan, question 7).

## Dowel and precision holes

- **Fit** is Slip, Normal or Press. A **Tolerance** dropdown lists the Precision Tol rows for that fit and units.
- **ISO rows** (Type ISO): the limits are worked out per size from ISO 286 for the code (for example H7 at 6 mm). **Limits rows** (Type Limits): the Upper and Lower values, relative to the nominal size.
- **The tolerance is applied to the hole diameter** in the model, which stays at the nominal size (Default). ISO rows use Inventor's own fit tolerance (Inventor works out the limits); Limits rows set the upper and lower deviation.
- **The hole faces are coloured by fit**, colours from the Settings tab: **press red, normal blue, slip green** (RGB in the tab). **Every face of the hole is coloured, the bottom too** (drill point cone or flat bottom; Aidan). **Only dowel and precision holes are coloured.**
- **Changing a dowel or precision hole into another kind removes everything dowel**: the tolerance, the colour on every face, and the dowel data (Aidan, spike round 5). One Undo brings it back.
- ISO 286 limits are Core logic shared with the hole table wizard (spec 04).

## Settings

New rows, in a new collapsible section "Holes" (Default):

- **Hole data**: path to the workbook, Browse for `*.xlsx`, blank uses the starter. Checked on Save like the material library.
- **Use Inventor's Hole command**: off by default. On brings back Inventor's Hole button and H shortcut; off gives them to AWB Hole. Takes effect when Settings is saved (Default).

## Open questions

1. ~~**Placement.**~~ Answered 2026-10-17 (Aidan, spike 081): sketch points, picked as Inventor's Hole picks them (see "The window").
2. ~~**Standard and Fastener type.**~~ Answered 2026-10-17 (Aidan): Standard lists AWB Clearance, AWB Tapped and AWB Dowel, for the units chosen at the top; Fastener type comes from the workbook's Screw column (SHCS, FHCS).
3. ~~**Dowel and precision as a type.**~~ Answered 2026-10-17 (Aidan): no separate Type; a dowel is the Clearance type with Standard AWB Dowel.
4. ~~**Several holes at once.**~~ Answered 2026-10-17 (Aidan, spike 081): yes, one feature with all the picked centres.
5. ~~**Edit AWB Hole on a hole made by Inventor's command.**~~ Answered 2026-10-17 (Aidan): it opens in AWB Hole and becomes an AWB hole on OK.
6. ~~**Seat sizes for a tapped hole.**~~ Answered 2026-10-18 (Aidan): the row of the same size in the Counterbore and Countersink tabs (task 099).
7. ~~**Default depth for a blind hole.**~~ Answered 2026-10-18 (Aidan): the last depth used, or 2 x D the first time, 2 being a value on the workbook's Settings tab (task 097).
8. ~~**Removing a dowel's colour.**~~ Answered 2026-10-18 (Aidan): every face of the hole goes back to the part's colour (task 096).
9. **Seat sizes for a dowel hole.** Aidan allowed a counterbore or countersink on dowel holes (2026-10-18) and asked where their sizes come from. Default (proposed): as for tapped holes, the Counterbore or Countersink row of the same nominal size (a 6 mm dowel uses `M6`). The other choice: seat columns of their own on the Dowel tab (for example Cbore dia, Cbore depth, Csink dia), so a dowel's relief can differ from a screw's.
10. **AWB Hole in a part edited in place.** Aidan wants it (2026-10-18). How it is done is settled by spike 103 with Aidan.
11. **Default centres per point or per sketch.** Inventor's Hole works per point: it preselects only the hole centres no feature uses yet. AWB Hole works per sketch: with nothing selected it skips any sketch a feature already consumes (Default, task 094), so a sketch where a hole uses some points while others are free is skipped whole, and the user has to select it to use its free points. Does Aidan want the per-point behaviour (the newest visible sketch with unused hole centres, only those picked)?
