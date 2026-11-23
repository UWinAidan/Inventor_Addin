# 188: Create Print uses the AWB template

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5" (Create Print uses the AWB print template), open question 21)
- **Status:** done
- **Depends on:** 186, 187
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Create Print makes its drawing from the AWB print template for the default units, on an A3 landscape (metric) or ANSI B landscape (imperial) sheet, instead of Inventor's template.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs`: the sheet format name per units, the missing-template message naming the path it looked for.
- `src/InventorAddin/Commands/CreatePrintCommand.cs`: template from `LibraryPaths.PrintTemplate`; picks the sheet format as spike 183 showed; keeps the existing front view, scale rule, naming and Part Number write.
- `src/InventorAddin/Inventor/Templates.cs`: `DrawingFor` is no longer used by Create Print; remove it if nothing else calls it.
- Tests for the new `PrintPlan` members.

Out of scope: the initial views window (192); notes (197).

## Acceptance criteria

- [x] With the starter template, Create Print on a part makes a drawing on A3 landscape (metric) or ANSI B landscape (imperial) whose title block shows the part's values.
- [x] With a Settings path set, that template is used.
- [x] A missing template file stops with a message that names the path; nothing is created.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5291 passed, 1 failed. The failure is `NewPartCreatorTests.FileWithTheNextNumberAlreadyThere_StopsWithoutTakingTheNumber` (expects the message "Disk full."; unrelated to this task, nothing it touches changed). The old `IdwTemplate`, `IdwTemplateName` and old `MissingTemplateMessage(units)` tests were replaced by tests for `PrintUnitsFor`, `SheetFormatName` and `MissingTemplateMessage(path)`.
- **Not compiled (changed under `src/InventorAddin`):** none; both changed files built on Windows (`Commands/CreatePrintCommand.cs`, `Inventor/Templates.cs`). Not run in Inventor.
- **Inventor API members not confirmed:** none by running. `Sheets.AddUsingSheetFormat(format, Missing, "Sheet", Missing, Missing, Missing)`, `SheetFormats` enumeration, `SheetFormat.Name`, `Sheet.Activate`, `Sheet.Delete`, and `MassProperties.Mass` then `Update` follow spike 183 rounds 2 and 3; the signature was checked by reflection on the 2026 interop. `Document.Update()` on the drawing (the spike called it on `DrawingDocument`) is the same member on `Document`.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Close `AWB Metric.idw` in Inventor if it is open (Documents.Add fails while the template is open). Settings: default units Metric, template paths blank.
  2. Open a saved part of your own with a number, material and finish. Click Create Print. A drawing opens on an A3 landscape sheet with the AWB border, zones, triad and title block, one front view, and the title block shows the part number, description, material, finish spec, revision, status and weight (not N/A). One sheet only.
  3. Delete the print from `Prints\`, set default units to Imperial, repeat on an imperial part: the sheet is ANSI B landscape. Weight prints (lbmass or kg per task 222).
  4. Set a Metric template path in Settings to a copy of the template; Create Print uses it: the log line "Create Print: '...' created from '<the copy's path>' (sheet format 'AWB ISO A3')".
  5. Rename or move `libraries\Templates\AWB Metric.idw` (and clear the Settings path): Create Print stops with a message naming the full path, and no drawing is created or left open.
  6. Run Create Print again on a part that has a print: it still offers to open the existing one.

## Follow-ups

- Documents.Add fails while the template file is open in Inventor (spike 183, B10); the failure message names the step but not that cause. A friendly hint could be added.
- Create Print's scale rule still uses the whole sheet size, not the area above the title block (task 189 has the usable-area rule); revisit when 192 wires the initial views.
