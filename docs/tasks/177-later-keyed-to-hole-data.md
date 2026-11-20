# 177: Fix: Later is keyed to the hole data, not the message text

- **Milestone:** M4 part 2 (fix from Aidan's check of 174 to 176, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update; open question 23)
- **Status:** done
- **Depends on:** 175, 176
- **Needs:** cloud (Core only; runs on Windows too)
- **Parallel-safe with:** 179, 180

## Goal

Checklist step 176-4 failed: after Later, a second change to the part did not bring the window back. The log shows each check still found "VIEW2 on Sheet:1: 20 holes moved." and stayed quiet, because `HoleTableAutoCheck` remembers Later as (sheet key, view key, reason text). A second move of the same 20 holes gives the same text. Aidan (2026-11-20): key Later to the actual hole data (positions, sizes, descriptions), so any real change brings the window back.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableStaleness.cs`: `Check` also returns a fingerprint of what Update would draw: every row of `refresh.Plan` (table, tag, XDIM and YDIM text as drawn, the group's description key), plus the gone hole ids, in a stable order (ordinal). Add it as a property on `HoleTableStaleness` (for example `string Fingerprint { get; init; } = ""`). `CouldNot` leaves it empty. A short stable hash of the text (SHA-256, hex) is fine; it must not depend on dictionary or hash-set order, process or culture.
- `src/InventorAddin.Core/ViewModels/HoleTableWizardViewModel.cs`: `HoleTableStoredViewCheck` gains the fingerprint as an init property with default `""`, so existing constructor calls still compile. Its `Text` is unchanged.
- `src/InventorAddin.Core/HoleTable/HoleTableStoredCheck.cs`: copy the fingerprint onto each item.
- `src/InventorAddin.Core/HoleTable/HoleTableAutoCheck.cs`: `Signature` uses (sheet key, view key, fingerprint). When a fingerprint is empty, fall back to the reason text, as now. Update the class and method comments ("the out-of-date result" now means the views and their hole data).
- Tests in `tests/InventorAddin.Core.Tests` (the existing auto-check and staleness test files).
- `docs/features/04-hole-table-wizard.md`: open question 23: the remembered result is the views and their hole data (positions, sizes, descriptions as Update would draw them), so any change to them asks again, even when the reason text reads the same (Aidan, 2026-11-20).

Out of scope: anything under `src/InventorAddin`. The add-in already passes the out-of-date items to `Later`, so the fingerprint travels with them. Confirm this in `HoleTableAutoChecker.CheckAndPrompt` and say so in Verification.

## Acceptance criteria

- [ ] Same holes, same positions: the same fingerprint (two analyses built separately, holes in a different input order).
- [ ] One hole moved again by an amount that leaves the reason text the same ("20 holes moved." before and after): a different fingerprint, and `ShouldPrompt` returns true after `Later`.
- [ ] A hole's description changed (for example a depth) with the same counts: a different fingerprint.
- [ ] A move below the table's precision (the XDIM and YDIM text unchanged): the same fingerprint, so no prompt.
- [ ] The existing `HoleTableAutoCheck`, staleness, stored check and wizard tests pass.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- The plan rows already carry `XText` and `YText` as drawn (see the moved count in `HoleTableStaleness.Check`). Use those, not the raw sheet coordinates, so a move the table cannot show does not prompt (DECISIONS.md 2026-11-18, task 171).
- The description key is `HoleTableDescription.Key`.
- Keep `WizardClosed` and `UpdatedNow` working as now.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core` (0 errors); `dotnet test tests/InventorAddin.Core.Tests`: first run 2 failed (RenamePlan timing, flaky), rerun 4944 passed, 0 failed. 7 new tests (5 staleness fingerprint, 2 auto-check).
- **Not compiled (changed under `src/InventorAddin`):** none. `HoleTableAutoChecker.CheckAndPrompt` already passes `result.OutOfDate` items (filtered with `.Where`, which keeps the same objects) to `_policy.Later`, so the fingerprint travels with them; no add-in change needed.
- **Inventor API members not confirmed:** none (Core only).
- **Manual checklist for Inventor:**
  1. Drawing with stored hole tables. Move a hole in the part, save, go back to the drawing: the window comes up. Choose Later.
  2. Switch away and back with nothing changed: no window.
  3. Move the same hole again by a different amount, save, go back to the drawing: the window comes back, even if its text reads the same as before.

## Follow-ups

Things noticed but not done.
