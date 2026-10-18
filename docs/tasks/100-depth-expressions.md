# 100: Depth boxes take Inventor parameters and expressions

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** done
- **Depends on:** 097, 099
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan, 090 check: the depth boxes (Depth, Thread depth, Drill depth) accept Inventor parameters and expressions with units, as Inventor's own boxes do, for example `d0`, `1.5*d0+1`, `12 mm`, `0.5 in`, and the hole stays parametric: the hole's parameter holds the expression, so changing `d0` later changes the hole.

## Scope

- Core: an expression checker interface the view-model uses (valid or not, its value in cm, the reason Inventor gives), `AwbHoleViewModel` (the depth texts), `HoleSpec`/`HoleGeometry` (an expression beside each depth value), `HoleStoredData` and `HoleReadBack` (store and read the expression), `HoleRules` checks on the value, with tests using a fake checker
- `src/InventorAddin/Holes/`: the checker over the part's `UnitsOfMeasure` and parameters; `HoleFeatureWriter` writes `Parameter.Expression` for depths that have one (a plain number keeps going in as a value in cm, or as an expression with the window's unit; pick one and say why); `HoleFeatureReader` reads the expression back

## Acceptance criteria

- [ ] A plain number is read in the window's units, as today. Text with a unit (`12 mm`, `0.5 in`) is taken in that unit. A parameter name or an expression is checked by Inventor against the part's parameters; an invalid one shows Inventor's reason under the box and disables OK, as "Enter a number." does today.
- [ ] Core's checks (thread not deeper than the drill, a blind hole deeper than its seat) use the evaluated value.
- [ ] After OK the hole's depth parameters show the expression in Inventor's fx dialog (`d0`, `1.5*d0+1`); changing `d0` there moves the hole's depth.
- [ ] A depth that refers to the hole's own parameters (a cycle) is refused with Inventor's message, the last good hole kept.
- [ ] Editing a hole shows its expression in the box, not only the value.
- [ ] The last depth used (task 097) stores the value, not the expression.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- `HoleLastDepths.Remember` (task 097) takes `HoleSpec.Depth`; pass the expression's value so the setting stays a number.

- Check `UnitsOfMeasure` members (`IsExpressionValid`, `GetValueFromExpression`, `GetStringFromValue`, `kDefaultDisplayLengthUnits`) against the interop by reflection, and use the part document's own `UnitsOfMeasure` so its parameters resolve. You may test with an invisible Inventor in a console app (task 093's notes).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 3364 passed. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: builds, no new warnings. Spike 100 (a console app driving an invisible Inventor 2026) answered the API questions below.
- **Not compiled (changed under `src/InventorAddin`):** none; the add-in was built on Windows. Not yet run inside Inventor.
- **Inventor API members not confirmed:** none. Spike 100 called each one: `UnitsOfMeasure.IsExpressionValid` and `GetValueFromExpression` (taking a `UnitsTypeEnum` as the units), `Parameter.Expression`, `Parameter.set_Units`, `Parameter.DrivenBy`, `DistanceExtent.Distance`, `HoleTapInfo.ThreadDepth`.
- **What spike 100 found:**
  - `IsExpressionValid` answers false for text it cannot read, and `GetValueFromExpression` then fails with "Unspecified error". Inventor gives no reason, so the window shows its own message (`HoleExpressions.NotALengthText`).
  - A number with no unit is read in the units passed: `1.5*d0+1` with d0 = 50 mm is 76 mm read in millimetres and 100.4 mm read in inches. Names are case sensitive. A unitless or angle parameter on its own is not a length.
  - Writing `Parameter.Expression` keeps the hole parametric. Inventor rewrites it (`1.5*dd+1` becomes `1.5 ul * dd + 1 mm`) and follows a change to `dd`. Writing `Value`, or `SetDistanceExtent2` with a number, makes the parameter plain again.
  - Setting the parameter's units to `in` before writing the expression makes `dd+1` read as dd + 1 in.
  - An expression that refers to the hole's own parameters fails with "Unspecified error", and the hole is unchanged.
  - Reassigning `TapInfo` (the tap drill write) keeps the thread depth's expression.
- **Choices made:**
  - A plain number still goes to Inventor as a value in cm, as before. Only text that is not a plain number is written as an expression, after the parameter takes the window's length unit. Typing a plain number over an expression replaces it.
  - An expression lasts only while its depth keeps the value it came to. A units switch, or a new size putting back the Tapped row's depths, turns it into a plain number.
  - Editing a hole checks its expression again, so the depth takes today's value. An expression Inventor can no longer read shows as text that is not a length, and OK is disabled.
  - The reader treats a parameter as an expression only when other parameters drive it (`DrivenBy.Count > 0`), so `12 mm` reads back as a plain number.
- **Manual checklist for Inventor:**
  1. A part with an extrusion of distance `d0` (say 20 mm). AWB Hole, Distance, Depth `d0`: the hole is 20 mm deep. OK; Manage > Parameters: the hole's depth reads `d0`. Change `d0` to 30: the hole follows.
  2. Depth `1.5*d0+1`, `12 mm`, `0.5 in` (in a metric window): each gives the right depth. `abc`: the message under the box, OK disabled.
  3. Tapped blind with Thread depth `d0` and Drill depth `d0+5 mm`: both parametric. Thread depth deeper than drill: refused.
  4. Depth set to the hole's own depth parameter (its `dNN` name from Manage > Parameters): refused with Inventor's message, the last good hole kept.
  5. Edit the hole: the boxes show `d0` and `1.5*d0+1`. Change `d0` first, then edit: the depth is today's value.
  6. Make a new blind hole after step 1: it starts at the last depth's value, not at `d0`.
  7. A tapped blind hole with expressions on both depths, then change its thread size, so the hole is made again: both depth parameters still read their expressions.

## Follow-ups

- An expression changed in Inventor's own Parameters dialog after OK is not seen by Edit: an AWB hole opens on its stored expression, not the parameter's. Task 092's stored-versus-applied comparison could compare the depth expressions too.
- Inventor gives no reason for text it cannot read, so the message under the box is the add-in's own. If a later Inventor gives one, `HoleExpressionChecker` can pass it on.
