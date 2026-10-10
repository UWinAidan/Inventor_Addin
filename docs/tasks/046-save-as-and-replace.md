# 046: Save As and Replace

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Save As and Replace)
- **Status:** done (add-in not compiled)
- **Depends on:** 032 (its Results must be filled in), 045
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files)

## Goal

From inside an assembly, a Save As and Replace button copies the selected component under a new number and replaces that occurrence with the copy.

## Before starting

Read the Results of task 032, question 7. If they are empty, stop.

## Scope

- `src/InventorAddin/Commands/SaveAsReplaceCommand.cs` (new), reusing the Save As Copy window and view-model
- `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin.Core/Ribbon/RibbonModels.cs`, `RibbonLayout.cs` and its tests

## Acceptance criteria

- [x] A small **Save As and Replace** button on the CAD Automation panel of the Assembly ribbon, after Save As Copy, with the `SaveAsReplace` icon
- [x] Needs exactly one component occurrence selected in the active assembly (`doc.SelectSet`, 1-based; a `ComponentOccurrence`, which a `ComponentOccurrenceProxy` inside a sub-assembly also is); otherwise `"Select one component in the assembly first."`
- [x] Opens the Save As Copy dialog for the selected component's file; on Create makes the copy as 045 does (in the component's folder), then replaces **only the selected occurrence** with `occurrence.Replace(copyPath, false)` (spike 032: constraints are kept). The copy is not opened in its own window
- [x] A top-level occurrence: the replace runs in one transaction on the assembly, so one Undo puts the original back
- [x] An occurrence inside a sub-assembly (Aidan, spec 02 question 25): before Create, the dialog says the sub-assembly (named) will change and be saved, so every assembly that uses it changes and Undo cannot put it back. The replace edits the sub-assembly (spike 032), which is then saved. A read-only sub-assembly stops with a message before the copy is made
- [x] A read-only or library component stops with a message
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (question 7): a selection inside a sub-assembly is a `ComponentOccurrenceProxy` (`Type` = `kComponentOccurrenceProxyObject`); `proxy.ParentOccurrence.Name` names the sub-assembly occurrence, and `proxy.Replace(path, false)` marks the sub-assembly document dirty, not the top assembly. The sub-assembly document to save is the one that holds `proxy.NativeObject`: reach it through the native occurrence's parent definition (`((ComponentOccurrence)proxy.NativeObject).Parent` is an `AssemblyComponentDefinition`, whose `Document` is the sub-assembly). These two members were not run in the spike: list them under members not confirmed. Deeper nesting works the same way, since `NativeObject` is the occurrence in its own assembly.
- `Replace(path, true)` replaces every occurrence of that file; do not use it (question 16 default).

- Replacing only the selected occurrence is a Default (spec 02, question 16).

## Verification

Filled in by the implementer.

- **Ran:** (cloud session, `CLAUDE_CODE_REMOTE=true`)
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1681 passed, 0 failed (the XAML rules test reads the changed `SaveAsCopyWindow.xaml`).
  - New tests: `Numbering/SaveAsReplacerTests.cs` (every stop in `Check`, the sub-assembly name and warning, the step order for a top-level and a nested occurrence, a copy that will not close, a failed replace, a failed sub-assembly save, a failed copy). `ViewModels/SaveAsCopyViewModelTests.cs` gains the `ForReplace` cases. `RibbonLayoutTests` updated for Save As and Replace.
- **Beyond the listed files:**
  - `src/InventorAddin.Core/Numbering/SaveAsReplacer.cs` (new): the selection check, messages and steps in Core. It runs `SaveAsCopyCreator.Copy` unchanged, then closes the copy, replaces the occurrence and, for a nested occurrence, saves the sub-assembly. The add-in supplies Inventor through `ISaveAsReplaceTarget` (which extends `ISaveAsCopySource`).
  - `src/InventorAddin.Core/ViewModels/SaveAsCopyViewModel.cs`: `ForReplace(...)` reuses the view-model with header and window title "Save As and Replace", Save button "Save and replace", and a `Warning` for the sub-assembly case. Save As Copy is unchanged.
  - `src/InventorAddin.Core/Numbering/SaveAsCopyCreator.cs`: only the `ISaveAsCopySource.OpenCopy` doc comment changed (Save As and Replace opens the copy invisibly).
  - `src/InventorAddin/UI/SaveAsCopyWindow.xaml` and `.xaml.cs`: the Save button text and window title come from the view-model; the warning shows under the Location section when set.
  - `src/InventorAddin/Extraction/PartPropertiesReader.cs`: `ReadEditBlock` made `internal` so the command reuses it for the component and the assembly it edits.
