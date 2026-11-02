# 120: H opens AWB Hole again after the shortcuts are reset

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 091
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 121, 128, 129)

## Goal

PR #29 check 091-3 failed (Aidan, 2026-10-25): after Tools > Customize > Keyboard > **Reset All**, H no longer opened AWB Hole. Toggling "Use Inventor's Hole command" off and on in Settings fixed it. Spec 09 says the takeover is re-applied when the user resets shortcuts; make that true.

## Scope

- `src/InventorAddin/UI/HoleCommandTakeover.cs`: the `UserInterfaceEvents.OnResetShortcuts` handler (line 177 today). Find out why it does not leave H on AWB Hole: whether the event fires for Reset All at all, whether it fires before Inventor writes its defaults (so our override is overwritten afterwards), and whether the handler's own state ("already taken over") makes it skip. Log each step to `awbaddin.log` so Aidan's check shows which.
- Likely fix, if the event fires early: re-apply the takeover after Inventor has finished, for example on the next idle (`OnActivateCommand`/a one-shot `DispatcherTimer` on the UI thread) or on the next AWB command start, and re-check the H binding when the Customize dialog closes if an event for that exists. Re-apply means exactly what toggling the setting does today, so reuse that path rather than a second copy.
- If the takeover rules change, they belong in `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` with tests (its `For` is unused by the adapter today; `TODO.md` follow-up from 091, folded in here: move the adapter's "failed, so give back" rule into Core while here).

Out of scope: the reload case (121), the Sheet Metal tab (128), the split button (129).

## Acceptance criteria

- [ ] After Reset All with the switch off, H opens AWB Hole and Inventor's Hole button stays hidden, without a restart or a Settings toggle.
- [ ] After Reset All with the switch on, H opens Inventor's Hole.
- [ ] Each step of the reset handling is logged.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Notes for the implementer

- Check every Inventor member against existing usage or the API reference (`CLAUDE.md`). List the unconfirmed ones in Verification.

## Verification

Implemented on Aidan's Windows PC (Inventor 2026 installed, Inventor open, so no deploy).

Why it failed: `awbaddin.log` from the 091-3 check (2026-10-24 14:21:24) shows the event **did** fire and the handler
re-applied without skipping ("Hole takeover (shortcuts reset): AWB Hole has the Hole button ... and the H shortcut"),
yet H opened Inventor's Hole. The interop docs say `OnResetShortcuts` fires "when command shortcuts/aliases are reset in
the Customize dialog", i.e. while the dialog is still open; the `BeginInvoke` re-apply ran inside the dialog's modal loop,
and the dialog then wrote its own (default) shortcut table when it closed. The handler's own state was not the cause:
it applied `_owner` (AWB) unconditionally.

What changed:

- Core `HoleShortcutResetPlan` (new): after a reset, waits until Inventor's main window is enabled again (a modal dialog
  disables it), asks for a re-apply, then watches the two Hole commands' override shortcuts for 5 s and re-applies if
  they change (at most 3 re-applies). Gives up with a warning after 30 min with the window still disabled. Unreadable
  window state counts as enabled; unreadable shortcuts never trigger a re-apply. Every step returns its log line.
- Core `HoleTakeoverRules`: `OwnerAfterApply` (the "failed, so give back" rule, moved from the adapter; the TODO
  follow-up from 091), `ResetReapplyOrder` (AWB: give back, then take over, exactly what toggling the switch off and on
  does, which fixed it in 091-3; Inventor: give back), `ShortcutsApplied` and `ShortcutsMessage` (the check and the log
  text). `For` is still unused by the adapter.
- `HoleCommandTakeover.cs`: `OnResetShortcuts` now logs the reset (window enabled or not, both shortcuts as read) and
  starts a 500 ms WinForms timer (as `StartupTab`); each tick reads the window state and shortcuts, asks the plan, and
  re-applies through the same `Apply` path Settings uses, with the owner read from `AddinServices.Settings`. `Apply` uses
  `OwnerAfterApply`. `Dispose` stops the timer.

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: build succeeded, 0 errors, 8 warnings (all
    CS8600 in command files this task did not touch).
  - `dotnet test tests/InventorAddin.Core.Tests`: first run 3732 passed, 1 failed
    (`SaveAsReplacerTests.Replace_CopyFails_IsSaveAsCopysFailure_AndNothingIsReplaced`, not related to this task). That
    class plus the hole takeover tests re-run: 132 passed. Two more full runs: 3733 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; the solution built on Windows. Not yet run in Inventor.
- **Inventor API members not confirmed:** none by name; all confirmed by reflection on `Autodesk.Inventor.Interop.dll`
  (2026) or existing usage: `UserInterfaceEvents.OnResetShortcuts(NameValueMap)`, `ControlDefinition.OverrideShortcut`,
  `Application.MainFrameHWND` (Int32; read as in `WindowHost`). Behaviour not confirmed, to be shown by the log in the
  check: that the Customize dialog is modal, so Inventor's main window reads disabled while it is open
  (`user32 IsWindowEnabled`), and that the dialog's shortcuts are written by the time its window closes.
- **Manual checklist for Inventor:**
  1. Switch off. Tools > Customize > Keyboard > Reset All, close the dialog. Press H in a part: AWB Hole opens. Inventor's Hole button is still hidden.
  2. Switch on, Save. Reset All again. H opens Inventor's Hole.
  3. Check `awbaddin.log` for the reset lines. Expected, in order: "Inventor reported a shortcut reset; Inventor's window
     is disabled, so the Customize dialog is open. Shortcuts now: ..."; when the dialog closes, "the dialog has closed
     (N s after the reset). Shortcuts before re-applying: ... Re-applying."; the give-back and take-over lines with
     "(shortcuts reset)"; about 5 s later "done; shortcuts 5 s after re-applying: AWB Hole 'H', Inventor's Hole none".
     If the first line says the window is **enabled**, the dialog is not modal and the re-apply runs at once: send the
     log, since the fix then needs another signal for the dialog closing.

## Follow-ups

- `SaveAsReplacerTests.Replace_CopyFails_IsSaveAsCopysFailure_AndNothingIsReplaced` failed once and passed on three
  re-runs; it looks flaky, like the `NewPartCreatorTests` case noted in task 135 (probably the shared temp folder).
- Parent, after review: a dialog that closes after the last allowed re-apply stops the plan with a warning instead of re-applying again (the cap now holds on that path too; test `ADialogClosingAfterTheLastReApply_DoesNotReApplyAgain`).
- `HoleTakeoverRules.For` is still unused by the adapter; remove it or use it in `GiveBack`.
