# 169: Fix: a WPF ComboBox automation exception is caught by the Hole Table's origin read

- **Milestone:** M4 part 2 (fixes from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/08-window-style.md` (the shared dropdown style); `docs/features/04-hole-table-wizard.md`
- **Status:** todo
- **Depends on:** 168
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 170

## Goal

The log shows, under the Hole Table's origin read:

`WARN Hole table: View origin point: Unable to cast object of type 'System.Windows.Automation.Peers.ComboBoxAutomationPeer' to type 'System.Windows.Automation.Provider.IScrollProvider'.`

That is a WPF UI Automation exception, not an Inventor one. While the add-in waits on an Inventor COM call on the UI thread, COM pumps window messages; a UI Automation client (Windows itself, a screen tool, the touch keyboard) asks one of our dropdowns for its Scroll pattern, WPF's `ComboBoxAutomationPeer` hands back an object that is not an `IScrollProvider`, and the cast throws. The exception unwinds out through the COM call, so `ComSafe.Get` around the origin read catches it and the origin read is lost, though Inventor did nothing wrong. After this task the automation request no longer throws, so no unrelated exception lands in a COM read.

## Scope

- Find the cause and confirm it. Start with the hypothesis above: WPF's `ComboBoxAutomationPeer.GetPattern(PatternInterface.Scroll)` returns the peer itself while the dropdown is closed, and the peer does not implement `IScrollProvider` (check the .NET WPF source for `ComboBoxAutomationPeer` and the caller doing the cast). Check whether our shared ComboBox template in `src/InventorAddin/UI/Theme/Styles.xaml` makes it worse (its popup `ScrollViewer` is not named as WPF's own templates name it).
- Fix it at the source, for every dropdown in our windows, not only the Hole Table window. Options, the implementer chooses with reasons:
  - a ComboBox subclass (in `src/InventorAddin/UI/`) whose automation peer returns `null` or a real scroll provider for `PatternInterface.Scroll`, used by the shared style's windows; or
  - a template change, if that alone removes the bad path.
- Also guard the Hole Table origin read so a non-COM exception there is logged as what it is: if anything other than a `COMException` (or other Inventor error) reaches the origin read, retry the read once before giving up, and log the exception type. Keep this small; the real fix is the one above.
- Any window XAML that must switch to the subclass.

Out of scope: the origin fallbacks (task 168); `UseLayoutRounding` (never add it, see `CLAUDE.md`).

## Acceptance criteria

- [ ] The cause is written up in Verification, with the WPF source location that throws.
- [ ] No dropdown in our windows returns a Scroll pattern object that is not an `IScrollProvider`.
- [ ] The dropdowns look and behave as before (open, keyboard, mouse wheel, editable dropdowns).
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; Core tests pass.

## Notes for the implementer

- If you can, reproduce it in a small test outside Inventor: create the peer for a styled ComboBox (`UIElementAutomationPeer.CreatePeerForElement`) and call `GetPattern(PatternInterface.Scroll)`, then cast. A WPF test needs an STA thread; if it cannot live in the Core test project (no WPF there), do it as a throwaway check and describe it in Verification.
- `Inventor` namespace clashes: alias System types (`CLAUDE.md`).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open Hole Table, pick sheets and views from the dropdowns, Analyze several times: the log has no `ComboBoxAutomationPeer` warning.
  2. The dropdowns in Hole Table, AWB Hole, Settings and Part Properties open, scroll and select as before.

## Follow-ups

Things noticed but not done.
