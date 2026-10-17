# 081: Spike: AWB Hole through the Inventor API (Windows)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
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

Run on Aidan's PC, 2026-10-17, Inventor 2026.2. Two tools: a throwaway console app (scratchpad `Spike081`) driving its own invisible Inventor on a scratch block (questions 1, 4, 5, 6), and a temporary spike command in the add-in on branch `spike/081-in-inventor`, never merged (questions 2, 3, 7), tried by Aidan with the results read from `awbaddin.log`.

### 1. Creating each hole type and termination

- **All types, seats, terminations and bottoms work** through `HoleFeatures`: `AddDrilledByThroughAllExtent`, `AddDrilledByDistanceExtent2`, `AddDrilledByToFaceExtent2`, `AddCBoreByThroughAllExtent`, `AddCBoreByDistanceExtent2`, `AddCSinkByThroughAllExtent`, and the matching `...ToFaceExtent2` methods. The `...2` methods take a `HoleDrillPointTypeEnum`: `kAngledDrillPointType` with `"118 deg"` or `"90 deg"`, and **`kFlatDrillPointType` gives a flat bottom**. Values are centimetres or unit strings (`"6 mm"`).
- **Direction:** for a sketch whose normal points out of the material, **`kPositiveExtentDirection` goes into the material**; negative gives a hole that cuts nothing (health `kDriverLostHealth`, no faces, no error). The add-in must check `HealthStatus` and `Faces.Count` after creating a hole.
- **Placement:** `CreateSketchPlacementDefinition(points)` with sketch points, several centres in one feature. `CreatePointPlacementDefinition` refuses a transient point (E_INVALIDARG) and accepts a work point. Sketch points are the way.
- **Editing a hole afterwards works without deleting it**: `HoleDiameter.Expression`, `SetDistanceExtent2`, `SetThroughAllExtent`, `SetToFaceExtent2`, `SetCBore`, `SetCSink`, `SetDrilled`, and `HoleCenterPoints =` a new collection. Each change plus `Document.Update` takes 3 to 18 ms on the scratch block.
- **Tapped holes:** `CreateTapInfo(rightHanded, "ISO Metric profile", "M6x1", "6H", fullDepth, threadDepth)`; imperial `"ANSI Unified Screw Threads"`, `"1/4-20 UNC"`, `"2B"`. The designation must be Inventor's: `M6` alone fails (use `M6x1`), and number sizes are written **without `#`** (`10-32 UNF`, `4-40 UNC`), so the workbook's `#10-32 UNF` needs mapping. Thread depth and drill depth are editable after creation (`TapInfo.ThreadDepth.Expression`, `SetDistanceExtent2`).
- **Our own tap drill:** a hole made with `CreateTapInfo` models the thread's **minor diameter** (4.917 mm for M6x1) whatever `TapDrillDiameter` says; setting `TapDrillDiameter` changes only the label. **The hidden `HoleFeatures._CreateTapInfo(..., ModelDiameterFromThreadEnum.kThreadTapDrillDiameter, ..., TapDrillDiameter)` models exactly our tap drill** (5.2 mm measured), the value in the thread table's units (mm for metric, inches for imperial; a number, not a unit string). Switching an existing tapped hole to that mode fails, so a tapped hole is created that way from the start. It is an underscore (hidden) member, so it could change in a later Inventor.
- **One transaction, one Undo** covers the hole, its tolerance, its colour and its attributes (`TransactionManager.StartTransaction` on the part, `End`; one `UndoTransaction` removes all of it). Aidan confirmed Ctrl+Z in Inventor.

### 2. Live preview

- **A real hole feature inside an open transaction**, edited as options change, with `Document.Update`: **30 to 60 ms per change** in Inventor (about 56 ms seen by Aidan); type and sliders update in real time. Cancel aborts the transaction and leaves nothing; OK ends it as one Undo step. The window stays modeless meanwhile.
- **Client graphics** (`ClientGraphicsCollection.Add`, `TransientBRep.CreateSolidCylinderCone`, `AddSurfaceGraphics`): about 17 to 27 ms, but only an approximation (no seats, threads or drill points without drawing each one).
- **Recommendation: the real feature** is the preview (exact for every type); client graphics are not needed.

### 3. Replacing Inventor's Hole

