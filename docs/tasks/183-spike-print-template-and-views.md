# 183: Spike: building the print template and placing views

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", open questions 21 and 22)
- **Status:** done
- **Depends on:** none
- **Needs:** **windows** (spike, local session with Aidan in Inventor 2026)
- **Parallel-safe with:** 184 (same session, separate rounds)

## Goal

Settle, before any template or view task is written against them, the Inventor calls that build the AWB print template from code and place the initial views. Every later template and view task (186, 187, 192) uses only members this spike confirms.

## Questions

**A. Title block and border, drawn from code**

1. Creating a title block definition from code: `DrawingDocument.TitleBlockDefinitions.Add`, editing its sketch
   (`Edit`/`ExitEdit`), lines, and text boxes. Where is the insertion point (bottom-right corner?), and can geometry sit
   outside the block's own rectangle (the small part number in the right margin, task 185's layout)?
2. Property fields in title block text: the `<Property Document='model' ...>` (or equivalent) formatted text for
   Part Number, Description, Material, Revision Number, Designer, Project, Mass, Design Status, and the custom
   properties Finish, Finish Thickness and Material Size. Can one text box hold two property fields on one line
   (the Finish field: Finish, ", ", Finish Thickness)? Does Design Status print as text (Work In Progress) or a number?
   Can Mass be formatted in kg or lb with 2 decimals?
3. Drawing fields: the drawing's own Author and Creation Date; sheet scale (the first view's), sheet number and sheet
   count, sheet size; the model's file name with its extension.
4. Fixed text with `±`, `°` and bold runs; rotated text (90°) for the edge part number; Tahoma.
5. The logo: a sketch image (`SketchImages.Add`?) from `libraries/Branding/aidanwbrown-mark-black-1024.png`, embedded
   (not linked) so the template carries it.
6. Border: Inventor's default border with zones (`Sheet.AddDefaultBorder` and its zone count, label and centre mark
   arguments, or a `BorderDefinitions` default border) versus a custom border drawn per sheet size. Which gives numbers
   left to right, letters from the top, centring arrows, and the zone counts in spec 05?
7. The X/Y triad in the bottom-left corner: part of a custom border, a sketched symbol placed on each sheet, or
   something else that follows every sheet size?
8. Sheet formats for ISO A4 to A0 and ANSI A to E (portrait A4 and ANSI A; landscape otherwise) and saving the
   document as an `.idw` template in a chosen folder. Sheet colour white (the drafting standard's sheet colour or the
   application's?), third angle projection in the drafting standard, Tahoma as the default text style.
9. Parts list and revision table styles in the template: column sets (item, quantity, part number, description,
   Make/Buy as a custom column, manufacturer from Vendor, manufacturer part number from Stock Number).

**B. Using the template**

10. `Documents.Add(kDrawingDocumentObject, <template path>, true)` with the AWB template, then choosing a sheet
    format (A3 landscape or ANSI B landscape) for the first sheet. Does the new drawing read the model's properties
    once a view of the model is placed?

**C. Placing views** (for task 192)

11. Projected views from a base view (`DrawingViews.AddProjectedView`) for top and right in third angle; an iso view
    (`kIsoTopRightViewOrientation` base view or a projected corner view); shaded or hidden-line style.
12. A sheet metal part: the folded front and the flat pattern (`AddBaseView` with the `SheetMetalFoldedModel` option
    false in its additional options?); what happens with no flat pattern.
13. A view's sheet size after placing (`DrawingView.Width`, `Height`, `Left`, `Top`, `Center`) to check the layout.

## How

- A spike branch `spike/183-print-template`, off `main`. Drive the running Inventor from PowerShell over COM (as spike
  160 did) or with a temporary developer button. Scripts stay in the session scratchpad, not in the repo.
- Aidan uses a test part, a sheet metal part and an assembly of his own (no employer data).
- Check every member against the Inventor API reference or the interop by reflection before trying it (`CLAUDE.md`).
- Record each confirmed member, its arguments and what it returned under Results. Then update tasks 186, 187, 192
  (and 185 if the layout must change, for example a different insertion point) with the confirmed calls.

## Results

Filled in during the spike, round by round, with what each call returned. Nothing from the spike branch is merged; the
tasks that depend on this one are updated with the confirmed members before they run.

Method: as spike 160, PowerShell 5.1 over COM, late bound. Scripts and logs in `spike-local/183/` (gitignored).

**Round 1 (2026-11-22): title block, fields and border on a new unsaved drawing** (`spike183-round1a.ps1` read-only
dump of Inventor's default template and the test part's property sets; `round1b` builds; `round1c` reads results).

- **Default template:** `FileManager.GetTemplateFile(kDrawingDocumentObject, kDefaultSystemOfMeasure,
  kDefault_DraftingStandard, Missing)` returned `...\Templates\en-US\standard.dwg` (ANSI, D size); the metric call
  returned an empty string. `Documents.Add(kDrawingDocumentObject, path, true)` made the drawing.
- **Title block definition:** `TitleBlockDefinitions.Add(name)`, then `Edit` (by-ref `DrawingSketch`, called through
  `IDispatch` with a `DispatchWrapper` as in spike 160), `SketchLines.AddAsTwoPointRectangle`, `AddByTwoPoints`,
  `TextBoxes.AddFitted(Point2d, formattedText, Missing)`, `ExitEdit(true, Missing)`. `Sheet.AddTitleBlock(def,
  kBottomRightPosition, Missing)`.
- **Insertion (A1):** the sketch's bottom-left (0,0) of an 18 x 5.5 cm rectangle landed at (67.09, 2.54), so the
  rectangle's bottom-right corner sits on the border's inner bottom-right corner. `TitleBlock.RangeBox` covers the
  lines only. Text outside the rectangle is allowed and drawn: text at x = 18.6 (rotated) printed in the right margin,
  outside the border. So task 185's layout (origin at the block's bottom-left, part number in the margin) works.
