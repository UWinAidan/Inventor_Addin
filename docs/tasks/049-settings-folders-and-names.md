# 049: Settings: shorter units label, CAD folders, other names that mean me

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Decided by Aidan: CAD folders, Other names that mean me); `docs/features/08-window-style.md` (Settings)
- **Status:** done (add-in not compiled)
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

- [x] `AddinSettings.DefaultWorkFolder = @"C:\Users\4aida\OneDrive\Documents\CADING"` (Aidan's, spec 02 question 23) and `WorkFolder`, defaulting to it. Null in the file becomes the default; blank stays blank (no work folder), as `RegisterFolder` does
- [x] `OtherCadFolders`: a list of folder paths, empty by default, written as a JSON array. Null in the file becomes empty. The two folders Aidan named are **not** written into code or tests (Default: one may name an organisation); he enters them in Settings
- [x] `OtherNamesForMe`: a string, `""` by default, null becomes `""`. Stored as typed (comma-separated); 051 parses it
- [x] Round trips, and a file written before these settings existed loads with the defaults

View-model and window

- [x] The General section's "Units for new files" label becomes **"New file units"** (Aidan). Its access key stays U
- [x] A new section **Folders** (after Part numbers): **Work folder** (text box, help text "Where New Part and Mass Update start, and the first folder Rename searches.", Browse button), **Other CAD folders** (multi-line text box, one folder per line, help text "Other folders that hold your CAD. Rename searches these too.")
- [x] In General, under Default designer: **Other names** (text box, help text "Names in old files that mean you, separated by commas, for example Aidan.Brown, 4aida. Your Windows user name always counts.")
- [x] Validation: Work folder blank or a full path (reuse `ValidateRegisterFolder`'s rule, with its own message naming the work folder); each non-blank line of Other CAD folders a full path, the message naming the line number. Blank lines are dropped and each path trimmed on save. Save is disabled while any is invalid
- [x] Every label fits the 100-wide label column (keep labels short; put the explanation in help text). Access keys stay unique across the window; list them in Verification
- [x] **Browse handlers do not touch the disk on the UI thread.** Spike 032 measured `Directory.Exists` blocking 8 to 42 s on a disconnected drive. Drop the existence check before setting `InitialDirectory` in both Browse handlers (register folder and work folder); if the picker needs one, run it off the UI thread with a short timeout
- [x] The XAML rules test passes; spec 08's Settings section lists the new rows
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow task 037's pattern for `RegisterFolder` and its Browse button. Use the shared `SideButton` style from task 039 if Browse fits beside the box; otherwise keep it on its own row as 037 did.
- A multi-line `TextBox` (`AcceptsReturn`, `TextWrapping`, a fixed number of lines) needs a style in the shared theme if the XAML rules test forbids local sizes; add one to `Styles.xaml` if needed and say so.

## Verification

Filled in by the implementer.

- **Ran (after review fixes):** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1407 passed, 0 failed (the settings, view-model and XAML rules tests alone: 278 passed). Cloud session (`CLAUDE_CODE_REMOTE=true`), so `src/InventorAddin` was not built.
- **Not compiled (changed under `src/InventorAddin`):** `UI/SettingsWindow.xaml`, `UI/SettingsWindow.xaml.cs`, `UI/Theme/Styles.xaml` (new `MultiLineTextBox` style, as the notes allowed). Only the XAML rules test has read them.
- **Inventor API members not confirmed:** none used. WPF members not confirmed by a build: `TextBox.MinLines` and `MaxLines` sizing the box when the shared template's `ScrollViewer` hides its scroll bars; `DockPanel.Dock` on a button inside a `FieldRow`; `OpenFolderDialog.InitialDirectory` set to a folder that does not exist or a drive that is not mapped: whether `ShowDialog` throws, and which type it throws (the retry catches `ExternalException`, which covers `COMException` and `Win32Exception`, plus `IOException` and `UnauthorizedAccessException`); and whether the shell itself stalls on a disconnected drive when the picker opens there.
- **Layout and naming choices:**
  - The Other CAD folders row is labelled **Other folders**: "Other CAD folders" measures about 105 px at 13 px Segoe UI, over the 100-wide label column. The help text says they hold CAD and the error messages use "Other folders, line N". Aidan may prefer another short label.
  - Browse (both handlers, through one `PickFolder` helper) sets `InitialDirectory` from the box without a disk check. If `ShowDialog` throws for that folder (`ExternalException`, `IOException` or `UnauthorizedAccessException`; review of 049, catch widened by the parent after the second review round), a fresh picker with no starting folder is shown instead, so a stale or unmapped path cannot crash Inventor.
  - Both Browse buttons now sit beside their box in the shared `SideButton` style (the register folder's used to be a row of its own, from 037), the button docked right and the box filling the rest.
  - The other CAD folders text is shown with `\r\n` between folders (what a WPF text box inserts); `\n` and `\r` alone are also read as line breaks. The line number in an error counts blank lines, so it matches what is on screen.
  - `AddinSettings` now has a hand-written `Equals` and `GetHashCode`, because a record compares a list by reference. A test fails if a new settable property is added without a matching case in `Equality_ComparesEverySetting`.
  - A JSON value for `otherCadFolders` that is not a list makes the file invalid (renamed to `.bad`), the same as a wrong type for any other setting except units. Null entries in the list are dropped.
- **Access keys:** D Default designer, N Other names, U New file units, R Register folder, B Browse (register folder), W Work folder, E Browse (work folder), O Other folders, S Show developer tools. Save and Cancel have none (Enter and Esc).
- **Manual checklist for Inventor:**
  1. Build on Windows (`dotnet build InventorAddin.slnx`), start Inventor, open any document or none, click **AWB Addin > Settings**.
  2. Check the labels are not cut off in light and dark themes: Default designer, Other names, New file units, Register folder, Work folder, Other folders.
  3. General: Other names is under Default designer, empty, with its help text. New file units shows Metric.
  4. Part numbers: Register folder shows `B:\aidan\AWB_Design_Data` with **Browse...** beside it, help text below.
  5. Folders: Work folder shows `C:\Users\4aida\OneDrive\Documents\CADING` with **Browse...** beside it. Other folders is a four-line box, empty.
  6. Type `Relative` in Work folder: the status line says "Enter the work folder as a full folder path, such as C:\CAD." and Save is disabled. Clear it: Save enables (blank is allowed).
  7. In Other folders type a full path, press Enter, type `nope`: the status line names line 2 and Save is disabled. Replace `nope` with a second full path, add blank lines and spaces around the paths. Type a fifth and sixth line: the box stays four lines tall and scrolls.
  8. Press Alt+N, Alt+U, Alt+R, Alt+W, Alt+O: each moves to its box. Alt+B and Alt+E open the folder picker for the register folder and the work folder.
  9. With a disconnected drive, type a path on it (for example the NAS while it is off) into Register folder, then click Browse: the picker should open without the long freeze run 1 saw. Note how long it takes. Do the same for Work folder. Then type a path on a drive letter that is not mapped (for example `Q:\x`) and click Browse: the picker should open (in its default folder if it could not start there). Note any error box or crash.
  10. Pick a folder with Browse beside Work folder: it fills the box. Save. Reopen Settings: the folders show trimmed, one per line, blank lines gone; the other names show as typed, trimmed. Open `%APPDATA%\AwbAddin\settings.json`: `workFolder`, `otherCadFolders` (an array) and `otherNamesForMe` are there.

## Follow-ups

Things noticed but not done.

- `FieldRow` centres its label vertically on the row, so "Other folders" sits halfway down the four-line box. A top-aligned variant of `FieldRow` (or a label style for tall rows) would line it up with the first line. Shared control, so not changed here.
- The shared text box template hides scroll bars, so the four-line box gives no sign that it holds more lines. A styled scroll bar in the theme would fix it for both colour sets.
- `ValidateRegisterFolder`'s "not a full path" message ("Enter a full folder path, such as B:\Data.") does not name the register folder, unlike the work folder's. Now that the window has three folder settings, it could say which one.
