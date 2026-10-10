# 045: Save As Copy

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Save As Copy)
- **Status:** done (add-in not compiled)
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

- [x] A small **Save As Copy** button on the CAD Automation panel of the Part and Assembly ribbons, after New Part, with the `SaveAsCopy` icon
- [x] The view-model: name (pre-filled with the original's part name) and part type (pre-filled with the original's), the same validation and preview as `NewPartViewModel` (reuse, do not copy), header `Save As Copy` with the original's file name as subline
- [x] Needs the original saved. Saves the copy to the original's folder under `PartFileName.Build(newNumber, name, extension)`, by 032's method (save a copy, then open it), assigns the number through the register, writes `CreationProperties.Build(...)` on the copy (Designer and Detailer from Settings), saves it, and opens it as the active document. The original is not changed or marked modified
- [x] Failure handling as in 043
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032: `SaveAs(path, false)` was tested and renames the open document, which this command must not do. Use `doc.SaveAs(copyPath, true)` (save a copy; the open document keeps its name), then `Documents.Open(copyPath, true)`, write the properties, `Save()`. `SaveAs` with `true` compiles but its behaviour was not run in the spike: list it under members not confirmed, and make the first checklist step check that the original keeps its name, is not marked modified, and that the copy holds the original's unsaved edits.
- A comma in the copy's name is accepted (spike 032, question 2).

- Writing the creation properties on the copy and keeping everything else is a Default.
- Saving a copy of an assembly copies the assembly file only; its components are shared with the original. Say so in the dialog's subline note.

## Verification

Filled in by the implementer.

