# 045: Save As Copy

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Save As Copy)
- **Status:** todo
- **Depends on:** 032 (its Results must be filled in), 040, 043
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files)

## Goal

A Save As Copy button copies the active part or assembly under a new part number and name in the same folder, writes the creation properties on the copy, and opens it.

## Before starting

Read the Results of task 032, questions 2 and 4 (how `SaveAs` with a copy behaves). If they are empty, stop.

## Scope

- `src/InventorAddin.Core/ViewModels/SaveAsCopyViewModel.cs` (new), with tests
- `src/InventorAddin/Commands/SaveAsCopyCommand.cs` (new)
- `src/InventorAddin/UI/SaveAsCopyWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin.Core/Ribbon/RibbonModels.cs`, `RibbonLayout.cs` and its tests

Out of scope:

- Replacing an occurrence (046).

## Acceptance criteria

- [ ] A small **Save As Copy** button on the CAD Automation panel of the Part and Assembly ribbons, after New Part, with the `SaveAsCopy` icon
- [ ] The view-model: name (pre-filled with the original's part name) and part type (pre-filled with the original's), the same validation and preview as `NewPartViewModel` (reuse, do not copy), header `Save As Copy` with the original's file name as subline
- [ ] Needs the original saved. Saves the copy to the original's folder under `PartFileName.Build(newNumber, name, extension)`, by 032's method (save a copy, then open it), assigns the number through the register, writes `CreationProperties.Build(...)` on the copy (Designer and Detailer from Settings), saves it, and opens it as the active document. The original is not changed or marked modified
- [ ] Failure handling as in 043
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032: `SaveAs(path, false)` was tested and renames the open document, which this command must not do. Use `doc.SaveAs(copyPath, true)` (save a copy; the open document keeps its name), then `Documents.Open(copyPath, true)`, write the properties, `Save()`. `SaveAs` with `true` compiles but its behaviour was not run in the spike: list it under members not confirmed, and make the first checklist step check that the original keeps its name, is not marked modified, and that the copy holds the original's unsaved edits.
- A comma in the copy's name is accepted (spike 032, question 2).

- Writing the creation properties on the copy and keeping everything else is a Default.
- Saving a copy of an assembly copies the assembly file only; its components are shared with the original. Say so in the dialog's subline note.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. On a numbered part: Save As Copy with a new name makes `AWB-1000N,new_name.ipt` in the same folder and opens it; its Part Number, Description, Designer, Detailer and Part Type are the new values; material and other properties match the original
  2. The original is unchanged and not marked modified
  3. The register has a row for the copy

## Follow-ups

Things noticed but not done.
