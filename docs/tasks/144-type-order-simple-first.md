# 144: Hole types in the order Simple, Clearance, Tapped, Taper Tapped

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
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

- **Ran:** on Windows (Inventor not running), `dotnet build InventorAddin.slnx` with deploy: 0 errors, 9 warnings (existing CS8600 and similar, none in changed files). `dotnet test tests/InventorAddin.Core.Tests`: 4461 passed, 0 failed.
- **Order changed in:** `HoleStandards.Types` (`src/InventorAddin.Core/Holes/HoleSpec.cs`). `AwbHoleViewModel.TypeOptions` is built from it, and the window's Type row binds to `TypeOptions` (`AwbHoleWindow.xaml` sets no order or icons of its own, so it is unchanged). Pinned by `HoleRulesTests` (`HoleStandards.Types`), `AwbHoleViewModelTests.FixedLists_AreInTheWindowsOrder` and `AwbHoleDiagramViewModelTests.TypeSeatAndTermination_EachNameTheirIcon` (icons in the new order).
- **Positions checked:** no code indexes `Types` or `TypeOptions` (no `[i]`, `First()`, `IndexOf`, `SelectedIndex`). A new hole's type is `_type = HoleStandards.ClearanceType`, by name, so it still starts on Clearance (pinned by `NewHole_StartsOnTheDefaults`). An edited hole's type comes from `HoleStandards.TypeName(spec.Kind)`, by kind; `Kind` comes from `KindFor(_type, _standard)`, by name. The Type setter matches by value (`Find(TypeOptions, value)`). Icons are by name (`HoleIcons.ForType`). Settings store no hole type (AddinSettings keeps last depths and seats keyed by units, size and seat, not by type position). `ChoiceList.FocusChosen` falls back to the first item only when nothing is selected, which the Type row never is. `HoleKind` enum order is untouched.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed. The whole solution was built on Windows.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:**
  1. Open AWB Hole: Type shows Simple, Clearance, Tapped, Taper Tapped, left to right.
  2. A new hole starts on the same type as before this change.
  3. Edit an existing hole of each type: the right type is chosen.

## Follow-ups

- With Simple first, a new hole starting on Clearance (the second button) may look odd now. Ask Aidan whether a new hole should start on Simple; left as today per scope.
