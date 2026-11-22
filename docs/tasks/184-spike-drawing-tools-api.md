# 184: Spike: Inventor calls for the M5 drawing tools

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", "Core logic", open questions 15 to 20)
- **Status:** done
- **Depends on:** none
- **Needs:** **windows** (spike, local session with Aidan in Inventor 2026)
- **Parallel-safe with:** 183 (same session, separate rounds)

## Goal

Settle the Inventor calls the later M5 commands need (Check Out of Bounds, notes, part marking, Batch Print, REF dimensions, Stock Size, parts list), so each add-in task names only confirmed members.

## Questions

1. **Extents on a sheet** (task 195): `DrawingView` `Left`/`Top`/`Width`/`Height`; a range box for general and leader
   notes, dimensions, balloons, sketched symbols, parts lists, hole tables, revision tables and custom tables. The
   border's inner rectangle (`Border.RangeBox`?) and the title block's (`TitleBlock.RangeBox`).
2. **Notes** (task 197): `GeneralNotes.AddFitted` with formatted text (bold, numbered list), its position and width;
   an attribute set on a note (`AttributeSets.Add`) and finding notes by it after Save, close and reopen.
3. **Part marking** (tasks 199, 200): a leader note to an edge in a view (`LeaderNotes.Add` with a `GeometryIntent` on a
   `DrawingCurve`) after the user picks it (`CommandManager.Pick` with a drawing curve filter); a rectangle in a
   drawing sketch on a view with a dashed line type and a colour override (`SketchLine.LineType`, `OverrideColor`?).
   In a part: an emboss (engrave) feature from a sketch text box on a planar face whose text holds the Part Number
   property field, so a new Part Number updates the text; its depth and direction.
4. **Batch Print** (task 202): the PDF translator add-in (`TranslatorAddIns` item for PDF), its options (all sheets,
   colour), exporting one drawing. Can it append several drawings to one PDF? If not, what merges them (a NuGet
   library with a licence that suits a public repo, for example PDFsharp, MIT)? Opening each drawing without showing
   it, and closing it after.
5. **REF dimensions** (task 204): finding a view's extreme edges (`DrawingView.DrawingCurves`, a curve's
   `StartPoint`/`EndPoint`/`Evaluator2D` range); `GeneralDimensions.AddLinear` with two `GeometryIntent`s and a text
   point; making the text "<value> REF" bold (`Text.FormattedText` with `<DimensionValue/>`?) or a reference
   tolerance.
6. **Stock size** (task 206): the part's range box (`ComponentDefinition.RangeBox`) versus a tight oriented box
   (`SurfaceBody.OrientedMinimumRangeBox`?); which one to use for a part modelled off-axis.
