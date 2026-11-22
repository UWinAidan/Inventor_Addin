# 184: Spike: Inventor calls for the M5 drawing tools

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", "Core logic", open questions 15 to 20)
- **Status:** todo
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
