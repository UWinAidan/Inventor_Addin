# 121: The AWB Addin tab and AWB Hole button come back after the add-in is reloaded

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (ribbon); `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 120
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 120, 128, 129)

## Goal

PR #29 check 091-4 failed (Aidan, 2026-10-25): after unloading and reloading the add-in in the Add-In Manager, the AWB Addin ribbon tab did not come back and AWB Hole was missing from the 3D Model tab, though H still opened it. Restarting Inventor fixed it. A reload must rebuild the ribbon as a start does.

## Scope

- `src/InventorAddin/StandardAddInServer.cs` (`Activate`, `Deactivate`), `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin/UI/HoleCommandTakeover.cs`.
- Find out why: `Activate` is called with `firstTime = false` on a reload, and the ribbon code may build controls only on `firstTime` (or `RibbonSetup.AddButton` returns early because a control with the name is still there, `TODO.md` follow-up from task 056). Also check what `Deactivate` removes: tabs, panels, controls and `ButtonDefinition`s should all be deleted on unload, so a reload starts clean; and that H being left on AWB Hole after unload is wrong (the takeover's `Dispose` should give Inventor's Hole back on unload, then take it again on load).
- Log what `Activate` and `Deactivate` build and remove.

Out of scope: the reset case (120).

## Acceptance criteria

- [ ] Unload, then reload: the AWB Addin tab and AWB Hole in the 3D Model tab's Modify panel are back, with icons, and work.
- [ ] While unloaded, Inventor's Hole button is back and H opens Inventor's Hole.
- [ ] Unload and reload twice in a row leaves no duplicate buttons or panels.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Keep the `ButtonDefinition`s referenced (`RibbonSetup._commands`), and release them on unload.
- Check every Inventor member against existing usage or the API reference.

## Verification

Implemented on Aidan's Windows PC (Inventor 2026 installed, Inventor open, so no deploy).

Why it failed (inferred, not seen in Inventor): the code never used `firstTime`; `Create` ran in full on a reload. But
nothing was deleted on unload: `Deactivate` only detached the `OnExecute` handlers and hid our Modify-panel button. On a
reload `GetOrAddTab`, `GetOrAddPanel`, `AddButton` and the takeover's `FindControl` all reused whatever tab, panel or
control with our name was still there. The log of the 2026-10-24 14:23 reload (Startup tab: "Inventor was already
running when the add-in loaded") shows the takeover reporting success ("AWB Hole has the Hole button"), so it found and
showed a control, yet the button was not visible. The likeliest cause is the tab and controls left over from the first
load: found by name, so nothing new was added, but not shown. H still worked, so the definition `AddinCommand.Register`
reused was fine; the shortcut is on the definition, not the control. The fix: our tabs and our Modify-panel button are
deleted on unload, and deleted again before building if any are left.

What changed:

- Core `AddinUi` (new, `Ribbon/AddinUi.cs`): the add-in's controls in Inventor's ribbons: its tab on every ribbon (all
  four, whatever the layout shows now) and its button on Inventor's panels (AWB Hole on Part > `id_TabModel` >
  `id_PanelP_ModelModify`, from `HoleTakeoverRules`). `AddinUiRemovalReport` and `RibbonBuildReport` tally what was
  removed or added and make the log lines.
- `RibbonSetup`: `RemoveUi` deletes, each item on its own and logged, our button(s) on Inventor's Modify panel, then
  our tab on every ribbon (`RibbonTab.Delete`, which should take its panels and buttons). It runs at the start of
  `Create` ("add-in loading": should find nothing) and at the end of `Dispose` ("add-in unloading"), after the takeover
  has given Inventor's Hole and H back and the handlers are detached. The command definitions are **not** deleted (see
  Follow-ups); a reload reuses them, as before. `Create` logs one line with commands registered, tabs, panels and
  buttons added, and anything that was already there and reused. In `Dispose`, each command's release is in its own
  try/catch with a warning, so one failure cannot skip the others or `RemoveUi`, or throw out of `Deactivate`.
  `_failedCommands` is cleared on dispose.
- `StandardAddInServer.Activate` logs `firstTime`.
- `HoleCommandTakeover`: doc comment only (its `Dispose` already gave Hole and H back; `RibbonSetup` now deletes our
  button afterwards).

- **Ran** (after the review changes):
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: build succeeded, 0 errors, 8 warnings (all
    CS8600 in command files this task did not touch).
  - `dotnet test tests/InventorAddin.Core.Tests`: 3745 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; the solution built on Windows. Not yet run in Inventor.
- **Inventor API members not confirmed:** none by name. Confirmed by reflection on
  `Bin\Public Assemblies\Autodesk.Inventor.Interop.dll` (2026): `RibbonTab.Delete()`, `CommandControl.Delete()`,
  `RibbonTab.InternalName`, `CommandControl.InternalName`, the `Ribbons`, `RibbonTabs`, `RibbonPanels` and
  `CommandControls` indexers (also in existing usage). Behaviour not confirmed, to be shown by the check:
  - that `RibbonTab.Delete` on our tab also removes its panels and buttons (the next load's line would then say
    "nothing was left");
  - that a kept definition, put on a freshly added tab and panel, shows and runs (the reload in the 091-4 check suggests
    the definition itself was fine).
- **Manual checklist for Inventor:**
  1. Open a part. Tools > Add-Ins, unload AWB Addin. The AWB Addin tab is gone from every ribbon; Inventor's Hole button
     is back on the 3D Model tab's Modify panel; H opens Inventor's Hole. In `awbaddin.log`: "Hole takeover (add-in
     unloading): Inventor's Hole has its button ... back", then "Ribbon (add-in unloading): removed 4 tabs, and 1 button
     on Inventor's panels." (fewer tabs if some ribbon's layout has no panels), and no "could not remove" warning.
  2. Load it again. The AWB Addin tab is back on every ribbon, with icons; AWB Hole is back, large, before where
     Inventor's Hole was on the Modify panel, and Inventor's Hole is hidden; H opens AWB Hole; click a few AWB Addin
     buttons (About, AWB Hole) and they work. In the log: "Inventor activated the add-in with firstTime = False", "Ribbon
     (add-in loading): nothing was left from an earlier load; the ribbon is built from scratch.", and a "Ribbon (add-in
     loading): 13 commands registered; the tab added on ..." line with no "Already there and reused" part.
  3. Repeat unload and load once more: no duplicate buttons or panels on the AWB Addin tab or the Modify panel.
  4. Restart Inventor: the ribbon is as before, and the first load's log line also says "nothing was left".
  5. Customisations survive. Give one AWB command a keyboard shortcut (Tools > Customize > Keyboard; for example About)
     and put it on the Quick Access Toolbar (right-click its button > Add to Quick Access Toolbar). Then:
     a. restart Inventor: the shortcut still runs it, and the Quick Access Toolbar entry is still there and runs it;
     b. unload and reload it in the Add-In Manager: the same, both still there and working.
  6. If step 2 shows "removed what an earlier load left" or "Already there and reused", or any "could not remove"
     warning, send the log: Inventor kept something the unload deleted.

## Follow-ups

- **Departs from the brief.** The Scope says tabs, panels, controls "and `ButtonDefinition`s should all be deleted on
  unload". The definitions are kept (review of this task): `Deactivate` runs at every normal Inventor exit, so deleting
  the 13 definitions each time risks dropping users' Quick Access Toolbar entries and keyboard shortcuts on our
  commands, and the bug does not need it (H still worked after the reload, so the reused definition was fine; the stale
  tab and Modify-panel control were the problem). For the parent to log as a Default.
- The task's spec link names `docs/features/01-foundations.md`; the file is `docs/features/01-ribbon-and-shell.md`.
- `TODO.md` follow-up from task 056 (`RibbonSetup.AddButton` returns early on an existing control): every load now
  starts with no add-in controls, so it can be closed once check 2 above passes. The early return stays as a guard and is
  now counted in the build log line ("Already there and reused").
- Task 128 (Sheet Metal takeover) must add its button to `AddinUi.ForeignButtons`, or an unload will leave it behind.
