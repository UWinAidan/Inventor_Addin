# 126: Editable seat boxes on clearance and tapped holes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": Seat sizes; "Types and seats")
- **Status:** todo
- **Depends on:** 123, 124, 125
- **Needs:** windows build (Core first, then the window)
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: **seat dimensions are editable on every hole type.** Clearance and tapped holes start the counterbore or countersink from the workbook row for the chosen Fastener type and size, but show editable boxes like the dowel seat boxes (task 106), so he can change them for the application, for example a small countersink lead-in on a tapped or dowel hole. Fastener type stays, as the way to pick the starting values.

## Scope

- Core (`ViewModels/AwbHoleViewModel.cs`, `Holes/HoleSpec.cs`, `HoleRules.cs`, `HoleSeatCheck.cs`, `HoleStoredData.cs`, `HoleReadBack.cs`, `HoleReapply.cs`):
  - The seat boxes of task 106 (counterbore diameter and depth, countersink diameter and angle) show for every seated hole. On a clearance or tapped hole they start from the row for the Fastener type and size; changing Fastener type or size refills the boxes the user has not typed in (task 124's rule).
  - The seat is described by its dimensions, not only by its row; stored data records the dimensions (version bump, old data still reads: a stored row resolves to its dimensions).
  - A seat on a tapped or clearance hole no longer needs a row of its size: with no row the boxes start from the last values used, else empty and OK waits, as for dowels (Default). The Seat list therefore offers every seat on every type (this widens task 099's "only seats with a shown row").
  - Checks: diameter wider than the hole (a countersink wider than the hole; a counterbore wider than the hole), depth above zero unless task 127 allows zero, angle between 1 and 179 deg.
- Add-in: `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: show the existing dowel seat boxes for every seat; Fastener type stays above them. `HoleFeatureWriter.cs`: write the dimensions from the boxes for every type (the dowel path already does).
- Re-apply (task 092): compare the seat dimensions, which closes the follow-up "clearance and tapped seat sizes (not stored)".
- Tests for all Core parts.

Out of scope: spotfaces (112), the centre-drill spot and zero depth (127).

## Acceptance criteria

- [ ] A counterbored SHCS M6 clearance hole shows its counterbore boxes filled from the row; changing them changes the model; Edit opens on the changed values.
- [ ] A tapped M6 hole can take a countersink with no M6 Countersink row, typed in the boxes.
- [ ] Changing Fastener type refills untyped boxes and keeps typed ones.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Clearance M6, Counterbore, SHCS: boxes show the row values. Change the diameter, OK, Edit: the changed value is shown and modelled.
  2. Tapped M6, Countersink: type a small lead-in (for example 7 x 90). OK: the countersink is in the model.
  3. Change Fastener type with one box typed: only the untyped box changes.

## Follow-ups
