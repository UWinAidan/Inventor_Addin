# 126: Editable seat boxes on clearance and tapped holes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": Seat sizes; "Types and seats")
- **Status:** done
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

- [x] A counterbored SHCS M6 clearance hole shows its counterbore boxes filled from the row; changing them changes the model; Edit opens on the changed values.
- [x] A tapped M6 hole can take a countersink with no M6 Countersink row, typed in the boxes.
- [x] Changing Fastener type refills untyped boxes and keeps typed ones.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran (Aidan's PC, Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded, no warnings in changed files.
  - `dotnet test tests/InventorAddin.Core.Tests`: passed 3888, failed 0 (3849 before the task; 10 existing tests rewritten for the new behaviour, 39 tests added).
  - `dotnet build InventorAddin.slnx -tl:off`: build succeeded, with the deploy step (Inventor was closed). The only warnings are the existing CS8600 ones in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`, which this task does not touch.
- **Not compiled (changed under `src/InventorAddin`):** none. `HoleFeatureWriter.cs`, `HoleSession.cs` (comments only) and `AwbHoleWindow.xaml` (comments only) compiled in the Windows build above. Nothing has been run in Inventor.
- **Inventor API members not confirmed:** none. No new members are used. The writer change only drops the dowel-only condition on the existing `HoleFeature.CBoreDepth` and `Parameter.DrivenBy.Count` call (task 106).
- **What changed:**
  - Core: the seat dimensions (`CboreDiameter`, `CboreDepth`, `CboreDepthExpression`, `CsinkDiameter`, `CsinkAngleDeg`) apply to every kind. `HoleRules` takes a value from the spec and falls back to the row for a value left null. A tapped seat needs no row: a value with no row is asked for under its box. Seat problems show under the boxes on every kind. The countersink angle must be between 1 and 179 deg. `HoleRules.SeatRowStart` gives the boxes' starting values. Stored data is version 2: it records the dimensions for every seat, and the fastener is optional on a tapped hole. Version 1 still reads, and its row resolves to its dimensions through `HoleRules` and the window. Re-apply compares the seat dimensions on every kind. An Inventor-made hole opens on its own seat dimensions. The last tapped seat is remembered by units and thread (`AddinSettings.LastTappedSeatsMm/In`).
  - Window view-model: the Seat list offers every seat on every type. The boxes show for every seat, and untyped boxes refill when the units, size or Fastener type change. Fastener type is hidden on a tapped hole that has no row of its size. The diagram draws the seat from the boxes.
  - Add-in: the writer clears an old counterbore depth expression on every kind, not just dowels. The window XAML already bound the boxes to `ShowsCounterboreSizes`/`ShowsCountersinkSizes`, which now follow the seat only. Only comments changed there.
- **Manual checklist for Inventor:** open a part with an unconsumed sketch containing a few points (a new part with one sketch and three points is enough). Use AWB Hole on the 3D Model tab, or press H.
  1. Clearance M6, Counterbore, SHCS: Fastener type sits above the Cbore dia and Cbore depth boxes, which show the row values. Change the diameter (for example to 12) and OK. The model has a 12 mm counterbore. Right-click the hole, Edit Feature: the box shows 12 and the depth is still the row's.
  2. Tapped M6, Countersink. If the workbook has no M6 Countersink row, Fastener type is hidden and the boxes are empty, with "Enter a countersink diameter." under them and OK disabled. Type a small lead-in (for example 7 and 90). OK: the countersink is in the model. Start AWB Hole again on a new point with Tapped M6 Countersink: the boxes start at 7 and 90 (remembered). The add-in log says "remembered the seat for the next M6 tapped hole (Metric): countersink 7 mm at 90 deg".
  3. Clearance M6, Counterbore, SHCS. Type a new diameter but leave the depth alone, then change Fastener type: only the depth changes. Clear the diameter box (empty it) and change Fastener type again: the diameter follows the row.
  4. Clearance M6 counterbored from its row: OK, then Edit. Change Size to M8: both boxes follow the M8 row (a seat equal to its row is not typed). Cancel.
  5. Countersink angle 0.5 or 180: the box says "between 1 and 179 degrees" and OK is disabled.
  6. Clearance counterbore depth typed as an expression (for example `d0/2` when the part has a `d0` parameter): the box shows "= ... mm". OK, then open Parameters: the counterbore depth parameter holds the expression. Edit the hole, type a plain number in its place, and OK: the parameter is a plain value again.
  7. A hole made before this task (version 1 data), for example a counterbored clearance hole from an earlier build: Edit opens on the row's values with no re-apply offer. OK stores the new data.
  8. With "Use Inventor's Hole command" on, change a clearance counterbore's diameter in Inventor's Hole dialog, then switch back and Edit it in AWB Hole: the re-apply offer lists the counterbore diameter.

## Defaults

Behaviour picked to keep work moving. The parent should log these in `DECISIONS.md` as Default; Aidan may overturn them.

- **An edited clearance or tapped hole's seat values count as typed only where they differ from the row** for its Fastener type and size, or are expressions. A seat made from its row therefore still follows a size or fastener change (task 093). This narrows task 124's "an edited hole's own values count as typed" for these boxes only. A dowel's seat values still all count as typed.
- **A tapped hole's last seat is remembered per units and thread** (`LastTappedSeatsMm/In` in the settings file) and is used only for values its row does not give, which in practice means a size with no row. A clearance hole's seat always has its row, so nothing is remembered for it.
- **Fastener type is hidden on a tapped hole with no row of its size**, because there is nothing to pick.
- **A named fastener with no row of the size is reported only when a box leaves a value for the row to give.** When every box is typed, the fastener is kept as a label and is no problem.
- **Stored data version 2:** every seat's dimensions are written. A tapped hole's fastener is optional. Version 1 data still reads, its seat dimensions read as null, and they come from the row.
- **An Inventor-made hole opens on its own seat dimensions** on every kind, with the closest or first row picked as Fastener type, and the notes say the hole's own seat is kept.

## Follow-ups

- Spec 09: the parent may want to add the Defaults above to "Seat boxes" and to the tapped row of the "Types and seats" table, and log them in `DECISIONS.md`.
- Aidan may want to confirm the first Default: whether an edited seat equal to its row should follow a size change, or stay as typed like the other edited values (task 124).
- `HoleDowelSeat`, `HoleLastDowelSeats` and `AddinSettings.DowelSeatMap` now hold tapped seats too. A rename (for example `HoleSeatSizes`, `HoleLastSeats`) would read better. It was left out to keep this diff small.
- `HoleDowelSeat.Valid` still accepts a remembered angle between 0 and 180 deg, while `HoleRules` now asks for 1 to 179. A remembered angle outside 1 to 179 would start the box with a problem under it. That is harmless, but it is inconsistent.
- A seated clearance hole still needs its Counterbore or Countersink row for the through hole and the Size list. A clearance seat with no row (for example a lead-in on a plain clearance size) would need the Size list to come from the Clearance tab when seated. That is a behaviour change for Aidan to decide.
