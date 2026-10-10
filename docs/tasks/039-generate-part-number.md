# 039: Part number display and Generate part number in Part Properties

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Part number; The register; Commands: Generate part number); `docs/features/07-part-properties.md`; `docs/features/08-window-style.md` (Part number display)
- **Status:** done (add-in not compiled)
- **Depends on:** 033, 035, 036, 037, 038
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 040, 042

## Goal

Part Properties decides "has a number" by spec 02 (in the format and in the register) and, for a file without one, offers a Generate part number button that assigns the next number from the register and writes it. This is the first time the add-in writes to the register, so it also sets up the register store for the session.

## Scope

- `src/InventorAddin.Core/PartProperties/PartNumberDisplay.cs` (replaced by the new rule, or removed; see criteria)
- `src/InventorAddin.Core/PartProperties/IPartNumberAssigner.cs` (new)
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs`
- `src/InventorAddin/AddinServices.cs` (creates the `RegisterStore` from settings on demand)
- `src/InventorAddin/Commands/PartPropertiesCommand.cs`
- `src/InventorAddin/UI/PartPropertiesWindow.xaml`
- `src/InventorAddin/Extraction/PropertyReader.cs` (delete the unused `Set`, follow-up from task 018)
- tests for each Core file touched

Out of scope:

- Renaming the file (047). The Rename button is not added here.
- Mass registering (041, 048).

## Acceptance criteria

Display

- [x] The view-model takes a `Func<string?, bool> hasNumber` (or an interface with that one method) from the caller; the add-in passes `RegisterStore.HasNumber`, which reads only the local copy
- [x] `IsPartNumberAssigned` is `hasNumber(partNumber)`
- [x] `PartNumberDisplay`: the number formatted by `PartNumber.ToString()` when the Part Number is well formed (registered or not), otherwise `Not assigned`
- [x] New `IsPartNumberUnregistered` (well formed, not in the register) and `PartNumberNote`: `"Not in the register"` when unregistered, otherwise null. The window shows the note in muted text beside the number
- [x] The old file-name comparison rule is gone; `PartNumberDisplay` (the Core class) is removed or reduced to the `NotAssignedText` constant, and its tests are replaced with tests of the new rule
- [x] Remove the unbound `PartNumber`, `Material` and `Finish` view-model properties (follow-up from task 029) if nothing binds them

Generate

- [x] `GenerateCommand`, shown (`CanGenerate` visibility flag) only when `IsPartNumberAssigned` is false and the file is editable; disabled with the note `"Save the file before giving it a part number."` when the file has never been saved
- [x] It calls `IPartNumberAssigner.Assign()` (supplied by the command), which returns the assigned number text, whether it was assigned offline, and any duplicate messages; or throws with a message
- [x] On success the window shows the new number in the Part number row, hides the button, and the status line says `"Part number AWB-10001 assigned and written. Save the document to keep it."`, plus `" Assigned offline; it will be added to the shared register at the next sync."` when offline
- [x] On failure the status line shows the message from the exception (as Apply does) and nothing else changes
- [x] Duplicates reported by the sync are shown once in a message box by the add-in and logged as warnings

Add-in

- [x] `AddinServices` exposes a `RegisterStore` built from `Settings.RegisterFolder` (blank: a store with no shared folder, always offline) and `%APPDATA%\AwbAddin\register`, rebuilt when settings change
- [x] The command's assigner: calls `RegisterStore.Assign` with the part name, part type, kind and file path from the snapshot, **then** writes Part Number with `PropertyWriter` inside one transaction (`RunInTransaction`), so one Undo removes the written number. The register entry stays (rows are never removed); say so in a log line if the write fails after assigning
- [x] Before assigning, the assigner calls `RegisterStore.Sync()` and re-reads `HasNumber`; if the file's number is now registered it refuses with `"This file already has a registered number."` (another PC registered it)
- [x] `PropertyReader.Set` is deleted and nothing else calls it
- [x] The window follows spec 08 and the XAML rules test: the button sits in the Part number row, styled as a secondary button, with access key G
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added while running M2, from the task 036 review: `Sync()` returns `RegisterSyncResult` (not `SyncResult`, which is `PartRegister.Merge`'s). `Assign` and `Sync` can throw `RegisterBusyException` and `RegisterUnreadableException`; show each message to the user and write nothing to the file. If `Assign` throws after the shared file was written, the number may already be in the register; the message should not claim nothing was assigned.

- "Has a number" is Aidan's rule. Showing an unregistered well-formed number with a note and offering Generate is a Default (spec 02, question 13).
- Writing Part Number uses `PartPropertyStorage.PartNumber` and `PropertyWrite.SetText`; `PropertyWriter` already handles standard properties.
- After a successful Generate, the view-model's `_original` snapshot must hold the new Part Number so a later Apply does not see it as a change. Part Number is never in the write plan.
- Any number of things can fail here (NAS, lock, write). Every failure must leave the window usable and say what happened.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, no warnings. `dotnet test tests/InventorAddin.Core.Tests`: 1338 passed, 0 failed (includes the XAML rules test on the changed window and `Styles.xaml`). This was a cloud session (`CLAUDE_CODE_REMOTE=true`), so `src/InventorAddin` was not built.
- **Not compiled (changed under `src/InventorAddin`):** `AddinServices.cs`, `Commands/PartPropertiesCommand.cs`, `Extraction/PropertyReader.cs` (the `Set` method deleted), `UI/PartPropertiesWindow.xaml`, `UI/PartPropertiesWindow.xaml.cs`, `UI/Theme/Styles.xaml` (new keyed style `SideButton`; outside the listed files, needed for the 8 px gap before the button because windows may not set `Margin`).
- **Inventor API members not confirmed:** none new. The command reuses `DocumentEditCommand.RunInTransaction` and `PropertyWriter.Apply` through the existing `TransactionWriteTarget`. New non-Inventor members, also not compiled: WPF `MessageBox.Show(Window, string, string, MessageBoxButton, MessageBoxImage)` and `BooleanToVisibilityConverter`; `EventHandler<IReadOnlyList<string>>` as the event type.
- **Where the logic went:** the Generate steps (sync, re-check, assign, then write; the messages for each failure) are in Core as `RegisterPartNumberAssigner` (`src/InventorAddin.Core/PartProperties/RegisterPartNumberAssigner.cs`, new, not in the listed files), tested against temporary folders. The command only builds it from the session's `RegisterStore`, the snapshot and the existing transaction write target.
- **Manual checklist for Inventor:**
  1. With the NAS reachable and an empty test register folder set in Settings: open an old part with no number. Part number shows Not assigned and the Generate part number button shows (Alt+G works)
  2. Generate: the row shows `AWB-10001`, the button disappears, the status line says `Part number AWB-10001 assigned and written. Save the document to keep it.`, `part-register.csv` in the folder has one row with the part's name and path, and `%APPDATA%\AwbAddin\register\part-register.csv` matches
  3. Close the window, Undo once in Inventor (the Undo list entry is "Part Properties"): Part Number reverts; the register row stays
  4. Generate again, save the part, close and reopen Part Properties: the number shows with no note and no button
  5. Disconnect Tailscale (or point Settings at a missing folder): Generate on another part says it was assigned offline. A missing folder answers at once; a disconnected drive can take up to about 6 seconds, because the sync and the assignment each probe the folder (see Follow-ups). Reconnect, Generate on a third part: the offline row is now in the shared file
  6. Type `AWB-10500` as Part Number in Inventor's iProperties dialog on a test part: the window shows it with "Not in the register" and offers Generate
  7. A never-saved part: Generate is disabled and "Save the file before giving it a part number." shows beside it
  8. A file named in the new format (`AWB-10001,test_plate.ipt`) with no Part Number set: header shows `test plate`, Part number shows Not assigned (Inventor's file-name fallback `AWB-10001,test_plate` is not a number)
  9. With a file named `part-register.lock` (fresh) in the register folder: Generate shows the "Another PC is writing the part register" message in red and the window stays usable; delete the lock and Generate works
  10. Make a duplicate: Generate offline on one part, then add the same number to the shared file by hand from another "PC" row, reconnect and Generate on another part: one message box lists the duplicate, and `awbaddin.log` has a warning for it
  11. Change the register folder in Settings while Inventor runs, then Generate: the new folder is used without restarting

## Follow-ups

Things noticed but not done.

- Decision 2026-10-07 "For a never-saved file, any non-blank Part Number counts as assigned" (spec 08, question 5) is superseded by spec 02's rule as built here: a never-saved file shows its Part Number only when it is in the format. `DECISIONS.md` and spec 08 question 5 may want a note.
- Offline Generate probes the register folder twice (once in `RegisterStore.Sync`, once in `RegisterStore.Assign`), so a hanging drive costs up to about 6 seconds rather than 3. `RegisterStore` could let `Assign` reuse the result of a sync made just before it.
- Generate blocks the window while it syncs and assigns (up to the probe and lock timeouts). A wait cursor or a "Assigning..." status would help; nothing shows now.
- When the re-check refuses ("This file already has a registered number."), duplicates the sync found are only logged, not shown in a message box.
- The window still says nothing to show the file's number is now registered after a refusal; reopening the window shows it.
