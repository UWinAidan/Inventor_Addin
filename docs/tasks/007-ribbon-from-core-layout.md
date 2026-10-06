# 007: Ribbon built from the Core layout

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Target)
- **Status:** done
- **Depends on:** 004, 005
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** 006

## Goal

`RibbonSetup` draws whatever `RibbonLayout` (task 004) describes for each environment, and the Developer panel follows the `ShowDeveloperTools` setting. Adding a button later means adding a Core layout entry and registering the command.

## Scope

- `src/InventorAddin/UI/RibbonSetup.cs`
- `src/InventorAddin/StandardAddInServer.cs` (pass the loaded settings to `RibbonSetup`)
- `src/InventorAddin/Commands/ExportDataCommands.cs` (use the Core `CommandNames` constants for `InternalName`)

Out of scope:

- `AddinCommand.cs` and `DocumentEditCommand.cs` (task 006)
- Icons
- Changing the ribbon while Inventor runs. The setting takes effect at the next start.

## Acceptance criteria

- [x] Tab id and name come from the Core constants
- [x] For each environment in Core's enum, `RibbonSetup` gets the ribbon by name, and creates the tab only if the layout has panels for it
- [x] Panels and buttons are added in the order Core gives. `Large` and `Small` map to the `UseLargeIcon` argument of `CommandControls.AddButton`
- [x] Commands are registered once and looked up by internal name. A layout entry naming a command that is not registered is logged as a `WARN` and skipped, not thrown
- [x] Re-running `Create` in the same session (add-in reloaded) does not duplicate tabs, panels or buttons; the existing get-or-add helpers stay
- [x] With `ShowDeveloperTools` false, no Developer panel and, since nothing else exists yet, no Workflow Tools tab
- [x] Commands stay referenced in `_commands`
- [x] No engineering or layout rules left in `RibbonSetup`: no checks on ribbon names
- [x] The verification section lists the changed files as not compiled

## Notes for the implementer

- `CommandControls.AddButton(ButtonDefinition, UseLargeIcon, ShowText, TargetControlInternalName, InsertBeforeTargetControl)`: the existing call passes only the first two. Keep doing that unless the reference confirms the rest.
- When the developer panel was created in an earlier session and the setting is now off, Inventor may still show it, because Inventor persists ribbon customisation. Do not add deletion code; record what Aidan sees in the follow-ups after he tests it. The manual checklist covers it.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core` (0 errors) and `dotnet test tests/InventorAddin.Core.Tests` (144 passed, 0 failed, 0 skipped). This task adds no Core code; the layout rules it consumes are tested by task 004.
- **Not compiled (changed under `src/InventorAddin`):** `UI/RibbonSetup.cs`, `StandardAddInServer.cs`, `Commands/ExportDataCommands.cs`. Built in a cloud session, where the add-in project cannot be built.
- **Inventor API members not confirmed:** none new. Every member used was already in the previous `RibbonSetup`: `UserInterfaceManager.Ribbons[string]`, `Ribbon.RibbonTabs` (enumerate, `Add(name, id, clientId)`), `RibbonTab.InternalName`, `RibbonTab.RibbonPanels` (enumerate, `Add(name, id, clientId)`), `RibbonPanel.InternalName`, `RibbonPanel.CommandControls` (enumerate, `AddButton(ButtonDefinition, bool)`), `CommandControl.InternalName`. `AddButton` is still called with two arguments; only the second now varies.
- **Manual checklist for Inventor:**
  1. Build with `settings.json` absent. Start Inventor. There is no Workflow Tools tab on any ribbon.
  2. Close Inventor. Set `"showDeveloperTools": true` in `%APPDATA%\InventorWorkflowTools\settings.json`. Start Inventor.
  3. With no document open, the Workflow Tools tab has a Developer panel with *Export Libraries* only.
  4. Open a part, an assembly and a drawing. Each has the tab with *Export Model Data* and *Export Libraries*, in that order. Both buttons work. Both buttons are now small (the layout marks them `Small`; the old code always passed `true` for large), so they should sit stacked rather than as two large buttons.
  5. Unload and reload the add-in from the Add-in Manager (setting still true). No duplicate tab, panel or button appears.
  6. Set the setting back to false and restart. Note whether the tab or panel still appears (Inventor may cache it) and report it.
  7. Check the log file in `%APPDATA%\InventorWorkflowTools` has no `WARN` line about an unregistered ribbon command.

## Follow-ups

- After Aidan runs checklist step 6: record whether Inventor keeps showing the Developer panel or the Workflow Tools tab from an earlier session once `ShowDeveloperTools` is off. If it does, a separate task can decide whether to remove stale panels at startup.
- `RibbonSetup.Create` does not catch a failure for one environment (for example a ribbon name Inventor does not know), so one failure stops the rest of the ribbon and propagates out of `Activate`. A later task could wrap each environment and log the error instead.
