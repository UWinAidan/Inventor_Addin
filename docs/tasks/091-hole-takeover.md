# 091: AWB Hole replaces Inventor's Hole and its H shortcut

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command", "Settings"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** done
- **Depends on:** 086, 090
- **Needs:** windows build
- **Parallel-safe with:** 092

## Goal

On the 3D Model tab, AWB Hole sits where Inventor's Hole is, Inventor's Hole is hidden, and H opens AWB Hole; the Settings switch gives both back to Inventor, and H always opens whichever Hole is showing.

## Scope

- `src/InventorAddin/UI/` (a small takeover class used by `RibbonSetup` and after Settings is saved), `Commands/ShellCommands.cs` (apply after Save)
- Core: the decision of what to show from the setting, with tests

## Acceptance criteria

- [ ] Taken over (switch off, the default): our AWB Hole button added large before `PartHoleCmd` in `Ribbons["Part"].RibbonTabs["id_TabModel"].RibbonPanels["id_PanelP_ModelModify"]`, `PartHoleCmd`'s control hidden, `OverrideShortcut = "H"` on ours and `""` on `PartHoleCmd`.
- [ ] Given back (switch on): Inventor's control shown, our button there hidden, `PartHoleCmd.OverrideShortcut = DefaultShortcut` and ours cleared, so **H opens Inventor's Hole** (the spike's round 1 bug).
- [ ] Applied at startup and right after Settings is saved, without a restart; re-applied on `UserInterfaceEvents.OnResetShortcuts`.
- [ ] When the add-in unloads, Inventor's Hole and H are given back, so a removed add-in never leaves Inventor without its Hole (as the spike did).
- [ ] Failures are logged and leave Inventor's Hole working.
- [ ] Delete the spike branch `spike/081-in-inventor` (local) once this is committed.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- The note under the Settings checkbox (task 086) already says the switch takes effect when Settings is saved and names the Hole button on the 3D Model tab; check the wording still fits what this task does.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed), `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: build succeeded, 0 errors, 8 warnings (all CS8600 in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand`, none from this task). `dotnet test tests/InventorAddin.Core.Tests`: 3533 passed, 0 failed (18 new in `HoleTakeoverRulesTests`). Not run inside Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; `UI/HoleCommandTakeover.cs` (new), `UI/RibbonSetup.cs` and `Commands/ShellCommands.cs` were all compiled on Windows. None of it has been run in Inventor yet.
- **Inventor API members not confirmed:** none by name: `ControlDefinitions[...]`, `ControlDefinition.OverrideShortcut` (get and set) and `DefaultShortcut`, `CommandControls.AddButton(def, true, true, "PartHoleCmd", true)`, `CommandControl.Visible` and `InternalName`, `Ribbons["Part"].RibbonTabs["id_TabModel"].RibbonPanels["id_PanelP_ModelModify"]`, `UserInterfaceManager.UserInterfaceEvents` and `OnResetShortcuts` were all used in spike 081 and compile here. Behaviour not yet seen in Inventor: whether `OnResetShortcuts` fires before or after the reset is applied (the re-apply is queued with `Dispatcher.BeginInvoke` so it runs after Inventor's event returns, either way); and whether our hidden control on the Modify panel is left behind after the add-in is disabled (Inventor normally drops an unloaded add-in's controls).
- **Settings note (task 086):** "On gives the H shortcut and the Hole button on the 3D Model tab back to Inventor's Hole; off gives them to AWB Hole. Takes effect when you save." still fits: the switch is applied on every successful Save.
- **Spike branch `spike/081-in-inventor`:** holds nothing not carried over. Its six commits are the planning commit (older versions of spec 09, task 081 and the tracking files, all superseded on this branch) and the throwaway `Spike081.cs` with its `StandardAddInServer` hook. Safe to delete.
- **Manual checklist for Inventor:**
  1. Open a part (any scratch part). 3D Model tab, Modify panel: a large AWB Hole button where Hole was, Inventor's Hole hidden; press H: the AWB Hole window opens. Restart Inventor and check again. The log (`awbaddin.log`) has "Hole takeover (startup): AWB Hole has the Hole button on the 3D Model tab and the H shortcut."
  2. AWB Addin tab > Settings, tick "Use Inventor's Hole command", Save: no restart, Inventor's Hole is back on the Modify panel, the AWB Hole button there is gone, H opens Inventor's Hole dialog. AWB Hole is still on the AWB Addin tab. Open Settings, untick, Save: AWB Hole is back in place of Hole and H opens it.
  3. With the switch off: Tools > Customize > Keyboard, Reset All (or reset shortcuts), close the dialog, press H in a part: the AWB Hole window opens. With the switch on, after a reset H opens Inventor's Hole. The log has a "Hole takeover (shortcuts reset)" line.
  4. Tools > Add-Ins, unload AWB Addin (untick Loaded): in a part, Inventor's Hole is on the Modify panel and H opens it; no leftover AWB Hole button. Load the add-in again: AWB Hole takes over again.
  5. Sheet Metal and Flat Pattern tabs still show Inventor's Hole (not touched).
  6. With AWB Hole taken over, end Inventor from Task Manager, disable the add-in in the Add-In Manager, restart: Inventor's Hole is visible on the Modify panel and H opens it (a hidden control or the override may survive a crash; spike 081 confirmed only the shortcut persists).

## Follow-ups

- Review: the parent wrapped the `BeginInvoke` call in `OnResetShortcuts` in its own try/catch so nothing escapes into Inventor's event; rebuilt on Windows. Not done: "taking over failed, so give back" sits in `HoleCommandTakeover.Apply` and `HoleTakeoverRules.For` is unused by the adapter; route `Apply` through Core or drop `For`.

- Taking over clears any shortcut the user had given Inventor's Hole themselves; giving back then restores Inventor's default (H), not their key. A give-back leaves a non-empty override alone, but after a takeover it is always empty. Remembering the user's own key (for example in settings) would fix it, if Aidan wants that.
- `Deactivate` runs on every Inventor exit too, so H and the Hole button are given back at each exit and taken again at each start. The persisted override (spike 081 E) is therefore not relied on; a crash leaves H on AWB Hole until the next start, which then re-applies.
- If a user gives AWB Hole a key of their own, giving Hole back clears it.
