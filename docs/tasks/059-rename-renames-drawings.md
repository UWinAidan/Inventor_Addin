# 059: Rename renames the part's drawings

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename"; "Commands: Create Print")
- **Status:** done (Core tested; add-in compiled locally, not tested in Inventor)
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

- [x] A Core rule, for example `RenamePlan.DrawingsOfModel(string oldModelFullFileName, IEnumerable<string> files)`: the `.dwg` and `.idw` files among `files` that sit in the model's folder or its `Prints` folder (`PrintPlan.PrintsFolderName`) and whose file name without extension equals the model's old one, ignoring case. Each comes with its new path: same folder, the model's new file name without extension, the drawing's own extension. Tested, including a drawing in another folder with the same name (not included), `PRINTS` in another case, and an `OldVersions` copy (already excluded by the search)
- [x] The renamed file's own folder and its `Prints` folder are always searched. Check whether `SearchFolders` and `ListFilesToSearch` already cover them (the file's folder or a parent of it is always listed with subfolders) and add a test that pins it; change code only if it does not
- [x] `PartRenamer`: a drawing of the model that uses the renamed file is updated as task 058 updates every file and then **saved under its new name**. The target does this in one step, for example `IRenameTarget.UpdateAndRenameDrawing(drawing, newDrawing, oldModel, newModel)`:
  - closed drawing: open invisibly, replace the reference, `Dirty = true`, `SaveAs(newDrawing, false)`, `Close(true)`, delete the old drawing file
  - open drawing: replace the reference if needed, `SaveAs(newDrawing, false)`, `Close(true)` (spike 052: the old `.dwg` stays locked while the document is open), delete the old file, then `Documents.Open(newDrawing, true)` so Aidan's window comes back