7. **Parts list** (task 208): a custom column (`PartsListColumns.Add` with `kCustomProperty`), writing a cell's value
   as an override (`PartsListCell.Value`, `Static`?), and whether it survives an update; a row's referenced document
   (`PartsListRow.ReferencedRows`?, `ReferencedDocumentDescriptor`); Content Center members
   (`PartComponentDefinition.IsContentMember`) and library files (the active project's library paths).

## How

- A spike branch `spike/184-drawing-tools`, off `main`, in the same local session as 183. PowerShell over COM or a
  temporary developer button; scripts stay in the scratchpad.
- Aidan's own test models only.
- Record each confirmed member under Results, then update tasks 195, 197, 199, 200, 202, 204, 206 and 208 with the
  confirmed calls before they run.

## Results

Filled in during the spike, round by round, with what each call returned. Nothing from the spike branch is merged; the
tasks that depend on this one are updated with the confirmed members before they run.

Method: as spike 183, PowerShell 5.1 over COM, late bound. Scripts and logs in `spike-local/184/` (gitignored); test
files in `C:\AwbSpike\184\` (a copy of spike 183's round 3 drawing, an off-axis bar, a copy of Aidan's assembly with
one Content Center screw).

**Round 1 (2026-11-22): extents, notes, REF dimension, PDF, range boxes, parts list** (`spike184-round1.ps1`).

- **PowerShell trap:** `foreach` over `DrawingView.DrawingCurves` sees one object; walk it with `Count` and `Item(i)`.
  C# `foreach` is not affected.
- **Extents (q1):** `Border.RangeBox` is the border's inner rectangle (A3: 1,1 to 41,28.7 cm). `TitleBlock.RangeBox`
  is the block (23,1 to 41,6.5). `GeneralNote.RangeBox`, `PartsList.RangeBox`, `RevisionTable.RangeBox` are real; a
  note placed past the border edge reported a range past it (40 to 43.23), so overlap shows. `DrawingView`,
  `SketchedSymbol`, `Balloon` and `LinearGeneralDimension` have no `RangeBox` (late bound it returns null). For views
  use `Left`, `Top`, `Width`, `Height`; for symbols and balloons `Position` (the symbol's is its centre); for
  dimensions `Text.Origin`. A tighter extent for those three is round 2's question.
- **Notes (q2):** `Sheet.DrawingNotes.GeneralNotes.AddFitted(Point2d, formattedText, Missing)`; the point is the
  note's top-left. `<StyleOverride Bold='True'>` and `<Br/>` are kept; a numbered list is plain text lines ("1. ...");
  a `<List>` tag is not formatted text (it was stored escaped and printed literally). `Width` can be set and the note
  rewraps. `note.AttributeSets.Add('AwbNotes', Missing)` then `AttributeSet.Add('Kind', kStringType, value)`; after
  Save, Close and reopen, `DrawingDocument.AttributeManager.FindObjects('AwbNotes', 'Kind', value)` returned the
  note (`kGeneralNoteObject`). `GeneralNote.Text` drops the line breaks.
- **REF dimension (q5):** a view's curves give `StartPoint`, `EndPoint` (sheet cm) and `ProjectedCurveType`; the
  vertical lines with the least and greatest X are the extreme edges. `Sheet.CreateGeometryIntent(curve, Missing)` for
  each, then `GeneralDimensions.AddLinear(textPoint, intent1, intent2, kHorizontalDimensionType, Missing, Missing,
  Missing)`; `ModelValue` is in cm. `Text.FormattedText = "<StyleOverride Bold='True'><DimensionValue/>
  REF</StyleOverride>"` printed "260,00 REF" in bold (Inventor split it into two bold runs). `Tolerance.SetToReference()`
  prints "(260,00)" instead. Both work; spec 05 says "REF" text.
- **Decimal comma:** the ISO standard copied in spike 183 prints "260,00". Task 187 sets the standard's
  `DecimalMarkerType` to a period (member exists; value not tried) and checks the precision.
- **PDF (q4):** the add-in with `ClassIdString` `{0AC6FD96-2F4D-42CE-8BE0-8AEA580399E4}` ("Translator: PDF").
  `HasSaveCopyAsOptions(doc, context, options)` fills the defaults (`Sheet_Range`, `All_Color_AS_Black`,
  `Remove_Line_Weights`, `Vector_Resolution`, `Custom_Begin_Sheet`, ...). Set `Sheet_Range` = `kPrintAllSheets`
  (14082) with `options.Value(name) = v`; context `Type` = `kFileBrowseIOMechanism`; a `DataMedium` with `FileName`;
  `SaveCopyAs(doc, context, options, medium)`. A 3-sheet drawing gave a 3-page PDF (4.2 s); there is no option to
  append to an existing PDF, so Batch Print merges the files itself (a PDF library; licence to check in task 202).
  `Documents.Open(path, false)` opened a drawing without showing it (visible document count unchanged), the export
  worked on it, and `Close(true)` closed it.
- **Range boxes (q6):** on the bar rotated 30°, `ComponentDefinition.RangeBox` gave 9.66 x 6.73 x 1.0 cm;
  `ComponentDefinition.OrientedMinimumRangeBox` (and `SurfaceBody.OrientedMinimumRangeBox`) gave 10 x 2 x 1, the true
  size: the lengths of `DirectionOne`, `DirectionTwo`, `DirectionThree`, with `CornerPoint`. On an axis-aligned part
  both agree. Stock Size uses the oriented box.
- **Parts list (q7):** `PartsListRow.Item(columnIndex)` is the cell; `cell.Value = 'BUY'` makes it `Static` (an
  override). It survived `DrawingDocument.Update()` and save, close, reopen. `SaveItemOverridesToBOM()` failed with
  E_FAIL (not needed). `PartsListRow.ReferencedFiles.Item(1)` gives `FullFileName` and `ReferencedDocument`.
  `ComponentDefinition.IsContentMember` was true for the Content Center screw only, both from
  `AssemblyDocument.AllReferencedDocuments` and from parts list rows. The active project's `ContentCenterPath` reads
  the folder; `LibraryPaths` was empty in Aidan's Default project.

**Round 2 (2026-11-22): pick, leader note, marking zone, tighter extents, engraved part number**
(`spike184-round2.ps1`; Aidan clicked the edge).

- **Pick (q3):** `CommandManager.Pick(kDrawingCurveSegmentFilter, prompt)` waits for the user and returns a
  `DrawingCurveSegment` (null on Esc). `segment.Parent` is the `DrawingCurve`, and `curve.Parent` the `DrawingView`.
- **Leader note (q3):** `Sheet.DrawingNotes.LeaderNotes.Add(points, formattedText, Missing)`, where `points` is an
  `ObjectCollection` of the text position (`Point2d`) and then `Sheet.CreateGeometryIntent(curve, Missing)`. A Design
  Tracking Part Number field (`<Property Document='model' ... PropertyID='5'>`) in a leader note shows the model's
  part number. `LeaderNote.RangeBox` works.
- **Marking zone (q3):** `DrawingView.Sketches.Add()`, then `DrawingSketch.Edit()` and `ExitEdit()` (no by-ref
  argument here). `SheetToSketchSpace(view.Center)` gives the centre. `SketchLines.AddAsTwoPointRectangle(a, b)`
  returns the four lines, and on each `LineType = kDashedLineType` and `OverrideColor = CreateColor(0, 112, 192)`
  stuck. `TextBoxes.AddFitted` adds the zone text. **A view sketch is in model units:** a 4 x 1.5 cm rectangle on a
  1 : 4 view is 1 x 0.375 cm on the sheet. So spec 05's 40 x 15 mm zone is 40 x 15 mm on the part, whatever the view
  scale.
- **Tighter extents (q1):** `GeneralDimension.Text.RangeBox` gives the dimension text's box.
  - Balloons: `Balloon.Position` plus `Leader.AllNodes` positions (each node's `Position`).
  - Sketched symbols: `Definition.Sketch.RangeBox` is null, so use `Position` (the symbol's centre). The template's
    own triad is the only symbol, and it lies inside the border.
- **Engraved part number (q3, task 200):** in a copy of the test part:
  - The largest planar face (`Faces`, `SurfaceType = kPlaneSurface`, `Evaluator.Area`), `PartComponentDefinition.
    Sketches.Add(face, false)`, then `ModelToSketchSpace(face.PointOnFace)`.
  - `TextBoxes.AddFitted(point, "<StyleOverride FontSize='0.3'><Property Document='model' ...
    PropertyID='5'>PART NUMBER</Property></StyleOverride>", Missing)`. FontSize is the character height in cm, so 0.3
    is 3 mm; the text box came out 3.87 x 0.31 cm.
  - `Profiles.AddForSolid(false, ObjectCollection with the text box, Missing)`, then
    `Features.EmbossFeatures.AddEngraveFromFace(profile, 0.02, kNegativeExtentDirection, Missing, Missing)`: it was
    up to date and cut (the volume dropped).
  - Setting the Part Number property and calling `PartDocument.Update()` changed the text to the new number, and the
    feature stayed up to date. The direction was right first time on this face; task 200 checks the volume drops, and
    if it does not, deletes the feature and tries `kPositiveExtentDirection`.

**Afterwards:** tasks 195, 197, 199, 200, 202, 204, 206 and 208 carry the confirmed calls in their notes. Defaults
logged: PDFsharp merges the PDFs; the marking zone is 40 x 15 mm on the part. Not tried: an engrave on a face where
negative is the wrong direction, a REF dimension on a view whose extremes are arcs, a sheet metal part with no flat
pattern.
