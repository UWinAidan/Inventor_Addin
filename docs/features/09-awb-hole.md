# 09: AWB Hole (hole generator for part files)

M4 part 1. Our own hole command for part files. It creates real Inventor hole features with options taken from a workbook Aidan edits, applies fit tolerances to dowel and precision holes, and colours those holes by fit. The hole table wizard for drawings (spec 04) is M4 part 2, planned later.

Decided by Aidan on 2026-10-17 unless marked Default. How each part is done in Inventor was settled by spike 081 (`docs/tasks/081-spike-awb-hole.md`, Results and Recommendations), walked through with Aidan. Aidan's changes after his first look at PR #29 (2026-10-24) are tasks 103, 105 and 107 to 119; how the new types, the spotface and the client graphics preview are done in Inventor is settled by spikes 107 and 108 with Aidan.

## The command

- **AWB Hole** creates real Inventor hole features (`HoleFeature`), editable afterwards like any hole. The command works in part files.
- **It works in a part file and in a part edited in place inside an assembly** (Aidan, 2026-10-18), for new holes and for Edit Feature and double-click. **This is the top priority** (Aidan, 2026-10-24), and every other AWB tool must work in place too (spike 103, tasks 105 and 109). In place, AWB Hole runs on the part being edited, exactly as in the part's own window: the hole, its transactions and the blue highlight are on the part, and a sketch point, sketch, hole or hole face picked in the assembly is taken as the part's own; one Undo in the assembly window removes the hole (spike 103; task 105).
- **Switching to another document ends the window as Cancel does** (Aidan, 2026-10-25; spike 103): in place, when the assembly is switched away from, and in a part's own window, when the part is. Return and closing the assembly end it too.
- **It replaces Inventor's Hole** on the 3D Model tab of the part environment: the AWB Hole button sits where Inventor's Hole button is (large, before `PartHoleCmd` in the Modify panel), Inventor's Hole button is hidden, and the **H** shortcut opens AWB Hole. The takeover survives a restart, is re-applied if the user resets shortcuts (Tools > Customize > Keyboard > Reset All; task 120), and comes back when the add-in is unloaded and loaded again (task 121). **The Sheet Metal tab is taken over the same way, under the same switch; the Flat Pattern tab keeps Inventor's Hole** (Aidan, 2026-10-25; task 128).
- The mini toolbar's Hole button on a sketch point opens AWB Hole too, while AWB Hole has taken over Hole (spike 103).
- **AWB Hole is a split button** on the 3D Model and Sheet Metal tabs (Aidan, 2026-10-25; task 129). Its dropdown has **Inventor Hole**, which runs Inventor's Hole once, and **Use Inventor's Hole by default**, a toggle that is the same setting as "Use Inventor's Hole command" in AWB Settings and takes effect at once.
- **A Settings switch, "Use Inventor's Hole command", brings Inventor's Hole back**: its button and its H shortcut. **H always opens whichever Hole command is showing** (Aidan, spike round 2). The switch takes effect when Settings is saved (Default; the spike showed the change works while Inventor runs). AWB Hole also has a button on the AWB Addin tab, so it stays available either way (Default).
- **Editing opens AWB Hole.** Inventor's own **Edit Feature** on a hole, from the browser's right-click menu and from the viewport's left-click mini toolbar, and **double-click** on a hole, open the AWB Hole window for that hole; there is no separate menu item (Aidan). Inventor's Edit Feature entry is replaced by ours with Inventor's icon. If Inventor's own hole edit starts by some other route, the add-in stops it and opens AWB Hole. A hole made by Inventor's command opens too and becomes an AWB hole on OK (Aidan, question 5).
- **The AWB settings are stored on the hole feature** (an `AWB_Hole` attribute set with a version number).
- **A hole changed while Inventor's Hole was restored** (switch on): when its stored AWB data no longer matches the hole, the add-in offers to re-apply the AWB fit, tolerance and colour, when the hole is next opened in AWB Hole (Default). Inventor's own edit keeps the colour.

## The window

It follows Inventor's Hole panel, with a **Metric or Imperial** selector on the header's line (Aidan, 2026-10-24). Fields, in order:

