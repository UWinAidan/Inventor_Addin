# 048: Mass Update command and window

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Mass Update)
- **Status:** todo
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

- [ ] A small **Mass Update** button on a new **Modelling** panel (after Drawing Tools, before Developer) of the Part, Assembly and ZeroDoc ribbons, with the `MassUpdate` icon
- [ ] The window is built on `MassUpdateViewModel` (041), follows spec 08 and the XAML rules test, and opens with `Settings.WorkFolder` (049) as folder (Aidan, spec 02 question 23), with Browse. The view-model gets the names list from `PersonNames.NamesThatMeanMe` (051)
- [ ] Scan: `RegisterStore.Sync()` first (reporting duplicates as in 039), then each file from `ScanFolder.FilesToScan` opened invisibly by 032's method with `SilentOperation` on, read, and closed; files already open in Inventor (found with a loop over `Documents` comparing `FullFileName` ignoring case; `get_ItemByName` throws when the file is not open) are read from the open document and marked open. A progress line shows `n of N`, and the scan can be cancelled. `SilentOperation` is restored in a `finally`
- [ ] Apply: the confirmation from the view-model; then for each `FileUpdate`, in order: register action through `RegisterStore` (register an existing number as a new row, or assign the next number), then the property writes (Part Number for an assigned number, Designer and Detailer for replacement) with `PropertyWriter`, then save and close. Each file's result goes back to the view-model
- [ ] One file failing does not stop the others; its row shows the error and it is logged
- [ ] A register error (`RegisterBusyException`, `RegisterUnreadableException`: spec 02 question 19) stops Apply before any file changes and shows the message
- [ ] A file whose only update is Register writes nothing to the file, so it is not opened or saved (task 041 follow-up)
- [ ] Folded from task 041's follow-up: a duplicate unregistered number shows in its row at scan time ("Not registered: AWB-10005 is also on a.ipt"), and Register is not offered there; the view-model change and its tests are in scope (`MassUpdateViewModel`, `MassUpdatePlan`)
- [ ] The scan and apply run on the UI thread with the window kept responsive between files (or by 032's advice), never with a COM call off the UI thread
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (question 5): `Documents.Open(path, false)` with `SilentOperation` on, read, `Close(true)` takes about 60 ms a file; a write then `Save()` about 140 ms and sticks. `Documents.Count` counts invisible documents too.
- The scope gains `src/InventorAddin.Core/ViewModels/MassUpdateViewModel.cs` and `src/InventorAddin.Core/Numbering/MassUpdate/MassUpdatePlan.cs` with their tests, for the duplicate row text.

- One Mass Update command for the folder scan and the bulk replacement is a Default (spec 01, question 1; spec 02).
- Open files are skipped for writing, as the spec says; they are only read.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Copy a few old test files into a test folder with an `OldVersions` subfolder. Scan it: the `OldVersions` files are not listed, each file shows its number state and whether Designer or Detailer is your login name
  2. Tick Replace names on two files, Apply: both saved with Designer and Detailer from Settings
  3. Tick Assign on a file without a number: it gets the next number, the register has its row, the file name is unchanged
  4. Give a file `AWB-10900` by hand in Inventor, scan, tick Register: the register gains that number for the file
  5. Keep one file open in Inventor during the scan: it is listed as open and offers no action
  6. Cancel a scan part way: the window stays usable

## Follow-ups

Things noticed but not done.
