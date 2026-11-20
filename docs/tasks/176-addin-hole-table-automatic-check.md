# 176: Add-in: check a drawing's hole tables when it becomes active or updates, and show the prompt

- **Milestone:** M4 part 2 (request from Aidan, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** todo
- **Depends on:** 175
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** none

## Goal

Hook task 175's `HoleTableAutoCheck` to Inventor: watch for a drawing becoming active and for a drawing updating, run the write-free check when the policy says so, and show a small window naming the out-of-date views with **Update now** and **Later**. Add the setting's checkbox to the Settings window.

## Scope

- `src/InventorAddin/HoleTable/HoleTableAutoChecker.cs` (new), started from `StandardAddInServer.Activate` and disposed in `Deactivate`, following `UI/StartupTab.cs` (kept-referenced `ApplicationEvents`, a `System.Windows.Forms.Timer`, nothing ever throws into Inventor, every handler in try/catch with a log line):
  - `ApplicationEvents.OnActivateDocument` (after): a `DrawingDocument` was activated.
  - `ApplicationEvents.OnDocumentChange` (after) for a `DrawingDocument` whose reasons include `kUpdateWithReferencesCmdType`: the drawing updated. Changes of other kinds (the user drawing in the drawing, the add-in's own table commands) are ignored.
  - `ApplicationEvents.OnCloseDocument`: tell the policy to forget the drawing.
  - A timer ticking about every 500 ms while any check is due (stopped when none is) gives the policy the current conditions: the active document, whether the active edit object is the drawing itself (`Application.ActiveEditObject`), whether a command is running (check the API for how; `CommandManager.ActiveCommand` or the `UserInputEvents` the add-in already uses in `HoleSession`), and whether an AWB window is open (`WindowHost` knows; add a static `IsAnyOpen` if it has none).
  - Running the check: a `HoleTableSource` for the drawing, `GetKeyedSheets()`, `HoleTableStoredCheck.Run`, wait cursor while it runs. Log one Info line with the result and the time it took (`Hole table check: DRAWING.idw, 1 view out of date (VIEW1 on Sheet:1: 2 holes moved), 0.4 s`). Show the prompt when the policy says so; log the user's answer and Update's result.
  - Nothing is read or written for a drawing with the setting off.
- `src/InventorAddin/HoleTable/HoleTableSource.cs`: override `GetKeyedSheets()` so it lists only sheets that already have a key and never calls `WriteKeys` (as `GetKeyedViews` does for views).
- `src/InventorAddin/UI/HoleTableOutOfDateWindow.xaml` and `.xaml.cs` (new): small, shown with `WindowHost.ShowDialog` and the Hole Table icon, bound to `HoleTableOutOfDatePromptViewModel`: the heading, the view lines, **Update now** (default button) and **Later** (cancel button; Esc). Closes on `CloseRequested`. Shared window style (spec 08), no `UseLayoutRounding`, `AwbComboBox` rules do not apply (no dropdowns).
- `src/InventorAddin/Commands/HoleTableCommand.cs`: tell the checker the wizard opened and closed for that drawing, so it runs the closing check of task 175 and no automatic check runs while the wizard is open.
- `src/InventorAddin/UI/SettingsWindow.xaml`: the checkbox and its note in the Holes section, after "Use Inventor's Hole command", bound to the task 175 properties.

Out of scope: Core changes (task 175), unless a gap is found; then stop and say so.

## Acceptance criteria

- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests` passes (includes the XAML style rules).
- [ ] Every Inventor API member used is confirmed by reflection on `C:\Program Files\Autodesk\Inventor 2026\Bin\Autodesk.Inventor.Interop.dll` or by existing use; list them in Verification.
- [ ] The automatic check adds no undo entry and does not mark the drawing changed (keyed-only listing of sheets and views).
- [ ] Event handlers unsubscribe on `Deactivate`; the timer stops when nothing is due.
- [ ] The manual checklist below is filled in.

## Notes for the implementer

- `OnDocumentChange`'s document parameter is `_Document`; cast as `CLAUDE.md` says. Its reasons are a `CommandTypesEnum` bit set.
- The check runs on Inventor's UI thread from the timer; keep it out of the event handlers themselves (they only tell the policy).
- Task 175's API: `HoleTableAutoCheck` (`DrawingActivated`, `DrawingUpdated`, `DrawingClosed`, `TakeDue(HoleTableAutoCheckContext, now)`, `ShouldPrompt`, `Later`, `UpdatedNow`, `WizardClosed`, `Enabled`), `HoleTableStoredCheck.Run`, `HoleTableOutOfDatePromptViewModel` with an `UpdateRunner`, and the `SettingsViewModel` label and note constants. Keep `Enabled` in step with the setting when Settings is saved.
- Stopping the timer: you may add a one-line `bool AnyPending` to `HoleTableAutoCheck` (with a test) rather than inferring it; that small Core change is in scope.
- When the prompt closes with `Later` after a partly failed Update now, call `Later` with the failed views' original out-of-date entries (their original reasons, not the "Update failed." text), so the same failures do not prompt again at once. With `Updated`, call `UpdatedNow`.
- Build the check's log line with a `HoleTableMessages` helper in Core (with a test), so the wording is testable.
- Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
