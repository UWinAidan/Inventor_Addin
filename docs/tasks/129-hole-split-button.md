# 129: AWB Hole as a split button with Inventor Hole and the default toggle

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 128
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 120, 121, 128)

## Goal

Aidan, 2026-10-25: AWB Hole on the 3D Model and Sheet Metal tabs becomes a **split button**. Its dropdown has **Inventor Hole**, which runs Inventor's Hole once, and **Use Inventor's Hole by default**, a toggle that is the same setting as "Use Inventor's Hole command" in AWB Settings.

## Scope

- `src/InventorAddin/UI/RibbonSetup.cs`, `HoleCommandTakeover.cs`, a new command for Inventor Hole and one for the toggle in `src/InventorAddin/Commands/`.
- The split button (`CommandControls.AddSplitButton` or what the API reference gives; check the members), with AWB Hole as its main button. When the setting is on, the split button is hidden and Inventor's Hole shows, as today; Default: the toggle is then reached from AWB Settings or the AWB Addin tab's AWB Hole (which stays a plain button).
- **Inventor Hole** runs Inventor's Hole command once (`ControlDefinitions["PartHoleCmd"].Execute`, or the sheet metal one) without changing the setting. While it runs, the takeover must not redirect it to AWB Hole (task 092 stops Inventor's hole edit started other ways; a new Inventor hole from this button is allowed). Its icon is Inventor's Hole icon if it can be read, else ours (as task 092).
- **Use Inventor's Hole by default** shows its state (pressed or a check), saves the setting as Settings' Save does, and applies it at once. The Settings window shows the same value next time it opens.
- Core: any rule about which button shows for which setting goes in `HoleTakeoverRules` with tests.

## Acceptance criteria

- [ ] The split button shows on both tabs with the two dropdown items.
- [ ] Inventor Hole opens Inventor's Hole once; the next H opens AWB Hole.
- [ ] The toggle and the Settings switch always agree, and the toggle takes effect at once.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. 3D Model tab: AWB Hole has a dropdown with Inventor Hole and Use Inventor's Hole by default.
  2. Inventor Hole: Inventor's Hole opens and makes a hole. Then H: AWB Hole.
  3. Toggle on: Inventor's Hole button and H come back. Open AWB Settings: the switch is on. Turn it off there: the split button is back.
  4. The same on the Sheet Metal tab.

## Follow-ups
