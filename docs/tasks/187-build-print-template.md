# 187: Build Print Template command and the starter templates

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", open question 22)
- **Status:** done (templates rebuilt and checked by Aidan 2026-11-23, committed)
- **Depends on:** 183, 185, 186, 214
- **Needs:** **windows** (drives Inventor; local session)
- **Parallel-safe with:** none

## Goal

A developer command draws the AWB print template from task 185's layout and saves the metric and imperial templates; the starter templates it makes are committed in `libraries/Templates/`.

## Scope

- `src/InventorAddin/Drawing/PrintTemplateBuilder.cs`: walks `PrintTemplateLayout` and draws title block, border (zones, arrows, triad), sheet formats, styles (white sheet, third angle, Tahoma, parts list and revision table styles), maps each field to the property fields spike 183 confirmed, embeds the logo.
- `src/InventorAddin/Commands/BuildPrintTemplateCommand.cs`: developer-only button (Developer panel), asks for a folder, builds both templates, logs and reports.
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs`, `RibbonModels.cs`: the button (existing developer icon).
- `libraries/Templates/AWB Metric.idw`, `libraries/Templates/AWB Imperial.idw`: built by the command on Aidan's PC and committed.

Out of scope: Create Print using the template (188); views.

## Acceptance criteria

- [ ] Running the command makes both `.idw` files; opened in Inventor, each has the sheet formats ISO A4 to A0 and ANSI A to E (A4 and ANSI A portrait), white sheets, third angle, the zone grid, centring arrows and triad, and the title block as in the mockup.
- [ ] A drawing made from either template with a view of a test part shows that part's values in every field, including Status (Inventor's Status field, User Status; task 216) and Mass in kg or lb with 2 decimals; changing a property in the model changes the print without any AWB step.
- [ ] The logo is embedded (the template opens on a PC without `libraries/Branding/`).
- [ ] Only the developer panel shows the button; it is query-free (it creates new files), command type as Create Print.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Spike 183's results (rounds 1 to 3) are the only source for API calls; anything not there is listed under Verification. In short:
  - Title block: `TitleBlockDefinitions.Add`, `Edit` through `IDispatch` (by-ref sketch), `AddFitted` text with the confirmed field syntax, `ExitEdit`, `Sheet.AddTitleBlock(def, kBottomRightPosition, Missing)`. The sketch origin is the block's bottom-left; the block's bottom-right sits on the border's inner corner.
  - Custom property fields carry no `PropertyID` (they match by name).
  - Border: `AddDefaultBorder` as in the results. The triad is a sketched symbol whose `Position` is its centre.
  - Styles: third angle only through a local copy of the standard. Parts list and revision table styles go through copied styles and a copied object defaults style.
  - Sheet colour: `SheetSettings.SheetColor`.
  - Sheets: one sheet format per size (`SheetFormats.Add(sheet, name)`), then save as `.idw`.
- Triad: place it where the layout says; on A4 and ANSI A portrait that is just above the title block's left end (Aidan, spec 05 question 24; task 214). Use `SheetSizeSpec.TriadOriginMm` (border coordinates), not `Triad.OriginXMm/OriginYMm` (only the inset), and check `TriadExtentMm` (17.5) against the drawn triad. Draw each cell's `Texts`, `Logo` and `Symbol`, never its `FixedText`, which only describes the cell (task 185).
- Decimal marker: the copied ISO standard prints dimensions with a decimal comma ("260,00", spike 184). Set the standard's `DecimalMarkerType` to a period (the member is confirmed; its value is not) and list it under Verification.
- Mass: the field shows "N/A" until the model's mass is computed; the acceptance check reads `MassProperties.Mass` and calls `DrawingDocument.Update()` first. Mass in lb is not settled (spike 183, round 3). Try the imperial template with lb as the drawing's mass unit from the start. If the field still prints kg, stop and add an open question to spec 05; do not invent another field.
- Keep every engineering choice (positions, texts, sizes) in Core's layout; the builder only draws.
- Close Inventor's documents it opened; a failure deletes the half-made file and says which step failed (as Create Print does).
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Written on Aidan's PC with Inventor running, so the add-in was built but **nothing was run in Inventor**. The command has not been
run; `libraries/Templates/AWB Metric.idw` and `AWB Imperial.idw` do not exist yet, and every acceptance check that needs Inventor
(both templates open correctly, a drawing from each shows the values, mass in kg and lb, the logo, sheet formats, triad) is left for Aidan.

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (warnings are the existing CS8600 `Cursor` ones, one more of the same kind in the new command).
  - `dotnet build src/InventorAddin.Core`: 0 errors, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 5135 passed, 0 failed (ribbon, command-document and branding tests updated for the new button).
- **Compiled, not run:** `src/InventorAddin/Drawing/PrintTemplateBuilder.cs` and `src/InventorAddin/Commands/BuildPrintTemplateCommand.cs` (new), `src/InventorAddin/UI/RibbonSetup.cs`
  (one line). They build on Windows against the 2026 interop, so every member name and signature exists; how Inventor behaves when each is called is not tested.
- **Deliberate choices:**
  - No transaction: the drawing is a new unsaved document the builder makes and closes without saving on any failure, so there is nothing for Undo to protect.
  - The template is saved as `<name>.building.idw` beside the target, closed, then moved over the old file, so a failure never damages an existing template.
  - The button's icon is four copies of Export Model Data's PNGs named `BuildPrintTemplate.*` (the layout test requires one icon name per command); a placeholder like Hole Table's.
  - The default sheet (`PrintTemplateLayout.DefaultSheetName`: A3, ANSI B) is made last, so the saved template's own sheet is the one Create Print will want. Format names (`SheetSizeSpec.FormatName`, "AWB ISO A3", "AWB ANSI B") and the triad numbers (`TriadSpec`: axis weight, label distance and offsets, extent) live in Core with tests; Create Print (188) must use the same names.
  - A failure of `SheetFormat.Delete()` or of setting the decimal marker is a warning in the log, not a stop; so is any failed text placement.
  - Imperial starts from Inventor's `Templates\en-US\English\ANSI (in).idw` (the file exists on this PC; spike 183 only used the ISO one) and sets the drawing's mass unit to lb before any field is made.
- **Inventor API members not confirmed (not in spike 183's results or in existing code; they compile, but nothing shows what Inventor does with them):**
  - The typed `TitleBlockDefinition.Edit(out DrawingSketch)` call (the spike used the IDispatch call; the builder falls back to it and warns if the typed call throws).
  - `SheetFormat.Delete()` on Inventor's three starting formats; `TitleBlockDefinition.Delete()` on its own `ISO` / `ANSI` block after the sheets stopped using it (spike 183 said "the build should delete" them).
  - The ANSI (in) starting template and `DrawingStandardStyle.Copy` on its ANSI standard (the spike copied the ISO one).
  - `DrawingStandardStyle.DecimalMarkerType = DecimalMarkerTypeEnum.kPeriodDecimalMarker` (member known, value not tried): check that dimensions print "260.00".
  - `Sheet.AddDefaultBorder` with the four margin arguments (cm, as doubles) instead of `Missing` (order Top, Bottom, Left, Right is confirmed by the reviewer), and the `DefaultBorder` getters `TopMargin`, `BottomMargin`, `LeftMargin`, `RightMargin`, `HorizontalZoneCount`, `VerticalZoneCount`: the builder reads `DefaultBorder.TopMargin` etc. back and logs a warning when they differ from the layout (ISO 10 mm and 20 mm filing edge; ANSI 12.7 mm and 20 mm).
  - `PartsListStyle.AddColumn` with `kCustomProperty` and an empty string for the property set (the spike passed `Missing`; a typed call cannot). If refused, it retries with the user-defined set's id and records a warning. Horizontal alignment `kAlignTextLeft` for both title and value.
  - `PartsListStyle.AddColumn` widths from the layout (mm / 10 cm), and the Make/Buy column placed before the two manufacturer columns.
  - `SketchLine.LineWeight` (used by the hole table), `SketchCircles.AddByCenterRadius`, `SketchCircle.LineWeight`, `SketchLine.LineType = kChainLineType` for the centre lines (chain is a guess at "long dash, short gap, short dash, short gap"; its failure is only a warning).
  - `TextBox.HorizontalJustification`, `RangeBox` and `Origin` for centred text in a title block definition (the hole table uses them in a sketched symbol definition); `TextBox.RangeBox` for the triad labels (logged as 0 when unreadable).
  - `<StyleOverride Font='Tahoma' FontSize='...'>` wrapped around a `<Property>` element, and `FontSize` in cm (seen in Inventor's own title block, not tried by the spike); `Bold='True'` wrapped around a property field.
  - `<DerivedProperty DerivedID='29704'>` and `'29703'` in one text (the spike used them in one line) and `29705` for the sheet size on an ISO sheet (the spike read it on ANSI D only: ANSI shows a letter; ISO is unknown).
  - Text placement: after `AddFitted`, the box is justified left (or centre) and lower (both used by `HoleTableMarker`) and moved by its `RangeBox` so the bottom of its box sits at the layout's y (as `HoleTableDrawer.AddText` does). The layout's y is a baseline, so descenders hang slightly below it; check the heights in the title block. The rotated small part number is moved so its right edge sits at the layout's x.
  - `TextBox.Rotation = 90 degrees` for the small part number in the block's own sketch with the origin outside the block (as the spike did, but at the layout's x).
  - The logo `SketchImage.Position` set again after `Width` and `Height` (the spike found position is the top-left).
  - The triad symbol has an insertion point at its sketch origin (`SketchPoints.Add` and `InsertionPoint = true`, as the hole table symbols do); `SketchedSymbols.Add` at the triad origin is taken to put that point there. The builder logs the position asked and read back.
  - `Documents.Add` returning `_Document` assigned to `Document` (as Create Print does), `Sheet.Delete`/`SketchedSymbol.Delete`/`Border.Delete`/`TitleBlock.Delete` (used by the spike through late binding).
  - `TransientObjects.CreateColor(255, 255, 255, 1.0)` (the spike passed three arguments).
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Close every drawing in Inventor, then build and deploy the add-in (`dotnet build InventorAddin.slnx`, Inventor closed), start Inventor, and turn on Settings > Show developer tools if it is off.
  2. Open any part, a drawing or no document: the **Developer** panel shows **Build Print Template** (small button, the Export Model Data picture). It is not on the other panels.
  3. Click it. Choose the repository's `libraries\Templates` folder (create it if it is not there). It finds the logo in `libraries\Branding`. Two dialogs may ask about replacing files; answer Yes.
  4. Expect one message saying both files were made, in the order metric then imperial. If it says it failed, note the step it names, send `%APPDATA%\AwbAddin\awbaddin.log`, and check that no `.building.idw` file is left in the folder.
  5. Open `AWB Metric.idw` (it stays open for the checks; step 9 closes it). In the log (`awbaddin.log`, lines "Build Print Template") read the warnings: they list margin, zone-count, title block position and centring differences. Note them.
  6. Look at each sheet format: Browser > Drawing Resources > Sheet Formats > right-click "AWB ISO A4" > New Sheet; do the same for A3, A2, A1 and A0 (A4 is portrait). On each check: white sheet, numbers 1 to N across the top and bottom (A4 4, A3 8, A2 12, A1 16, A0 24), letters A to F/H/L/P down both sides (6, 6, 8, 12, 16), a centring arrow in the middle of each edge, the title block in the bottom-right corner (180 x 55 mm) against the border, the X/Y triad inside the border at the bottom-left (on A4 it sits just above the left end of the title block, not under it), the small part number rotated in the right margin level with the border's bottom.
  7. Look at the title block against `docs/ui-mockups/title-block.png`: the logo (19 mm, upper-left of the left section), the tolerance box text with ± and °, the third angle symbol (frustum and two circles, dashed centre lines) and the caption, the URL, the project and file name row, all labels in small text, REV and part number bold and large. Note anything off: text sitting low or high in its cell, the REV letter off-centre, the URL not centred, overlapping text.
  8. Check Tools > Document Settings > Standard (or the Styles Editor): the active standard is "AWB Metric", **Third angle**, sheet colour white; Styles Editor > Parts List shows "AWB Parts List" with columns ITEM, QTY, PART NUMBER, DESCRIPTION, MAKE/BUY, MANUFACTURER, MANUFACTURER PART NUMBER; Revision Table "AWB Revision Table". Note that revision table columns are Inventor's own (follow-up).
  9. Save nothing. Close `AWB Metric.idw`. Make a new drawing from it (File > New > Browse to the file) and add a sheet from AWB ISO A3; place a base view of a test part that has Part Number, Description, Material, Designer, Revision Number, Project, a custom Status, Material Size and Finish Spec. Every field shows that part's value (including Status, not Design Status) and a mass with 2 decimals and `kg` (the part's mass may show N/A until it is computed: open the part's Physical properties or run the part once; the field then updates). Dimension a feature: the number prints with a period ("260.00").
  10. Change the part's description in the part: the title block follows with no AWB step. Check the drawing's Author and Date fields, SCALE, SHEET (1 OF 1), SIZE and FILE NAME (with `.ipt`).
  11. Repeat steps 5 to 10 with `AWB Imperial.idw`: formats AWB ANSI A (portrait), B, C, D, E; the margin is 0.5 in; UNITS reads IN; the imperial tolerance text. **WEIGHT must read in lb with 2 decimals.** If it prints kg, stop, do not work around it: add the question to spec 05 (task 187 notes: "do not invent another field") and tell the parent.
  12. Check that the logo is embedded: reopen `AWB Metric.idw`; expand the title block definition > the sketch > the image's properties show "Link" off; or copy the file to a PC (or rename `libraries\Branding` away) and open it: the logo still shows.
  13. When both templates pass, copy nothing by hand: the parent commits `libraries\Templates\AWB Metric.idw` and `AWB Imperial.idw` (about 400 KB each). The next build copies them beside the add-in (`libraries\Templates\*.idw` is already in the project).

## Follow-ups

- The revision table keeps Inventor's own columns; `PrintTemplateLayout.RevisionTableColumns` (REV, DESCRIPTION, DATE, BY) is not applied. `RevisionTableStyle.AddColumn` and `RemoveAllColumns` exist but were not tried by the spike; a small spike round, then a task.
- The parts list keeps Inventor's four columns as they are (ITEM, QTY, PART NUMBER, DESCRIPTION); their widths are not set from `PrintTemplateLayout.PartsListColumns` and the combined width is not checked against the layout.
- Zone frame and centring arrow geometry (`SheetSizeSpec.CentringArrow`, `ZoneColumnEdgesXMm`) is not used: the default border draws them (spike 183). If the arrows look wrong next to the mockup, either move to a custom border or drop that Core data.
- The decimal marker is set on the template's standard only; if Create Print (188) gets its dimensions from another standard, set it there too.
- `libraries\Branding` is not copied into the add-in's output, so the command finds the logo only beside the chosen folder or above the add-in's folder, and asks otherwise. Add the logo to the project's output if the command should run from a deployed add-in.
- A zero-document build still needs Inventor's `Templates\en-US` folder; the year (2026) is a constant in the builder.
