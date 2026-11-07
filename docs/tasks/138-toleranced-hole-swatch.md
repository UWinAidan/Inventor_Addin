# 138: Colour swatch on a toleranced hole's Tolerance row

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes", question 28)
- **Status:** planned
- **Depends on:** 115 (toleranced Clearance and Simple holes), 118 (the window's final rows)
- **Needs:** windows build
- **Parallel-safe with:** none (same window and view-model as 118)

## Goal

Aidan, 2026-11-07, answering spec 09 question 28: a toleranced Clearance or Simple hole shows the precision colour swatch beside its Tolerance row, matching the colour the hole gets. This overturns task 115's Default of no swatch.

Today a dowel shows a swatch and its fit's name beside Fit (`FitSwatch`, `FitColourText`, `ShowsFitColour` in `AwbHoleViewModel`; the `SideColourSwatch` style in `AwbHoleWindow.xaml`). A toleranced hole is coloured on OK (task 115) but the window does not show the colour.

## Scope

- Core (`AwbHoleViewModel`):
  - A swatch value and its text for the Tolerance row, set only while Toleranced is ticked on a Clearance or Simple hole, else null.
  - The colour is the one the hole's faces get on OK: take it from the same place the writer's colour comes from (the chosen Precision Tol row's fit level, and Custom's colour as task 115 colours it), not a second copy of the rule. If that rule lives outside the view-model, call it.
  - The text names the colour as the dowel's does (for example `Normal fit`).
  - Raise property changes when Toleranced, the Tolerance row, Type, Standard or Units change.
- `src/InventorAddin/UI/AwbHoleWindow.xaml`: the swatch and its text beside the Tolerance dropdown, as Fit's row shows them for a dowel. Keep task 118's heights: the swatch sits on the existing row and adds no row.
- A dowel's Fit row is unchanged.
- Tests for every Core behaviour above.

## Acceptance criteria

- [ ] Ticking Toleranced on a Clearance or Simple hole shows a swatch beside Tolerance, in the colour the hole's faces get on OK.
- [ ] Changing the Tolerance row (including Custom) changes the swatch to match the faces' colour.
- [ ] Unticked, or on a type that cannot be toleranced, there is no swatch.
- [ ] The window is no taller than after task 118.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- Read task 115's Verification and Defaults for how a toleranced hole is coloured, and `HoleFitOption.SwatchOf`.
- Watch the row's width: Tolerance entries are long (`N7  -0.004 / -0.016 Medium press`). If the swatch and text push the window wider, show the swatch alone and log the choice as a Default.

## Verification

## Defaults

## Follow-ups
