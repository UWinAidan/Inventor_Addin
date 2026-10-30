# 137: A seat on a clearance size with no row of its size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Seat boxes", question 26)
- **Status:** todo
- **Depends on:** 112
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** none

## Goal

Aidan answered spec 09 question 26 (2026-10-30): a seat is allowed on a clearance size that has no Counterbore, Countersink or Spotface row of its size. Its boxes start empty and OK waits for values. Today a seated clearance hole needs its row.

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`: with a seat chosen on a clearance hole, the Size list is the Clearance tab's sizes (not only the sizes with a row on the seat's tab); the Seat list offers every seat on every clearance size; a size with no row of the seat gives empty seat boxes (no last-value fallback; Aidan said "start empty"), Fastener type hidden, and OK disabled until the boxes hold valid values, as for a tapped seat with no row (task 126).
- `src/InventorAddin.Core/Holes/HoleRules.cs`, `HoleSeatCheck.cs`, and any other Core rule that refuses a seated clearance hole with no row, or sizes its through hole from the seat row: with no row, the through hole is the Clearance row at the chosen fit and the seat comes from the boxes.
- `HoleStoredData.cs`, `HoleReadBack.cs`, `HoleEditRules.cs`: an edited clearance hole with a seat and no row opens with its own seat values, and stores and reads back. No stored version bump unless needed.
- Tests in `tests/InventorAddin.Core.Tests`.

Out of scope:

- Tapped, dowel, Simple and Taper Tapped seats (already sized in boxes).
- The add-in project, unless a Core signature change forces a one-line caller update (say so in Verification, "not compiled" unless built on Windows).

## Acceptance criteria

- [ ] A clearance hole with Counterbore, Countersink or Spotface can be described, checked, stored and read back at a size with no row of that seat.
- [ ] In the view-model: picking that seat on that size shows empty seat boxes and no Fastener type; OK is disabled until the boxes are valid; changing to a size with a row fills the boxes from the row (untyped values only, as task 124).
- [ ] A clearance size with a row behaves as before.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Follow the pattern task 126 used for a tapped seat with no row (`HoleRules.SeatRowStart`, `Refresh` in `AwbHoleViewModel`), but without the last-values fallback.
- Decisions: `DECISIONS.md` rows dated 2026-10-30 (question 26 and task 126). An edited value equal to its row counts as untyped (question 25, Aidan).
- Anything else that looks like a decision: log it under Defaults below.

## Verification

Filled in by the implementer.

## Defaults

## Follow-ups
