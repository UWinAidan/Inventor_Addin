# 043: New Part command and window

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Templates; Written on creation; Commands: New Part)
- **Status:** done (add-in not compiled)
- **Depends on:** 032 (its Results must be filled in), 036, 039, 040, 042, 049
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

- [x] A small **New Part** button on the CAD Automation panel of the Part, Assembly and ZeroDoc ribbons, first in the panel, with the `NewPart` icon from 042. Layout tests updated
- [x] The command is not a `DocumentEditCommand` (there may be no active document); pick the command type that matches creating a document and say why in a comment
- [x] The dialog is built on `NewPartViewModel` (040), follows spec 08 and the XAML rules test, and gets its default folder from the active document's folder, or `Settings.WorkFolder` (049) when no saved document is open (Aidan, spec 02 question 23). The project workspace is not used: spike 032 found it blank. With both blank the folder is empty and Browse is needed. Browse sits beside the folder box with the shared `SideButton` style, and never calls `Directory.Exists` on the UI thread
- [x] On Create, in this order: find the template (a missing template stops with a message naming the kind and units, before any number is assigned); check the target file does not exist (using a number placeholder is not enough: check again after assigning and stop with a message if it does); assign the number through `AddinServices.RegisterStore`; create the document from the template; write `CreationProperties.Build(...)` with `PropertyWriter`; save to `PartFileName.Build(...)` in the chosen folder
- [x] A failure after the number was assigned shows a message saying which number was used and that it stays in the register, logs the exception, and closes any half-made document without saving
- [x] Offline assignment and duplicates are reported as in 039
- [x] The new document is left open and active
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (Results questions 1 to 3):
  - Template: `InventorHost.App.FileManager.GetTemplateFile(DocumentTypeEnum, SystemOfMeasureEnum, DraftingStandardEnum.kDefault_DraftingStandard, object subType)`. Metric is `kMetricSystemOfMeasure`, imperial `kEnglishSystemOfMeasure`. Sheet metal passes the subtype `"{9C464203-9BAE-11D3-8BAD-0060B0CE6BB4}"`; parts and assemblies pass `Type.Missing`. A returned `""` means no template: stop with the message. Put the lookup in `Templates.cs` so 044 adds the drawing case there.
  - Create: `Documents.Add(DocumentTypeEnum.kPartDocumentObject or kAssemblyDocumentObject, templatePath, true)`. Write properties with `PropertyWriter` (no transaction is needed on a new document; one does no harm). Save with `doc.SaveAs(fullPath, false)`; a comma in the name is accepted.
  - `Documents.Add` can take 2 to 7 s the first time; show a wait cursor while creating.

- Every Inventor member used must come from 032's Results or existing code; list any other in Verification.
- `Documents.Add` takes a `DocumentTypeEnum`; map from `NewFileKind` in the add-in. The sheet metal subtype is in 032's Results.

## Verification

Filled in by the implementer.

- **Ran:** (cloud session, `CLAUDE_CODE_REMOTE=true`)
  - `dotnet build src/InventorAddin.Core`: succeeded, no warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1497 passed, 0 failed (the XAML rules test reads the new `NewPartWindow.xaml`).
  - New tests: `Numbering/NewPartCreatorTests.cs` (the Create steps, their order, every stop before and after the number is assigned, the default folder, the notices) and three `RegisterStoreTests` for the new `Assign(Func<PartNumber, RegisterRequest>)`. `RibbonLayoutTests` updated for New Part (first on CAD Automation on ZeroDoc, Part and Assembly; ZeroDoc now has a CAD Automation panel without developer tools).
- **Beyond the listed files (Core):**
  - `src/InventorAddin.Core/Numbering/NewPartCreator.cs` (new): the Create steps and messages, so the order and failure handling are tested in Core rather than written as `if`s in the add-in. The add-in supplies Inventor through `INewPartDocuments`.
  - `src/InventorAddin.Core/Numbering/RegisterStore.cs`: an `Assign` overload that builds the register row from the number about to be assigned. The register's `FilePath` is the new file's full path, which holds the number, so the old `Assign(RegisterRequest)` could not record it. The overload also lets the "file already exists" check run with the real number before the row is written, so a name already on disk costs no number. The check runs again after assigning, and stops naming the number, as the acceptance criteria ask.