- **Reading results:** `TitleBlock.GetResultText(textBox)` works with the text boxes from `Definition.Sketch.TextBoxes`;
  the text box objects returned by `AddFitted` during `Edit` fail it ("Specified cast is not valid").
- **Model property fields (A2)** resolved once a base view of the model was on the sheet. Syntax:
  `<Property Document='model' PropertySet='<set name>' Property='<name>' FormatID='<fmtid>' PropertyID='<id>'>LABEL</Property>`.
  Design Tracking `{32853F0F-3444-11D1-9E93-0060B03C1CA6}`: Part Number 5, Description 29, Material 20, Project 7,
  Design Status 40, Designer 41, Vendor 30, Stock Number 55. Summary `{F29F85E0-4FF9-1068-AB91-08002B27B3D9}`: Revision
  Number 9, Author 4. Custom `{D5CDD505-2E9C-101B-9397-08002B2CF9AE}`: the id is per document (`Property.PropId`; in the
  test part Finish 5, Finish Thickness 7, Material size 9). A custom field with `Property='Finish'` and no `PropertyID`
  also resolved. Two fields with ", " between in one text box gave "<finish>, <thickness>".
- **Design Status** prints `WorkInProgress` (one word), not a number.
- **Mass:** `<PhysicalProperty PhysicalPropertyID='72449' Precision='2'>MASS</PhysicalProperty>` gave "4.33 kg" (with or
  without `Document='model'`). The Design Tracking Mass property (id 58) gives raw grams. kg comes from the part's
  units; lb not tried.
- **Drawing fields (A3):** Author `<Property Document='drawing' ... Summary 'Author' 4>`, creation date Design Tracking
  'Creation Time' 4 (prints 2026-11-22). `<DerivedProperty DerivedID='n'>`: 29697 model file name with extension,
  29698 model full path, 29699 model version, 29700 to 29702 the same for the drawing, 29703 number of sheets,
  29704 sheet number, 29705 sheet size (letter, "D"), 29706 sheet revision, 29707 initial view scale ("1 : 1"),
  29708 sheet name. 29690 to 29696 and 29709 to 29720 are refused ("Value does not fall within the expected range").
- **Text (A4):** `±` and `°` as Unicode characters print correctly; `<StyleOverride Bold='True'>` gives bold runs;
  `TextBox.Rotation = π/2` gives vertical text. `Font='Tahoma'` in a StyleOverride was dropped on read-back because
  the default style is already Tahoma.
- **Logo (A5):** `DrawingSketch.SketchImages.Add(path, Point2d, Link=false)` embeds the image (`LinkedToFile` false).
  `Position` is the image's top-left corner. Setting `Height` does not keep the aspect: set `Width` as well.