- Inventor's Hole is command **`PartHoleCmd`**, default shortcut **H** (`DrawingFillHatchSketchRegionCmd` also uses H, in drawings only). On the Part ribbon it is a large button in **tab `id_TabModel`, panel `id_PanelP_ModelModify`**; it also sits small on the Sheet Metal (`id_PanelP_SheetMetalModify`) and Flat Pattern (`id_PanelP_FlatPatternModify`) tabs.
- `CommandControls.AddButton(def, true, true, "PartHoleCmd", true)` puts our button where Inventor's is; `CommandControl.Visible = false` hides Inventor's. **Aidan: AWB Hole replaces Hole on 3D Model** (A).
- `ControlDefinition.OverrideShortcut = "H"` on ours and `""` on `PartHoleCmd` gives H to AWB Hole. **It persists across a restart** (E), and both can be switched while Inventor runs (D).
- **Bug found:** giving Hole back by setting `PartHoleCmd.OverrideShortcut = ""` left Inventor's Hole with an empty override, so **H did nothing** while both buttons showed. Fix: whenever Inventor's Hole is restored, give it its default back (`OverrideShortcut = DefaultShortcut`) and clear ours. Round 2 checks it.
- `UserInterfaceEvents.OnResetShortcuts` exists, so a user resetting shortcuts can be followed by re-applying the takeover.

### 4. A tolerance on the hole diameter

- `HoleDiameter.Tolerance.SetToFits(kLimitsFitsStackedTolerance, "H7", "")`: **Inventor works out the ISO 286 limits itself** (6 mm: H7 +0.012/0, N7 -0.004/-0.016, P7 -0.008/-0.020, G7 +0.016/+0.004, F7 +0.022/+0.010). Core still needs ISO 286 limits to show them in the window and for the hole table (spec 04).
- `SetToDeviation(upper, lower)` applies Limits rows (cm, or unit strings): 1/4 in with +0.0005/0 in read back exactly.
- The model stays at the nominal size (`ModelValueType = kNominalValue`); `kUpperValue` and `kMedianValue` move it to the limit or the middle. Recommendation: keep nominal.
- Tolerances survive save and reopen.

### 5. Colouring hole faces

