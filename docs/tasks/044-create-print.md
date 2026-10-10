# 044: Create Print

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Create Print)
- **Status:** todo
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

- [ ] `PrintPlan.DrawingFileName(string modelFullFileName, string drawingExtension)`: the model's folder and file name with the drawing extension (`C:\Work\AWB-10001,test_plate.ipt` and `.dwg` give `C:\Work\AWB-10001,test_plate.dwg`). The extension is the drawing template's own (spike 032: a `.dwg`-template drawing cannot be saved as `.idw`)
- [ ] `PrintPlan.StandardScales`: 10, 5, 4, 2, 1, 1/2, 1/4, 1/5, 1/10, 1/20, 1/50 (as doubles, largest first; Default)
- [ ] `PrintPlan.ChooseScale(double viewWidthCm, double viewHeightCm, double sheetWidthCm, double sheetHeightCm)`: the largest standard scale at which the view at scale 1 fits within 60% of the sheet's width and 60% of its height (Default); the smallest scale when none fits; throws for non-positive sizes
- [ ] Tests with invented sizes, including a tiny part and a large assembly

Add-in

- [ ] A large **Create Print** button on the Drawing Tools panel of the Part and Assembly ribbons, with the `CreatePrint` icon
- [ ] Needs the model saved: an unsaved model stops with `"Save the part before creating its print."`
- [ ] If the drawing file already exists, asks `"A print for this part already exists. Open it?"` and opens it on Yes; makes nothing new either way
- [ ] If a drawing with the other extension (`.idw` or `.dwg`) already exists beside the model, the same question applies to it
- [ ] Otherwise: the drawing template in `Settings.DefaultUnits`, looked up with the **ANSI** drafting standard (Aidan, spec 02 question 24), falling back to Inventor's units-neutral default drawing template; both blank stops with a message naming the units; a front base view of the model centred on the first sheet at `ChooseScale` (view size at scale 1 by 032's finding, from the model's range box); the drawing's Part Number set to the model's Part Number when `RegisterStore.HasNumber` is true; saved to `DrawingFileName`; left open and active
- [ ] Any failure closes the half-made drawing without saving and says what failed
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. On a saved, numbered part: Create Print makes `AWB-1000N,name.idw` (or `.dwg`) beside it, with one front view centred at a sensible scale, and the drawing's Part Number matches
  2. Run it again: asked to open the existing print
  3. On an assembly: same
  4. On a never-saved part: the save message
  5. Is one front view at that scale what you want, or should the view or scale differ?

## Follow-ups

Things noticed but not done.
