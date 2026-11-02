# 137: A seat on a clearance size with no row of its size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Seat boxes", question 26)
- **Status:** done
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

- [x] A clearance hole with Counterbore, Countersink or Spotface can be described, checked, stored and read back at a size with no row of that seat.
- [x] In the view-model: picking that seat on that size shows empty seat boxes and no Fastener type; OK is disabled until the boxes are valid; changing to a size with a row fills the boxes from the row (untyped values only, as task 124).
- [x] A clearance size with a row behaves as before.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Follow the pattern task 126 used for a tapped seat with no row (`HoleRules.SeatRowStart`, `Refresh` in `AwbHoleViewModel`), but without the last-values fallback.
- Decisions: `DECISIONS.md` rows dated 2026-10-30 (question 26 and task 126). An edited value equal to its row counts as untyped (question 25, Aidan).
- Anything else that looks like a decision: log it under Defaults below.

## Verification

- **Ran (Windows):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4032 passed, 0 failed. Before the task: 4001 passed. Two cases were removed from `HoleStoredDataTests.UnresolvedSpecs` because a seated clearance hole with no fastener is now valid; 33 tests were added.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings were already there (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands).
- **What was built, file by file:**
  - `HoleSpec.cs`: `UsesClearanceFit` is true for a clearance hole with no seat or a spotface, and also for one with a counterbore or countersink and no Fastener. `Normalised` keeps `ClearanceFit` whenever this is true.
  - `HoleRules.cs`: a clearance counterbore or countersink on a size with no row of that seat goes to `BuildSeatWithoutRow`. The through hole is the Clearance row at `spec.ClearanceFit`, the seat comes from the boxes (an empty box is asked for under it), and the resolved Fastener is null. A size that is not in the Clearance tab is reported under Size. A size with a row works as before. The spotface path from task 112 needed no change.
  - `HoleStoredData.cs`: a seated clearance hole's Fastener is now optional from version 2. `ClearanceFit` is written and required whenever `UsesClearanceFit` is true. Version 1 data still requires a fastener. No version bump: data written before always has a fastener and reads as before.
  - `HoleReadBack.cs`: an Inventor counterbore or countersink whose seat matches no row, but whose through hole matches a Clearance row, now opens at that Clearance size with its own seat values (`SeatOnClearanceSize`):
    - If the size has no row of the seat, it opens with no fastener and the matched fit.
    - If it has rows, it takes the row whose Hole fit gives the hole's diameter, else the first.
    - Each case adds a note. The old closest-seat-row fallback stays for a through hole that matches no Clearance row.
    - `ClearanceMatch` was pulled out of `DowelOrClearance` so both use it.
  - `HoleEditRules.cs`: no change needed; nothing in it depends on the seat or fastener.
  - `ViewModels/AwbHoleViewModel.cs`:
    - Size, Seat and Fastener type are now settled in that order on every kind.
    - A clearance hole's Size list is the Clearance tab's whatever the seat.
    - Its Fastener type list is the Screw values of the seat tab's rows of that size (`ClearanceSeatRows`), so a size with no row has no Fastener type.
    - The Fit list shows for a clearance counterbore or countersink whose size has no row.
    - The seat boxes start from `HoleRules.SeatRowStart`, which is empty for a size with no row. No last values are used.
    - An edited hole's values count as typed because no row gives them (task 126's `OpenTypedSeat`).
