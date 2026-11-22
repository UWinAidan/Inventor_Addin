# 187: Build Print Template command and the starter templates

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", open question 22)
- **Status:** todo
- **Depends on:** 183, 185, 186
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
- [ ] A drawing made from either template with a view of a test part shows that part's values in every field, including Status (the custom Status property, task 210) and Mass in kg or lb with 2 decimals; changing a property in the model changes the print without any AWB step.
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
- Triad: leave it off A4 and ANSI A portrait, where it would lie over the title block (Default, spec 05 question 24). Draw each cell's `Texts`, `Logo` and `Symbol`, never its `FixedText`, which only describes the cell (task 185).
- Decimal marker: the copied ISO standard prints dimensions with a decimal comma ("260,00", spike 184). Set the standard's `DecimalMarkerType` to a period (the member is confirmed; its value is not) and list it under Verification.
- Mass: the field shows "N/A" until the model's mass is computed; the acceptance check reads `MassProperties.Mass` and calls `DrawingDocument.Update()` first. Mass in lb is not settled (spike 183, round 3). Try the imperial template with lb as the drawing's mass unit from the start. If the field still prints kg, stop and add an open question to spec 05; do not invent another field.
- Keep every engineering choice (positions, texts, sizes) in Core's layout; the builder only draws.
- Close Inventor's documents it opened; a failure deletes the half-made file and says which step failed (as Create Print does).
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
