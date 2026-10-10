# 039: Part number display and Generate part number in Part Properties

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Part number; The register; Commands: Generate part number); `docs/features/07-part-properties.md`; `docs/features/08-window-style.md` (Part number display)
- **Status:** todo
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

- [ ] The view-model takes a `Func<string?, bool> hasNumber` (or an interface with that one method) from the caller; the add-in passes `RegisterStore.HasNumber`, which reads only the local copy
- [ ] `IsPartNumberAssigned` is `hasNumber(partNumber)`
- [ ] `PartNumberDisplay`: the number formatted by `PartNumber.ToString()` when the Part Number is well formed (registered or not), otherwise `Not assigned`
- [ ] New `IsPartNumberUnregistered` (well formed, not in the register) and `PartNumberNote`: `"Not in the register"` when unregistered, otherwise null. The window shows the note in muted text beside the number
- [ ] The old file-name comparison rule is gone; `PartNumberDisplay` (the Core class) is removed or reduced to the `NotAssignedText` constant, and its tests are replaced with tests of the new rule
- [ ] Remove the unbound `PartNumber`, `Material` and `Finish` view-model properties (follow-up from task 029) if nothing binds them

Generate

- [ ] `GenerateCommand`, shown (`CanGenerate` visibility flag) only when `IsPartNumberAssigned` is false and the file is editable; disabled with the note `"Save the file before giving it a part number."` when the file has never been saved
- [ ] It calls `IPartNumberAssigner.Assign()` (supplied by the command), which returns the assigned number text, whether it was assigned offline, and any duplicate messages; or throws with a message
- [ ] On success the window shows the new number in the Part number row, hides the button, and the status line says `"Part number AWB-10001 assigned and written. Save the document to keep it."`, plus `" Assigned offline; it will be added to the shared register at the next sync."` when offline
- [ ] On failure the status line shows the message from the exception (as Apply does) and nothing else changes
- [ ] Duplicates reported by the sync are shown once in a message box by the add-in and logged as warnings

Add-in

- [ ] `AddinServices` exposes a `RegisterStore` built from `Settings.RegisterFolder` (blank: a store with no shared folder, always offline) and `%APPDATA%\AwbAddin\register`, rebuilt when settings change
- [ ] The command's assigner: calls `RegisterStore.Assign` with the part name, part type, kind and file path from the snapshot, **then** writes Part Number with `PropertyWriter` inside one transaction (`RunInTransaction`), so one Undo removes the written number. The register entry stays (rows are never removed); say so in a log line if the write fails after assigning
- [ ] Before assigning, the assigner calls `RegisterStore.Sync()` and re-reads `HasNumber`; if the file's number is now registered it refuses with `"This file already has a registered number."` (another PC registered it)
- [ ] `PropertyReader.Set` is deleted and nothing else calls it
- [ ] The window follows spec 08 and the XAML rules test: the button sits in the Part number row, styled as a secondary button, with access key G
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added while running M2, from the task 036 review: `Sync()` returns `RegisterSyncResult` (not `SyncResult`, which is `PartRegister.Merge`'s). `Assign` and `Sync` can throw `RegisterBusyException` and `RegisterUnreadableException`; show each message to the user and write nothing to the file. If `Assign` throws after the shared file was written, the number may already be in the register; the message should not claim nothing was assigned.

- "Has a number" is Aidan's rule. Showing an unregistered well-formed number with a note and offering Generate is a Default (spec 02, question 13).
- Writing Part Number uses `PartPropertyStorage.PartNumber` and `PropertyWrite.SetText`; `PropertyWriter` already handles standard properties.
- After a successful Generate, the view-model's `_original` snapshot must hold the new Part Number so a later Apply does not see it as a change. Part Number is never in the write plan.
- Any number of things can fail here (NAS, lock, write). Every failure must leave the window usable and say what happened.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. With the NAS reachable and an empty test register folder set in Settings: open an old part with no number. Part number shows Not assigned and the Generate button shows
  2. Generate: the row shows `AWB-10001`, the status line says it was written, `part-register.csv` in the folder has one row with the part's name and path, and `%APPDATA%\AwbAddin\register\part-register.csv` matches
  3. Undo once in Inventor: Part Number reverts; the register row stays
  4. Generate again, save the part, close and reopen Part Properties: the number shows with no note and no button
  5. Disconnect Tailscale (or point Settings at a missing folder): Generate on another part says it was assigned offline, and within about 3 seconds. Reconnect, Generate on a third part: the offline row is now in the shared file
  6. Type `AWB-10500` as Part Number in Inventor's iProperties dialog on a test part: the window shows it with "Not in the register" and offers Generate
  7. A never-saved part: Generate is disabled with the save note
  8. A file named in the new format (`AWB-10001,test_plate.ipt`): header shows `test plate`

## Follow-ups

Things noticed but not done.
