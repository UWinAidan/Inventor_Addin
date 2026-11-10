# 124: Typed values and expressions are kept when size, type, standard or units change

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** done
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

- [x] The 100-7 case keeps `d0` and `d0+5 mm` across a size change.
- [x] An untouched box follows the new size's table value.
- [x] Clearing a box hands it back to the table.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran (Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded, no warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 3817 passed, 0 failed, 0 skipped.
  - `dotnet build InventorAddin.slnx -tl:off`: succeeded, 0 errors, 8 warnings (the existing CS8600 warnings in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand`; none from this task). The deploy step ran.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` was changed; the add-in already writes the spec's expressions.
- **Inventor API members not confirmed:** none used. One assumption about Inventor to check by hand (checklist step 4): a typed number shown in the other units is written to Inventor as an expression with its unit, for example `12 mm` in an inch window, in the user's number format (`12,5 mm` in a comma locale).
- **What changed:** `AwbHoleViewModel` keeps a record of the boxes the user typed (`_typed`: the number in the units it was typed in, and the expression if it was one). The cascade (`Refresh`) replaces only the boxes not in it: the thread and drill depths, the blind depth, and a dowel's four seat boxes. A box the hole does not use (thread depths on a clearance hole, dowel seat boxes off AWB Dowel) is emptied and its typed value comes back with it. Diameter and Custom limits do not exist yet; they get the same treatment when they are added. Tests: new `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleTypedValuesViewModelTests.cs` (18 tests), and 8 existing tests that tested the behaviour this task overrides (task 100's expression dropped on a size or units change; task 106's start values replacing a typed dowel seat box) were changed to the new behaviour.
- **Manual checklist for Inventor:**
  1. Open a part with a sketch point and a user parameter `d0` (for example 20 mm). AWB Hole: Tapped, M6, Distance, Thread depth `d0`, Drill depth `d0+5 mm`, OK. Right-click the hole, Edit with AWB Hole, change the size to M8. Both boxes still show `d0` and `d0+5 mm` with their values beside them. OK, then change `d0` in Parameters: the hole's depths follow.
  2. A new blind tapped M6 with the depths left alone, change to M8: the thread and drill depths change to M8's table values.
  3. Type a drill depth `d0+5 mm`, then clear the box: it shows the table value; change the size: it follows the new size.
  4. Blind clearance hole, type Depth `12`, switch Units to Imperial: the box shows `12 mm` with `= 0.4724 in` beside it, and the preview depth does not change. Switch back to Metric: the box shows `12`. Switch to Imperial again, OK, and check in Inventor's Edit Hole dialog that the depth is 12 mm.
  5. Type a thread depth on a blind tapped hole, change Type to Clearance and back to Tapped: the typed thread depth comes back.
  6. AWB Dowel with a counterbore, type a counterbore diameter, change the size: the diameter stays, the depth (not typed) takes the new size's value.

## Defaults taken

- A typed number shown in the other units keeps its length. In a box that takes expressions (the depths and a dowel's counterbore depth) it is written with its unit, `12 mm`, with the inch value beside it, so the hole stays exactly 12 mm; in a box for numbers only (a dowel's diameters) it is shown converted, to 4 decimals of an inch or 3 of a mm. Switching back shows it as typed. The countersink angle has no units and is not converted.
- On a units switch a typed expression is kept as written and read again by Inventor in the new units (a number with no unit in it, as in `d0+1`, is then read in the new units). If Inventor cannot read it in the new units, the box shows it with Inventor's reason and OK waits; switching back brings it back as it was.
- An edited hole's own values count as typed (as the task said). An edited expression Inventor can no longer read counts as its stored number.
- Clearing a box: the thread and drill depths take the Tapped row's value at once, as before; the depth and a dowel's seat boxes stay empty until the next change the rule follows, as before. Either way the box follows the table from then on.
- Typed values are kept for the window's life, across type and standard changes as well: a typed thread depth comes back after Tapped, Clearance, Tapped, and a typed dowel seat value comes back after leaving AWB Dowel and returning.

## Follow-ups

- Spec 09 "Dowel and precision holes" and the task 106 remarks said a typed dowel seat value is kept only while the units and size stay; task 124 now keeps it. The parent may want to note this in the spec's dowel section.
- When the Simple hole Diameter box and the Custom tolerance Upper and Lower boxes are added, they should use the same `_typed` record (`MarkTyped` in the setter, `SettleBox` in `Refresh`).
