# 144: Hole types in the order Simple, Clearance, Tapped, Taper Tapped

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 112, 114
- **Needs:** Core, then windows build
- **Parallel-safe with:** 140 to 143 (not 146 or 147: same window files)

## Goal

Aidan's decision (2026-11-09): the Type row lists Simple, Clearance, Tapped, Taper Tapped, with Simple first. Today the order is Clearance, Tapped, Simple, Taper Tapped (`HoleSpec.Types`).

## Scope

- `src/InventorAddin.Core/Holes/HoleSpec.cs` (`Types`) and wherever else the order is set, such as `AwbHoleViewModel.TypeOptions`, and its tests.
- `src/InventorAddin/UI/AwbHoleWindow.xaml`, only if the window sets its own order or Type icons.
- `docs/features/09-awb-hole.md`: the type list in the new order.

Out of scope:

- Which type a new hole starts on. That stays as today; say in Follow-ups if the order change makes it look odd.
- The look of the Type icons (task 148).

## Acceptance criteria

- [ ] The window's Type row and any other list of types are in the new order; a Core test pins the order.
- [ ] Nothing that relied on list positions (an index, a default of "the first type") changes behaviour; say what you checked.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Search for every use of `HoleSpec.Types` and the type constants before changing the order. A setting or stored hole that saved a type by name is not affected; one saved by position would be.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open AWB Hole: Type shows Simple, Clearance, Tapped, Taper Tapped, left to right.
  2. A new hole starts on the same type as before this change.
  3. Edit an existing hole of each type: the right type is chosen.

## Follow-ups
