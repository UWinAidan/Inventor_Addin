# 044: Create Print

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Create Print)
- **Status:** done (add-in not compiled)
- **Depends on:** 032 (its Results must be filled in), 034, 037, 042, 043
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files)

## Goal

A Create Print button makes a drawing of the active part or assembly from Inventor's default drawing template, with one front base view at a scale that fits, saved beside the model under the same name and carrying the model's part number.

## Before starting

Read the Results of task 032, questions 1 and 6. If they are empty, stop.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs` (new): drawing file name and view scale
- `tests/InventorAddin.Core.Tests/Drawing/PrintPlanTests.cs` (new)
- `src/InventorAddin/Commands/CreatePrintCommand.cs` (new)
- `src/InventorAddin/Inventor/Templates.cs` (drawing template, if 043 did not already cover it)
- `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin.Core/Ribbon/RibbonModels.cs`, `RibbonLayout.cs` and its tests

Out of scope:

- A custom drawing template and title block (M5). Placing more than one view (spec 05).

## Acceptance criteria

Core

- [x] `PrintPlan.DrawingFileName(string modelFullFileName, string drawingExtension)`: the model's folder and file name with the drawing extension (`C:\Work\AWB-10001,test_plate.ipt` and `.dwg` give `C:\Work\AWB-10001,test_plate.dwg`). The extension is the drawing template's own (spike 032: a `.dwg`-template drawing cannot be saved as `.idw`)
- [x] `PrintPlan.StandardScales`: 10, 5, 4, 2, 1, 1/2, 1/4, 1/5, 1/10, 1/20, 1/50 (as doubles, largest first; Default)
- [x] `PrintPlan.ChooseScale(double viewWidthCm, double viewHeightCm, double sheetWidthCm, double sheetHeightCm)`: the largest standard scale at which the view at scale 1 fits within 60% of the sheet's width and 60% of its height (Default); the smallest scale when none fits; throws for non-positive sizes
- [x] Tests with invented sizes, including a tiny part and a large assembly

Add-in

- [x] A large **Create Print** button on the Drawing Tools panel of the Part and Assembly ribbons, with the `CreatePrint` icon
- [x] Needs the model saved: an unsaved model stops with `"Save the part before creating its print."`
- [x] If the drawing file already exists, asks `"A print for this part already exists. Open it?"` and opens it on Yes; makes nothing new either way
- [x] If a drawing with the other extension (`.idw` or `.dwg`) already exists beside the model, the same question applies to it
- [x] Otherwise: the drawing template in `Settings.DefaultUnits`, looked up with the **ANSI** drafting standard (Aidan, spec 02 question 24), falling back to Inventor's units-neutral default drawing template; both blank stops with a message naming the units; a front base view of the model centred on the first sheet at `ChooseScale` (view size at scale 1 by 032's finding, from the model's range box); the drawing's Part Number set to the model's Part Number when `RegisterStore.HasNumber` is true; saved to `DrawingFileName`; left open and active
- [x] Any failure closes the half-made drawing without saving and says what failed
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (Results questions 1 and 6):
  - Drawing template: `FileManager.GetTemplateFile(DocumentTypeEnum.kDrawingDocumentObject, units, DraftingStandardEnum.kANSI_DraftingStandard, Type.Missing)`; fallback `FileManager.GetTemplateFileWithOptions(DocumentTypeEnum.kDrawingDocumentObject, Type.Missing, Type.Missing, Type.Missing, Type.Missing)`. `kDefault_DraftingStandard` returns `""` for drawings. On Aidan's install both give `.dwg`.
  - Drawing: `Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, template, true)`; sheet `dwg.ActiveSheet` (ANSI D, 86.36 x 55.88 cm in the metric template).
  - View: `sheet.DrawingViews.AddBaseView((_Document)model, TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2), scale, ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineDrawingViewStyle)`. The first parameter is `_Document`: cast it.
  - View size at scale 1 for a front view: the model's `ComponentDefinition.RangeBox` X extent by Y extent (cm). Get the definition from `PartDocument` or `AssemblyDocument`.
  - Part Number: copy it only when `RegisterStore.HasNumber` is true, never Inventor's file-name fallback.

- Create Print is Aidan's (2026-10-10). Assemblies, units from Settings, the scale list and the 60% fit are Defaults.
- Only the Part Number is copied; the M5 title block will read the model's properties.

## Verification

Filled in by the implementer.

- **Ran:** (cloud session, `CLAUDE_CODE_REMOTE=true`)
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1547 passed, 0 failed (after the review fixes; 1543 before).
  - New tests: `Drawing/PrintPlanTests.cs` (file name, existing-print candidates in both extensions, the scale list, `ChooseScale` with a tiny part, a small part, a large assembly, one too large for any scale, a tall view, exact fits, and non-positive, NaN and infinite sizes; the messages). `RibbonLayoutTests` updated: Create Print is the one large button on Drawing Tools on Part and Assembly only; Part and Assembly now show CAD Automation, Drawing Tools (and Developer); Drawing is unchanged; seven distinct icon names.
- **Beyond the listed Core members:** `PrintPlan` also holds `DrawingExtensions`, `ExistingPrintCandidates` and `FindExistingPrint` (the "other extension also counts" rule, so the add-in has no `if` for it), and the command's messages (`UnsavedMessage`, `AlreadyExistsQuestion`, `WrongDocumentMessage`, `NoGeometryMessage`, `MissingTemplateMessage(units)`, `FailedMessage(step, reason, cleanup)`). `FailedMessage` ends according to `PrintCleanup`: no drawing was made (`Documents.Add` failed), the drawing was closed without saving, or closing it failed and the user is told to close it without saving.
- **Not compiled (changed under `src/InventorAddin`):** `Commands/CreatePrintCommand.cs` (new), `Inventor/Templates.cs` (new `DrawingFor`), `UI/RibbonSetup.cs`. None of it has been built.
- **Inventor API members not confirmed:**
  - `Document.Activate()`, called (inside `ComSafe.Run`) after `Documents.Open(path, true)` when the user chooses to open an existing print, in case that drawing was already open and Open does not bring it forward. Not in 032's Results or existing code. If it does not compile, delete that line.
  - `Documents.Open(string, bool)` returning a `Document`: 032 used `Documents.Open(path, true)` and assigned it to a `Document` (`d = ... Open(path, false)`), so this is very likely fine.
  - Everything else is from 032's Results or existing code: `FileManager.GetTemplateFile(kDrawingDocumentObject, units, kANSI_DraftingStandard, Type.Missing)`, `FileManager.GetTemplateFileWithOptions(kDrawingDocumentObject, Type.Missing x4)`, `Documents.Add`, `(DrawingDocument)doc`, `DrawingDocument.ActiveSheet`, `Sheet.Width`/`Height`/`DrawingViews`, `DrawingViews.AddBaseView((_Document)model, point, scale, kFrontViewOrientation, kHiddenLineDrawingViewStyle)`, `TransientGeometry.CreatePoint2d`, `PartDocument`/`AssemblyDocument.ComponentDefinition.RangeBox`, `Box.MinPoint`/`MaxPoint`, `Document.SaveAs`, `Document.Close(true)`, `TransactionManager.StartTransaction((_Document)doc, name)`, `CommandTypesEnum.kFileOperationsCmdType` (as 043).
  - `AddBaseView`'s return value is ignored; 032 assigned it to a `DrawingView`, which is not needed here.
- **Manual checklist for Inventor:**
  1. With a part or assembly open, the AWB Addin tab shows a Drawing Tools panel after CAD Automation with one large Create Print button (CreatePrint icon). A drawing has no Drawing Tools panel.
  2. On a saved, numbered part (Part Properties shows the number with no note): Create Print makes `AWB-1000N,name.dwg` beside it (`.dwg` on your install), opens it as the active document, with one front view in hidden-line style centred on the sheet at a sensible scale. Check the log line `Create Print: '...' created with a front view at scale ...`. File > iProperties on the drawing: Part Number matches the part.
  3. Run it again on the same part: asked "A print for this part already exists. Open it?". Yes opens (or brings forward) the drawing; No does nothing. No new file either way. Repeat with the drawing already open and confirm Yes makes it the active window.
  4. Rename the `.dwg` to `.idw` in Explorer (or put any `.idw` with the part's name beside it): Create Print asks the same question about the `.idw`.
  5. On a saved part with no number (an old file, Part Number blank or the file-name fallback): the drawing is made, and the log line ends with `and no Part Number (the model has no number).` The drawing's iProperties will show the drawing's own file name as Part Number: that is Inventor's fallback for a blank Part Number, not a value copied from the part. It is the drawing's name (`.dwg` file name), not the part's.
  6. On a saved assembly: same as 2.
  7. On a never-saved part: "Save the part before creating its print." and nothing is made.
  8. On a saved empty part (no bodies): "The model has no geometry to show in a front view, so no print was created."
  9. A very small part (a few mm) gets 10:1 or 5:1; a large assembly gets a reducing scale such as 1:10.
  10. Edit > Undo in the new drawing once: the view (and Part Number) go together. Check this is acceptable; the saved file keeps them.
  11. Is one front view at that scale what you want, or should the view or scale differ?

## Follow-ups

Things noticed but not done.

- **Default: an empty or flat model stops the command.** When the range box cannot be read or its X or Y extent is not positive (an empty part, or geometry that is flat in the front view), Create Print stops before making a drawing with `PrintPlan.NoGeometryMessage`. The spec does not cover this; the parent may want to log it as a Default and ask Aidan whether to place the view at 1:1 instead.
- **Default: the model is `ActiveDocument`, not `ActiveEditDocument`.** While editing a part in place inside an assembly, Create Print prints the assembly (the document whose ribbon is showing is the part's). Spec 02 could say which is wanted.
- **Default: the existing-print check runs before the template check**, so an existing print is offered even when no drawing template is found. When both `.dwg` and `.idw` exist, the one with the template's extension is offered.
- The messages say "part" for assemblies too ("Save the part before creating its print.", "A print for this part already exists."), as the acceptance criteria give them. "model" or the document kind may read better for assemblies.
- Spec 08's "Ribbon icons" list should name Create Print (task 042's follow-up asks each command task to add its icon there); spec 08 was not in this task's files.
- `PrintPlan.UnitsName` copies the private `NewPartCreator.UnitsName` ("metric"/"imperial"). A shared `UnitsSystem` display helper in Core would remove the duplicate.
