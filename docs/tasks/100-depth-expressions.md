# 100: Depth boxes take Inventor parameters and expressions

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** todo
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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. A part with an extrusion of distance `d0` (say 20 mm). AWB Hole, Distance, Depth `d0`: the hole is 20 mm deep. OK; Manage > Parameters: the hole's depth reads `d0`. Change `d0` to 30: the hole follows.
  2. Depth `1.5*d0+1`, `12 mm`, `0.5 in` (in a metric window): each gives the right depth. `abc`: Inventor's reason under the box, OK disabled.
  3. Tapped blind with Thread depth `d0` and Drill depth `d0+5 mm`: both parametric. Thread depth deeper than drill: refused.
  4. Edit the hole (after 092): the boxes show `d0` and `1.5*d0+1`.

## Follow-ups
