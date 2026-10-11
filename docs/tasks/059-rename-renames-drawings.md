# 059: Rename renames the part's drawings

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename"; "Commands: Create Print")
- **Status:** todo
- **Depends on:** 052, 057, 058
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Fix bugs 2 and 5 from Aidan's run 2 checks. Rename also renames the part's drawings to match its new name. Because the drawing then keeps the model's name, Create Print after a rename finds and opens it instead of making a second print.

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (which drawings belong to the model, their new names, the `PartRenamer` step, result text)
- `src/InventorAddin.Core/Drawing/PrintPlan.cs` (only to share the "print of this model" rule from task 057)
- `src/InventorAddin/Commands/RenameService.cs`
- `src/InventorAddin.Core/ViewModels/RenameViewModel.cs` only if the result text needs it
- The matching tests under `tests/InventorAddin.Core.Tests/`

## Acceptance criteria

- [ ] A Core rule, for example `RenamePlan.DrawingsOfModel(string oldModelFullFileName, IEnumerable<string> files)`: the `.dwg` and `.idw` files among `files` that sit in the model's folder or its `Prints` folder (`PrintPlan.PrintsFolderName`) and whose file name without extension equals the model's old one, ignoring case. Each comes with its new path: same folder, the model's new file name without extension, the drawing's own extension. Tested, including a drawing in another folder with the same name (not included), `PRINTS` in another case, and an `OldVersions` copy (already excluded by the search)
- [ ] The renamed file's own folder and its `Prints` folder are always searched. Check whether `SearchFolders` and `ListFilesToSearch` already cover them (the file's folder or a parent of it is always listed with subfolders) and add a test that pins it; change code only if it does not
- [ ] `PartRenamer`: a drawing of the model that uses the renamed file is updated as task 058 updates every file and then **saved under its new name**. The target does this in one step, for example `IRenameTarget.UpdateAndRenameDrawing(drawing, newDrawing, oldModel, newModel)`:
  - closed drawing: open invisibly, replace the reference, `Dirty = true`, `SaveAs(newDrawing, false)`, `Close(true)`, delete the old drawing file
  - open drawing: replace the reference if needed, `SaveAs(newDrawing, false)`, `Close(true)` (spike 052: the old `.dwg` stays locked while the document is open), delete the old file, then `Documents.Open(newDrawing, true)` so Aidan's window comes back
- [ ] If a file with the new drawing name already exists (on disk or open), that drawing is only updated as task 058 does, keeps its name, and the result says so as a problem note. Tested
- [ ] A drawing whose rename fails after its reference was saved is reported as a problem; the part's old file is still deleted only under task 058's rules, since the drawing no longer uses it
- [ ] The result lists each drawing renamed ("renamed to …") among the updated files, and the confirmation message before the rename says drawings named after the part will be renamed too
- [ ] `PartRenamer` tests with a fake target: closed and open drawings renamed; a drawing with the name taken; a drawing in the wrong folder left alone; order of steps (the drawing is renamed after the model's `SaveAs`)
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results, section 5, first. `SaveAs(path, false)` on a drawing must keep the drawing's extension (spike 032: a `.dwg` drawing cannot be saved as `.idw`).
- Prints beside the model (made before task 057) are renamed where they are (Default, spec 02); they are not moved into `Prints`.
- A drawing of the model is also a file that uses it, so do not process it twice: the drawing step replaces task 058's ordinary update for those files.
- Reopening an open drawing: `Documents.Open(path, true)` is confirmed (spike 032 and Create Print). Do not `Activate` it; the renamed part stays the active document.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
