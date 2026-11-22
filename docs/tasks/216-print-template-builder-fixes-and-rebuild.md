# 216: Print template builder fixes and rebuild

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`; task 187
- **Status:** built, awaiting Aidan's rebuild
- **Depends on:** 187, 215
- **Needs:** **windows** (drives Inventor; Aidan runs the command)
- **Parallel-safe with:** none

## Goal

The builder draws task 215's corrected layout. The metric standard prints a decimal point in every style that shows numbers. The Status cell reads Inventor's own Status field. The build measures each text against its cell. Aidan then rebuilds both templates and checks them again.

## Scope

- `src/InventorAddin/Drawing/PrintTemplateBuilder.cs`:
  - **Decimal marker.** On the AWB Metric standard and every style that shows numbers (dimension styles, text styles where applicable, parts list, revision table, hole table styles), set the decimal marker to a period. Aidan's check: "260,00" still printed with a comma after task 187 set only the standard's `DecimalMarkerType`. Find where Inventor keeps the marker per style, for example `DimensionStyle` members.
    - Check each member against the interop by reflection (`C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll` and its `.xml`).
    - List each member under Verification.
  - **Status field.** Read Design Tracking "User Status" (`kUserStatusDesignTrackingProperties`, PropertyID 17, the Status tab's Status text) instead of the custom Status property (Aidan, 2026-11-22, task 219).
    - Change `PrintField.Status`'s documentation in Core to match.
    - The field uses the confirmed `<Property Document='model' PropertySet='Design Tracking Properties' ... PropertyID='17'>` form.
  - **Measure each text.** After drawing each text, read its `RangeBox` and warn in the log (the command's report shows the warning count) when it extends past its cell. Use the layout's cell bounds.
- **Text size meaning (from task 215).** The layout's sizes are the mockup's SVG font sizes (em). Inventor's `<StyleOverride FontSize=...>` appears to be the cap height: spike 184's engraved part number at FontSize 0.3 cm was 3.87 cm wide for "AWB-ENGRAVE-TEST", about 1.3 times the em-based Tahoma width.
  - So `AddText` passes `size × PrintTextFit.CapHeightEm` (0.727) as FontSize.
  - Confirm by measuring one text's `RangeBox` height or width after drawing against `PrintTextFit.WidthMm`, and log the ratio.
  - Also read the margins from task 215's single `MarginMm` (`FilingMarginMm` and `OtherMarginMm` still work).
- **Rebuild.** Aidan deploys and runs Build Print Template again. The parent commits the two `.idw` files when he passes them.

## Acceptance criteria

- [ ] A metric drawing from the rebuilt template dimensions "260.00", and parts list and hole table numbers use a period.
- [ ] STATUS shows the part's Status tab value.
- [ ] The build log reports no text past its cell.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors and the Core tests pass; every Inventor member is confirmed or listed.

## Verification

Filled in by the implementer (Windows, Inventor 2026 installed and running; I did not run Inventor or the command).

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 10 warnings (none in `PrintTemplateBuilder.cs`). `dotnet test tests/InventorAddin.Core.Tests`: 5161 passed, 0 failed. The builder is built but not run: whether the changes work is for Aidan's rebuild.
- **Changed, built but not run in Inventor:** `src/InventorAddin/Drawing/PrintTemplateBuilder.cs`. Core: only the `PrintField.Status` doc comment changed.
- **What changed:**
  - `SetStyleDecimalMarkers` (called from `ConfigureStandard`): sets `DecimalMarkerType` and `AlternateDecimalMarkerType` to period on every `DimensionStyle` and `FeatureControlFrameStyle` in the drawing, reads each back, logs the counts and warns for any that did not read back as a period. By reflection the interop has no decimal marker member on parts list, revision table, hole table or text styles (`UnitsFormatting.DecimalMarkerType` is on `HoleTableColumn`, a placed table's column, and `UnitAttributes` is on notes), so those follow the standard. If a placed hole table still prints a comma, that column member is the next step.
  - Status reads `<Property Document='model' PropertySet='Design Tracking Properties' Property='User Status' FormatID='{32853F0F-...}' PropertyID='17'>`. The `Property` name 'User Status' is from the task text; the PropertyID is what Inventor most likely resolves on.
  - `AddText` passes `size x PrintTextFit.CapHeightEm` as FontSize. After placing, `MeasureText` reads the `RangeBox`. It warns when a text passes its cell (0.1 mm tolerance); field values are measured with their placeholder, so a longer real value can still overflow. For labels and fixed text it also logs min/mean/max of drawn width over `PrintTextFit.WidthMm` once per title block. The rotated small part number and triad labels are not checked (no cell).
  - Margins: the builder already reads `FilingMarginMm` and `OtherMarginMm`, which task 215 made aliases of `MarginMm`; no change needed.
- **Inventor API members confirmed by reflection on the interop DLL:** `DimensionStyle.DecimalMarkerType`, `DimensionStyle.AlternateDecimalMarkerType`, `FeatureControlFrameStyle.DecimalMarkerType`, `FeatureControlFrameStyle.AlternateDecimalMarkerType`, `DrawingStylesManager.DimensionStyles`, `DrawingStylesManager.FeatureControlFrameStyles`, `DecimalMarkerTypeEnum.kPeriodDecimalMarker`. `.Name` on both style types and `TextBox.RangeBox` are used elsewhere in the add-in.
- **Not confirmed (only by running):**
  - That parts list and hole table numbers print a period: no style member sets a marker for them, so they rely on the standard.
  - That setting these markers is what stops "260,00" (the standard's own marker was already set and did not).
  - The `Property='User Status'` name and that the field shows the Status tab's text.
  - That `FontSize` equals the cap height (the logged ratio answers this).
  - Whether `RangeBox` of a title block text gives title block coordinates in cm (the triad code assumes the same).
- **Manual checklist for Inventor** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209):
  1. Close any open AWB templates. Deploy the new build (`dotnet build InventorAddin.slnx`, Inventor closed), start Inventor, and open any document.
  2. Run Build Print Template for both Metric and Imperial. Note the warning count in the report. Then search `%APPDATA%\AwbAddinwbaddin.log` for "extends past its cell": there must be none, or note which texts are listed.
  3. In `%APPDATA%\AwbAddin\awbaddin.log`, find "decimal marker set to a period on N dimension style(s)..."; it must say 0 did not read back as a period.
  4. In the same log, find "drawn width over PrintTextFit width"; the mean should be near 1.0 (roughly 0.9 to 1.1). Send me the numbers if it is not.
  5. Open `libraries/Templates/AWB Metric.idw` and look at each sheet format (A4 portrait through A0): no text touches or crosses a cell line, and the labels and values sit where the mockup puts them.
  6. Make a new drawing from the metric template, with a part that has a Status set on its Status tab (iProperties, Status tab). Check the STATUS cell shows that text.
  7. Place a linear dimension of 260 mm: it must read "260.00". Add a parts list and a hole table: their numbers use a period.
  8. Repeat 5 to 7 on the imperial template (dimensions in inches, period marker, mass in lb).
  9. If all pass, the parent commits the two `.idw` files.

## Follow-ups

- Field values are measured with a placeholder, so a long real value (a long description or material) can still overflow its cell; a second check against `PrintTextFit.WidthMm` with sample long values in Core tests would catch this without Inventor.
- If a placed hole table still prints a comma after the rebuild, set `HoleTableColumn.UnitsFormatting.DecimalMarkerType` in the hole table command (the style has no marker member).