| Field | Contents |
|---|---|
| Units | Metric or Imperial, on the header's line beside the title (Aidan, 2026-10-24, question 15 c). Starts on the workbook's Settings "Default units" |
| Type | Clearance, Tapped, Simple, Taper Tapped (Aidan, 2026-10-24; see "Types and seats") |
| Seat | As Inventor's: None, Counterbore, Countersink, Spotface, for every type and standard (Aidan, 2026-10-18 and 2026-10-24; see "Types and seats") |
| Standard | AWB Clearance, AWB Tapped, AWB Dowel, AWB Taper Tapped, for the units chosen at the top (Aidan, question 2). Clearance lists AWB Clearance and AWB Dowel; Tapped lists AWB Tapped; Taper Tapped lists AWB Taper Tapped (Default). Not shown for Simple |
| Fastener type | The Screw values of the seat's tab (Counterbore or Countersink), for example SHCS, FHCS, in workbook order (Aidan, question 2); it picks the seat boxes' starting values, which stay editable (Aidan, 2026-10-25); shown only for a clearance or tapped hole with a counterbore or countersink, under Size (Aidan, 2026-10-23). Not shown for a dowel, Simple or Taper Tapped hole, or for a spotface (Default) |
| Seat sizes | Editable boxes for the seat: counterbore diameter and depth, countersink diameter and angle, spotface diameter and depth. **Shown for every seat on every type** (Aidan, 2026-10-25): a clearance or tapped hole starts them from the row for the Fastener type and size, a dowel from its Dowel row (Aidan, 2026-10-23), a Simple or Taper Tapped hole as "Seat boxes" says; see "Types and seats" (task 126). The countersink angle offers 60 and 90 deg as quick choices, for a centre-drill spot on any hole (Aidan, 2026-10-25; Default for the dropdown; task 127) |
| Size | The rows of the matching tab for the units, in workbook order, `Show = N` rows left out. Not shown for Simple |
| Diameter | Simple holes only: the hole diameter in the window's units (see "Simple holes") |
| Fit | Clearance: Close, Normal, Loose. Dowel and precision: Slip, Normal, Press |
| Toleranced | Clearance (AWB Clearance) and Simple holes only: a checkbox on one row, off by default; ticked, it shows the Tolerance row below it (Aidan, 2026-10-24; see "Toleranced clearance and Simple holes") |
| Tolerance | Dowels, and toleranced Clearance and Simple holes: the Precision Tol rows (for a dowel those of its fit and units); the row with Default = Y is preselected. Each entry shows the tolerance at the chosen size, for example `H7  +0.012 / 0`, with the row's name as secondary text (Aidan, 2026-10-18). Last in the list, **Custom** with Upper and Lower boxes (Aidan, 2026-10-24) |
| Full depth, Thread depth | Through All tapped holes only (Aidan, 2026-10-28, task 136): a Full depth checkbox like Inventor's, ticked by default, threads the whole hole. Unticked, a Thread depth box shows, starting at the default thread depth (1.5 x D, from Settings), measured from the bottom of the seat, and taking Inventor parameters and expressions with units like Depth |
| Thread depth, drill depth | Blind tapped and taper tapped holes only: defaults from the workbook, editable per hole, **measured from the bottom of the seat** when there is one (Aidan, 2026-10-24, question 12). Like Depth, they take Inventor parameters and expressions with units |
| Termination | Distance, Through All (Default). To is left out until the end face can be picked; an edited hole that already ends at a face shows To with a note (task 090) |
| Depth | Blind clearance, dowel and Simple holes, in the window's units, measured from the top face as Inventor's (Default, question 16). Starts at the last depth used, or the Settings tab's multiplier x D (starter 2) the first time (Aidan, question 7). Takes Inventor parameters and expressions with units (`d0`, `1.5*d0+1`, `12 mm`), and the hole stays parametric (Aidan, 2026-10-18) |
| Drill point | Blind holes only: the Drill Point rows (118 deg, 90 deg, 60 deg, Flat; 60 deg added by Aidan, 2026-10-25, task 131), each with its own icon; Default = Y preselected |
| Direction | Flip, as Inventor's |
| Centres | One line, for example "3 picked", the help in its tooltip, at the end of the depth and direction rows, with no section of its own (Aidan, 2026-10-24, question 15 b); for an edited hole whose centres are not sketch points, says they cannot be picked (task 090) |

