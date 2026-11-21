# 176: Add-in: check a drawing's hole tables when it becomes active or updates, and show the prompt

- **Milestone:** M4 part 2 (request from Aidan, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** done
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

- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests` passes (includes the XAML style rules).
- [x] Every Inventor API member used is confirmed by reflection on `C:\Program Files\Autodesk\Inventor 2026\Bin\Autodesk.Inventor.Interop.dll` or by existing use; list them in Verification.
- [x] The automatic check adds no undo entry and does not mark the drawing changed (keyed-only listing of sheets and views).
- [x] Event handlers unsubscribe on `Deactivate`; the timer stops when nothing is due.
- [x] The manual checklist below is filled in.

## Notes for the implementer

- `OnDocumentChange`'s document parameter is `_Document`; cast as `CLAUDE.md` says. Its reasons are a `CommandTypesEnum` bit set.
- The check runs on Inventor's UI thread from the timer; keep it out of the event handlers themselves (they only tell the policy).
- Task 175's API: `HoleTableAutoCheck` (`DrawingActivated`, `DrawingUpdated`, `DrawingClosed`, `TakeDue(HoleTableAutoCheckContext, now)`, `ShouldPrompt`, `Later`, `UpdatedNow`, `WizardClosed`, `Enabled`), `HoleTableStoredCheck.Run`, `HoleTableOutOfDatePromptViewModel` with an `UpdateRunner`, and the `SettingsViewModel` label and note constants. Keep `Enabled` in step with the setting when Settings is saved.
- Stopping the timer: you may add a one-line `bool AnyPending` to `HoleTableAutoCheck` (with a test) rather than inferring it; that small Core change is in scope.
- When the prompt closes with `Later` after a partly failed Update now, call `Later` with the failed views' original out-of-date entries (their original reasons, not the "Update failed." text), so the same failures do not prompt again at once. With `Updated`, call `UpdatedNow`.
- Build the check's log line with a `HoleTableMessages` helper in Core (with a test), so the wording is testable.
- Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

- **Ran:** Inventor was not running. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (8 existing CS8600 warnings in other commands). `dotnet test tests/InventorAddin.Core.Tests`: 4937 passed, 0 failed (includes the XAML style rules; 5 new tests: `AnyPending` x2, `AutoCheckLogLine` x3). One earlier run showed 2 unrelated Numbering failures on a temp folder that did not exist; they passed on rerun. Nothing was run inside Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; all of it built on Windows. It is built but not run in Inventor: `HoleTable/HoleTableAutoChecker.cs` (new), `HoleTable/HoleTableSource.cs`, `UI/HoleTableOutOfDateWindow.xaml(.cs)` (new), `UI/WindowHost.cs`, `UI/SettingsWindow.xaml`, `Commands/HoleTableCommand.cs`, `StandardAddInServer.cs`. Core: `HoleTableAutoCheck.AnyPending`, `HoleTableMessages.AutoCheckLogLine`.
- **Inventor API members:** confirmed by reflection on `Autodesk.Inventor.Interop.dll` and by the build: `Application.ApplicationEvents`, `ApplicationEvents.OnActivateDocument` / `OnDocumentChange` / `OnCloseDocument` (handler parameters read by reflection: `_Document`, `EventTimingEnum`, `CommandTypesEnum ReasonsForChange` on change, `string FullDocumentName` on close, `NameValueMap`, `out HandlingCodeEnum`), `EventTimingEnum.kAfter`, `HandlingCodeEnum.kEventNotHandled`, `CommandTypesEnum.kUpdateWithReferencesCmdType`, `_Document.DocumentType`, `_Document.FullDocumentName`, `DocumentTypeEnum.kDrawingDocumentObject`, `Application.ActiveDocument`, `Application.ActiveEditObject`, `CommandManager._ActiveCommand` (returns `CommandIDEnum`), `CommandManager.ActiveCommand` (returns `string`), `CommandIDEnum.kDefaultCommand` and `Sheet.Parent` (returns `object`). Not confirmed at run time: that `_ActiveCommand` returns `kDefaultCommand` whenever no command is running (the first "waiting" line for a command logs both `_ActiveCommand` and `ActiveCommand`), and that `ActiveEditObject` in a plain drawing is the drawing or one of its sheets, not some third type. The log line `Hole table check: waiting, ...` (once per reason) shows what was returned. Item 1 below tests this.
- **Manual checklist for Inventor:** use a drawing with at least one view that has hole tables made by the Hole Table wizard, and a second drawing with no hole tables. Setting "Check hole tables when a drawing opens or updates" is on (default). Log: `%APPDATA%\AwbAddin\awbaddin.log`.
  1. Activation: close the drawing, open it (or switch to its tab from another document). With nothing changed, wait 3 seconds. Expect no window. If it does not behave so, read the `Hole table check: waiting` lines in the log: they give the command values (`_ActiveCommand` and `ActiveCommand`) or the edit object type that held it back. The log has one `Hole table check: NAME.idw, no views out of date, 0.x s` line. If it has only `Hole table check: waiting, ...` lines, tell me the reason text.
  2. Update after a part edit: open the drawing, open its part, move a hole or add one, save, return to the drawing (or let it update). Expect a small "Hole Table" window after about 2 seconds, headed "Some hole tables in NAME.idw are out of date:", with a line such as `VIEW1 on Sheet:1: 1 hole moved.`, and buttons Update now (default, Enter) and Later (Esc). The log has an out-of-date line naming the view and the time taken.
  3. Part active: with the part (not the drawing) active, make the drawing out of date. Expect no window while the part is active; the window appears about 2 seconds after switching to the drawing.
  4. Later, then the same: click Later. Switch away from the drawing and back. Expect no window (same result as answered). The log has `answered Later`.
  5. Later, then a different change: change the part again so the result differs (another hole moved or added). Return to the drawing. Expect the window again.
  6. Update now with one Undo per view: with two or more out-of-date views, click Update now. Expect the window to close and the tables to redraw. Press Undo once: one view's tables go back. Undo again: the next view's. The log has `answered Update now` and the result text.
  7. Update failure (optional, if you can make one): if a view cannot be updated, expect the window to stay with the heading "...could not be updated:", the reason lines starting "Update failed.", and the button reading Close. Closing it should not prompt again at once for the same failures.
  8. Wizard open: open the Hole Table wizard on an out-of-date drawing. Expect the wizard's own message panel and no second window from the automatic check. Close the wizard with Cancel. Expect no prompt. The log shows one check line after it closes. Open a different out-of-date change afterwards and confirm the prompt returns.
  9. Wizard closed: with nothing open, do step 2 again and confirm the prompt appears (the wizard's flag was cleared).
  10. Setting off: in Settings, untick "Check hole tables when a drawing opens or updates", Save. Repeat steps 1 and 2. Expect no window and no `Hole table check` lines in the log for those events.
  11. No undo entry or save prompt: set the setting on, open a drawing that has hole tables and is saved, wait for the check, with nothing out of date. Expect the Undo button to be greyed (or its last entry unchanged) and the drawing to show no unsaved-change mark; closing it must not ask to save. Repeat with a drawing that has views but no hole tables.
  12. While a command runs: start a sketch or a dimension command in the drawing right after it updates. Expect the check to wait until the command ends, not to open a window over it.

## Follow-ups

- The wait conditions (idle command, drawing in edit) use `CommandManager._ActiveCommand` and `Application.ActiveEditObject` as the brief describes. If checklist item 1 shows only "Hole table check: waiting" lines, the logged `_ActiveCommand` and `ActiveCommand` values, or the edit object's type, show which idle test to change.
- `WindowHost.IsAnyOpen` counts only windows shown through `WindowHost`; a WinForms `MessageBox` open at the time is not counted. A running command covers most such cases.
- Unsaved drawings (empty `FullDocumentName`) are never checked.
- The timer keeps ticking every 500 ms while a check is pending for a drawing that is not the active one.
