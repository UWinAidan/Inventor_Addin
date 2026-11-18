# 169: Fix: a WPF ComboBox automation exception is caught by the Hole Table's origin read

- **Milestone:** M4 part 2 (fixes from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/08-window-style.md` (the shared dropdown style); `docs/features/04-hole-table-wizard.md`
- **Status:** done, awaiting Inventor check
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

- [x] The cause is written up in Verification, with the WPF source location that throws.
- [x] No dropdown in our windows returns a Scroll pattern object that is not an `IScrollProvider`.
- [x] The dropdowns look and behave as before (open, keyboard, mouse wheel, editable dropdowns).
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; Core tests pass.

## Notes for the implementer

- If you can, reproduce it in a small test outside Inventor: create the peer for a styled ComboBox (`UIElementAutomationPeer.CreatePeerForElement`) and call `GetPattern(PatternInterface.Scroll)`, then cast. A WPF test needs an STA thread; if it cannot live in the Core test project (no WPF there), do it as a throwaway check and describe it in Verification.
- `Inventor` namespace clashes: alias System types (`CLAUDE.md`).

## Verification

Filled in by the implementer.

- **Cause (confirmed by the reviewer, WPF 8.0.11):** `PresentationFramework`, `System/Windows/Automation/Peers/ComboBoxAutomationPeer.cs`, `GetPattern` (dotnet/wpf `release/8.0`, line 64): `else if (pattern == PatternInterface.Scroll && !owner.IsDropDownOpen) { iface = this; }`. The peer does not implement `IScrollProvider`. `PresentationCore`, `MS/internal/Automation/ScrollProviderWrapper.cs` line 136, `ScrollProviderWrapper.Wrap`, casts it: `(IScrollProvider) iface`, and throws. It is reached from `AutomationPeer.GetWrappedPattern` via `ElementProxy.GetPatternProvider` when a UI Automation client asks for Scroll. A throwaway STA app (deleted) creating the peer of a closed stock ComboBox and calling `GetWrappedPattern(ScrollPatternIdentifiers.Pattern.Id)` threw the exact message in Aidan's log; the same call on `AwbComboBox` returns null. Our template is not involved. Not traced: the route out through the COM call (inferred: UIA's request runs on the UI thread inside COM's message pump).
- **Changes:** new `src/InventorAddin/UI/Controls/AwbComboBox.cs` (ComboBox subclass, peer returns null for Scroll, returned only while the dropdown is closed, so the real provider is kept when it is open (review round 1); everything else delegated); every `<ComboBox>` in the seven window XAML files became `<controls:AwbComboBox>`; `Styles.xaml` has an implicit style for `AwbComboBox` based on the ComboBox style (implicit styles match the exact type; the keyed styles ShortComboBox, EditableComboBox and CodeComboBox still apply, their TargetType is the base class). `HoleTableSource.ReadIndicatorPoint` (used by `ReadOrigins`) retries the indicator-point read once when the exception is not a `COMException`, logging the exception type; a `COMException` is not retried.
- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 9 warnings (existing CS8600 ones). `dotnet test tests/InventorAddin.Core.Tests`: 4834 passed, 0 failed. No Core change, so no new tests.
- **Not compiled (changed under `src/InventorAddin`):** all compiled on Windows by the build above; nothing run in Inventor. XAML parse errors would show only at window open, so the windows are untested.
- **Inventor API members not confirmed:** none new (`view.OriginIndicator.Intent.PointOnSheet` is the existing call).
- **Manual checklist for Inventor:**
  1. Open each of the seven windows (New Part, Part Properties, Save As Copy, Settings, Material/Finish, AWB Hole, Hole Table): each opens with the AWB look; a style failure shows as the default Windows look or an error in `awbaddin.log`.
  2. AWB Hole: pick a countersink angle, then Custom... and type a value (the editable dropdown).
  3. In a long list, use the mouse wheel and arrow keys, and type a letter to jump to an item.
  4. Hole Table: pick sheets and views, Analyze several times: no `ComboBoxAutomationPeer` warning in the log. The warning came from an outside UI Automation client, so its absence in one session proves little.

## Follow-ups

Things noticed but not done.

- `SaveAsReplacerTests.Replace_TopLevel_CopiesAsSaveAsCopy_ClosesTheCopy_ThenReplacesTheOccurrence_AndSavesNothingElse` failed in 2 of about 9 full runs (its temp `cad` folder "does not exist"); a flaky test, unrelated to this task (reviewer).
