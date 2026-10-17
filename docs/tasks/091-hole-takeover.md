# 091: AWB Hole replaces Inventor's Hole and its H shortcut

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command", "Settings"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** todo
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
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. 3D Model tab: AWB Hole where Hole was, Hole hidden, H opens AWB Hole; still so after a restart.
  2. Settings, tick "Use Inventor's Hole command", Save: Hole is back, H opens it, no restart. Untick: AWB Hole and H are ours again.
  3. Tools > Customize > Keyboard, reset shortcuts: H still opens the Hole that is showing.
  4. Disable the add-in in the Add-In Manager: Inventor's Hole and H work.

## Follow-ups
