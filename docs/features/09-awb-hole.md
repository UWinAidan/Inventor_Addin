# 09: AWB Hole (hole generator for part files)

M4 part 1. Our own hole command for part files. It creates real Inventor hole features with options taken from a workbook Aidan edits, applies fit tolerances to dowel and precision holes, and colours those holes by fit. The hole table wizard for drawings (spec 04) is M4 part 2, planned later.

Decided by Aidan on 2026-10-17 unless marked Default. Items marked **(spike 081)** depend on how the Inventor API behaves and are settled by the spike before the rest is planned.

## The command

- **AWB Hole** creates real Inventor hole features (`HoleFeature`), editable afterwards like any hole.
- **It replaces Inventor's Hole** on the 3D Model tab of the part environment: the AWB Hole button sits where Inventor's Hole button is, Inventor's Hole button is hidden, and the **H** shortcut opens AWB Hole. **(spike 081)**
- **A Settings switch brings Inventor's Hole back**: its button and its H shortcut, with AWB Hole still available beside it (Default: AWB Hole stays on the AWB Addin tab either way).
- **Edit AWB Hole** reopens an AWB hole in the window with its AWB settings. The AWB settings are stored on the hole feature. **(spike 081: how data is stored on a feature; where the Edit command is offered, for example the hole's right-click menu)**
- **A hole edited with Inventor's own Hole command**: when the add-in finds that an AWB hole no longer matches what it stored, it offers to re-apply the AWB fit, tolerance and colour. **(spike 081: how the change is detected)**

## The window

It follows Inventor's Hole panel, with a **Metric or Imperial** selector at the top. Fields, in order:

| Field | Contents |
|---|---|
| Units | Metric or Imperial. Starts on the workbook's Settings "Default units" |
| Type | As Inventor's: Simple, Clearance, Tapped (see "Types and seats") |
| Seat | As Inventor's: None, Counterbore, Countersink |
| Standard | Default: the workbook for the chosen units (open question 2) |
| Fastener type | Default: the Screw column for a seated hole (SHCS, FHCS); not shown for other types (open question 2) |
| Size | The rows of the matching tab for the units, in workbook order, `Show = N` rows left out |
| Fit | Clearance: Close, Normal, Loose. Dowel and precision: Slip, Normal, Press |
| Tolerance | Dowel and precision only: the Precision Tol rows for the units and fit; the row with Default = Y is preselected |
| Thread depth, drill depth | Tapped only: defaults from the workbook, editable per hole |
| Termination | Distance, Through All, To (Default: Inventor's three) |
| Drill point | Blind holes only: the Drill Point rows (118 deg, 90 deg, Flat); Default = Y preselected |
| Direction | Flip, as Inventor's |

- **Live 3D preview** while options change is required. **(spike 081: the approach)**
- **Placement** of the holes (open question 1). **(spike 081)**

## Types and seats

Hole types and seats stay as Inventor's. How the workbook's tabs map onto them (Default, open question 3):

| Type | Seat | Sizes from | Fit | Coloured |
|---|---|---|---|---|
| Clearance | None | Clearance | Close, Normal, Loose (the column used) | no |
| Clearance | Counterbore | Counterbore, through hole from Clearance at its "Hole fit" | from the row | no |
| Clearance | Countersink | Countersink, through hole from Clearance at its "Hole fit" | from the row | no |
| Tapped | None, Counterbore, Countersink | Tapped | none | no |
| Simple (dowel and precision) | None | Dowel | Slip, Normal, Press, with a Tolerance | **yes** |

## The workbook: `libraries/AWB_Holes.xlsx`

- **Every option in the window comes from it.** A starter copy is committed in `libraries/` and shipped beside the DLL, like the finish list. A Settings path points at Aidan's own copy in `B:\aidan\AWB_Design_Data`; blank uses the starter (Default, as for the material library and finish list).
- **Read it as it is.** Do not rename tabs or headers; the add-in finds data by tab name and header text. The values are starter values Aidan will edit.
- Tabs: How to edit (text only, not read), **Settings**, **Clearance** (Close, Normal, Loose), **Counterbore**, **Countersink**, **Tapped**, **Dowel**, **Precision Tol**, **Drill Point**. Each data tab has a title in row 1, a note in row 2, headers in row 4 and one row per entry below. Metric rows are in mm, Imperial rows in inches. `Show = N` hides a row.
- Read without Excel (Default): Core reads the `.xlsx` package directly, using the value each cell last showed in Excel, so formulas need no recalculation.
- A missing tab, a missing header or a value that cannot be read is reported the way the finish list reports its problems, naming the tab, row and header. Rows with problems are left out; the rest are used.

## Tapped holes

- The standard tap drill from the Tapped tab.
- **Default thread depth 1.5 x D and drill depth thread depth + 0.5 x D**, the multipliers from the Settings tab. Both are editable per hole in the window.
- The Tapped tab's "Default thread depth" and "Default drill depth" columns are formulas over the same Settings values. Default: the add-in uses the value in the cell, so a depth Aidan types over a formula is honoured; it works the depth out from Settings when the cell is blank.

## Blind holes

The bottom comes from the Drill Point tab: 118 deg (default), 90 deg, or Flat. **(spike 081: a flat bottom through the API)**

## Dowel and precision holes

- **Fit** is Slip, Normal or Press. A **Tolerance** dropdown lists the Precision Tol rows for that fit and units.
- **ISO rows** (Type ISO): the limits are worked out per size from ISO 286 for the code (for example H7 at 6 mm). **Limits rows** (Type Limits): the Upper and Lower values, relative to the nominal size.
- **The tolerance is applied to the hole diameter** in the model. **(spike 081: applying a diameter tolerance)**
- **The hole faces are coloured by fit**, colours from the Settings tab: **press red, normal blue, slip green** (RGB in the tab). **Only dowel and precision holes are coloured.** **(spike 081: colouring faces)**
- ISO 286 limits are Core logic shared with the hole table wizard (spec 04).

## Settings

New rows, in a new collapsible section "Holes" (Default):

- **Hole data**: path to the workbook, Browse for `*.xlsx`, blank uses the starter. Checked on Save like the material library.
- **Use Inventor's Hole command**: off by default. On brings back Inventor's Hole button and H shortcut. Takes effect when Inventor next starts, like developer tools (Default, until spike 081 shows whether the ribbon can change while Inventor runs).

## Open questions

1. **Placement.** How are the hole centres given: Inventor's way (click on a face, with sketch points or references), sketch points selected before the command, or both? Default until answered: as Inventor's Hole command does it, settled by spike 081.
2. **Standard and Fastener type.** What do they list? Default: Standard is the workbook for the chosen units ("AWB Metric" or "AWB Imperial"); Fastener type is the Screw column for seated holes.
3. **Dowel and precision as a type.** Default: they are Inventor's Simple hole with the Dowel tab's sizes and the Slip, Normal, Press fit. Is a separate "Dowel" or "Precision" entry wanted in the Type list?
4. **Several holes at once.** One feature with several centres, as Inventor's Hole does? Default: yes.
5. **Edit AWB Hole on a hole made by Inventor's command.** Offer to convert it to an AWB hole, or refuse? Default: offer to convert.
