# 09: AWB Hole (hole generator for part files)

M4 part 1. Our own hole command for part files. It creates real Inventor hole features with options taken from a workbook Aidan edits, applies fit tolerances to dowel and precision holes, and colours those holes by fit. The hole table wizard for drawings (spec 04) is M4 part 2, planned later.

Decided by Aidan on 2026-10-17 unless marked Default. How each part is done in Inventor was settled by spike 081 (`docs/tasks/081-spike-awb-hole.md`, Results and Recommendations), walked through with Aidan.

## The command

- **AWB Hole** creates real Inventor hole features (`HoleFeature`), editable afterwards like any hole. The command works in part files.
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
| Seat | As Inventor's: None, Counterbore, Countersink |
| Standard | AWB Clearance, AWB Tapped, AWB Dowel, for the units chosen at the top (Aidan, question 2). Clearance lists AWB Clearance and AWB Dowel; Tapped lists AWB Tapped |
| Fastener type | The Screw values of the seat's tab (Counterbore or Countersink), for example SHCS, FHCS, in workbook order (Aidan, question 2); shown only for a seated hole |
| Size | The rows of the matching tab for the units, in workbook order, `Show = N` rows left out |
| Fit | Clearance: Close, Normal, Loose. Dowel and precision: Slip, Normal, Press |
| Tolerance | Dowel and precision only: the Precision Tol rows for the units and fit; the row with Default = Y is preselected |
| Thread depth, drill depth | Tapped only: defaults from the workbook, editable per hole |
| Termination | Distance, Through All, To (Default: Inventor's three) |
| Drill point | Blind holes only: the Drill Point rows (118 deg, 90 deg, Flat); Default = Y preselected |
| Direction | Flip, as Inventor's |

- **The window is modeless** and uses the add-in's shared light and dark look (Aidan). It stays open while the user picks hole centres in the model.
- **Hole centres, as Inventor's Hole picks them** (Aidan, spike round 2): when the window opens, the sketch the user selected (or the sketch of the selected points), else the newest visible sketch with hole-centre points, gives the centres, all of them picked. Clicking a point adds or removes it, without Ctrl. Picked centres are highlighted blue. The preview follows. There is no "use selected points" button. When editing, the hole's own centres are picked and points of its sketch can be added or removed. The sketch is shown while the window is open and put back afterwards. OK needs at least one centre. One feature holds all the centres (question 4).
- **Live 3D preview** while options change: the real hole feature is created and changed as options change (about 30 to 60 ms a change). Each change is applied on its own; if Inventor refuses one, the hole stays at its last good state and the window says why (Aidan, spike round 6). Core checks values before they reach Inventor so refusals are rare.
- **OK** keeps the hole as one Undo step; **Cancel** leaves the part as it was, for a new hole and for an edited one.
- **Editing reads the hole first**: type, seat, size, fit, tolerance, depths, termination, drill point and the dowel setting are loaded from the hole and its stored AWB data before the first preview, and a seat is kept unless the user changes the type (Aidan, spike round 5).

## Types and seats

Hole types and seats stay as Inventor's. A dowel or precision hole is the Clearance type with Standard AWB Dowel; there is no separate Type entry (Aidan, question 3). How the workbook's tabs map onto them:

| Type, Standard | Seat | Sizes from | Fit | Coloured |
|---|---|---|---|---|
| Clearance, AWB Clearance | None | Clearance | Close, Normal, Loose (the column used) | no |
| Clearance, AWB Clearance | Counterbore | Counterbore, through hole from Clearance at its "Hole fit" | from the row | no |
| Clearance, AWB Clearance | Countersink | Countersink, through hole from Clearance at its "Hole fit" | from the row | no |
| Tapped, AWB Tapped | None, Counterbore, Countersink | Tapped | none | no |
| Clearance, AWB Dowel (dowel and precision) | None | Dowel | Slip, Normal, Press, with a Tolerance | **yes** |

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

The bottom comes from the Drill Point tab: 118 deg (default), 90 deg, or Flat.

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
6. **Seat sizes for a tapped hole.** "Types and seats" lets a tapped hole have a counterbore or countersink, but the Tapped tab has no seat sizes, and the Counterbore and Countersink tabs are keyed by clearance size and screw (`M6`, `#10`, `SHCS`), not by thread (`M6`, `#10-32 UNF`). Where should a tapped hole's seat diameter, depth and angle come from? Until this is answered, Core refuses a seat on a tapped hole with a message (task 084).
7. **Default depth for a blind hole.** A blind clearance or dowel hole starts with no depth, and OK stays disabled until one is entered (Default, task 085). Should it start from a default, for example 2 x D or a value in the Settings tab?
