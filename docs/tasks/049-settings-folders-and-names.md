# 049: Settings: shorter units label, CAD folders, other names that mean me

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Decided by Aidan: CAD folders, Other names that mean me); `docs/features/08-window-style.md` (Settings)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 050

## Goal

Settings holds the three new values run 2 needs, and the units label fits its column: Work folder and Other CAD folders (where New Part, Mass Update and Rename look), and Other names that mean me (old Designer and Detailer values to treat like the Windows user name). From Aidan's run 1 checks and his answer to spec 02 question 23.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs`
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs`
- `src/InventorAddin/UI/SettingsWindow.xaml` and `SettingsWindow.xaml.cs`
- `docs/features/08-window-style.md` (the Settings section lists the rows)
- `tests/InventorAddin.Core.Tests/Settings/SettingsStoreTests.cs`, `tests/InventorAddin.Core.Tests/ViewModels/SettingsViewModelTests.cs`

Out of scope:

- Using the new settings (043, 047, 048, 051). Parsing the names list into names (051).

## Acceptance criteria

Settings model

- [ ] `AddinSettings.DefaultWorkFolder = @"C:\Users\4aida\OneDrive\Documents\CADING"` (Aidan's, spec 02 question 23) and `WorkFolder`, defaulting to it. Null in the file becomes the default; blank stays blank (no work folder), as `RegisterFolder` does
- [ ] `OtherCadFolders`: a list of folder paths, empty by default, written as a JSON array. Null in the file becomes empty. The two folders Aidan named are **not** written into code or tests (Default: one may name an organisation); he enters them in Settings
- [ ] `OtherNamesForMe`: a string, `""` by default, null becomes `""`. Stored as typed (comma-separated); 051 parses it
- [ ] Round trips, and a file written before these settings existed loads with the defaults

View-model and window

- [ ] The General section's "Units for new files" label becomes **"New file units"** (Aidan). Its access key stays U
- [ ] A new section **Folders** (after Part numbers): **Work folder** (text box, help text "Where New Part and Mass Update start, and the first folder Rename searches.", Browse button), **Other CAD folders** (multi-line text box, one folder per line, help text "Other folders that hold your CAD. Rename searches these too.")
- [ ] In General, under Default designer: **Other names** (text box, help text "Names in old files that mean you, separated by commas, for example Aidan.Brown, 4aida. Your Windows user name always counts.")
- [ ] Validation: Work folder blank or a full path (reuse `ValidateRegisterFolder`'s rule, with its own message naming the work folder); each non-blank line of Other CAD folders a full path, the message naming the line number. Blank lines are dropped and each path trimmed on save. Save is disabled while any is invalid
- [ ] Every label fits the 100-wide label column (keep labels short; put the explanation in help text). Access keys stay unique across the window; list them in Verification
- [ ] **Browse handlers do not touch the disk on the UI thread.** Spike 032 measured `Directory.Exists` blocking 8 to 42 s on a disconnected drive. Drop the existence check before setting `InitialDirectory` in both Browse handlers (register folder and work folder); if the picker needs one, run it off the UI thread with a short timeout
- [ ] The XAML rules test passes; spec 08's Settings section lists the new rows
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow task 037's pattern for `RegisterFolder` and its Browse button. Use the shared `SideButton` style from task 039 if Browse fits beside the box; otherwise keep it on its own row as 037 did.
- A multi-line `TextBox` (`AcceptsReturn`, `TextWrapping`, a fixed number of lines) needs a style in the shared theme if the XAML rules test forbids local sizes; add one to `Styles.xaml` if needed and say so.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
