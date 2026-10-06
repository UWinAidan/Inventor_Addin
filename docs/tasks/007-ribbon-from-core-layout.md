# 007: Ribbon built from the Core layout

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Target)
- **Status:** todo
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

- [ ] Tab id and name come from the Core constants
- [ ] For each environment in Core's enum, `RibbonSetup` gets the ribbon by name, and creates the tab only if the layout has panels for it
- [ ] Panels and buttons are added in the order Core gives. `Large` and `Small` map to the `UseLargeIcon` argument of `CommandControls.AddButton`
- [ ] Commands are registered once and looked up by internal name. A layout entry naming a command that is not registered is logged as a `WARN` and skipped, not thrown
- [ ] Re-running `Create` in the same session (add-in reloaded) does not duplicate tabs, panels or buttons; the existing get-or-add helpers stay
- [ ] With `ShowDeveloperTools` false, no Developer panel and, since nothing else exists yet, no Workflow Tools tab
- [ ] Commands stay referenced in `_commands`
- [ ] No engineering or layout rules left in `RibbonSetup`: no checks on ribbon names
- [ ] The verification section lists the changed files as not compiled

## Notes for the implementer

- `CommandControls.AddButton(ButtonDefinition, UseLargeIcon, ShowText, TargetControlInternalName, InsertBeforeTargetControl)`: the existing call passes only the first two. Keep doing that unless the reference confirms the rest.
- When the developer panel was created in an earlier session and the setting is now off, Inventor may still show it, because Inventor persists ribbon customisation. Do not add deletion code; record what Aidan sees in the follow-ups after he tests it. The manual checklist covers it.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Build with `settings.json` absent. Start Inventor. There is no Workflow Tools tab on any ribbon.
  2. Close Inventor. Set `"showDeveloperTools": true` in `%APPDATA%\InventorWorkflowTools\settings.json`. Start Inventor.
  3. With no document open, the Workflow Tools tab has a Developer panel with *Export Libraries* only.
  4. Open a part, an assembly and a drawing. Each has the tab with *Export Model Data* and *Export Libraries*. Both buttons work.
  5. Set the setting back to false and restart. Note whether the tab or panel still appears (Inventor may cache it) and report it.

## Follow-ups
