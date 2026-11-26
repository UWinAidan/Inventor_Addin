# 202: Batch Print command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 3)
- **Status:** review
- **Depends on:** 184, 191, 201
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

An assembly button that makes one PDF of the assembly's drawing and every drawing under it, saved next to the assembly.

## Scope

- `src/InventorAddin/Drawing/PdfExporter.cs` (spike 184, question 4; a merge library only if the spike chose one, with its licence named in the PR).
- `src/InventorAddin/Commands/BatchPrintCommand.cs` + ribbon button (assembly); progress and the report at the end.

## Acceptance criteria

- [ ] A test assembly with three printed parts and one without: one PDF, the assembly first, then the three; the report names the part with no print.
- [ ] An existing PDF is replaced only after asking.
- [ ] Drawings opened for printing are closed again; nothing is saved.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 1):
  - The PDF add-in: `ApplicationAddIns` item with `ClassIdString` `{0AC6FD96-2F4D-42CE-8BE0-8AEA580399E4}`, cast to `TranslatorAddIn`.
  - Setup: a `TranslationContext` with `Type = kFileBrowseIOMechanism`, `HasSaveCopyAsOptions(doc, context, options)` to fill the defaults, then `options.Value["Sheet_Range"] = kPrintAllSheets` and `All_Color_AS_Black = 0`.
  - Export: a `DataMedium` with `FileName`, then `SaveCopyAs(doc, context, options, medium)`. One PDF per drawing, every sheet.
  - Hidden drawings: `Documents.Open(path, false)` opens without showing; `Close(true)` after.
  - There is no append option, so merge the files with PDFsharp (MIT; Default, DECISIONS.md 2026-11-22) and name the licence in the PR.

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults (task 202)

- The assembly itself being a Content Center or library file is refused up front ("nothing to print"); the root is checked by folder against the active project's library paths.
- A part is library when `PartComponentDefinition.IsContentMember` is true or its file lies under one of `DesignProject.LibraryPaths` (`ReferencedFileDescriptor` has no `LocationType`, so the folder rule stands in). A library sub-assembly's children are not read.
- Suppressed occurrences and virtual components (no file) are left out of the tree.
- The "replace the PDF?" question is asked before any drawing is opened; No ends the command with no report.
- A drawing that fails to open or export is recorded and the rest still print; the report says "Printed N of M" and names each failure with its reason. If every drawing fails, no PDF is made. A drawing the user already has open is exported where it is and left open; the others are opened hidden and closed with `Close(true)`.
- The PDF is merged into a temporary name beside the target and moved over it, so a failed merge leaves an old PDF as it was. A locked PDF gives a "close it in your PDF viewer" message.
- Button: assembly ribbon only, Drawing panel next to Open Drawing, large; command type `kFileOperationsCmdType` (it writes a file, edits no document).
- PDFsharp 6.1.1 (MIT licence; name it in the PR) is referenced from `src/InventorAddin/InventorAddin.csproj`, pinned at 6.1.1 (not 6.2.0) because 6.2.0 pulls Microsoft.Extensions.* 8.0, which clashes with Inventor's own 6.0.0.0 copies in its default load context. The build output has Microsoft.Extensions.DependencyInjection(.Abstractions), Logging(.Abstractions), Options and Primitives, all assembly version 6.0.0.0 (file 6.0.21), the same as Inventor's Bin; plus PdfSharp.dll, .Charting, .Quality, .Snippets, .System, .WPFonts (6.1.1), which Inventor does not ship.
- Library folders: a relative `ProjectPath.Path` is resolved against the .ipj's folder (`BatchPrintPlan.ResolveProjectFolder`, tested). Virtual components are skipped by an explicit `VirtualComponentDefinition` check as well as the missing file. Any merge failure shows a plain message (`PdfMergeFailedMessage`) and the old PDF stays; the temp folder is `<IdPrefix>BatchPrint`.

## Files

Core: `Drawing/BatchPrintPlan.cs` (failures, `ReportText(failures)`, messages, `IsUnderAnyFolder`), `Ribbon/RibbonModels.cs`, `RibbonLayout.cs`, `CommandDocuments.cs`. Add-in: `Commands/BatchPrintCommand.cs`, `Drawing/PdfExporter.cs`, `Drawing/BatchPrintTreeReader.cs` (not in the scope list; the tree read is its own thin class), `UI/RibbonSetup.cs`, `InventorAddin.csproj`.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: Build succeeded, 0 errors. `dotnet test`: 5740 passed, 0 failed (after the review fixes).
- **Not compiled (changed under `src/InventorAddin`):** none; all compiled on Windows. Not run inside Inventor: nothing under `src/InventorAddin` has been run.
- **Inventor API members not confirmed by a run:** all exist by reflection on the 2026 interop and the build. The PDF calls follow spike 184 round 1 but are unrun in the add-in: `TranslatorAddIn.HasSaveCopyAsOptions[doc, context, options]` (C# indexer form), `options.Value["Sheet_Range"] = PrintRangeEnum.kPrintAllSheets` (an enum value, the spike's wording), `All_Color_AS_Black = 0`, and `DataMedium.FileName` only (no `MediumType`, as in the spike). `ComponentOccurrence.SubOccurrences` for a sub-assembly occurrence, `DefinitionDocumentType`, `Suppressed`, `DesignProject.LibraryPaths`, `Application.StatusBarText`, `Application.SilentOperation` during the batch are existing-use or reflection only.
- **Manual checklist for Inventor:**
  1. Assembly with three parts that have prints (in `Prints\`) and one part without, saved. Open it, click Batch Print (Drawing panel, assembly ribbon). Expect: a status bar count, then a report "Printed 4 drawings" (the assembly's own print plus three) naming the part with no drawing, and `<assembly name>.pdf` beside the assembly with the assembly's sheets first, then the three parts', every sheet, in colour.
  2. Click it again: it asks to replace the PDF; No leaves the file and shows nothing; Yes replaces it.
  3. Open the PDF in a viewer and click Batch Print: expect the "close it in your PDF viewer" message and the old PDF untouched.
  4. After the run, the Inventor window list shows no drawings that were not open before; none is marked as changed or saved (check the files' modified times).
  5. Open one of the part drawings first, then run: it is still open afterwards, and still unchanged.
  6. Add a Content Center part and a sub-assembly: the Content Center part is counted as "Skipped 1 library part"; the sub-assembly's drawing comes before its children's, in browser order.
  7. Rename one print to break it (or make one print unopenable): the report says "Printed N of M" and names it with the reason.
  8. Run it on a part, a drawing and an unsaved assembly: each gets a plain message.
  9. Open a library assembly (under a Libraries path of the project): refused with the library message.
  10. The merge works inside Inventor: after step 1, the log (`awbaddin.log`) has no FileLoadException or other error mentioning Microsoft.Extensions.* or PdfSharp.
  11. After a run with a broken drawing (step 7), no hidden drawing is left in the Documents list (Inventor's window list and a check from the API show only the documents that were open before).

## Follow-ups

- PDFsharp licence (MIT) to be named in the PR.
- No cancel button or progress bar: progress is the status bar text only. Add a cancel if a big assembly feels slow.
- Imperial and metric drawings export in one go; page sizes in the merged PDF follow each drawing's sheet.