- **Choices made here (Defaults for the parent to log):**
  - A top-level occurrence in an assembly whose file is read-only or not modifiable stops before the dialog. The brief only covered the sub-assembly.
  - A suppressed occurrence stops ("unsuppress it first"). So does a virtual component, a never-saved component, or a never-saved sub-assembly, because the sub-assembly is saved afterwards.
  - Once the copy is saved it is a finished, numbered file. If the replace or the sub-assembly save fails, the copy and its register row stay, and the message says to replace by hand or to save the sub-assembly.
  - The command is a `DocumentEditCommand`, so it works on `ActiveEditDocument` (the same as `ActiveDocument` unless a sub-assembly is being edited in place). Its command type is `kShapeEditCmdType`.
  - For a nested occurrence the replace also runs in a transaction on the active (top) assembly, which is aborted on failure. The sub-assembly is saved after that transaction ends.
  - The Save button reads "Save and replace". The warning uses the error text colour.
- **Not compiled (changed under `src/InventorAddin`):** `Commands/SaveAsReplaceCommand.cs` (new), `UI/SaveAsCopyWindow.xaml`, `UI/SaveAsCopyWindow.xaml.cs`, `UI/RibbonSetup.cs`, `Extraction/PartPropertiesReader.cs`. None of it has been built.
- **Inventor API members not confirmed:**
  - `((ComponentOccurrence)proxy.NativeObject).Parent` cast to `AssemblyComponentDefinition`, and its `.Document`, which the brief flagged as not run in the spike. If `Parent` is typed differently, the cast may need adjusting.
  - `SelectSet.Count`: the spike used only the indexer `SelectSet[1]`.
  - `CommandTypesEnum.kShapeEditCmdType`: no existing code uses it; it is only named in `DocumentEditCommand`'s doc comment.
  - `Document.Save()` on a sub-assembly that is open only as a reference of the active assembly: does it save without a dialog, and does it also save other dirty files?
  - `SaveAs(path, true)` on a component document that the active assembly references: does it leave the assembly pointing at the original? This is the same open point as 045's step 1d.
  - Running `Documents.Open(copy, false)` and then `Close(true)` before `Replace(copy, false)`: each call is confirmed (spike 032), but this sequence was not run.
  - How Undo behaves after a nested replace that ran in a transaction on the top assembly and was then saved.
  - `ComponentOccurrence.Suppressed`, `Definition`, `Definition.Document`, `VirtualComponentDefinition`, `Replace(string, bool)`, `ComponentOccurrenceProxy`, `ParentOccurrence.Name` and `Document.SelectSet` come from `AssemblyExtractor` or spike 032.
- **Manual checklist for Inventor:**
  1. Open an assembly that has two occurrences of one saved, numbered part, each with a constraint. The **Save As and Replace** button is on the CAD Automation panel of the Assembly ribbon, small, right after Save As Copy, with the SaveAsReplace icon. It is not on the Part, Drawing or ZeroDoc ribbons.
  2. Nothing selected, then Save As and Replace: the message "Select one component in the assembly first." Select two occurrences, or a face: the same message.
  3. Select one occurrence (for example `X:1`), then Save As and Replace. The dialog header and window title read Save As and Replace over the part's file name, Part name and Part type are pre-filled, Folder is the part's folder, there is no warning, and the button reads Save and replace.
  4. Save and replace with a new name: `AWB-1000N,<name>.ipt` appears in the part's folder. No new window opens. Only that occurrence now uses the copy (check its file in the browser), the other still uses the original, and the constraints are healthy. The copy's Part Number, Description, Designer, Detailer and Part Type are the new values (open it to check). The register has a row for it.
  5. Edit > Undo once: the original is back in that place. The copy file stays on disk and its row stays in the register. Check that the original part is not marked modified and that the assembly was not repointed to the copy in other places (the `SaveAs(path, true)` question).
  6. In an assembly `Top.iam` holding `Sub.iam` (saved), which holds part `X`: in the browser, expand `Sub:1` and select `X:1` (or use Part priority in the graphics window). Save As and Replace: the dialog shows a warning naming `Sub.iam (Sub:1)`, saying it will be saved, that every assembly using it changes, and that Undo cannot put it back. Save and replace: `X:1` inside `Sub.iam` uses the copy, `Sub.iam` is saved (it is not marked modified, and its file time changed), and no dialog appeared during the save. Record what one Undo does afterwards.
  7. Set `Sub.iam` read-only in Windows, then repeat step 6: a message says the sub-assembly is read-only before the dialog, and no number is assigned.
  8. Select a component from a library, or a read-only part file: a message says it is not copied and replaced. Select a virtual component, then a suppressed one: each gives its message.

## Follow-ups

Things noticed but not done.

- Spec 08's "Ribbon icons" list and "The windows" list should name Save As and Replace, and say the Save As Copy dialog also serves it, with the sub-assembly warning. Spec 08 was not in this task's files.
- While a sub-assembly is edited in place, the command reads `ActiveEditDocument.SelectSet`. Whether Inventor keeps the selection there or on the top document was not checked; if the button says nothing is selected in that case, read `ActiveDocument`'s select set instead.
- `NewPartException` and `NewPartResult` now serve New Part, Save As Copy and Save As and Replace (see the 045 follow-up about a neutral name).
