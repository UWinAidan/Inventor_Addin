# 225: Spike: view orientation, dimension styles and the lb unit text

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (spike run 2026-11-23 against Inventor 2026 over COM)
- **Depends on:** 192
- **Needs:** **windows** (spike, parent with Aidan in Inventor; same session as spike 218)
- **Parallel-safe with:** any

## Goal

Settle the Inventor calls the chunk 2 fixes need before their tasks are built.

## Scope

- **Front view from a direction:** how to place a base view looking along any direction with a chosen "up" (for example `kArbitraryViewOrientation` with a `Camera`, or a custom view orientation): needed for sheet metal (look straight at the flat pattern's base face, longest edge horizontal), for the Auto front view (largest projected area, longest side horizontal) and for the Front view dropdown. Do projected views from such a base view still come out third angle?
- **Projected areas:** how to get a part's or assembly's extent along each axis or along a face normal (`OrientedMinimumRangeBox`, `RangeBox`, the flat pattern's base face and its edges).
- **Bend notes:** whether a flat pattern view gets bend notes (direction, angle, radius) automatically, and how to add them (`DrawingNotes.BendNotes.Add` or similar) for every bend centreline.
- **Dimension styles:** on a Create Print drawing, which `DimensionStyle` each kind of dimension uses (ordinate, linear, radial, angular, hole notes) and why only 2 styles got the period; why setting the decimal marker on the feature control frame style fails with E_FAIL.
- **Mass "lb":** whether the WEIGHT field can print "lb" instead of "lbmass" (unit string settings on the document or the standard, a precision or units attribute, a custom unit). If not, question 29's fallback (a Mass text property) is used.
- Record results in this file; no repo code changes.

## Results

**Method.** As in spikes 183 and 184: PowerShell 5.1 over COM, late bound.

- The scripts are in the session's job folder (`tmp\225\`, not in the repo). The drawings they saved are in
  `C:\AwbSpike\225\`.
- Every model was a copy:
  - the test part (26 x 2 x 12 cm, with 22 hole features)
  - the off-axis bar from spike 184 (10 x 2 x 1 cm, rotated 30°)
  - the sheet metal part from spike 183 round 3
  - the spike 184 assembly
  - the q28 lb test part
- Drawings were made with `Documents.Add(kDrawingDocumentObject, template, false)` from `AWB Metric.idw` and
  `AWB Imperial.idw`, then `Sheets.AddUsingSheetFormat`.
- The templates were not opened or changed, and their file times are unchanged.

**A dialog can stop the script.** The second `Documents.Add` from `AWB Metric.idw` in the session opened a modal "Style
Conflict" dialog, and the script waited until someone clicked OK. The dialog's text answers question 4.

- After that, each script set `Application.SilentOperation = true` while it ran and set it back to false at the end.
- The add-in should do the same around `Documents.Add` in Create Print, or fix the template so the conflict never happens
  (question 4).

### 1. Front view from any direction

**Placing the view.** `DrawingViews.AddBaseView(model, Point2d, scale, kArbitraryViewOrientation, style, Missing, camera,
Missing)` works.

- Make `camera` with `TransientObjects.CreateCamera()`:
  - `Target` is the model centre, as a `Point`.
  - `Eye` is `Target + n * 100`, where `n` is the unit direction from the model toward the viewer.
  - `UpVector` is set with `TransientGeometry.CreateUnitVector`.
- The placed view's `Camera` reads back the same eye direction and up vector.
- `Camera.ViewOrientationType` reads `kFrontViewOrientation` when the camera matches a standard view. Otherwise it reads
  `kArbitraryViewOrientation`.
- Checks:
  - n = +Z with up = +Y gave a 6.006 x 0.462 view, the same as `kFrontViewOrientation`.
  - n = +X with up = +Z gave 0.462 x 2.772.
  - A skewed n = (1,-1,1)/√3 also placed.

**Projected views stay third angle, whatever the base camera.** Each was placed with `AddProjectedView(base, Point2d,
kFromBaseDrawingViewStyle, Missing)`.

- A view placed right of the base looks from `right = up × n`. The dot product with that direction was 1.000 in every case:
  standard, +X with Z up, skewed, bar, sheet metal and assembly.
- A view placed above the base looks from `up`, and one placed left of it looks from `-right`.
- A projected view of an arbitrary base reports `kArbitraryViewOrientation`. If it happens to match a standard view, it
  reports that name, for example `kBottomViewOrientation`.

**Re-aiming an existing view also works.** Set `view.Camera.Eye`, `Target` and `UpVector`, then call
`view.Camera.Apply()`. It returned ok and the view changed to 0.462 x 2.772. Passing the camera to `AddBaseView` is
simpler.

**Off-axis bar (Auto for a part).**

- `ComponentDefinition.OrientedMinimumRangeBox`, with its directions sorted by `Length`, gave:
  - 10 cm along (0.866, -0.5, 0)
  - 2 cm along (0.5, 0.866, 0)
  - 1 cm along Z
- The view looked along the shortest direction (the largest projected area), with the middle direction as up and the
  longest horizontal. It came out 10 x 2 at 1 : 1. `kFrontViewOrientation` gave 9.66 x 6.73.
- The box centre is `CornerPoint + (DirectionOne + DirectionTwo + DirectionThree) / 2`.

**Assembly (Auto).**

- `AssemblyComponentDefinition.RangeBox` is 66.0 x 37.9 x 172.7 cm.
- `OrientedMinimumRangeBox` works on assemblies too: 62.3 x 35.9 x 172.7, rotated about Z, in 124 ms.
- `kFrontViewOrientation` gave the edge-on view Aidan saw (3.83 x 2.20 at 0.058).
- Looking along the smallest RangeBox extent (Y), with the longest extent (Z) horizontal, gave 10.02 x 3.83. Its projected
  top view was third angle.

**Sheet metal.**

- `FlatPattern.BaseFace` is a face of the **folded** body. Its `Parent` is the body of the `SheetMetalComponentDefinition`.
  The flat pattern's own faces are `TopFace` and `BottomFace`, and their body parent is `kFlatPatternObject`.
- `FlatPattern.GetSheetMetalEntity(BaseFace)` fails with E_FAIL. It is not needed.
- `SheetMetalComponentDefinition.ASideFace` was null on the test part (`ASideFaceStatus` = `kASideDefault`, no
  `ASideDefinitions`). Use `BaseFace` instead.
- Outward normal: `BaseFace.Geometry.Normal`, negated when `BaseFace.IsParamReversed` is true.
- Longest straight edge:
  - Walk `BaseFace.Edges` and keep the edges whose `Edge.GeometryType` is `kLineSegmentCurve`.
  - Compare `StartVertex.Point.DistanceTo(StopVertex.Point)`.
  - Filter on `GeometryType`, because **`Edge.CurveType` reads `kLineCurve`, not `kLineSegmentCurve`**.
- The camera uses n = the face normal, horizontal = the longest edge and up = `n × horizontal`. On the test part the view
  was 1.5 wide at 1 : 4 (the 6 cm edge), and its right and top projections were third angle.
- The sign of the edge direction is arbitrary, and flipping it turns the view 180°. The task must choose the sign, for
  example so that up points toward the model's +Y or +Z.
- The flat pattern view (`SheetMetalFoldedModel` = false, `kFrontViewOrientation`) came out `FlatPattern.Length` x
  `Width`, longest side horizontal, on this part.
- If a part's flat pattern comes out vertical, `FlatPattern.FlatPatternOrientations` (1, 'Default') and the `kFlatPivot*`
  view orientations exist. Neither was tried.

### 2. Extents

- **Along the model axes:** `ComponentDefinition.RangeBox` (`MinPoint` and `MaxPoint`, in cm), on parts and assemblies.
- **The true minimum box:** `OrientedMinimumRangeBox`. It has `CornerPoint` and `DirectionOne`, `DirectionTwo` and
  `DirectionThree`, and each direction's `Length` is one extent. It works on parts, sheet metal, `FlatPattern` and
  assemblies.
- **Projected area along an axis:** the product of the other two extents.
- **Along a face normal:** use the face's plane normal as in question 1. The extents seen in the view are its `Width` and
  `Height` divided by the scale.
- **Flat pattern:** `FlatPattern.Length` and `Width` give the flat size (21.06 x 6.0), and `FlatPattern.RangeBox` agrees.

### 3. Bend notes

- **They are not added automatically.** After the flat pattern view was placed, `Sheet.DrawingNotes.BendNotes.Count` was
  0.
- The view's curves carry `DrawingCurve.EdgeType`:
  - `kBendUpEdge` and `kBendDownEdge` for the bend centrelines
  - `kBendExtentEdge` for the bend extents
  - `kUnknownEdge` for the rest

  On the test part there were 2 up, 2 down, 8 extent and 4 unknown.
- `Sheet.DrawingNotes.BendNotes.Add(drawingCurve, Missing)` on each up and down centreline added 4 notes. Skip the extent
  edges.
- The notes read "UP 90.00° R3.18" or "DOWN 90.00° R3.18": direction, angle and inside radius.
  - They come from the default `FormattedBendNote`, `<BendDirection/> <BendAngle .../> R<BendRadius .../>`.
  - Their style is `ObjectDefaultsStyle.BendNoteStyle`.
- `FlatPattern.FlatBendResults` lists 8 results for the 4 bends, one per face (`IsOnBottomFace`). Each has
  `IsDirectionUp`, `Angle` (radians) and `InnerRadius` (cm). The notes do not need them.
- Walk a view's curves with `view.DrawingCurves(Missing)`, then `Count` and `Item(i)`, as in spike 184.

### 4. Dimension styles and the decimal comma

**Which style each kind uses.** On a fresh drawing from `AWB Metric.idw` the standard is 'AWB Metric' and the object
defaults are 'AWB Object Defaults'. Both are local styles.

- These object defaults members all name **'Default (ISO)'**:
  - Linear, Ordinate, OrdinateSet, Baseline, Chain, Angular, Radial, Diameter
  - HoleNote, HoleTag, ThreadNote, ChamferNote, BendNote, BendTag, PunchNote
- `LeaderTextStyle` names 'Default - Method 1b (ISO)'.
- `FeatureControlFrameStyle` names 'Feature Control Frame (ISO)'.

So the builder setting the period on only 2 styles was correct: those 2 names are all the dimension styles the standard
uses.

**What each kind printed.** All of these used 'Default (ISO)':

| Kind | Call | Printed |
|---|---|---|
| Linear | `GeneralDimensions.AddLinear` | "260,00" |
| Angular | `AddAngular(textPoint, lineIntent, lineIntent, Missing x6)` | "270,00°" |
| Radial | `AddRadius` | "R2,25" |
| Diameter | `AddDiameter` | "⌀4,50" |
| Ordinate set | `DrawingDimensions.OrdinateDimensionSets.Add(ObjectCollection of intents, Point2d, kHorizontalDimensionType, Missing, Missing)` | "0,00" and "25,00" |
| Hole note | `DrawingNotes.HoleThreadNotes.Add(Point2d, circleCurve, false, Missing)`, on the circle of a hole feature | "⌀4,50 THRU / ⌴ ⌀9,40" |

**Two placements failed.**

- `HoleThreadNotes.Add` with `Missing` for `LinearDiameterType` failed with E_FAIL on every circle. Passing `false`
  worked.
- A single `OrdinateDimensions.Add(intent, Point2d, type, Missing, Missing)` failed with "Value does not fall within the
  expected range". It failed with a line intent and with a point intent (`CreateGeometryIntent(curve,
  kCenterPointIntent)`). It probably needs an origin indicator first (`DrawingView.CreateOriginIndicator`, not tried). Use
  the ordinate set instead.

**Why the period is lost.** On the fresh drawing both dimension styles read `StyleLocation = kBothStyleLocation` and
`DecimalMarkerType = kCommaDecimalMarker`.

- The builder set them to a period in the template. But they have the same names as styles in the style library, and on
  `Documents.Add` Inventor replaces them with the library definitions.
- The dialog said so: "The following style definitions in template ...\AWB Metric.idw differ from the definitions in the
  style library; the style library definitions will be used. Dimension:Default - Method 1b (ISO), Dimension:Default
  (ISO)".
- The local AWB standard and object defaults survive, because the library has no styles with those names.

**The fix, proven on a test template outside the repo** (`C:\AwbSpike\225\tpl-fix.idw`, built from a drawing made from AWB
Metric):

- For each distinct dimension style the object defaults name, `style.Copy('AWB ' + name)` makes a local style
  (`kLocalStyleLocation`).
- On the copy, set `DecimalMarkerType` and `AlternateDecimalMarkerType` to `kPeriodDecimalMarker` (both returned ok).
- Assign the copy to every `ObjectDefaultsStyle` member that named the original.
- A drawing made from the saved template kept the AWB copies with a period. It printed:
  - "260.00", "270.00°", "R2.25" and "⌀4.50"
  - ordinates "0.00" and "25.00"
  - hole note "⌀4.50 THRU / ⌴ ⌀9.40"
  - bend notes "DOWN 90.00° R3.18"
- The renamed styles are not in the library, so there is no conflict for Inventor to resolve. (`SilentOperation` was on
  during this run.)

**The feature control frame E_FAIL.**

- `FeatureControlFrameStyle.DecimalMarkerType` and its alternate fail with E_FAIL for any value (period, comma or default).
  They fail on the library-linked style and on a local `Copy`.
- Other writes on the same style work, for example `Comments` and `LeadingZeroDisplay`.
- The cause is `LinearUnits = kDefaultDisplayLengthUnits`. After `copy.LinearUnits = kMillimeterLengthUnits`, setting the
  marker worked, read back as a period and survived the template round trip.
- The spike also set `AlternateUnitsDisplay = true` to test the alternate marker. **The builder must not do this**: frames
  would print dual units. Whether the alternate marker can be set without it was not tried.
- On the imperial template the ANSI frame style fails the same way. Use `kInchLengthUnits` there.

**The imperial template.** Every member uses 'Default (ANSI)', which already prints a period ("10.24", "R.09",
"DOWN 90° R3.18"). Only the frame style needs the change. Copying and renaming the ANSI styles too would stop a future
library change from reaching the template.

### 5. Mass "lb": not possible through the field

**What prints.** With the part's mass unit set to lb, the title block WEIGHT field prints "9.55 lbmass". The same
`<PhysicalProperty PhysicalPropertyID='72449' Precision='2'>` field in a general note prints the same.

**What was tried, with no change to the text:**

- These attributes on `<PhysicalProperty>`: `Units='lb'`, `UnitString`, `ShowUnits`, `ShowUnitString`, `ShowUnitsString`,
  `UnitTypeDisplay`, `PrimaryUnitTypeDisplay`, `DisplayUnits`, `UnitsVisible`, `MassUnits`, `Unit`, `UnitType`. Each was
  accepted and then dropped from the stored text.
- `GeneralNote.UnitAttributes.PrimaryUnitTypeDisplay`, set to false and to true.
- `GeneralNote.UnitAttributes.UseStandardsNotation = false`.

**Why it cannot work.**

- `UnitsOfMeasure.GetStringFromType(kLbMassMassUnits)` is "lbmass", and `GetStringFromValue(mass, 'lb')` also gives
  "9.550 lbmass". Inventor's name for the unit is fixed.
- The only mass units are kg, g, slug, lbmass and ounce.
- Reflection found two "show unit string" switches, `UnitsFormatting.ShowUnitString` and
  `CustomPropertyFormat.ShowUnitsString`. They belong to table columns and exported parameters, not to title block fields.

So question 29's fallback stands: the add-in writes a "Mass" text property and the template prints it (task 222).

**New for task 222: saving the unit.** On a part opened invisibly:

- `UnitsOfMeasure.MassUnits = kLbMassMassUnits`, then `Dirty = true` and `Save()`: the file time changed, but on reopen
  the unit read **kg**.
- The same change plus a custom property write, then `Save()`: the unit read **lb** on reopen.
- Spike 183 round 4 saw `Dirty = true` alone work, so the result is not reliable. Task 222 writes the Mass property in the
  same save anyway. It should check the unit after a reopen.
- Hold the `UnitsOfMeasure` object in a variable before setting `MassUnits`. In late-bound PowerShell, setting it through
  the property chain did not always apply.

## Acceptance criteria

- [x] Each question has an answer or a "not possible" with what was tried.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** the spike scripts listed in Results against the running Inventor 2026, late bound from PowerShell 5.1. No repo
  code changed, so no build or test run was needed.
- **Not compiled (changed under `src/InventorAddin`):** nothing changed.
- **Inventor API members not confirmed:** none. Every member in Results was either called or, where it says "by
  reflection", read from the interop DLL.
- **Manual checklist for Inventor:** none (spike).

## Follow-ups

Things noticed but not done.

- Task 226: in the template builder, copy the dimension styles and the feature control frame style to local styles with AWB
  names, as in question 4. Give the frame style explicit `LinearUnits` before setting its marker, and do not set
  `AlternateUnitsDisplay`. The drawing then has no style conflict, and every dimension kind, hole note and bend note prints
  a period.
- Create Print: set `Application.SilentOperation = true` around `Documents.Add` and restore it afterwards. A modal "Style
  Conflict" dialog stops an invisible `Documents.Add`, and this matters until the templates are rebuilt with renamed
  styles.
- Tasks 229 and 233: choose the sign of the horizontal and up vectors, for example up toward the model's +Y or +Z, then
  right = up × n. Otherwise the front view can come out upside down.
- Task 229: on the imperial drawing the bend notes printed the radius in mm ("R3.18") even with ANSI styles. The likely
  cause is that bend notes use the part's units (`BendNote.UsePartUnits`, by reflection). Check this on an imperial print.
- Task 222: write the mass unit together with a property change before `Save()`. In this run `Dirty = true` alone did not
  keep the unit.
- A single `OrdinateDimensions.Add` needs a step that was not tried, probably `DrawingView.CreateOriginIndicator` first.
  The ordinate set works.
