# 048: Mass Update command and window

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Mass Update)
- **Status:** done (add-in not compiled)
- **Depends on:** 032 (its Results must be filled in), 036, 039, 041, 042, 049, 051
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files)

## Goal

A Mass Update button scans a folder of old files, shows each file's number and Designer state, and on the files ticked registers existing numbers, assigns new ones, and replaces the Windows user name in Designer and Detailer, saving each file.

## Before starting

Read the Results of task 032, questions 3 and 5. If they are empty, stop.

## Scope

- `src/InventorAddin/Commands/MassUpdateCommand.cs` (new)
- `src/InventorAddin/Extraction/FileScanner.cs` (new: opens a file invisibly, reads a `ScannedFile`, closes it)
- `src/InventorAddin/UI/MassUpdateWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`CommandNames.MassUpdate`, and a Modelling panel id and name if not there), `RibbonLayout.cs` and its tests

## Acceptance criteria

- [x] A small **Mass Update** button on a new **Modelling** panel (after Drawing Tools, before Developer) of the Part, Assembly and ZeroDoc ribbons, with the `MassUpdate` icon
- [x] The window is built on `MassUpdateViewModel` (041), follows spec 08 and the XAML rules test, and opens with `Settings.WorkFolder` (049) as folder (Aidan, spec 02 question 23), with Browse. The view-model gets the names list from `PersonNames.NamesThatMeanMe` (051)
- [x] Scan: `RegisterStore.Sync()` first (reporting duplicates as in 039), then each file from `ScanFolder.FilesToScan` opened invisibly by 032's method with `SilentOperation` on, read, and closed; files already open in Inventor (found with a loop over `Documents` comparing `FullFileName` ignoring case; `get_ItemByName` throws when the file is not open) are read from the open document and marked open. A progress line shows `n of N`, and the scan can be cancelled. `SilentOperation` is restored in a `finally`
- [x] Apply: the confirmation from the view-model; then for each `FileUpdate`, in order: register action through `RegisterStore` (register an existing number as a new row, or assign the next number), then the property writes (Part Number for an assigned number, Designer and Detailer for replacement) with `PropertyWriter`, then save and close. Each file's result goes back to the view-model
- [x] The row and count text in `MassUpdateViewModel` says "your name" instead of "Windows user name" (for example "Your name as Designer", "7 with your name"), because 051 also matches the Other names from Settings (Default); its tests updated
- [x] One file failing does not stop the others; its row shows the error and it is logged
- [x] A register error (`RegisterBusyException`, `RegisterUnreadableException`: spec 02 question 19) stops Apply before any file changes and shows the message
- [x] A file whose only update is Register writes nothing to the file, so it is not opened or saved (task 041 follow-up)
- [x] Folded from task 041's follow-up: a duplicate unregistered number shows in its row at scan time ("Not registered: AWB-10005 is also on a.ipt"), and Register is not offered there; the view-model change and its tests are in scope (`MassUpdateViewModel`, `MassUpdatePlan`)
- [x] The scan and apply run on the UI thread with the window kept responsive between files (or by 032's advice), never with a COM call off the UI thread
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (question 5): `Documents.Open(path, false)` with `SilentOperation` on, read, `Close(true)` takes about 60 ms a file; a write then `Save()` about 140 ms and sticks. `Documents.Count` counts invisible documents too.
- The scope gains `src/InventorAddin.Core/ViewModels/MassUpdateViewModel.cs` and `src/InventorAddin.Core/Numbering/MassUpdate/MassUpdatePlan.cs` with their tests, for the duplicate row text.

- One Mass Update command for the folder scan and the bulk replacement is a Default (spec 01, question 1; spec 02).
- Open files are skipped for writing, as the spec says; they are only read.

## Verification

Filled in by the implementer.

- **Ran:** cloud session (`CLAUDE_CODE_REMOTE=true`). `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1861 passed, 0 failed (1796 before the task). `src/InventorAddin` was not built: it cannot be built in the cloud. The XAML rules test reads the new window and passes; it was checked to fail when a `Margin` is typed into it.
- **Not compiled (changed under `src/InventorAddin`):**
  - `Commands/MassUpdateCommand.cs` (new)
  - `Extraction/FileScanner.cs` (new)
  - `UI/MassUpdateWindow.xaml` and `.xaml.cs` (new)
  - `UI/RibbonSetup.cs` (registers the command)
  - `UI/Theme/Styles.xaml` (four list styles: `ListScroller`, `ListItem`, `InlineCheckBox`, `SideIdentifierText`)
  - `Commands/InventorNewDocument.cs` (also implements `IMassUpdateDocument`; same three members it already had)