- **Ran:** (cloud session, `CLAUDE_CODE_REMOTE=true`)
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1632 passed, 0 failed (the XAML rules test reads the new `SaveAsCopyWindow.xaml`).
  - New tests: `Numbering/SaveAsCopyCreatorTests.cs` (the steps and their order, every stop before the number is assigned, every failure after it, the half-made copy closed and deleted, offline and duplicates), `ViewModels/SaveAsCopyViewModelTests.cs` (pre-fills, header, assembly note, part type, preview and validation, and a check that they match New Part's), `ViewModels/NewFileNameCheckTests.cs`. `RibbonLayoutTests` updated for Save As Copy.
- **Beyond the listed files:**
  - `src/InventorAddin.Core/Numbering/SaveAsCopyCreator.cs` (new): the copy steps and messages in Core, as `NewPartCreator` does for New Part. The add-in supplies Inventor through `ISaveAsCopySource` and `ICopiedDocument`. Failures reuse `NewPartException`, `NewPartResult` and `NewPartCreator.Notice`.
  - `src/InventorAddin.Core/Numbering/NewPartCreator.cs`: the folder check, the register assignment (with its "file already exists" check and register error handling) and the full-path build moved into `internal static` methods (`CheckFolder`, `AssignNumber`, `FullPathFor`, `ProbeFolder`) that `SaveAsCopyCreator` calls. New Part's behaviour is unchanged; its tests pass unchanged.
  - `src/InventorAddin.Core/ViewModels/NewFileNameCheck.cs` (new): the preview and validation New Part had in `Recalculate`, now shared by both view-models ("reuse, do not copy"). `NewPartViewModel` uses it; its `NumberPlaceholder` and `ChooseFolderMessage` constants now point at it.
  - `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs`: `PartTypeOption.ListFor(code)` builds the part type list (blank, the list, the file's unknown code). Part Properties, New Part and Save As Copy use it.
  - `src/InventorAddin/Commands/InventorNewDocument.cs` (new): New Part's private document adapter moved out of `NewPartCommand` and given `Save()`, so both commands write properties in a transaction the same way.
- **Choices made here (Defaults for the parent to log):**
  - A failure after the copy's file was saved closes the copy without saving and **deletes the copy's file**, so no file named with the new number is left holding the original's Part Number. Only a file that did not exist before the copy was saved is deleted. If it cannot be deleted, the message names it.
  - A blank Part Type on the original pre-fills the kind's default (A for an assembly, W for a weldment), as Part Properties and New Part do. A code not in the list is kept and shown as unknown.
  - The command copies `Application.ActiveDocument` (as Create Print), not `ActiveEditDocument`: with a part edited in place inside an assembly, the assembly is copied.
  - Weldment assemblies can be copied; the register row's Kind is `WeldmentAssembly`.
- **Not compiled (changed under `src/InventorAddin`):** `Commands/SaveAsCopyCommand.cs`, `Commands/InventorNewDocument.cs`, `Commands/NewPartCommand.cs`, `UI/SaveAsCopyWindow.xaml`, `UI/SaveAsCopyWindow.xaml.cs`, `UI/RibbonSetup.cs`. None of it has been built.
- **Inventor API members not confirmed:**
  - `Document.SaveAs(string, true)` (save a copy): compiles per the brief, but its behaviour was not run in spike 032. Checklist step 1 checks it.
  - Whether `SaveAs(path, true)` on an assembly with unsaved changes in its components shows a dialog (the spike never saw one, but never tried this case).
  - `Document.Activate()` after `Documents.Open(path, true)`: used the same way in `CreatePrintCommand` (task 044), itself not yet compiled. Wrapped in `ComSafe.Run`.
  - Everything else comes from 032's Results or existing code: `Documents.Open(string, bool)`, `Document.Save()`, `Document.Close(bool)`, `Document.FullFileName`, `TransactionManager.StartTransaction((_Document)doc, name)`, `Application.ActiveDocument`, `CommandTypesEnum.kFileOperationsCmdType` (as New Part; that member is itself still unconfirmed, see 043).
- **Manual checklist for Inventor:**
  1. **`SaveAs(path, true)` first** (split by the reviewer).
     a. Open a saved, numbered part with no edits and Save As Copy: the original's window title still shows its own file name, and closing the original afterwards does not ask to save.
     b. Open another saved part, make an edit without saving (for example change an extrusion distance), then Save As Copy: the copy opens as the active document and holds the edit; the original's file on disk does not have it.
     c. Open an assembly, edit one of its parts in place, then Save As Copy: the assembly (the top-level document) is the one copied, and saving the copy does not fail.
     d. Open a part that is placed in an assembly that is also open, then Save As Copy the part: the assembly is not marked modified and still points at the original part (spike 032 found a plain `SaveAs` repoints open assemblies).
  2. On a numbered part: the button is on the CAD Automation panel of the Part ribbon right after New Part, small, with the SaveAsCopy icon (also on the Assembly ribbon; not on Drawing or ZeroDoc). The dialog's header reads Save As Copy over the original's file name; Part name and Part type are pre-filled from the original; Folder is the original's folder; the File name preview reads `AWB-?????,<name>.ipt`.
  3. Save copy with name `new name`: `AWB-1000N,new_name.ipt` appears in the same folder and opens; its Part Number, Description, Designer, Detailer and Part Type are the new values; material and other properties match the original.
  4. The original is unchanged and not marked modified (unless you edited it, as in step 1).
  5. The register has a row for the copy with its full path.
  6. A name with a comma (`bracket, left`) is accepted and gives `AWB-1000N,bracket,_left.ipt`. A name with `?` disables Save copy and the status line names the character.
  7. On an assembly: the dialog shows the note that only the assembly file is copied; Part type is pre-filled (A when blank); the copy is `.iam` and uses the same component files as the original (check one component's file path in the copy).
  8. On a never-saved part: a message asks you to save it first; no dialog opens.
  9. With the NAS disconnected: the number is assigned offline and a message says it will be added at the next sync.
  10. Make a file named with the next number in the folder and Save As Copy with the same name: a message says the file already exists and nothing was assigned.
  11. Edit > Undo on the copy once: the creation properties are one undo step. Check this is acceptable; the file on disk keeps them.

## Follow-ups

Things noticed but not done.

- Spec 08's "Ribbon icons" list and "The windows" list should name Save As Copy and its dialog (Identity: Part name, Part type; Location: Folder read-only, File name preview, assembly note). Spec 08 was not in this task's files.
- Spec 02 question 17 (open Part Properties after creating) applies to Save As Copy too; nothing is done until it is answered.
- `NewPartException` and `NewPartResult` now serve Save As Copy as well; if 046 and 047 reuse them, a neutral name (for example `NumberedFileException`) would read better. Not renamed here to keep 043's code stable.