- **Border (A6):** `Sheet.AddDefaultBorder(8, kBorderLabelModeNumeric, 6, kBorderLabelModeAlphabetical,
  LabelFromBottomRight=false, DelimitByLines=true, Centermarks=true, Missing x7)` gave numbers 1 to 8 left to right
  (top and bottom), letters A to F from the top (both sides) and a centring arrow at the middle of each edge. This
  matches spec 05; no custom border is needed for zones.

**Round 2 (2026-11-22): template from ISO, sheet formats, using the template** (`spike183-round2.ps1`, `round2b`,
`round2c`, `probe2`, `probe3`). Saved test template `C:\AwbSpike\183\AWB-spike.idw` (outside the repo).

- **Start file (A8):** `Templates\en-US\Metric\ISO.idw` (ISO standard, mm, Tahoma text styles, its own title block
  'ISO' and three sheet formats, which the build should delete).
- **Third angle (A8):** setting `FirstAngleProjection = false` on 'Default Standard (ISO)' did not survive save and
  reopen (the style comes back from the style library). What works: `ActiveStandardStyle.Copy('<name>')` makes a local
  style (`StyleLocation` 51202), set `FirstAngleProjection = false` on the copy, assign it to
  `StylesManager.ActiveStandardStyle`. A drawing made from the saved template had the copy active, third angle.
- **Sheet colour (A8):** `DrawingDocument.SheetSettings.SheetColor = TransientObjects.CreateColor(255, 255, 255)`
  (was 235, 234, 219); kept in the saved template and in drawings made from it.
- **Sheet sizes and formats (A8):** on one sheet, for each size: delete the sheet's symbols, title block and border; set
  `Sheet.Size` (`kA4DrawingSheetSize`, `kA3DrawingSheetSize`, `kBDrawingSheetSize`) and `Sheet.Orientation`; add the
  default border, title block and triad; `SheetFormats.Add(sheet, name)`. Each format recorded its title block, the
  default border and 1 symbol. A4 portrait 21 x 29.7 (border 10 mm margins, 4 x 6 zones); ANSI B 43.18 x 27.94 (margins
  9.652 and 15.748 mm, 4 x 4); A3 landscape 42 x 29.7 (10 mm, 8 x 6). The title block landed on each border's inner
  bottom-right corner. `SaveAs(path, false)` to `.idw` works (374 KB).