- **Inventor API members not confirmed:** none beyond what spike 032 and existing code use. Every Inventor member used is already used elsewhere: `Documents` enumeration and `Document.FullFileName` (RenameService), `Application.SilentOperation` and `Documents.Open(path, false)` (RenameService, spike 032), `Document.Close(true)`, `Document.Save()` and `TransactionManager.StartTransaction((_Document)doc, ...)` (InventorNewDocument), `PropertyReader.GetString`, `DocumentKinds.Of`, `ModelExtractor.IsFileReadOnly`, `CommandTypesEnum.kFileOperationsCmdType` (NewPartCommand). WPF: `Microsoft.Win32.OpenFolderDialog` (NewPartWindow), `Dispatcher.Invoke(DispatcherPriority.Background, ...)` (RenameService). Not confirmed by anyone: whether a part loaded by an assembly that Mass Update opened stays in `Documents` after the assembly is closed (see Follow-ups); the code is written so it does not matter.
- **Choices made (Defaults for the parent to log):**
  - **Order of updates:** updates that register an existing number run before the rest, otherwise in plan order, so a number assigned in the same run can never take a number a ticked file already carries.
  - **Open before assign:** a file that must be changed is opened before its number is assigned, so a file that cannot be opened costs no number.
  - **Register failure part way** (lock busy, unreadable row, I/O error, sequence full) stops the run: that file shows the error, the rest show "Not changed: Apply stopped because the register could not be used.", and the message is shown. A register failure at the start (the sync) stops before any file changes and Apply stays enabled.
  - **A number registered meanwhile** (by another PC, between scan and Apply) is not added again; the row says "Not registered: AWB-10900 is already in the register." and any name replacement still happens.
  - **Scan with a register that cannot be synced** still scans, with this PC's copy, and says so; Apply then stops.
  - **"Open in Inventor"** means open when the command started (listed once; the window is modal). Read-only files fail at Apply with "The file is read-only." before any number is taken; Register-only on a read-only file still works.
  - **Wording:** the row text is "Has a number; your name as Designer" (lower case after the semicolon); counts "7 with your name".
  - **Scan-time duplicates:** the row reads "Not registered: AWB-10005 is also on a.ipt"; it still offers Replace names but not Register (or Assign). `ReportResults` no longer adds the plan's duplicate text, since the window can no longer tick Register on a duplicate.
  - **After Apply** reports results, Apply is disabled until the next scan, so an Assign is never applied twice.
  - **Scan cancel** keeps the rows read so far and says "Scan stopped after n of N files."
- **Outside the listed files** (needed by the criteria, flagged for review): `RegisterStore.RegisterExisting` and `RegisterExistingResult` (Core had no way to register a number a file already carries), `MassUpdateRunner.cs` (the scan and apply steps in Core, with tests, following `NewPartCreator` and `PartRenamer`), `InventorNewDocument.cs`, and the four styles in `Styles.xaml` (spec 08 has no style for a list).
- **Manual checklist for Inventor:**
  1. Build on Windows (`dotnet build InventorAddin.slnx`), start Inventor. With no document open, the **AWB Addin** tab shows a **Modelling** panel with a small **Mass Update** button and its icon, after CAD Automation; in a part and an assembly it sits after Drawing Tools and before Developer. Not on the Drawing ribbon.
  2. Copy a few old test files into a test folder with an `OldVersions` subfolder. Click **Mass Update**: the folder box holds the Work folder from Settings. Browse to the test folder, press **Scan**. The footer counts "Reading n of N"; the `OldVersions` files are not listed; each file shows its name, Part Number, "Has a number" / "Not in the register" / "No number", and "your name as Designer" (or Detailer) where that is your login name or one of the Other names from Settings. The counts line reads "N files, x to register, y to number, z with your name".
  3. Tick **Replace names** on two files, **Apply...**: the confirmation says "Change 2 files? ...". Yes: both rows say Done; open each and check iProperties: Designer and Detailer hold the Default designer.
  4. Tick **Assign** on a file without a number, Apply: it gets the next number as Part Number, the register has its row (with the file's path), and the file name is unchanged.
  5. Give a file `AWB-10900` by hand in Inventor, save and close it, scan, tick **Register** only, Apply: the register gains that number for the file, and the file's modified time does not change (it was not opened or saved).
  6. Copy that file in Explorer before registering, scan: the second copy reads "Not registered: AWB-10900 is also on <first file>" and has no Register box.
  7. Keep one file open in Inventor during the scan: it is listed as "Open in Inventor; close it first." and offers no tick box.
  8. Scan a folder with many files and press **Stop** part way: the scan stops after the current file, the rows read so far are listed, the footer says "Scan stopped after n of N files.", and the window stays usable. Try to close the window during a scan: it stops the scan instead of closing.
  9. Open `part-register.csv` in Excel and keep it open (or create `part-register.lock` by hand in the register folder), tick something, Apply: a message says the register is busy or cannot be read, and no file changes.
  10. Watch for any dialog flashing up while files are opened (SilentOperation), and check the window in Inventor's dark and light themes (the list's scroll bar is WPF's default; see Follow-ups).

## Follow-ups

Things noticed but not done.

- **Leftover documents.** When Mass Update opens an assembly invisibly and closes it, the parts it loaded may stay in `Documents` (Inventor keeps unreferenced documents until `Documents.CloseAll(true)`; not confirmed). Mass Update does not mistake them for files the user has open, because it lists open documents once at start. But a part left loaded by an assembly the user closed earlier is in that list, so Mass Update shows it as "Open in Inventor" although nothing is visibly open. A Windows check, and maybe `Documents.CloseAll(true)` after a scan, would settle it.
- **Scroll bar colours.** The file list's scroll bar uses WPF's default look, which does not follow the dark theme. Spec 08 has no style for scroll bars or lists; the four list styles added here (`ListScroller` 320 high, gaps 9 and 12) are not in spec 08's tables either.
- **Unticked carriers.** An assigned number can still equal an unregistered number carried by a scanned file that was not ticked (or a file outside the folder); only ticked Register actions run first. Spec 02 accepts the register as the source of numbers; Aidan may want Assign to skip numbers seen in the scan.
- **Scan off the network.** The register sync before a scan runs on the UI thread, as for New Part: up to 3 s probe plus up to 5 s lock wait with the window not redrawn. Listing the folder runs on a background task (60 s timeout) but the window is not pumped while it waits.
- Spec 02 "Commands: Mass Update" still says the list shows whether Designer or Detailer "equals the Windows user name"; the "your name" wording is recorded in the "Windows user name as Designer or Detailer" section only.
