# 008: Settings window

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Settings)
- **Status:** done
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

- [x] Core still targets plain `net8.0` with no WPF reference. `RelayCommand` implements `System.Windows.Input.ICommand`, which `net8.0` provides without WPF. If the Core build disagrees, stop and report rather than changing the Core project type
- [x] `SettingsViewModel` is built from an `AddinSettings` and a `SettingsStore`. It edits a copy, exposes `ShowDeveloperTools`, `IsDirty`, a `SaveCommand` (enabled only when dirty) and a `RestartNotice` string
- [x] Saving writes through the store and raises an event (or sets a result) the window uses to close. A save failure sets an error text on the view-model instead of throwing
- [x] Cancel or closing the window writes nothing
- [x] Tests cover dirty tracking, `SaveCommand.CanExecute`, a save round trip in a temporary folder, a failed save, and that `PropertyChanged` fires
- [x] The layout gains a "CAD Automation" panel with a small `WorkflowTools_Settings` button on the Part, Assembly and Drawing ribbons, placed before the Developer panel. Layout tests updated, including that the tab now always exists on those three ribbons
- [x] `SettingsCommand` is query-only (it changes no document)
- [x] `WindowHost` sets the owner with `WindowInteropHelper.Owner = new IntPtr(InventorHost.App.MainFrameHWND)` and calls `ShowDialog()`
- [x] The window has a checkbox "Show developer tools", the restart notice, Save and Cancel. It binds to the view-model only; no logic in code-behind beyond wiring close
- [x] After a save, `AddinServices` holds the new settings
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- The Core view-model base and `RelayCommand` will be reused by every later dialog. Keep them small; no MVVM package.
- `Application.MainFrameHWND` is not used in the repo yet. Confirm it against the API reference or list it as unconfirmed.
- ZeroDoc gets no Settings button in this task; see spec 01 open question 4.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors (Core still plain `net8.0`; `RelayCommand` implements `System.Windows.Input.ICommand` without any WPF reference). `dotnet test tests/InventorAddin.Core.Tests`: 186 passed, 0 failed, 0 skipped (42 more than after task 007: 21 in `SettingsViewModelTests`, 4 in `RelayCommandTests`, and 17 net from the updated `RibbonLayoutTests`). Cloud session on Linux; `src/InventorAddin` was not built (`CLAUDE_CODE_REMOTE=true`, no interop DLL).
- **Not compiled (changed under `src/InventorAddin`):**
  - `src/InventorAddin/UI/WindowHost.cs` (new)
  - `src/InventorAddin/UI/SettingsWindow.xaml` (new)
  - `src/InventorAddin/UI/SettingsWindow.xaml.cs` (new)
  - `src/InventorAddin/Commands/ShellCommands.cs` (new)
  - `src/InventorAddin/UI/RibbonSetup.cs` (registers `SettingsCommand`)
  - `src/InventorAddin/AddinServices.cs` (new `internal static UpdateSettings(AddinSettings)`; not in the task's file list, but needed for "After a save, `AddinServices` holds the new settings", as task 005's follow-up anticipated)
- **Inventor API members not confirmed:**
  - `Application.MainFrameHWND`. Not used anywhere else in the repo and the interop cannot be inspected here. The Inventor API reference describes it as a read-only `Long` (a 32-bit `int` in the interop). `WindowHost` reads it through `ComSafe.Get` as `long?`, so it compiles whether the interop declares `int` or `long`; if the property name is wrong the build fails on that line. A failed read (or 0) logs a `WARN` and shows the window centred on screen without an owner.
  - No other new Inventor members. `SettingsCommand` relies on the default `CommandTypesEnum.kQueryOnlyCmdType` from `AddinCommand`.
- **Not confirmed (WPF in Inventor):** this is the add-in's first XAML window. `InitializeComponent` loads the compiled XAML by pack URI (`/InventorAddin;component/ui/settingswindow.xaml`). If Inventor loads the add-in in a way WPF cannot resolve by assembly name, the button fails with an error message box mentioning the resource; see Follow-ups.
- **Manual checklist for Inventor:**
  1. Build. Delete `%APPDATA%\InventorWorkflowTools\settings.json`. Start Inventor and open a part.
  2. The Workflow Tools tab has a CAD Automation panel with a small Settings button, and no Developer panel. With no document open, there is no Workflow Tools tab.
  3. Click Settings. The window "Workflow Tools Settings" opens centred in front of Inventor and Inventor cannot be clicked until it closes. It has no taskbar entry. "Show developer tools" is unticked and Save is disabled. The notice reads "Changes take effect the next time Inventor starts."
  4. Tick "Show developer tools". Save becomes enabled. Untick it: Save is disabled again. Tick it and click Save (or press Enter); the window closes. `settings.json` now has `"showDeveloperTools": true`. The log has `INFO Settings saved to '...settings.json'.`
  5. Click Settings again (same session). The box is ticked (the session now holds the saved settings) and Save is disabled. The ribbon has not changed yet. Press Esc or Cancel.
  6. Restart Inventor. The Developer panel shows after CAD Automation on part, assembly and drawing ribbons, and the Settings button is on all three.
  7. Open Settings, untick, click Cancel. `settings.json` is unchanged. Repeat with the window's close button: still unchanged.
  8. Error path: close Inventor, make `settings.json` read-only, start Inventor, open Settings, change the box and click Save. The window stays open with a red message naming the file; the log has an `ERROR Could not save settings` entry with the exception. Cancel closes it. Remove the read-only flag afterwards.

Notes on choices the brief left open:

- `SettingsViewModel(AddinSettings, SettingsStore, ILog? log = null)`: the optional log lets a save failure be logged with its full exception while the window shows the short reason. Without it the view-model uses `NullLog`.
- The view-model keeps two private copies (last saved, being edited). `IsDirty` compares them with record equality, so ticking and unticking returns to not dirty. `SaveCommand.Execute` does nothing while not dirty.
- On success the view-model raises `Saved` (the window sets `DialogResult = true`) and exposes a fresh copy as `SavedSettings`, which `SettingsCommand` passes to `AddinServices.UpdateSettings` after the dialog returns. On failure it catches any exception from the store, sets `ErrorText` and `HasError`, logs `ERROR`, stays dirty and keeps the window open.
- The error text is shown only when `HasError` is true, through a XAML `DataTrigger`, so the code-behind holds only the close wiring.
- If `AddinServices.DataFolder` is null (start-up services failed), the Settings button shows an information message instead of the window.

## Follow-ups

- If checklist step 3 fails with a XAML resource or `InitializeComponent` error, WPF could not resolve the add-in assembly by name from Inventor's load context. A separate task should then decide how to load windows (for example registering an `AssemblyResolve` handler at activation). Later dialogs (009 onward) follow the same pattern, so this is worth checking first.
- `ErrorText` stays visible after a failed save until the next save attempt, even if the user changes the checkbox. Fine for one setting; revisit when more settings are added.
- `RelayCommandTests.cs` was added beyond the listed files so the reusable command has its own tests.
- From review: checklist step 8 relies on Windows refusing to replace a read-only `settings.json`. If the save succeeds anyway, lock the file instead (open it in a program that denies sharing) to exercise the error path.