- **The window is modeless** and uses the add-in's shared light and dark look (Aidan). Aidan wants it closer to Inventor's Hole panel (2026-10-18): compact, icon buttons for Type, Seat, Termination and Drill point, and a live diagram of the hole with its dimensions. The mockup `docs/ui-mockups/awb-hole-window.png` was approved by Aidan on 2026-10-23 with changes (task 102; built by task 104): 360 wide; a depth typed as an expression shows its value beside the box; every icon is clearly different from the others (Distance from 118 deg, 118 deg from 90 deg, Tapped shows threads). It stays open while the user picks hole centres in the model.
- **The common holes fit without scrolling at 150 percent scale on a 1080p display** (Aidan, 2026-10-24, question 15): a working area about 690 tall. The diagram stays (Aidan likes it), smaller, and collapsible with its state remembered; rows and sections sit closer together in this window than spec 08's spacing; the centres are one line and Units is on the header's line. The body still scrolls as the fallback for a tall hole, and the footer with OK and Cancel stays visible. Task 118.
- **Hole centres, as Inventor's Hole picks them** (Aidan, spike round 2): when the window opens, the sketch the user selected (or the sketch of the selected points) gives the centres, all of them picked. With nothing selected, the centres are picked **per point, like Inventor's Hole** (Aidan, 2026-10-24, question 11): the newest visible sketch that has hole-centre points no feature uses yet, and only those points are picked. Clicking a point adds or removes it, without Ctrl. Picked centres are highlighted blue. The preview follows. There is no "use selected points" button. When editing, the hole's own centres are picked and points of its sketch can be added or removed. The sketch is shown while the window is open and put back afterwards. OK needs at least one centre. One feature holds all the centres (question 4).
- **Live 3D preview** while options change, as Inventor's Hole shows it (Aidan, 2026-10-24, question 13): the real hole feature is created and changed as options change (about 30 to 60 ms a change), and a **faint, see-through red copy of the hole volume is drawn over the model with client graphics**, end caps included, so a through hole looks red seen straight down the bore. **No face is painted for the preview.** On OK the client graphics go and the hole keeps the part's colour, or takes the fit colour for a dowel or toleranced hole. Each change is applied on its own; if Inventor refuses one, the hole stays at its last good state and the window says why (Aidan, spike round 6). Core checks values before they reach Inventor so refusals are rare. Spike 107, task 110.
- **Text boxes update the preview as you type** (Aidan, 2026-10-25; task 130), about 300 ms after the last keystroke, without leaving the box. While the text is incomplete or invalid, the last good preview stays and the message shows under the box. Every text box in the window works this way.
- **A typed value is kept when the size, type, standard or units change** (Aidan, 2026-10-25; task 124): a number or expression the user typed stays; only values the user has not typed follow the table. This overrides task 100's Default that a size change turns an expression into a number.
- **The diagram stays** (Aidan, 2026-10-25: he loves it).
- **OK** keeps the hole as one Undo step; **Cancel** leaves the part as it was, for a new hole and for an edited one.
- **Editing reads the hole first**: type, seat, size, fit, tolerance, depths, termination, drill point and the dowel setting are loaded from the hole and its stored AWB data before the first preview, and a seat is kept unless the user changes the type (Aidan, spike round 5). **The seat is kept when the type, standard or size changes, as long as the new choice can take it**, and comes back when the user changes back (Aidan, 2026-10-25; task 123).

## Types and seats

Hole types and seats stay as Inventor's: Clearance, Tapped, **Simple** and **Taper Tapped**, each with None, Counterbore, Countersink or **Spotface** (Aidan, 2026-10-24). **Every seat works on every type**, tapped and taper tapped included, and **its dimensions are editable in the window on every type** (Aidan, 2026-10-25; task 126); the rows below give the starting values. A dowel or precision hole is the Clearance type with Standard AWB Dowel; there is no separate Type entry (Aidan, question 3). How the workbook's tabs map onto them:

| Type, Standard | Seat | Sizes from | Fit | Coloured |
|---|---|---|---|---|
| Clearance, AWB Clearance | None | Clearance | Close, Normal, Loose (the column used) | only when toleranced |
| Clearance, AWB Clearance | Counterbore | Counterbore, through hole from Clearance at its "Hole fit" | from the row | only when toleranced |
| Clearance, AWB Clearance | Countersink | Countersink, through hole from Clearance at its "Hole fit" | from the row | only when toleranced |
| Clearance, AWB Clearance | Spotface | Clearance at the chosen fit; the spotface from the Spotface row of the same size, editable | Close, Normal, Loose | only when toleranced |
| Tapped, AWB Tapped | any | Tapped; a counterbore or countersink from the row of the same size (Aidan, question 6), a spotface from the Spotface row of the same size, editable | none | no |
| Taper Tapped, AWB Taper Tapped | any | Taper Tapped; a seat sized in the window (Default, question 19) | none | no |
| Simple | any | the Diameter typed; a seat sized in the window (Default, question 19) | none | only when toleranced |
| Clearance, AWB Dowel (dowel and precision) | any | Dowel; a counterbore or countersink sized in the window, starting from the Dowel row's seat columns (Aidan, question 9), a spotface from the Spotface row of the same size | Slip, Normal, Press, with a Tolerance | **yes**, the seat too |

"The same size" (Default): the seat rows, in the same units, whose Size has a Clearance row with the same nominal diameter as the thread or dowel. `M6` and a 6 mm dowel use the `M6` rows, `#10-32 UNF` uses `#10`, `1/4-20 UNC` and a 1/4 in dowel use `1/4`.

**Seat boxes** (Default, question 19): a spotface's diameter and depth start from the Spotface row of the same size, then the box can be changed; where there is no such row (Simple, Taper Tapped, or no row of that size) they start from the last values used with OK, else empty and OK waits. A seat on a Simple or Taper Tapped hole follows no fastener standard, like a dowel's: its boxes start from the last values used with OK (per units and seat for Simple, per units, size and seat for Taper Tapped), kept across restarts, else empty. Depths take expressions, diameters and angles take numbers, as for dowel seats.

## Simple holes

- **Just a diameter, no standard** (Aidan, 2026-10-24): no Standard, Size, Fastener type or Fit. A Diameter box in the window's units, taking Inventor parameters and expressions like Depth (Default). It starts at the last diameter used with OK for the units, kept across restarts, else empty and OK waits (Default, question 19).
- Blind Simple holes take Depth and Drill point like a clearance hole; a first Depth is the multiplier x the Diameter.
- A Simple hole can be toleranced (see below).

## Taper tapped holes

- **Tapered pipe threads, for example NPT and BSPT** (Aidan, 2026-10-24), from a new workbook tab, **Taper Tapped**. The tap drill from the tab, modelled at that diameter, with the thread showing in the model as Inventor's tapered tapped holes show it (spike 108).
- Thread and drill depths as for tapped holes: from the row's default columns, editable, measured from the bottom of the seat.
- No tolerance (thread class is separate).

## The workbook: `libraries/AWB_Holes.xlsx`