- **Tests:**
  - `tests/InventorAddin.Core.Tests/Holes/HoleClearanceSeatWithoutRowTests.cs` (18): rules, `Normalised`, stored data round trips and missing values, read-back from stored data and from Inventor facts.
  - `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleClearanceSeatWithoutRowViewModelTests.cs` (14, theory cases counted): Size list, seats on every size, empty boxes, hidden Fastener type, Fit, OK, size changes in both directions, a size with a row, edit and read-back.
  - Updated for the new lists:
    - `AwbHoleViewModelTests`: Fastener follows Size, the size is kept on a seat change, and a units switch takes the nearest Clearance size.
    - `HoleReadBackTests`: the unknown-counterbore note on a matching Clearance size, plus a new test for the old closest-row fallback.
    - `HoleStoredDataTests`: the two removed cases above.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` changed, and the whole solution was built on Windows against the changed Core.
- **Inventor API members not confirmed:** none are used.
- **Manual checklist** (for the combined check after task 114). Use a part with a sketch point, and a hole workbook with a Clearance size that has no Counterbore row and no Countersink row, for example a made-up `T7`.
  1. Open AWB Hole and choose Counterbore. The Size list shows every Clearance size, not only the counterbore sizes.
  2. Pick the size with no Counterbore row. Fastener type is hidden, Fit shows (Normal), and the counterbore boxes are empty with "Enter a counterbore diameter." and "Enter a counterbore depth." under them. OK is disabled.
  3. Type a diameter larger than the hole and a depth. OK enables, and the preview shows the hole at the Normal clearance diameter with that counterbore. Change Fit to Loose: the through hole grows.
  4. Pick a size that has a Counterbore row. Fastener type shows with that size's fasteners, Fit hides, and any box you did not type takes the row's value. A typed box keeps your value.
  5. Go back to the size with no row. The untyped boxes empty again, with no remembered values.
  6. Repeat steps 1 to 3 with Countersink, using the diameter and angle boxes.
  7. Make the hole with OK, then edit it (double-click). It opens at the same size, with Fit and seat values as made, Fastener type hidden, and no re-apply offer.
  8. Make the same counterbored hole with Inventor's own Hole command, with the through hole at a Clearance diameter for that size and a counterbore no row has. Edit it in AWB Hole. It opens at that size with its own counterbore, the matching fit, and a note.

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **A clearance hole's Fastener type list follows its Size**, as a tapped hole's does. It lists the fasteners of the seat tab's rows of that size, and is hidden for a size with none. Before, it listed every fastener in the tab and narrowed the Size list. Since the Size list is now the whole Clearance tab, choosing a fastener no longer changes the sizes.
- **The Size is kept when the seat changes** on a clearance hole, because the list no longer depends on the seat. Before, a new seat could move the size to the first size with a row.
- **The Fit list shows for a clearance seat with no row**, and the through hole is the Clearance row at that fit, Normal by default. A size with a row hides Fit and uses the row's Hole fit, as before.
- **A fastener named on a size with no row is dropped**: no problem is reported, and the hole stores none. This matches what task 126 does for tapped holes.
- **Stored data stays at version 3.** A seated clearance hole may have no Fastener, and then stores `ClearanceFit`. An add-in from before this task would fail to read such a hole and fall back to its geometry.
- **Inventor-made holes**: a seated hole whose seat matches no row is read at the Clearance size its through hole matches, before the old closest-seat-row fallback. If that size has rows, the row whose Hole fit gives the hole's diameter is picked, else the first. The note changed from "the closest, X, is picked" to "the hole's size, X, is picked" in that case.

## Follow-ups

- **Task 114, window.** The window binds `ShowsFit` already. Check that the Fit row appearing and disappearing as the Size changes on a seated clearance hole does not make the layout jump.
- **Hidden-only seat rows.** A clearance size whose only Counterbore or Countersink rows are hidden (`Show = N`) is treated as having a row. Fit is hidden and Fastener type is empty, because hidden rows are not listed, and `HoleRules` uses the first hidden row's Hole fit. This is consistent with how `HoleRules` finds hidden rows, but the window shows no fastener. Decide whether hidden-only rows should count as "no row".
- **A seat row whose size has no Clearance row** (for example a Counterbore row `TNONE`) can no longer be picked for a new hole, because the Size list is the Clearance tab. Before, it was listed and `HoleRules` reported the missing Clearance row. An edited hole at such a size still opens as it is.