- An appearance made in the part: `PartDocument.Assets.Add(kAssetTypeAppearance, "Generic", name, displayName)`, then its `"generic_diffuse"` `ColorAssetValue.Value = TransientObjects.CreateColor(r, g, b)`.
- `Face.Appearance = asset` works and survives a resize, a rectangular pattern (the pattern's faces are coloured too), Inventor's own Edit Feature (Aidan, C), and save and reopen.
- **Aidan: the bottom of the hole was not coloured.** Round 1 coloured `SideFaces`; **`Faces` includes the drill point cone and a flat bottom**, so the colour goes on `Faces`. Round 2 checks it.

### 6. Data on the feature

- `HoleFeature.AttributeSets.Add("AWB_Hole", true)` with string, integer and double attributes; they survive save and reopen and edits. `AttributeManager.FindObjects("AWB_Hole")` finds AWB holes in a few ms. A rectangular pattern does not copy them to its elements.

### 7. Edit AWB Hole and Inventor's edits

- **Inventor's Edit Feature on a hole is command `PartNGxHoleEditCtxCmd`.** Aidan wants no separate menu item: Edit Feature and double-click open the AWB Hole window for holes.
- **Double-click works**: `UserInputEvents.OnDoubleClick` with `kEventHandled` opens our window instead of Inventor's (C). In the browser it reports the hole; in the graphics window it reports a **Face**, mapped to its hole through `Face.CreatedByFeature`.
- A right-click raises `OnLinearMarkingMenu` and `OnRadialMarkingMenu`; adding our own item there worked (round 1's "Edit AWB Hole", which Aidan does not want).
- **Edits by Inventor's command:** `ModelingEvents.OnFeatureChange` fires before and after every change of a hole, from any source, with the feature; `OnActivateCommand` reports `PartNGxHoleEditCtxCmd`. Inventor's edit kept the colour, so a re-apply offer is needed only when the fit or tolerance no longer matches what is stored.

### Rounds 2 to 6 (with Aidan, in Inventor)

- **Inventor-style centre selection works** (round 2). When the window opens with nothing selected, the newest visible sketch with hole-centre points (`SketchPoint.HoleCenter`) is taken, or the sketch or points the user selected, and all its centres are picked. `CommandManager.CreateInteractionEvents` with `SelectEvents.AddSelectionFilter(kSketchPointFilter)`, `SingleSelectEnabled = true` and `OnSelect` runs while the modeless window is open; each click toggles a point without Ctrl (then `ResetSelections`), a `HighlightSet` (`SetColor(30, 120, 255)`) shows the centres in blue, and the preview follows. Creating the preview hole consumes and hides the sketch, so the window sets `PlanarSketch.Visible = true` while it is open and puts it back on close; the points stay clickable.
- **Editing picks centres too** (round 4): the edit window reads the hole's `HoleCenterPoints`, shows their sketch while open, and lets points of that sketch be added or removed.
- **The colour covers the bottom** (round 2): colouring `HoleFeature.Faces` includes the drill point cone and a flat bottom.
- **Edit Feature opens AWB Hole, by every route** (rounds 2 to 4):
  - Browser right-click: `OnLinearMarkingMenu` lists `PartNGxHoleEditCtxCmd`; `menu.AddButton(ourDef, false, true, "PartNGxHoleEditCtxCmd", true)` then `menu["PartNGxHoleEditCtxCmd"].Delete()` puts our "Edit Feature" in its place. The radial menu does not carry it.
  - Viewport left-click mini toolbar: `OnContextualMiniToolbar` gives the commands as a `NameValueMap` (`PartNGxHoleEditCtxCmd | SketchEditSketchCtxCmd | ...`); `Insert(ourName, value, index, true)` and `Remove("PartNGxHoleEditCtxCmd")` swap ours in. **The mini toolbar shows icons only**, so our definition needs one; Inventor's own Edit Feature icon can be copied from `PartNGxHoleEditCtxCmd.StandardIcon` / `LargeIcon`.
  - Double-click on a hole, in the browser or on a face: `OnDoubleClick` with `kEventHandled`.
  - Backstop: if `PartNGxHoleEditCtxCmd` starts anyway (`OnActivateCommand`), `CommandManager.StopActiveCommand()` and open ours.
- **The window must open after Inventor's event has returned** (round 3). Opened inside the right-click command or the double-click event, the window showed but took no input ("stuck"); opened through `Dispatcher.BeginInvoke` it works.
- **Editing reads the hole first** (round 5). Aidan found that opening the edit window turned a counterbore into a plain hole and dropped the dowel: the spike previewed its default type before reading the hole. The edit window must load every setting from the hole and its stored AWB data before the first preview, and keep a seat unless the user changes the type.
- **Unticking Dowel removes everything dowel** (round 5, Aidan): `Tolerance.SetToDefault()`, each face's `AppearanceSourceType = kPartAppearance` (the red goes; the face reads back `kFeatureAppearance`, "Default"), and the `AWB_Hole` attribute set deleted. One Undo brings the dowel back.
- **A refused change must not break the session** (round 6). With a diameter larger than the counterbore Inventor refused the change (E_INVALIDARG), and from then on every change failed, even valid ones, until Undo. Running **each preview change in its own inner transaction** (nested inside the window's transaction) and aborting only that step on failure keeps the hole at its last good state; later changes, type changes, Cancel and OK all work. Aidan confirmed.
- **H follows the restore** (round 2): with `PartHoleCmd.OverrideShortcut = DefaultShortcut` on restore and ours cleared, H opens Inventor's Hole while it is restored and ours while taken over.
- The spike window's plain look is for the spike only; the real window uses the add-in's shared light and dark look (Aidan).

## Recommendations

Walked through with Aidan on 2026-10-17; each is carried into spec 09.

1. **Create holes** with `HoleFeatures` from sketch points, direction `kPositiveExtentDirection` into the material, the `...Extent2` methods for drill points and flat bottoms. Check `HealthStatus` and `Faces.Count` after every create or change.
2. **Tapped holes** through `_CreateTapInfo` with `kThreadTapDrillDiameter` and the workbook's tap drill, in the thread table's units, so the model has our tap drill. Inventor's designations: `M6x1` (the add-in adds the pitch to the workbook's `M6`), `10-32 UNF` without `#`. Log a clear error if a later Inventor drops the hidden member.
3. **Preview with the real feature**: one outer transaction per window session; each change in an inner transaction, aborted alone if Inventor refuses it. OK ends the outer transaction (one Undo); Cancel aborts it. Core validates values before they reach Inventor (for example hole smaller than its counterbore, counterbore depth less than the hole depth), so refusals are rare.
4. **Selection**: as rounds 2 and 4, with the sketch shown while the window is open.
5. **Takeover**: our large button before `PartHoleCmd` in `id_TabModel` / `id_PanelP_ModelModify`, `PartHoleCmd` hidden, H moved to ours; restored by giving `PartHoleCmd` its default shortcut back and clearing ours. Re-apply on `OnResetShortcuts`. Sheet Metal and Flat Pattern keep Inventor's Hole (Default).
6. **Tolerance**: `SetToFits(kLimitsFitsStackedTolerance, code, "")` for ISO rows, `SetToDeviation(upper, lower)` for Limits rows, model at nominal. Core still computes ISO 286 limits to show them in the window and for the hole table.
7. **Colour**: one appearance per fit in the part (`Assets.Add`, `generic_diffuse` from the workbook's RGB), set on `HoleFeature.Faces`; removed with `AppearanceSourceType = kPartAppearance`.
8. **Stored data**: an `AWB_Hole` attribute set on the feature, with a version number; found with `AttributeManager.FindObjects`.
9. **Edit**: Edit Feature (browser menu and mini toolbar, with Inventor's icon), double-click and the backstop all open AWB Hole, after Inventor's event has returned. The window loads the hole fully before previewing. A hole made by Inventor's command opens too and is converted on OK (spec 09 question 5). A hole changed while Inventor's Hole was restored is offered a re-apply of fit, tolerance and colour when its stored data no longer matches.

## Verification

- **Ran:** the console app (four runs, logged in its scratch folder) and six rounds of the spike command in Inventor 2026.2 on Aidan's PC, built and deployed with `dotnet build InventorAddin.slnx`, 0 errors. Aidan tried every round.
- **Not compiled (changed under `src/InventorAddin`):** none merged. The spike code (`Spike081.cs` and a hook in `StandardAddInServer`) lives only on branch `spike/081-in-inventor` and is not merged.
- **Inventor API members confirmed (compile and run):** `HoleFeatures` (`AddDrilledByThroughAllExtent`, `AddDrilledByDistanceExtent2`, `AddDrilledByToFaceExtent2`, `AddCBoreByThroughAllExtent`, `AddCBoreByDistanceExtent2`, `AddCSinkByThroughAllExtent`, `CreateSketchPlacementDefinition`, `CreatePointPlacementDefinition`, `CreateTapInfo`, `_CreateTapInfo`); `HoleFeature` (`HoleDiameter`, `Depth`, `ExtentType`, `HoleType`, `Tapped`, `TapInfo`, `HoleCenterPoints` get and set, `SetDistanceExtent2`, `SetThroughAllExtent`, `SetCBore`, `SetCSink`, `SetDrilled`, `Faces`, `SideFaces`, `Appearance`, `AttributeSets`, `HealthStatus`, `Delete`); `HoleTapInfo` (`ThreadDesignation`, `ThreadType`, `Class`, `TapDrillDiameter`, `ThreadDepth`, `_DiameterInModel`); `Parameter.Tolerance` (`SetToFits`, `SetToDeviation`, `SetToDefault`, `Upper`, `Lower`, `ToleranceType`), `Parameter.ModelValueType`; `Assets.Add`, `ColorAssetValue.Value`, `TransientObjects.CreateColor`, `Face.Appearance`, `Face.AppearanceSourceType`, `Face.CreatedByFeature`; `AttributeSets.Add`, `AttributeSet.Add`, `AttributeSet.Delete`, `AttributeSets.NameIsUsed`, `AttributeManager.FindObjects`; `TransactionManager.StartTransaction` (nested), `Transaction.End`/`Abort`/`HasParentTransaction`, `UndoTransaction`; `CommandControls.AddButton` with a target, `CommandControl.Visible`, `ControlDefinition.OverrideShortcut`/`DefaultShortcut`/`IsShortcutOverridden`/`StandardIcon`/`LargeIcon`, `CommandManager.StopActiveCommand`/`ActiveCommand`/`CreateInteractionEvents`, `InteractionEvents` (`SelectEvents`, `Start`, `Stop`, `StatusBarText`, `OnTerminate`), `SelectEvents` (`AddSelectionFilter`, `SingleSelectEnabled`, `OnSelect`, `ResetSelections`), `PartDocument.CreateHighlightSet`, `HighlightSet.SetColor`/`AddItem`/`Clear`/`Delete`, `SketchPoint.HoleCenter`/`Geometry3d`, `PlanarSketch.Visible`; events `ModelingEvents.OnFeatureChange`, `UserInputEvents.OnActivateCommand`/`OnLinearMarkingMenu`/`OnRadialMarkingMenu`/`OnContextualMiniToolbar`/`OnDoubleClick`, `UserInterfaceEvents.OnResetShortcuts`, `ControlDefinitionEvents.OnCommandInputs` (hooked; it did not fire for Edit Feature).
- **Manual checklist for Inventor:** walked through with Aidan in six rounds (see Results).

## Follow-ups

- Spike branch `spike/081-in-inventor` is deleted once the real takeover task replaces it; until then do not merge it.
- The spike let OK through with no hole centres; the real window must not.