- **Every option in the window comes from it**, except a Simple hole's diameter. A starter copy is committed in `libraries/` and shipped beside the DLL, like the finish list. A Settings path points at Aidan's own copy in `B:\aidan\AWB_Design_Data`; blank uses the starter (Default, as for the material library and finish list).
- **Read it as it is.** Do not rename tabs or headers; the add-in finds data by tab name and header text. The values are starter values Aidan will edit.
- Tabs: How to edit (text only, not read), **Settings**, **Clearance** (Close, Normal, Loose), **Counterbore**, **Countersink**, **Spotface**, **Tapped**, **Taper Tapped**, **Dowel**, **Precision Tol**, **Drill Point**. Each data tab has a title in row 1, a note in row 2, headers in row 4 and one row per entry below; the Settings tab has no note row, so its headers are in row 3. Metric rows are in mm, Imperial rows in inches. `Show = N` hides a row.
- **Spotface** (Default): Units, Size, Spotface dia, Spotface depth, Show, keyed by Size as the Counterbore and Countersink tabs are. **Taper Tapped** (Default): Units, Size, Thread type, Tap drill, Default thread depth, Default drill depth, Show, laid out like the Tapped tab; the starter rows are invented NPT sizes (Imperial) and BSPT sizes (Metric). A workbook without these tabs (Aidan's own copy until he adds them) loads without them: Spotface boxes start empty, and Taper Tapped is listed with no sizes and a note naming the missing tab.
- Read without Excel (Default): Core reads the `.xlsx` package directly, using the value each cell last showed in Excel, so formulas need no recalculation.
- A missing tab, a missing header or a value that cannot be read is reported the way the finish list reports its problems, naming the tab, row and header. Rows with problems are left out; the rest are used.

## Tapped holes

- The standard tap drill from the Tapped tab, modelled at that diameter (spike 081: Inventor otherwise models the thread's minor diameter), with the thread showing in the model like an Inventor tapped hole (task 095). The thread is named as Inventor's thread table names it: the pitch is added to a metric size (`M6` with pitch 1 is `M6x1`), and number sizes drop the `#` (`#10-32 UNF` is `10-32 UNF`).
- **Default thread depth 1.5 x D and drill depth thread depth + 0.5 x D**, the multipliers from the Settings tab. Both are editable per hole in the window.
- **Measured from the bottom of the seat** (Aidan, 2026-10-24, question 12): with a counterbore or spotface, from the bottom of the seat; with a countersink, from where the cone meets the hole diameter (Default). The window and the diagram show the depths below the seat, and the add-in adds the seat's depth when it writes to Inventor, which measures from the placement plane. An edited hole is read back the same way. Task 116.
- **Full depth on a Through All tapped hole** (Aidan, 2026-10-28, task 136): a Full depth checkbox, ticked by default, threads the whole hole as now. Unticked, a Thread depth box shows (default 1.5 x D, takes expressions), so the hole is not tapped all the way through; the diagram shows the thread stopping at that depth. Defaults: each new hole starts ticked; an edited hole is read back from Inventor's full-depth setting; taper tapped holes are left as they are, since Aidan asked for tapped holes.
- The Tapped tab's "Default thread depth" and "Default drill depth" columns are formulas over the same Settings values. Default: the add-in uses the value in the cell, so a depth Aidan types over a formula is honoured; it works the depth out from Settings when the cell is blank.

## Blind holes

The bottom comes from the Drill Point tab: 118 deg (default), 90 deg, or Flat. A new blind clearance, dowel or Simple hole starts at the last depth used with OK, for each units, kept across restarts (Default), or at the Settings tab's "Blind depth multiplier" x the nominal diameter (starter 2) the first time (Aidan, question 7).

## Dowel and precision holes

- **Fit** is Slip, Normal or Press. A **Tolerance** dropdown lists the Precision Tol rows for that fit and units, then Custom (Default, question 18). **A fit has several levels** (Aidan, 2026-10-25), for example light, medium and heavy press, close and loose slip, and a couple of normal levels; the starter workbook has such rows, metric and imperial, with clear names, and the dropdown lists every one for the chosen fit (task 132).
- **ISO rows** (Type ISO): the limits are worked out per size from ISO 286 for the code (for example H7 at 6 mm). **Limits rows** (Type Limits): the Upper and Lower values, relative to the nominal size. **Custom**: the Upper and Lower typed in the window (see below).
- **The tolerance is applied to the hole diameter** in the model, which stays at the nominal size (Default). ISO rows use Inventor's own fit tolerance (Inventor works out the limits); Limits and Custom set the upper and lower deviation.
- **The hole faces are coloured by fit**, colours from the Settings tab: **press red, normal blue, slip green** (RGB in the tab). **Every face of the hole is coloured, the bottom too** (drill point cone or flat bottom; Aidan). **Only precision holes are coloured**: dowels, and toleranced Clearance and Simple holes (Aidan, 2026-10-24).
- **A colour the user put on a hole face by hand is kept** whenever the hole is changed, including when it is remade for a thread (Aidan, 2026-10-25; task 122). Only AWB fit colours are removed by the rules below.
- **Changing a dowel or precision hole into another kind removes everything dowel**: the tolerance, the colour on every face, and the dowel data (Aidan, spike round 5). One Undo brings it back.
- ISO 286 limits are Core logic shared with the hole table wizard (spec 04).
- **A seat on a dowel hole follows no fastener standard** (Aidan, 2026-10-23, question 9): a dowel's counterbore or countersink usually has a purpose of its own and needs its own dimensions. Fastener type is not shown; the window has editable boxes for the counterbore diameter and depth, or the countersink diameter and angle. They start from the Dowel tab's `Cbore dia`, `Cbore depth`, `Csink dia` and `Csink angle` columns for the row, then from the last values used with OK, kept per units and dowel size across restarts (Aidan, 2026-10-24, question 14). The counterbore depth takes Inventor parameters and expressions like the other depth boxes; the diameters and the angle take numbers (Default). A workbook without the columns, or a blank cell, falls back to the Counterbore or Countersink row of the same size, else the box starts empty and OK waits for a value (Default). An edited dowel hole opens on its own seat dimensions (Default). Task 106.

## Toleranced clearance and Simple holes

- **A "Toleranced" checkbox** on Clearance holes (Standard AWB Clearance) and Simple holes, off by default, on one row (Aidan, 2026-10-24). Ticked, the window opens a Tolerance row below it: **the same Tolerance dropdown dowels use, plus Custom**, with typed Upper and Lower limits. Not on Tapped or Taper Tapped holes: their thread class is separate.
- The list (Default, question 17): every Precision Tol row for the units, since a clearance hole has no Slip, Normal or Press fit; the Default = Y row preselected; each entry shows the tolerance at the hole's diameter as for dowels; Custom last. **Custom** (Default): Upper and Lower boxes in the window's units, numbers only, relative to the modelled diameter, Upper above Lower.
- The tolerance goes on the through hole's diameter (the Clearance size at its fit, or the Simple diameter), not on the seat (Default).
- **A toleranced hole is a precision hole** (Aidan, 2026-10-24): it takes the precision colour on every face, and the hole table (spec 04, M4 part 2) treats it as precision. Default (question 17): the colour of the Precision Tol row's fit (press red, normal blue, slip green); Custom takes the normal colour.
- Unticking Toleranced removes the tolerance and the colour, as changing a dowel into another kind does. The stored AWB data records the toleranced state, the row or the Custom limits, and Edit reads them back.
- Tasks 113 and 115.

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
9. ~~**Seat sizes for a dowel hole.**~~ Answered 2026-10-23 (Aidan): no fastener standard; editable seat boxes starting from new seat columns on the Dowel tab, then the last values used (see "Dowel and precision holes"; task 106).
10. ~~**AWB Hole in a part edited in place.**~~ Settled 2026-10-25 by spike 103 with Aidan: it runs on the part being edited, with transactions, highlight and client graphics on the part and proxies converted to the part's objects (task 105); every other command acts on the document being edited (task 109); the mini toolbar's Hole button is taken over too (task 133); switching to another document cancels the window (Aidan).
11. ~~**Default centres per point or per sketch.**~~ Answered 2026-10-24 (Aidan): per point, like Inventor's Hole (task 117).
12. ~~**Thread depth under a counterbore.**~~ Answered 2026-10-24 (Aidan): a seated hole's thread and drill depths are measured from the bottom of the seat (task 116).
13. ~~**The preview red is opaque.**~~ Answered 2026-10-24 (Aidan): a faint, see-through red copy of the hole volume drawn with client graphics, as Inventor's Hole does, end caps included; no face painting (spike 107, task 110).
14. ~~**Which "last values" a dowel seat remembers.**~~ Answered 2026-10-24 (Aidan): per units and dowel size, as built.
15. ~~**The window still scrolls for the common holes at 150 percent.**~~ Answered 2026-10-24 (Aidan): a smaller diagram, tighter row and section gaps in this window, the centres on one line (b) and Units on the header's line (c); scrolling stays as the fallback; the diagram stays (task 118).
16. **Depth of a seated blind clearance, dowel or Simple hole.** Question 12 moves the thread and drill depths below the seat. Default: a blind hole's Depth is still measured from the top face, as Inventor's. Should it also be measured from the bottom of the seat?
17. **The precision colour of a toleranced clearance or Simple hole.** Default: the fit colour of the Precision Tol row chosen (press red, normal blue, slip green), and normal blue for Custom; the list shows every Precision Tol row for the units. The other choice: one new "precision" colour on the workbook's Settings tab for every toleranced clearance and Simple hole.
18. **Custom tolerance on dowels.** Aidan asked for Custom on toleranced clearance and Simple holes, alongside "the same Tolerance dropdown dowels use". Default: Custom is in the dowel list too. Should dowels keep only the workbook rows?
19. **Starting values for a Simple hole and its seats, and for a Taper Tapped seat.** Default: the last values used with OK (Simple diameter per units; Simple seats per units and seat; Taper Tapped seats per units, size and seat), else empty and OK waits. A spotface takes the Spotface row of the same size where there is one. Would Aidan rather have workbook defaults for these (for example a Simple seat as a multiple of D)?
20. **A seat depth of 0.** Aidan (2026-10-25) asked to allow a seat depth of 0 if Inventor accepts one. Spike 108 question 7 checks it; task 127 builds the answer. Until then a seat depth must be above zero.