- **Not compiled (changed under `src/InventorAddin`):** `Commands/NewPartCommand.cs`, `Inventor/Templates.cs`, `UI/NewPartWindow.xaml`, `UI/NewPartWindow.xaml.cs`, `UI/RibbonSetup.cs`. None of it has been built.
- **Inventor API members not confirmed:**
  - `CommandTypesEnum.kFileOperationsCmdType`: not used in the repo before and not in 032's Results; the reviewer found it in Autodesk's API reference (value 4). If it does not compile, the fallback is `kQueryOnlyCmdType`.
  - `Documents.Add(...)` assigned to a `Document` variable: the reviewer found the return type `Document` in Autodesk's API reference; 032 did not record it.
  - Everything else comes from 032's Results or existing code: `FileManager.GetTemplateFile(DocumentTypeEnum, SystemOfMeasureEnum, DraftingStandardEnum.kDefault_DraftingStandard, object)`, `kMetricSystemOfMeasure`, `kEnglishSystemOfMeasure`, `Documents.Add(DocumentTypeEnum, string, bool)`, `Document.SaveAs(string, bool)`, `Document.Close(bool)`, `TransactionManager.StartTransaction((_Document)doc, name)`, `Application.ActiveDocument`, `Document.FullFileName`.
  - Not Inventor, but untested: whether `System.Windows.Forms.Cursor.Current = Cursors.WaitCursor` shows over Inventor's window while `Documents.Add` blocks.
- **Manual checklist for Inventor:**
  1. With no document open, the ZeroDoc ribbon's AWB Addin tab has a CAD Automation panel with a small New Part button (NewPart icon). With a part or assembly open, New Part is the first button on CAD Automation. Drawings have no New Part button.
  2. New Part from ZeroDoc: the dialog opens with the Work folder from Settings as Folder, Metric (or the New file units setting), Part, blank part type, and the status line "Enter a part name."; Create is disabled.
  3. With a saved part open, New Part opens with that part's folder. Clear the Work folder in Settings, close all documents: the folder is empty, the status line says "Choose a folder.", and Browse opens a folder picker.
  4. Name `test bracket`, part type M, Create: the wait cursor shows, then a part opens, active, named `AWB-<next number>,test_bracket.ipt` and saved in the folder; iProperties hold Part Number, Description `test bracket`, Designer and Detailer from Settings, Part Type M; the register has its row with that full path.
  5. Sheet metal part, Imperial: the sheet metal environment and inch units.
  6. Assembly: part type pre-fills A; the file is `.iam`.
  7. A name with `?`: Create is disabled and the status line names the character.
  8. With the NAS disconnected: the number is assigned offline and a message says it will be added to the shared register at the next sync.
  9. Make a file named with the next number (copy any part to `AWB-<next number>,test_bracket.ipt`) and Create `test bracket` again: a message says the file already exists and nothing was assigned; the register has no new row.
  10. Type a folder that does not exist and Create: a message says the folder does not exist or cannot be reached; nothing is assigned.
  11. Part Properties on the new part shows the number with no note, and the header shows the name with spaces.
  12. Edit > Undo on the new part once: the creation properties are one undo step (they were written in one transaction). Check that this is acceptable; the file on disk keeps them.

## Follow-ups

Things noticed but not done.

- Spec 08's "Ribbon icons" list should name New Part (task 042's follow-up asks each command task to add its icon there). Spec 08 was not in this task's files.
- The New Part dialog has no section in spec 08's "The windows" list. This task laid it out as File (File type, Units), Identity (Part name, Part type) and Location (Folder with Browse, File name preview), with validation messages in the error style. Spec 08 could record that.
- The status line shows "Enter a part name." in the error style as soon as the dialog opens. A muted style until the user has typed may read better; it needs a flag on `NewPartViewModel`.
- The TODO item from task 040 (New Part's folder checked only for blank and length) is covered at Create time: `NewPartCreator` refuses a folder that is not a full path, or that does not answer within 3 s, before assigning. The dialog itself still accepts any non-blank text until Create.
- `RegisterPartNumberAssigner` (Generate) could use the new `Assign(Func<...>)` overload too; it does not need it today because the file's path is known before the number.