- [x] If a file with the new drawing name already exists (on disk or open), that drawing is only updated as task 058 does, keeps its name, and the result says so as a problem note. Tested
- [x] A drawing whose rename fails after its reference was saved is reported as a problem; the part's old file is still deleted only under task 058's rules, since the drawing no longer uses it
- [x] The result lists each drawing renamed ("renamed to …") among the updated files, and the confirmation message before the rename says drawings named after the part will be renamed too
- [x] `PartRenamer` tests with a fake target: closed and open drawings renamed; a drawing with the name taken; a drawing in the wrong folder left alone; order of steps (the drawing is renamed after the model's `SaveAs`)
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results, section 5, first. `SaveAs(path, false)` on a drawing must keep the drawing's extension (spike 032: a `.dwg` drawing cannot be saved as `.idw`).
- Prints beside the model (made before task 057) are renamed where they are (Default, spec 02); they are not moved into `Prints`.
- A drawing of the model is also a file that uses it, so do not process it twice: the drawing step replaces task 058's ordinary update for those files.
- Reopening an open drawing: `Documents.Open(path, true)` is confirmed (spike 032 and Create Print). Do not `Activate` it; the renamed part stays the active document.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, only the 4 existing CS8600 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 2101 passed, 0 failed (baseline 2055, plus 46 new test cases), first run, no timeouts. The new tests use a fake target or fixed paths; the one that lists a real folder uses the synchronous `FilesToSearch`, with no timeout.
- **Core:** `PrintPlan.IsPrintOf(model, drawing)` is Create Print's "print of this model" rule (`ExistingPrintCandidates`, compared ignoring case and slash direction). `RenamePlan.DrawingsOfModel(oldModel, newModel, files)` uses it and returns `DrawingRename(Old, New)` records; it takes the new model path too, since the new drawing name needs it. Search coverage needed no code change: `SearchFolders` always lists the model's folder or a parent, and the listing recurses, so `Prints` is always searched (pinned by `FilesToSearch_TheRenamedFilesFolderAndItsPrintsFolder_AreAlwaysSearched` and `SearchFolders_AModelInsideACadFolder_IsCoveredWithItsPrintsFolder`). `IRenameTarget` gains `FileExists` and `UpdateAndRenameDrawing`, which returns `DrawingRenameUpdate?` (null when the drawing does not use the model; otherwise any reason it was not renamed, its old file not deleted, or it was not reopened). `PartRenamer` sends each drawing of the model to `UpdateAndRenameDrawing` in place of `UpdateReferences`, in the same loop, after the model's `SaveAs`. When the new name is taken, or the check throws, it calls `UpdateReferences` and adds a problem note. Each renamed drawing's new path is added to the files handled, so `SaveRepointedDocuments` does not save the reopened drawing a second time. `RenameFileResult.RenamedTo` and `RenameResult.DrawingsRenamed` are new. The result line reads `Updated: …\plate.dwg, renamed to BASE_PLATE,….dwg`. The summary adds ", N drawing(s) renamed". The confirmation lists the drawings to be renamed when the search found any. The last log line adds "N drawings renamed".
- **Fail-safe order (a choice within the brief):** `UpdateAndRenameDrawing` first updates and saves the drawing under its **old** name exactly as task 058 does (`ReplaceReference`, `Dirty = true`, `Save`). Only then does it run `Dirty = true`, `SaveAs(new, false)`, `Close(true)` and delete the old file. This costs one extra save. In return, a failure in `SaveAs`, the delete, or the reopen still leaves every drawing file on disk pointing at the renamed model, so the part's old file can go under task 058's rules (AC 5). Any failure before that save throws, the drawing counts as not updated, and the old part file is kept. The open drawing is closed and reopened as the AC requires (spike 052: the old `.dwg` stays locked while open). This is the one place Rename closes a document the user had open. It loses nothing, because the drawing was just saved and `SaveAs` made it the new file, and it is opened again with `Documents.Open(new, true)`. `InventorHost.ActiveDocument` is read before that open; if the active document changed, the one read before is activated again (`Activate()`). This is the window's document, not the renamed one, so a part renamed while edited in place inside an assembly returns Aidan to the assembly. The drawing itself is never activated.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `Commands/RenameService.cs`. `InventorRenameTarget` gets `FileExists` (the existing `TargetExists`: on disk or open), `UpdateAndRenameDrawing` and `Reopen`. The open-or-open-invisibly code from `UpdateReferences` moved into `OpenOrFind(path, out wasOpen)`, and both methods use it. `UpdateReferences` behaves as before.
- **Inventor API members not confirmed:** none by name. All are confirmed by spike 052 or existing code: `Document.SaveAs(string, bool)`, `Dirty` set, `Save`, `Close(bool)`, `Application.ActiveDocument` (via `InventorHost.ActiveDocument`), `Documents.Open(path, true)` (Create Print, Save As Copy), and `Document.Activate()` (Create Print). Not confirmed by a test: whether `Documents.Open(path, true)` activates the reopened drawing (the code handles either case); `SaveAs` straight after `Save` on a closed drawing opened invisibly (spike 052 did `SaveAs` on such a drawing, but without a `Save` before it; `Dirty = true` is set before the `SaveAs` either way).
- **Manual checklist for Inventor:**
  1. Close Inventor, build and deploy (`dotnet build InventorAddin.slnx`), start Inventor.
  2. In a scratch folder inside the work folder: a numbered part `BRACKET,AWB-1xxxx.ipt`, its print from Create Print (`Prints\BRACKET,AWB-1xxxx.dwg`), an old-style print beside it (`BRACKET,AWB-1xxxx.idw`, made by hand from an `.idw` template) and an assembly using the part. Also copy the `.dwg` print into another subfolder (`Other\BRACKET,AWB-1xxxx.dwg`). Save all and close all.
  3. Open only the part. Part Properties, Rename, type `BRACKET 2`. The confirmation lists both drawings ("… to BRACKET_2,AWB-1xxxx.dwg" and ".idw"), not the copy in `Other`. Confirm.
  4. The result lists `Prints\BRACKET,AWB-1xxxx.dwg, renamed to BRACKET_2,AWB-1xxxx.dwg` and the `.idw` the same way, the assembly as updated, and `Other\BRACKET,AWB-1xxxx.dwg` as updated without "renamed to". The summary says "2 drawings renamed". In Explorer: `Prints` holds only `BRACKET_2,AWB-1xxxx.dwg`; the `.idw` beside the part is renamed where it is (not moved into `Prints`); the old part file is gone.
  5. With the part active, click Create Print: it offers to open the existing print `Prints\BRACKET_2,AWB-1xxxx.dwg` and does not make a second print.
  6. Open each drawing: no missing-reference dialog, the view shows the renamed part.
  7. Open drawing: open the part and its `Prints` print (both visible, saved), go back to the part and rename it again (`BRACKET 3`). The drawing window closes and comes back as `BRACKET_3,…dwg`. The part stays the active window. The old `.dwg` is gone from Explorer. Closing everything asks to save nothing.
  8. Name taken: put a file named `BRACKET_4,AWB-1xxxx.dwg` in `Prints` beside the print, then rename the part to `BRACKET 4`. The result has a problem note saying the drawing was updated but not renamed because that name exists. The old print reopens clean on the renamed part, and the old part file is deleted.
  9. Edited in place: open an assembly that uses the part, and the part's `Prints` print (both visible, saved). In the assembly, double-click the part to edit it in place, open Part Properties, click Rename, type `BRACKET 5`, confirm. The drawing window closes and comes back as `BRACKET_5,…dwg`. Aidan is back in the assembly window, not in a separate part window or the drawing. The browser shows the occurrence renamed. Closing everything asks to save nothing.
  10. Check `%APPDATA%\AwbAddin\awbaddin.log`: the last Rename line includes "N drawings renamed".

## Follow-ups

Things noticed but not done.

- When a renamed drawing's old file cannot be deleted (for example, Close failed and the file stays locked), both drawing files remain. Both point at the renamed part, so nothing breaks; the result note asks Aidan to delete the old one by hand.
- Spec 02's Rename bullet could say that a drawing which was open is closed and reopened under its new name, and that it is first saved under its old name with the new reference, so a failed rename never breaks it.
- Task 061 (case-only rename through a temporary name): the drawing step should follow the model through both steps (old to temporary to final). The second step must search for drawings named after the temporary name, not the original search list.
