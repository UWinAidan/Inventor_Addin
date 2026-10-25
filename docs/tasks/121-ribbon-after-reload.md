# 121: The AWB Addin tab and AWB Hole button come back after the add-in is reloaded

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/01-foundations.md` (ribbon); `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
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

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tools > Add-Ins, unload AWB Addin. Inventor's Hole button is back; H opens Inventor's Hole.
  2. Load it again. The AWB Addin tab is back on every ribbon; AWB Hole is back in the 3D Model tab; H opens AWB Hole.
  3. Repeat once: no duplicate buttons.

## Follow-ups
