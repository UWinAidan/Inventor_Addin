# 124: Typed values and expressions are kept when size, type, standard or units change

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** todo
- **Depends on:** 100, 123
- **Needs:** cloud or windows (Core)
- **Parallel-safe with:** 122

## Goal

PR #29 check 100-7 failed (Aidan, 2026-10-25): a blind tapped hole with Thread depth `d0` and Drill depth `d0+5 mm`, changed to M8, had both replaced by the default numbers (12, 16). Aidan decided: **a value or expression the user typed is kept when the size, type, standard or units change; only values the user has not typed follow the table.** This overrides task 100's Default that a size change turns an expression into a number.

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`: each editable box (depth, thread depth, drill depth, seat boxes, and later Diameter and Custom limits) records whether its value was typed by the user, or came from the table, the last values used, or an edited hole's read-back. A cascade replaces only untyped values. A typed number keeps its value in its own units; on a units switch it is shown converted (a typed `12 mm` stays 12 mm, shown as the inch value with units in the box if needed, Default) and an expression is kept as written.
- Default: a value read back from an edited hole counts as typed, so Edit never resets the user's numbers; a box the user clears becomes untyped and follows the table again.
- Tests for each box and each cascade (size, type, standard, units), and for a cleared box.

## Acceptance criteria

- [ ] The 100-7 case keeps `d0` and `d0+5 mm` across a size change.
- [ ] An untouched box follows the new size's table value.
- [ ] Clearing a box hands it back to the table.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Blind tapped M6, Thread depth `d0`, Drill depth `d0+5 mm`. Edit, change to M8: both expressions stay, the hole stays parametric.
  2. A new blind tapped M6 with untouched depths, change to M8: the depths follow the table.
  3. Type a depth, change Units: the typed depth is kept.

## Follow-ups
