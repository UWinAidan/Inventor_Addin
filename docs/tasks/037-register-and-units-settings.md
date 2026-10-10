# 037: Register folder and units settings

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (The register: Location; Templates); `docs/features/08-window-style.md` (Settings window)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 033, 034, 035, 036, 038

## Goal

Settings holds where the register lives and whether new files are metric or imperial, and the Settings window lets Aidan change both.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs`
- `src/InventorAddin.Core/Settings/UnitsSystem.cs` (new)
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs`
- `src/InventorAddin/UI/SettingsWindow.xaml` and `SettingsWindow.xaml.cs`
- `tests/InventorAddin.Core.Tests/Settings/SettingsStoreTests.cs`, `tests/InventorAddin.Core.Tests/ViewModels/SettingsViewModelTests.cs`

Out of scope:

- Using either setting (039, 043, 044).
- A templates folder setting (later, when Aidan makes templates).

## Acceptance criteria

Settings

- [x] `AddinSettings.RegisterFolder` (string), default `B:\aidan\AWB_Design_Data` as a public constant `DefaultRegisterFolder`; null in the file becomes the default; blank is stored as blank and means "no shared register" (every assignment works from the local copy and says so)
- [x] `enum UnitsSystem { Metric, Imperial }` and `AddinSettings.DefaultUnits`, default `Metric`, stored as the member name in JSON (`"defaultUnits": "Metric"`); an unknown value in the file loads as `Metric` with a settings warning, not a `.bad` file
- [x] A settings file written before these properties existed loads with the defaults (test)

View-model

- [x] `RegisterFolder` text property with trim on save, and `RegisterFolderHelpText` = `"The shared part number register. Leave blank to use only this PC's copy."`
- [x] Validation: a register folder that contains a character not allowed in a Windows path (`< > " | ? *` or a control character) gives `"The register folder contains a character Windows does not allow."`; a relative path gives `"Enter a full folder path, such as B:\Data."`. A folder that does not exist is allowed (the NAS may be offline)
- [x] `DefaultUnits` property and a list of options with display text `Metric` and `Imperial`
- [x] Dirty tracking and Save cover both new settings (tests)

Window

- [x] Section **General** gains "Units for new files" (dropdown) under Default designer, and a new section **Part numbers** holds "Register folder" (text box with its help text under it, and a Browse button that opens a folder picker and fills the box)
- [x] Follows spec 08 and the XAML rules test (031): labelled rows, shared styles through `DynamicResource`, access keys unique in the window
- [x] "Show developer tools" gets access key S (follow-up from task 028), if S is still free once the new labels have theirs; otherwise pick a free letter and say which
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes, including `WindowXamlRulesTests`

## Notes for the implementer

- The default folder is Aidan's (2026-10-10). The setting is the only place it appears in code.
- For the folder picker on .NET 8 WPF use `Microsoft.Win32.OpenFolderDialog` (new in .NET 8). If it is not available in this project's target, use `System.Windows.Forms.FolderBrowserDialog` (the add-in already references WinForms for message boxes) and say which in Verification.
- Settings changes for these two take effect at the next command, not the next start (they are read from `AddinServices.Settings` each time); keep the restart note about developer tools only.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1053 passed, 0 failed (the total includes tests from tasks 033 and 038 running in the same tree); the settings tests alone (`SettingsStoreTests`, `SettingsViewModelTests`) 158 passed, 0 failed; `WindowXamlRulesTests` 63 passed, 0 failed. `src/InventorAddin` was not built (cloud session, `CLAUDE_CODE_REMOTE=true`).
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/UI/SettingsWindow.xaml`, `src/InventorAddin/UI/SettingsWindow.xaml.cs`.
- **Inventor API members not confirmed:** none; no Inventor API is used. Not confirmed by a build: `Microsoft.Win32.OpenFolderDialog` (new in .NET 8 WPF) and its members `Title`, `Multiselect`, `InitialDirectory`, `FolderName`, `ShowDialog(Window)`. The project targets `net8.0-windows` with `UseWPF`, so it should be there; if it is not, switch to `System.Windows.Forms.FolderBrowserDialog`.
- **Also changed:** `src/InventorAddin.Core/Settings/SettingsStore.cs` (not in the scope list): the unknown-units warning needs a converter that reports to `Load`, so a private `UnitsSystemConverter` lives there. A JSON `null` for `defaultUnits` loads as Metric without a warning, like a missing value; an unknown name, a number, true/false, an object or a list loads as Metric with one warning. Names match in any case (`"imperial"`).
- **Layout choices:** the Browse button is a row of its own under the help line, left-aligned, not beside the box: the shared styles have no gap for a button beside a text box and windows may not set a Margin (see Follow-ups). The units dropdown fills the value column like the text boxes. Access keys: D, U (Units for new files), R (Register folder), B (Browse...), S (Show developer tools): S was free.
- **Manual checklist for Inventor:**
  1. Open Settings: Units for new files shows Metric, Register folder shows `B:\aidan\AWB_Design_Data`, in both themes. Check that the labels "Units for new files" and "Register folder" are not cut off in the 100-wide label column
  2. Browse picks a folder and fills the box; Save, reopen Settings: both values kept
  3. Type a relative path (`Data`): "Enter a full folder path, such as B:\Data." shows and Save is disabled. Type `B:\Da*ta`: "The register folder contains a character Windows does not allow." Clear the box: no error, Save enabled
  4. Tab order and access keys work through every field: Alt+D, Alt+U, Alt+R, Alt+B, Alt+S
  5. Pick Imperial, Save, then open `%APPDATA%\AwbAddin\settings.json`: it holds `"defaultUnits": "Imperial"` and `"registerFolder"`. Change the units to `"Furlongs"` in the file and restart Inventor: the log has a settings warning, Settings shows Metric, and no `settings.json.bad` appears
  6. Browse with the box pointing at a disconnected network drive: note whether the click pauses before the picker opens (the picker checks the folder exists first)

## Follow-ups

Things noticed but not done.

- Label column width: FieldRow's label column is 100 wide (Styles.xaml). "Units for new files" is probably wider than that at 13 px Segoe UI and may be clipped. Either widen the column in the shared FieldRow style or shorten the label; check in Inventor (checklist step 1).
- A shared gap for a button beside a text box (for example a `BrowseButton` style in Styles.xaml with a left margin of 8), so Browse can sit on the same row as the register folder box. Folder fields in New Part (spec 02) will want the same.
- `Directory.Exists` in the Browse handler can block for some seconds on a disconnected mapped drive (spec 02, "Reachability"). If that shows in step 6, drop the existence check or move it off the UI thread.
