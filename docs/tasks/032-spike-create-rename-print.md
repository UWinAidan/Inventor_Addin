# 032: Spike: templates, creating and saving, rename, base view (Windows)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Templates; Commands)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows (run by Aidan in a local Claude session on his PC, with Inventor 2026)
- **Parallel-safe with:** every cloud task (it changes no committed source)

## Goal

Settle, in Inventor, the API behaviours that the second M2 run builds on: finding the default templates, creating and saving a new document, updating references when a file is renamed, opening files invisibly to read and write properties, placing a base view, and replacing an occurrence. Record the answers in this file. Tasks 043 to 048 are not run until this file says what works.

## Scope

Throwaway code on a local scratch branch (`spike/032`), never merged. The only committed change is this task file, with the Results section filled in.

Out of scope:

- Any real command, dialog or Core logic. Use the smallest code that answers each question; a temporary button on the Developer panel, or code in the About command, is fine.

## What to find out

Use a scratch folder inside the active project's workspace, with test files made for the spike (no work files). Wrap COM getters in `ComSafe.Get`. Log what you find with `AddinServices.Log.Info` and copy the lines into the Results.

**1. Default templates.**

- Call `InventorHost.App.FileManager.GetTemplateFile(DocumentTypeEnum, SystemOfMeasureEnum, DraftingStandardEnum, object DocumentSubType)` for: part, sheet metal part (subtype `"{9C464203-9BAE-11D3-8BAD-0060B0CE6BB4}"`), assembly and drawing, each with `kMetricSystemOfMeasure` and `kEnglishSystemOfMeasure`, and `kDefault_DraftingStandard`.
- Record each path returned, whether the file exists, and any exception. Record the drawing template's extension (`.idw` or `.dwg`).
- If `GetTemplateFile` fails or returns the wrong units, record `DesignProjectManager.ActiveDesignProject.TemplatesPath` and the template files under it, so the add-in can find them by name instead.

**2. Create, write, save.**

- `Documents.Add(DocumentTypeEnum.kPartDocumentObject, templatePath, true)` with the metric part template. Do the same for a sheet metal part and an assembly.
- Write Part Number, Description and a custom `Part Type` with `PropertyWriter.Apply` (no transaction needed on a new document? record whether a transaction works and whether one is needed).
- `doc.SaveAs(fullFileName, false)` to `AWB-90001,spike_part.ipt` in the scratch folder. A comma in the name: does Inventor accept it? Record the file name Inventor shows afterwards and whether Part Number kept the written value.

**3. Workspace path.**

- Record `DesignProjectManager.ActiveDesignProject.WorkspacePath` (and `FullFileName` of the project) for Aidan's active project. Is it a full path, or relative to the `.ipj`?

**4. Rename and update references** (the main question).

Make a part `A.ipt`, an assembly `Asm.iam` that places it, and a drawing `A.idw` with a view of it. Close all three. Then try to rename `A.ipt` to `B.ipt` so both still open without a missing-reference prompt:

- a. With `A.ipt` open as the active document: `doc.SaveAs(newPath, false)` (the open document becomes `B.ipt`; `A.ipt` stays on disk). Then for each referencing file: open it invisibly (`Documents.Open(path, false)`), find the reference to `A.ipt` in `doc.File.ReferencedFileDescriptors` (or `doc.ReferencedFileDescriptors`), call `ReplaceReference(newPath)`, `Save`, `Close`. Then delete `A.ipt`. Record which members compile and work.
- b. If a fails: copy the file on disk first (`System.IO.File.Copy`), replace the references as above, then delete the old file.
- Set `InventorHost.App.SilentOperation = true` while opening invisibly, and restore it afterwards. Does any dialog still appear?
- If `Asm.iam` is **open** in Inventor during the rename: what happens? Does `ReplaceReference` work on an open document, or must it be closed first?
- Record how long opening, replacing and saving takes for one assembly and one drawing.

**5. Open invisibly to read and write properties** (Mass Update).

- Open 20 to 50 closed parts invisibly one at a time, read Part Number and Designer, close. Record the time per file.
- Open one invisibly, write Designer with `PropertyWriter.Apply`, `Save`, `Close`. Reopen: did the value stick? Does a file open in Inventor show in `Documents` so it can be detected and skipped (`Documents.ItemByName` or a loop comparing `FullFileName`)?

**6. Base view on a new drawing** (Create Print).

- With a saved part open: `Documents.Add(kDrawingDocumentObject, drawingTemplate, true)`, then `sheet.DrawingViews.AddBaseView((_Document)part, point, scale, ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineDrawingViewStyle)` with `point = TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2)` and scale 1. Record the exact signature that compiles (optional arguments), and `Sheet.Width` and `Sheet.Height` (cm) for the default sheet.
- Record `ComponentDefinition.RangeBox` of the part next to the view's `Width` and `Height` at scale 1, so a fit rule can be checked.
- `SaveAs` the drawing beside the part with the part's file name and the template's extension. Write the drawing's Part Number. Record any prompt.

**7. Replace an occurrence** (Save As and Replace).

- In an assembly with two occurrences of one part: `occurrence.Replace(newPath, false)` on one. Does only that occurrence change? Does it keep its constraints? With `true`, do both change?
- Record how the selected occurrence is read: `doc.SelectSet[1]` as `ComponentOccurrence`, and what it is when the user selected in a sub-assembly (a `ComponentOccurrenceProxy`?).

**8. An unreachable register folder.**

- With Tailscale disconnected (or a mapped drive that is offline), time `System.IO.Directory.Exists(@"B:\aidan\AWB_Design_Data")` and `File.ReadAllText` on a file in it. How long until each returns or throws? This sets the 3 second timeout in task 036.

## Acceptance criteria

- [ ] The Results section answers questions 1 to 8, with the exact members and the method that worked
- [ ] Any member that did not compile is listed with the name that did
- [ ] The scratch branch is not merged or pushed to `main`; only this file is committed

## Notes for the implementer

- This runs on Windows with Inventor. Close Inventor before each build (`dotnet build InventorAddin.slnx -tl:off`), then start it and test.
- The member names in the questions are from the Inventor API reference and have not been compiled in this project. The point of the spike is to find which ones work.
- Keep the code you tried, in short, in the Results so tasks 043 to 048 can copy it.

## Results

To be filled in by the spike.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none committed
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** questions 1 to 8 above

## Follow-ups

Things noticed but not done.
