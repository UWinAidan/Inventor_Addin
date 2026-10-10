# 043: New Part command and window

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Templates; Written on creation; Commands: New Part)
- **Status:** todo
- **Depends on:** 032 (its Results must be filled in), 036, 039, 040, 042
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files with 044 to 048)

## Goal

A New Part button creates a part, sheet metal part or assembly from Inventor's default template, with the next part number, the creation properties written, and the file saved under its name.

## Before starting

Read the Results of task 032, questions 1, 2 and 3. Use the template lookup, creation and save calls it found to work. If the Results are empty, stop: this task is not runnable yet.

## Scope

- `src/InventorAddin/Commands/NewPartCommand.cs` (new)
- `src/InventorAddin/Inventor/Templates.cs` (new: default template for a kind and units, by 032's method)
- `src/InventorAddin/UI/NewPartWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/UI/RibbonSetup.cs` (register the command)
- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`CommandNames.NewPart`), `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` (entry), `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs`

Out of scope:

- Templates from a folder setting (later). Save As Copy (045).

## Acceptance criteria

- [ ] A small **New Part** button on the CAD Automation panel of the Part, Assembly and ZeroDoc ribbons, first in the panel, with the `NewPart` icon from 042. Layout tests updated
- [ ] The command is not a `DocumentEditCommand` (there may be no active document); pick the command type that matches creating a document and say why in a comment
- [ ] The dialog is built on `NewPartViewModel` (040), follows spec 08 and the XAML rules test, and gets its default folder from the active document's folder, or `ActiveDesignProject.WorkspacePath` when no saved document is open (by 032's finding for the workspace path)
- [ ] On Create, in this order: find the template (a missing template stops with a message naming the kind and units, before any number is assigned); check the target file does not exist (using a number placeholder is not enough: check again after assigning and stop with a message if it does); assign the number through `AddinServices.RegisterStore`; create the document from the template; write `CreationProperties.Build(...)` with `PropertyWriter`; save to `PartFileName.Build(...)` in the chosen folder
- [ ] A failure after the number was assigned shows a message saying which number was used and that it stays in the register, logs the exception, and closes any half-made document without saving
- [ ] Offline assignment and duplicates are reported as in 039
- [ ] The new document is left open and active
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Every Inventor member used must come from 032's Results or existing code; list any other in Verification.
- `Documents.Add` takes a `DocumentTypeEnum`; map from `NewFileKind` in the add-in. The sheet metal subtype is in 032's Results.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. New Part from the ZeroDoc ribbon: the dialog opens with the workspace as folder, Metric, Part
  2. Name `test bracket`, part type M, Create: a part opens named `AWB-1000N,test_bracket.ipt`, saved in the folder; iProperties hold Part Number, Description `test bracket`, Designer and Detailer from Settings, Part Type M; the register has its row
  3. Sheet metal part, imperial: the sheet metal environment and inch units
  4. Assembly: part type pre-fills A; the file is `.iam`
  5. A name with `?`: Create is disabled with the message
  6. With the NAS disconnected: the number is assigned offline and the message says so
  7. Part Properties on the new part shows the number with no note, and the header shows the name with spaces

## Follow-ups

Things noticed but not done.
