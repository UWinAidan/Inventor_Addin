# 008: Settings window

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Settings)
- **Status:** todo
- **Depends on:** 002, 007
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** none

## Goal

A Settings button on the CAD Automation panel opens a window where the user edits the settings that exist and saves them. It is also the first WPF window in the add-in, so it sets the pattern later dialogs follow.

## Scope

- `src/InventorAddin.Core/ViewModels/ObservableObject.cs` (new: minimal `INotifyPropertyChanged` base)
- `src/InventorAddin.Core/ViewModels/RelayCommand.cs` (new: minimal `ICommand`)
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs` (new)
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` and the command name constants (add the Settings button)
- `tests/InventorAddin.Core.Tests/ViewModels/SettingsViewModelTests.cs` (new)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs` (update)
- `src/InventorAddin/UI/WindowHost.cs` (new: shows a WPF window modally, owned by Inventor's main window)
- `src/InventorAddin/UI/SettingsWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/Commands/ShellCommands.cs` (new: `SettingsCommand`)
- `src/InventorAddin/UI/RibbonSetup.cs` (register the command)

Out of scope:

- Settings other than `ShowDeveloperTools`. Spec 01 open question 3 decides what else.
- Applying changes to the ribbon live. The window says they take effect next time Inventor starts.
- About (task 009), icons.

## Acceptance criteria

- [ ] Core still targets plain `net8.0` with no WPF reference. `RelayCommand` implements `System.Windows.Input.ICommand`, which `net8.0` provides without WPF. If the Core build disagrees, stop and report rather than changing the Core project type
- [ ] `SettingsViewModel` is built from an `AddinSettings` and a `SettingsStore`. It edits a copy, exposes `ShowDeveloperTools`, `IsDirty`, a `SaveCommand` (enabled only when dirty) and a `RestartNotice` string
- [ ] Saving writes through the store and raises an event (or sets a result) the window uses to close. A save failure sets an error text on the view-model instead of throwing
- [ ] Cancel or closing the window writes nothing
- [ ] Tests cover dirty tracking, `SaveCommand.CanExecute`, a save round trip in a temporary folder, a failed save, and that `PropertyChanged` fires
- [ ] The layout gains a "CAD Automation" panel with a small `WorkflowTools_Settings` button on the Part, Assembly and Drawing ribbons, placed before the Developer panel. Layout tests updated, including that the tab now always exists on those three ribbons
- [ ] `SettingsCommand` is query-only (it changes no document)
- [ ] `WindowHost` sets the owner with `WindowInteropHelper.Owner = new IntPtr(InventorHost.App.MainFrameHWND)` and calls `ShowDialog()`
- [ ] The window has a checkbox "Show developer tools", the restart notice, Save and Cancel. It binds to the view-model only; no logic in code-behind beyond wiring close
- [ ] After a save, `AddinServices` holds the new settings
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- The Core view-model base and `RelayCommand` will be reused by every later dialog. Keep them small; no MVVM package.
- `Application.MainFrameHWND` is not used in the repo yet. Confirm it against the API reference or list it as unconfirmed.
- ZeroDoc gets no Settings button in this task; see spec 01 open question 4.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Build. Delete `settings.json`. Start Inventor and open a part.
  2. The Workflow Tools tab has a CAD Automation panel with a Settings button, and no Developer panel.
  3. Click Settings. The window opens in front of Inventor and Inventor cannot be clicked until it closes. Save is disabled.
  4. Tick "Show developer tools". Save becomes enabled. Click Save; the window closes. `settings.json` now has `"showDeveloperTools": true`.
  5. Restart Inventor. The Developer panel shows on part, assembly and drawing ribbons.
  6. Open Settings, untick, click Cancel. `settings.json` is unchanged.

## Follow-ups
