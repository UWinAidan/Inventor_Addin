# 098: Tolerance list shows the limits for the size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Dowel and precision holes")
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build (Core view-model plus XAML)
- **Parallel-safe with:** 093, 094, 095, 096

## Goal

Aidan, 090 check: each entry of the Tolerance list shows the actual tolerance for the chosen size, for example `H7  +0.012 / 0`, with the Precision Tol row's name only as secondary text.

## Scope

- Core: `AwbHoleViewModel` (a tolerance option item with its main and secondary text, rebuilt when size, units or fit change), using `Iso286` for ISO rows; tests
- `src/InventorAddin/UI/AwbHoleWindow.xaml` (an item template with the secondary text in the shared look's secondary brush)

## Acceptance criteria

- [ ] ISO rows: the code and its limits at the chosen size, from `Iso286` (H7 at 6 mm: `H7  +0.012 / 0`). Limits rows: the row's Upper and Lower (`+0 / -0.020`). Main text is the tolerance; secondary text the row's name, when it differs from the main text.
- [ ] Limits are in the window's units: mm with 3 decimals, inches with 4; a zero limit is written `0`; a plus or minus sign on every non-zero limit.
- [ ] A size outside ISO 286's range shows the code and "not in ISO 286" and stays unselectable or flagged as today's rules decide (no change to the rules).
- [ ] The texts update when the size, units or fit change; the selection is kept.
- [ ] The closed dropdown shows the same main text.
- [ ] XAML rules test passes; light and dark both readable.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Dowel 6 mm, Normal fit: the list reads like `H7  +0.012 / 0` with the row's name under or beside it in grey. Change to 10 mm: `H7  +0.015 / 0`. A Limits row shows its own limits.
  2. Imperial: limits in inches, 4 decimals.

## Follow-ups
