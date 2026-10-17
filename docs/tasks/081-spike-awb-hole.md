# 081: Spike: AWB Hole through the Inventor API (Windows)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** windows (local session on Aidan's PC, walked through with Aidan)
- **Parallel-safe with:** none

## Goal

Find, in Inventor 2026, how the add-in can do each thing AWB Hole needs, so the rest of M4 part 1 is planned on proven ground. Nothing here is product code: the results are written into this file and walked through with Aidan before tasks 082 onwards are planned.

## How to run it

Like spike 067: a throwaway C# console app in the scratchpad that drives its own Inventor through COM, for everything that does not need the add-in loaded (questions 1, 4, 5, 6, 7). The ribbon, the shortcut and the live preview (questions 2 and 3) need code running inside Inventor, so they use a temporary spike command added to the add-in on a scratch branch, built and deployed, then thrown away. Nothing from the spike is merged except this file.

Use a scratch part made by the spike (a block with a few faces and sketch points), never one of Aidan's models.

## Questions

### 1. Creating each hole type and termination

Through `PartComponentDefinition.Features.HoleFeatures` (or `HoleFeatures.Add` with a placement definition), for metric and imperial values, in Inventor's internal centimetres:

1. Simple (drilled) hole: termination Distance, Through All, To (a face). Blind bottoms: 118 deg point, 90 deg point, **flat**.
2. Clearance hole sized by our own diameter (not Inventor's fastener table), with seat None, Counterbore (diameter, depth) and Countersink (diameter, angle).
3. Tapped hole: thread from our Tapped row (designation, class, pitch or TPI) with our own tap drill diameter, thread depth and drill depth. Which thread type names Inventor's thread table accepts (`HoleTapInfo` or `TapInfo`), and whether our tap drill can differ from the table's.
4. Placement: by sketch points, and by a point on a face (Inventor's on-face placement), several centres in one feature. Flip direction.
5. Changing a created hole afterwards (diameter, depth, termination, seat) through the API, without deleting it.
6. One transaction, one Undo for the whole hole (feature, tolerance, colour, stored data).

### 2. Live preview while options change

The window must show the hole in 3D as options change. Try, and time on a part with a few hundred faces:

1. Create the real feature inside a transaction, change its definition as options change, and on Cancel abort the transaction. Is the viewport updated between changes while a WPF window is open?
2. Client graphics (`ClientGraphics` on the part) drawing cylinders and cones, deleted on close.
3. Whatever Inventor's own preview uses that the API exposes, if anything (for example interaction events with preview graphics).

Record which is smooth enough, how Undo history looks afterwards, and whether the window can stay modeless while the user picks points.

### 3. Replacing Inventor's Hole on the 3D Model tab

1. Find Inventor's Hole command and its ribbon control on the Part ribbon's 3D Model tab (internal names of the tab, panel and control).
2. Add our button to that built-in panel, placed where the Hole button is (before or after it), large like Inventor's.
3. Hide Inventor's Hole control, and show it again. Does Inventor remember either across restarts or after the add-in unloads? Is a ribbon reset by the user a problem?
4. The **H** shortcut: can the add-in give H to our command and take it from Inventor's Hole (`OverrideShortcut` or similar), and give it back? What happens to a user's own shortcut customisation?
5. Can this be switched while Inventor runs (for the Settings switch), or only at startup?

### 4. A tolerance on the hole diameter

1. Apply a tolerance to `HoleFeature.HoleDiameter` (a `Parameter`): an ISO fit code (for example H7) and explicit limits (upper and lower deviation). Which API calls (`Parameter.Tolerance`, `SetToFits`, `SetToDeviation`), and how the model value relates (nominal, or the mean of the limits).
2. Does a drawing's hole note or dimension show the tolerance?
3. Do the tolerance survive save, reopen, and an edit in Inventor's Hole dialog?

### 5. Colouring hole faces

1. Get the hole's faces (`HoleFeature.Faces`, or the feature's side faces only).
2. Give them an appearance in a given RGB colour: create or find an appearance asset in the part (for example duplicate a generic appearance and set its colour), then set it on the faces.
3. Does the colour survive a recompute, a change of the hole's size, a pattern of the hole, save and reopen? Is it one Undo with the hole?

### 6. Storing data on a feature

1. Store AWB settings on the hole feature (`AttributeSets` on `HoleFeature`): units, type, size key, fit, tolerance row, depths.
2. Do they survive save, reopen, an edit with Inventor's Hole command, and copy and paste or patterning of the feature?
3. Find AWB holes in a part by attribute quickly (`AttributeManager.FindObjects`).

### 7. Edit AWB Hole and holes edited by Inventor

1. Offering Edit AWB Hole on a hole: a right-click (context) menu entry in the browser and the graphics window, through the user interface events, and only for AWB holes.
2. Detecting that Inventor's own Hole command changed an AWB hole: an event when a feature is edited, or comparing the stored settings with the feature when the part is opened or the hole is selected. What is cheap and reliable?

## Results

To be filled in on Aidan's PC.

## Recommendations

To be written after the results, then walked through with Aidan. They decide the approach for each of: hole creation, preview, the ribbon and shortcut takeover, tolerance, colour, stored data, and Edit AWB Hole.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none merged (spike code is thrown away)
- **Inventor API members not confirmed:** list every member used, as spike 067 did
- **Manual checklist for Inventor:** walked through with Aidan in the session

## Follow-ups
