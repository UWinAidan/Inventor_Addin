# 037: Register folder and units settings

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (The register: Location; Templates); `docs/features/08-window-style.md` (Settings window)
- **Status:** todo
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

- [ ] `AddinSettings.RegisterFolder` (string), default `B:\aidan\AWB_Design_Data` as a public constant `DefaultRegisterFolder`; null in the file becomes the default; blank is stored as blank and means "no shared register" (every assignment works from the local copy and says so)
- [ ] `enum UnitsSystem { Metric, Imperial }` and `AddinSettings.DefaultUnits`, default `Metric`, stored as the member name in JSON (`"defaultUnits": "Metric"`); an unknown value in the file loads as `Metric` with a settings warning, not a `.bad` file
- [ ] A settings file written before these properties existed loads with the defaults (test)

View-model

- [ ] `RegisterFolder` text property with trim on save, and `RegisterFolderHelpText` = `"The shared part number register. Leave blank to use only this PC's copy."`
- [ ] Validation: a register folder that contains a character not allowed in a Windows path (`< > " | ? *` or a control character) gives `"The register folder contains a character Windows does not allow."`; a relative path gives `"Enter a full folder path, such as B:\Data."`. A folder that does not exist is allowed (the NAS may be offline)
- [ ] `DefaultUnits` property and a list of options with display text `Metric` and `Imperial`
- [ ] Dirty tracking and Save cover both new settings (tests)

Window

- [ ] Section **General** gains "Units for new files" (dropdown) under Default designer, and a new section **Part numbers** holds "Register folder" (text box with its help text under it, and a Browse button that opens a folder picker and fills the box)
- [ ] Follows spec 08 and the XAML rules test (031): labelled rows, shared styles through `DynamicResource`, access keys unique in the window
- [ ] "Show developer tools" gets access key S (follow-up from task 028), if S is still free once the new labels have theirs; otherwise pick a free letter and say which
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, including `WindowXamlRulesTests`

## Notes for the implementer

- The default folder is Aidan's (2026-10-10). The setting is the only place it appears in code.
- For the folder picker on .NET 8 WPF use `Microsoft.Win32.OpenFolderDialog` (new in .NET 8). If it is not available in this project's target, use `System.Windows.Forms.FolderBrowserDialog` (the add-in already references WinForms for message boxes) and say which in Verification.
- Settings changes for these two take effect at the next command, not the next start (they are read from `AddinServices.Settings` each time); keep the restart note about developer tools only.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open Settings: Units for new files shows Metric, Register folder shows `B:\aidan\AWB_Design_Data`, in both themes
  2. Browse picks a folder and fills the box; Save, reopen Settings: both values kept
  3. Type a relative path: the error shows and Save is disabled
  4. Tab order and access keys work through every field

## Follow-ups

Things noticed but not done.