- **Logo (A5):** `Width = 2.0; Height = 2.0` gives a square 20 mm mark (round 1's stretch fixed).
- **Triad (A7):** a sketched symbol definition (lines and two text boxes) placed with
  `Sheet.SketchedSymbols.Add(def, point, 0, 1, Missing)` and carried by each sheet format. `Position` is the symbol's
  centre, not its sketch origin: placed at border min + 6 mm it sat about 8 mm lower and further left, over the border
  line. The build must offset by half the symbol's size (or set an insertion point in the definition; not tried).
  `SketchedSymbol.RangeBox` read as null here.
- **Using the template (B10):** `Documents.Add(kDrawingDocumentObject, template, true)` fails ("Value does not fall
  within the expected range") while the template file is open in Inventor; it works once it is closed.
  `Sheets.AddUsingSheetFormat(format, Missing, 'Sheet', Missing, Missing, Missing)` added an A3 sheet with border zones,
  title block and triad (passing the sheet metal part as Model failed with the same error), then the template's own
  first sheet was deleted. Part number, size, sheet 1 of 1 and scale read the sheet metal part's view.
- **Custom property fields:** they need no model, no view and no `PropertyID`, and **they match by name**: on the sheet
  metal part (Material Size id 9) a field written with id 10, and a Finish field with id 99, still showed its own
  values. So the template writes `<Property Document='model' PropertySet='Inventor User Defined Properties'
  Property='<name>' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>LABEL</Property>`, with no id. (Earlier refusals
  were a script bug: `$ud` and `$UD` are one variable in PowerShell.) `DrawingSettings.CustomPropertySourceFile` is
  not needed.
- **Mass on the sheet metal part** printed "N/A" (the test part printed 4.33 kg). Answered in round 3.

**Round 3 (2026-11-22): table styles, views, flat pattern, mass** (`spike183-round3.ps1`, `round3b` for the parts list
and lb).

- **Table styles (A9):** `DrawingStylesManager.PartsListStyles` and `RevisionTableStyles` list the library styles; the
  ISO ones are `Parts List (ISO)` and `Revision Table (ISO)`, read from `ActiveStandardStyle.ActiveObjectDefaults`
  (`Object Defaults (ISO)`). `Copy(name)` on each makes a local style. `PartsListStyle.AddColumn(PropertyType,
  PropertySetId, PropertyIdentifier, Title, Width, Missing, Missing)` added Vendor (`kFileProperty`, Design Tracking
  FMTID, 30, "MANUFACTURER"), Stock Number (same, 55, "MFR PART NO.") and Make/Buy (`kCustomProperty`, `Missing`,
  `'Make/Buy'`, "MAKE/BUY"), appended after the style's own columns. To make them the defaults: copy the object
  defaults (`ActiveObjectDefaults.Copy(name)`), set its `PartsListStyle` and `RevisionTableStyle`, and assign it to
  `ActiveStandardStyle.ActiveObjectDefaults`. All of this survived save and reopen: a new drawing's parts list and
  revision table took the copied styles.
- **Parts list:** `Sheet.PartsLists.Add(view, Point2d, kStructured, Missing, 1, false)` (the sheet must be active;
  `Missing` for the Int32 section count is refused). Columns: ITEM, QTY, PART NUMBER, DESCRIPTION (from ISO), then the
  three added; `PartsListColumn.CustomPropertyName` reads `Make/Buy`. The placement point is the list's **top-left**
  corner (range from the point to the right and down), so the builder places it at the border's top-right minus its
  width. Level reads back `kFirstLevelComponents` (same value as `kStructured`).
- **Revision table:** `Sheet.RevisionTables.Add2(Point2d, Missing x6)` uses the default style; columns ZONE, REV,
  DESCRIPTION, DATE, APPROVED; one row, sheet scope. Removing ZONE or setting columns on the copied style was not tried
  (`RevisionTableStyle.AddColumn` exists; there is no remove on the style).
- **Views (C11):** with the template's third angle standard, `AddProjectedView(base, Point2d, kFromBaseDrawingViewStyle,
  Missing)` placed to the right of the base gave `kRightViewOrientation`, above it `kTopViewOrientation`, and up-right
  `kIsoTopRightViewOrientation` (with `kShadedDrawingViewStyle` it is shaded). A base view with
  `kIsoTopRightViewOrientation` and the shaded style works too. Base view: `AddBaseView(doc, Point2d, scale,
  kFrontViewOrientation, kHiddenLineRemovedDrawingViewStyle, Missing, Missing, Missing)`. The point is the view's
  centre.
- **Sheet metal (C12):** `AddBaseView` with `AdditionalOptions` = a `NameValueMap` holding `SheetMetalFoldedModel` =
  false places the flat pattern (`IsFlatPatternView` true). `SheetMetalComponentDefinition.HasFlatPattern` and
  `FlatPattern.Length` and `Width` (cm) read it. A part with no flat pattern was not tried: check `HasFlatPattern`
  first.
- **View sizes (C13):** `DrawingView.Width`, `Height`, `Left`, `Top` (cm on the sheet; `Top` is the top edge) and
  `Center` read back as expected, for example a 26 x 2 cm front at 1 : 4 is 6.5 x 0.5, centred on the point given.
  `ScaleString` reads "1 : 4", and the sheet's scale field shows the first view's scale.
- **Mass "N/A":** the PhysicalProperty field shows N/A until the model's mass has been computed. Reading
  `ComponentDefinition.MassProperties.Mass` and then `DrawingDocument.Update()` changed it to "0.11 kg". The builder,
  and Create Print, read the mass before placing the views.
- **Mass in lb, not settled:** `Units` and `UnitID` attributes on `<PhysicalProperty>` are accepted but dropped (the
  stored text keeps only the id and Precision). Setting `UnitsOfMeasure.MassUnits` to `kLbMassMassUnits` on the drawing,
  or on the part, after the field was placed did not change "0.11 kg". Still to try in task 187: an imperial template
  whose drawing has lb as its mass unit from the start. If that fails, an lb template needs another way (a question for
  Aidan, not a choice for the implementer).

