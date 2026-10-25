# 120: H opens AWB Hole again after the shortcuts are reset

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
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

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Switch off. Tools > Customize > Keyboard > Reset All, close the dialog. Press H in a part: AWB Hole opens. Inventor's Hole button is still hidden.
  2. Switch on, Save. Reset All again. H opens Inventor's Hole.
  3. Check `awbaddin.log` for the reset lines.

## Follow-ups
